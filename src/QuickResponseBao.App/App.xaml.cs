using System.Drawing;
using System.Net.Http;
using System.Windows;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Diagnostics;
using QuickResponseBao.Infrastructure.Storage;
using QuickResponseBao.Infrastructure.Windows;
using QuickResponseBao.Infrastructure.Updates;
using Forms = System.Windows.Forms;

namespace QuickResponseBao.App;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _tray;
    private Icon? _appIcon;
    private CandidateWindow? _candidates;
    private ClipboardPasteService? _paste;
    private SafeFileLogger? _logger;
    private IReadOnlyList<QuickResponse> _cache = [];
    private bool _exiting;
    private bool? _lastPasteSucceeded;
    private bool? _lastClipboardRestored;
    private uint? _lastPasteSentCount;
    private int? _lastPasteErrorCode;
    private int? _lastPasteInputSize;
    private string _lastPasteTargetProcess = string.Empty;
    private bool? _lastPasteSamePermissionLevel;
    private string _lastFailureReason = string.Empty;
    private IReadOnlyList<string> _restartArguments = [];
    private static readonly HttpClient UpdateHttpClient = new() { Timeout = TimeSpan.FromMinutes(15) };

    public AppPaths Paths { get; private set; } = null!;
    public IQuickResponseRepository Repository { get; private set; } = null!;
    public JsonSettingsStore SettingsStore { get; private set; } = null!;
    public ICategoryRepository CategoryRepository => (ICategoryRepository)Repository;
    public IDatabaseBackupService BackupService { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public GlobalKeyboardListener Listener { get; private set; } = null!;
    public MainWindow MainAppWindow { get; private set; } = null!;
    public SearchService SearchService { get; } = new();
    public ThemeService ThemeService { get; private set; } = null!;
    public HttpClient UpdatesClient => UpdateHttpClient;
    public Task LogSafeErrorAsync(string context, Exception exception) =>
        _logger?.WriteAsync(context, exception) ?? Task.CompletedTask;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _restartArguments = e.Args;
        Paths = new AppPaths(); SettingsStore = new JsonSettingsStore(Paths); Settings = await SettingsStore.LoadAsync();
        LocalizationService.Apply(Settings.Language); ThemeService = new ThemeService(this); ThemeService.ThemeChanged += (_, _) => UpdateTray(); ThemeService.Apply(Settings.Theme);
        Repository = new SqliteQuickResponseRepository(Paths); await Repository.InitializeAsync();
        BackupService = new DatabaseBackupService(Paths);
        _logger = new SafeFileLogger(Paths); await _logger.WriteAsync("Application started");
        await ReloadCacheAsync();
        _paste = new ClipboardPasteService(); _candidates = new CandidateWindow();
        _candidates.Confirmed += CandidateConfirmed;
        _candidates.PositionMethodChanged += (_, method) => _ = _logger?.WriteAsync($"Candidate positioning: {method}");
        Listener = new GlobalKeyboardListener(Settings);
        Listener.SearchTextChanged += (_, query) => Dispatcher.BeginInvoke(() => ShowSuggestions(query));
        Listener.SearchCancelled += (_, _) => Dispatcher.BeginInvoke(HideSuggestions);
        Listener.NavigationRequested += (_, key) => Dispatcher.BeginInvoke(() => _candidates.Navigate(key));
        if (Settings.EnableListenerOnStartup && Settings.GlobalSearchEnabled) TryStartListener();
        CreateTray();
        MainAppWindow = new MainWindow(new MainViewModel(Repository, SearchService));
        MainAppWindow.Closing += MainWindowClosing;
        await MainAppWindow.InitializeAsync(Settings);
        if (!Settings.StartMinimized) MainAppWindow.Show();
        if (Settings.CheckUpdatesOnStartup) _ = CheckUpdatesOnStartupAsync();
        DispatcherUnhandledException += async (_, args) =>
        {
            await (_logger?.WriteAsync("Unhandled exception", args.Exception) ?? Task.CompletedTask);
            args.Handled = true; System.Windows.MessageBox.Show(args.Exception.Message, LocalizationService.Get("AppName"));
        };
    }

    public async Task ReloadCacheAsync() => _cache = await Repository.GetAllAsync();
    public void TryStartListener()
    {
        try { Listener.Start(); if (Listener.IsRunning) _lastFailureReason = string.Empty; }
        catch (Exception ex) { _lastFailureReason = $"Hook: {ex.Message}"; _ = _logger?.WriteAsync("Listener failed", ex); }
        UpdateTray();
    }
    public void PauseListener() { Listener.Stop(); HideSuggestions(); _ = _logger?.WriteAsync("Listener paused"); UpdateTray(); }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        Settings = settings; ThemeService.Apply(settings.Theme); await SettingsStore.SaveAsync(settings); Listener.UpdateSettings(settings); UpdateTray();
    }

    private void ShowSuggestions(string query)
    {
        var options = new SearchOptions(Settings.MatchSummary, Settings.MatchContent, Settings.MatchKeywords,
            Settings.MatchCategory, Settings.CaseSensitive, Settings.SortByUsage, Settings.MaximumSuggestions);
        var results = SearchService.Search(_cache, query, options);
        if (results.Count == 0) { HideSuggestions(); return; }
        _candidates!.ShowResults(query, results); Listener.SuggestionsVisible = true;
    }
    private void HideSuggestions() { _candidates?.Hide(); if (Listener is not null) Listener.SuggestionsVisible = false; }

    private async void CandidateConfirmed(object? sender, QuickResponse response)
    {
        HideSuggestions(); Listener.Reset();
        if (!Settings.AutoPasteEnabled) return;
        try
        {
            var paste = await _paste!.PasteAsync(response.Content, Settings.PreserveClipboard, Settings.RestoreClipboard, Settings.ClipboardRestoreDelayMs);
            _lastClipboardRestored = paste.ClipboardRestored;
            RecordPasteDiagnostics(paste.Target, paste.SendResult);
            _lastPasteSucceeded = true; _lastFailureReason = string.Empty;
            await Repository.IncrementUsageAsync(response.Id); await ReloadCacheAsync(); await MainAppWindow.RefreshAsync();
            await (_logger?.WriteAsync($"Paste succeeded; method={paste.Method}; clipboardRestored={paste.ClipboardRestored?.ToString() ?? "not-requested"}") ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            var message = ex is PasteShortcutException pasteError
                ? pasteError.Target.ElevationMismatch
                    ? string.Format(LocalizationService.Get("PasteFailedElevation"), string.IsNullOrWhiteSpace(pasteError.Target.ProcessName) ? LocalizationService.Get("Unavailable") : pasteError.Target.ProcessName)
                    : string.Format(LocalizationService.Get("PasteFailedWin32"), pasteError.ErrorCode)
                : $"{LocalizationService.Get("PasteFailedTitle")}: {ex.Message}";
            _lastPasteSucceeded = false; _lastClipboardRestored = Settings.RestoreClipboard ? false : null; _lastFailureReason = message;
            if (ex is PasteShortcutException failed)
                RecordPasteDiagnostics(failed.Target, new PasteSendResult(false, failed.SentCount, failed.ErrorCode, failed.InputSize));
            await (_logger?.WriteAsync("Paste failed", ex) ?? Task.CompletedTask);
            System.Windows.MessageBox.Show(message, LocalizationService.Get("PasteFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateTray()
    {
        _appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        _tray = new Forms.NotifyIcon { Icon = _appIcon ?? SystemIcons.Information, Text = "Quick Response Bao", Visible = true };
        _tray.DoubleClick += (_, _) => ShowMainWindow(); UpdateTray();
    }
    public void UpdateTray()
    {
        if (_tray is null) return;
        var menu = new Forms.ContextMenuStrip();
        var surface = (System.Windows.Media.SolidColorBrush)FindResource("SurfaceBrush"); var text = (System.Windows.Media.SolidColorBrush)FindResource("TextBrush");
        menu.BackColor = Color.FromArgb(surface.Color.A, surface.Color.R, surface.Color.G, surface.Color.B); menu.ForeColor = Color.FromArgb(text.Color.A, text.Color.R, text.Color.G, text.Color.B);
        menu.Items.Add(LocalizationService.Get("TrayOpen"), null, (_, _) => ShowMainWindow());
        menu.Items.Add(LocalizationService.Get(Listener?.IsRunning == true ? "TrayPause" : "TrayEnable"), null, (_, _) => { if (Listener?.IsRunning == true) PauseListener(); else TryStartListener(); });
        menu.Items.Add(LocalizationService.Get("TrayAdd"), null, (_, _) => { ShowMainWindow(); MainAppWindow.AddResponse(); });
        menu.Items.Add(LocalizationService.Get("Settings"), null, (_, _) => { ShowMainWindow(); MainAppWindow.OpenSettings(); });
        menu.Items.Add(LocalizationService.Get("TrayExit"), null, (_, _) => ExitApplication());
        foreach (Forms.ToolStripItem item in menu.Items) { item.BackColor = menu.BackColor; item.ForeColor = menu.ForeColor; }
        _tray.ContextMenuStrip?.Dispose(); _tray.ContextMenuStrip = menu;
    }
    private void ShowMainWindow() { MainAppWindow.Show(); MainAppWindow.WindowState = WindowState.Normal; MainAppWindow.Activate(); }
    private void MainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting || !Settings.MinimizeToTrayOnClose) return;
        e.Cancel = true; MainAppWindow.Hide();
        if (Settings.ShowNotifications) _tray?.ShowBalloonTip(2500, LocalizationService.Get("AppName"), LocalizationService.Get("TrayStillRunning"), Forms.ToolTipIcon.Info);
    }
    public async void ExitApplication()
    {
        _exiting = true; Listener?.Dispose(); ThemeService?.Dispose(); _tray?.Dispose(); _appIcon?.Dispose(); _candidates?.Close();
        await (_logger?.WriteAsync("Application exited") ?? Task.CompletedTask); MainAppWindow?.Close(); Shutdown();
    }

    public DiagnosticSnapshot GetDiagnosticSnapshot()
    {
        var input = Listener.InspectEnvironment();
        return new DiagnosticSnapshot(DateTimeOffset.Now, input.ProcessName, input.FocusProcessName, input.WindowTitle, input.IsWhitelisted,
            Listener.IsRunning, input.TextInputDetected, input.TextInputState == TextInputDetectionState.Unknown,
            input.PasswordFieldDetected, input.UiAutomationUnavailable, Listener.BufferLength,
            _candidates?.LastPositionMethod ?? CandidatePositionMethod.CurrentMonitorBottomRight,
            _lastPasteSucceeded, _lastClipboardRestored, _lastPasteSentCount, _lastPasteErrorCode, _lastPasteInputSize,
            _lastPasteTargetProcess, _lastPasteSamePermissionLevel, _lastFailureReason, Paths.Logs);
    }

    public void TestCandidateWindow()
    {
        var sample = new QuickResponse { Summary = LocalizationService.Get("CompatibilityTest"), Content = LocalizationService.Get("CandidatePositionTest"), Keywords = ["test"] };
        _candidates!.ShowResults("test", [new SearchResult(sample, 1)]); Listener.SuggestionsVisible = true;
    }

    public async Task TestPasteAsync()
    {
        try
        {
            var paste = await _paste!.PasteAsync("Quick Response Bao paste test", Settings.PreserveClipboard, Settings.RestoreClipboard, Settings.ClipboardRestoreDelayMs);
            _lastClipboardRestored = paste.ClipboardRestored;
            RecordPasteDiagnostics(paste.Target, paste.SendResult);
            _lastPasteSucceeded = true; _lastFailureReason = string.Empty;
        }
        catch (Exception ex)
        {
            _lastPasteSucceeded = false; _lastClipboardRestored = Settings.RestoreClipboard ? false : null; _lastFailureReason = ex.Message;
            if (ex is PasteShortcutException failed)
                RecordPasteDiagnostics(failed.Target, new PasteSendResult(false, failed.SentCount, failed.ErrorCode, failed.InputSize));
            throw;
        }
    }

    private void RecordPasteDiagnostics(PasteTargetInfo target, PasteSendResult send)
    {
        _lastPasteSentCount = send.SentCount; _lastPasteErrorCode = send.ErrorCode; _lastPasteInputSize = send.InputSize;
        _lastPasteTargetProcess = target.ProcessName; _lastPasteSamePermissionLevel = target.SamePermissionLevel;
    }

    public void InstallUpdate(string packagePath, ReleaseAssetKind kind)
    {
        new UpdateInstallerLauncher(Paths).Launch(packagePath, kind, AppContext.BaseDirectory, "QuickResponseBao.exe", _restartArguments);
        ExitApplication();
    }

    private async Task CheckUpdatesOnStartupAsync()
    {
        try
        {
            var current = ApplicationVersion.Current;
            var result = await new GitHubUpdateService(UpdateHttpClient).CheckAsync(current, Settings.IncludePrereleaseUpdates);
            if (!result.IsUpdateAvailable) return;
            var updateTask = await Dispatcher.InvokeAsync(() =>
                MainAppWindow.ShowUpdateWindowAsync(result, Settings.AutoDownloadUpdates));
            await updateTask;
        }
        catch (Exception ex) { await (_logger?.WriteAsync("Startup update check failed", ex) ?? Task.CompletedTask); }
    }
}
