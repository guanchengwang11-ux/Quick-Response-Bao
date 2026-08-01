using System.Drawing;
using System.Windows;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Diagnostics;
using QuickResponseBao.Infrastructure.Storage;
using QuickResponseBao.Infrastructure.Windows;
using Forms = System.Windows.Forms;

namespace QuickResponseBao.App;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _tray;
    private CandidateWindow? _candidates;
    private ClipboardPasteService? _paste;
    private SafeFileLogger? _logger;
    private IReadOnlyList<QuickResponse> _cache = [];
    private bool _exiting;

    public AppPaths Paths { get; private set; } = null!;
    public IQuickResponseRepository Repository { get; private set; } = null!;
    public JsonSettingsStore SettingsStore { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public GlobalKeyboardListener Listener { get; private set; } = null!;
    public MainWindow MainAppWindow { get; private set; } = null!;
    public SearchService SearchService { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Paths = new AppPaths(); SettingsStore = new JsonSettingsStore(Paths); Settings = await SettingsStore.LoadAsync();
        LocalizationService.Apply(Settings.Language);
        Repository = new SqliteQuickResponseRepository(Paths); await Repository.InitializeAsync();
        _logger = new SafeFileLogger(Paths); await _logger.WriteAsync("Application started");
        await ReloadCacheAsync();
        _paste = new ClipboardPasteService(); _candidates = new CandidateWindow();
        _candidates.Confirmed += CandidateConfirmed;
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
        DispatcherUnhandledException += async (_, args) =>
        {
            await (_logger?.WriteAsync("Unhandled exception", args.Exception) ?? Task.CompletedTask);
            args.Handled = true; System.Windows.MessageBox.Show(args.Exception.Message, "Quick Response Bao");
        };
    }

    public async Task ReloadCacheAsync() => _cache = await Repository.GetAllAsync();
    public void TryStartListener()
    {
        try { Listener.Start(); }
        catch (Exception ex) { _ = _logger?.WriteAsync("Listener failed", ex); }
        UpdateTray();
    }
    public void PauseListener() { Listener.Stop(); HideSuggestions(); _ = _logger?.WriteAsync("Listener paused"); UpdateTray(); }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        Settings = settings; await SettingsStore.SaveAsync(settings); Listener.UpdateSettings(settings); UpdateTray();
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
            await _paste!.PasteAsync(response.Content, Settings.PreserveClipboard, Settings.RestoreClipboard, Settings.ClipboardRestoreDelayMs);
            await Repository.IncrementUsageAsync(response.Id); await ReloadCacheAsync(); await MainAppWindow.RefreshAsync();
            await (_logger?.WriteAsync("Paste succeeded") ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            await (_logger?.WriteAsync("Paste failed", ex) ?? Task.CompletedTask);
            System.Windows.MessageBox.Show($"Paste failed: {ex.Message}", "Quick Response Bao");
        }
    }

    private void CreateTray()
    {
        _tray = new Forms.NotifyIcon { Icon = SystemIcons.Information, Text = "Quick Response Bao", Visible = true };
        _tray.DoubleClick += (_, _) => ShowMainWindow(); UpdateTray();
    }
    public void UpdateTray()
    {
        if (_tray is null) return; var chinese = Settings.Language != "en-US";
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(chinese ? "打开 Quick Response Bao" : "Open Quick Response Bao", null, (_, _) => ShowMainWindow());
        menu.Items.Add(Listener?.IsRunning == true ? (chinese ? "暂停话术检索" : "Pause response search") : (chinese ? "启用话术检索" : "Enable response search"), null, (_, _) => { if (Listener?.IsRunning == true) PauseListener(); else TryStartListener(); });
        menu.Items.Add(chinese ? "新增话术" : "Add response", null, (_, _) => { ShowMainWindow(); MainAppWindow.AddResponse(); });
        menu.Items.Add(chinese ? "设置" : "Settings", null, (_, _) => { ShowMainWindow(); MainAppWindow.OpenSettings(); });
        menu.Items.Add(chinese ? "退出" : "Exit", null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip?.Dispose(); _tray.ContextMenuStrip = menu;
    }
    private void ShowMainWindow() { MainAppWindow.Show(); MainAppWindow.WindowState = WindowState.Normal; MainAppWindow.Activate(); }
    private void MainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting || !Settings.MinimizeToTrayOnClose) return;
        e.Cancel = true; MainAppWindow.Hide();
        if (Settings.ShowNotifications) _tray?.ShowBalloonTip(2500, "Quick Response Bao", Settings.Language == "en-US" ? "Quick Response Bao is still running in the system tray." : "Quick Response Bao 将继续在系统托盘中运行。", Forms.ToolTipIcon.Info);
    }
    public async void ExitApplication()
    {
        _exiting = true; Listener?.Dispose(); _tray?.Dispose(); _candidates?.Close();
        await (_logger?.WriteAsync("Application exited") ?? Task.CompletedTask); MainAppWindow?.Close(); Shutdown();
    }
}
