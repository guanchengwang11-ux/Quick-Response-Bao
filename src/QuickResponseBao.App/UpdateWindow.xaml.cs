using System.IO;
using System.Net.Http;
using System.Windows;
using QuickResponseBao.App.Services;
using QuickResponseBao.Infrastructure.Storage;
using QuickResponseBao.Infrastructure.Updates;

namespace QuickResponseBao.App;

public partial class UpdateWindow : Window
{
    private readonly HttpClient _client;
    private readonly AppPaths _paths;
    private readonly string _currentVersion;
    private readonly bool _includePrerelease;
    private UpdateCheckResult? _checkResult;
    private SelectedUpdateAsset? _selection;
    private string? _downloadedPath;
    private CancellationTokenSource? _downloadCancellation;
    private bool _busy;

    public UpdateWindow(HttpClient client, AppPaths paths, string currentVersion, bool includePrerelease, UpdateCheckResult? initial = null)
    {
        InitializeComponent(); _client = client; _paths = paths; _currentVersion = currentVersion; _includePrerelease = includePrerelease;
        if (initial is not null) ApplyResult(initial); else Loaded += async (_, _) => await CheckAsync();
        Closing += (_, e) => { if (_busy) { _downloadCancellation?.Cancel(); e.Cancel = true; } };
    }

    public async Task StartAutomaticDownloadAsync()
    {
        if (_checkResult is null) await CheckAsync();
        if (_selection is not null) await DownloadAsync();
    }

    public void RefreshLocalization()
    {
        if (_downloadCancellation is not null) SetStatus("⏳", LocalizationService.Get("DownloadingUpdate"));
        else if (_downloadedPath is not null) SetStatus("✓", LocalizationService.Get("DownloadVerified"));
        else if (_checkResult?.IsUpdateAvailable == true) SetStatus("!", LocalizationService.Get("UpdateAvailable"));
        else if (_checkResult is not null) SetStatus("✓", LocalizationService.Get("LatestVersionMessage"));
    }

    private async void Check_Click(object sender, RoutedEventArgs e) => await CheckAsync();
    private async Task CheckAsync()
    {
        SetBusy(true); SetStatus("⏳", LocalizationService.Get("CheckingUpdates"));
        try
        {
            var result = await new GitHubUpdateService(_client).CheckAsync(_currentVersion, _includePrerelease); ApplyResult(result);
        }
        catch (Exception ex) { ClearUpdate(); SetStatus("✗", $"{LocalizationService.Get("UpdateCheckFailed")}: {ex.Message}"); }
        finally { SetBusy(false); }
    }

    private void ApplyResult(UpdateCheckResult result)
    {
        _checkResult = result; _downloadedPath = null; InstallButton.IsEnabled = false;
        VersionText.Text = result.LatestVersion ?? LocalizationService.Get("NoPublishedRelease");
        if (!result.IsUpdateAvailable || result.Update is null)
        {
            _selection = null; FileNameText.Text = FileSizeText.Text = string.Empty; NotesText.Text = string.Empty;
            DownloadButton.IsEnabled = false; SetStatus("✓", LocalizationService.Get("LatestVersionMessage")); return;
        }
        NotesText.Text = result.Update.Notes;
        try
        {
            var installedSetup = File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));
            _selection = ReleaseAssetSelector.Select(result.Update, installedSetup); FileNameText.Text = _selection.Asset.Name; FileSizeText.Text = FormatBytes(_selection.Asset.Size);
            DownloadButton.IsEnabled = true; SetStatus("!", LocalizationService.Get("UpdateAvailable"));
        }
        catch (Exception ex) { _selection = null; DownloadButton.IsEnabled = false; SetStatus("✗", ErrorText(ex)); }
    }

    private async void Download_Click(object sender, RoutedEventArgs e) => await DownloadAsync();
    private async Task DownloadAsync()
    {
        if (_selection is null || _busy) return;
        _downloadCancellation = new CancellationTokenSource(); DownloadProgress.Value = 0; SetBusy(true, downloading: true);
        SetStatus("⏳", LocalizationService.Get("DownloadingUpdate"));
        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            DownloadProgress.Value = value.Percentage;
            ProgressText.Text = $"{value.Percentage}% · {FormatBytes(value.DownloadedBytes)} / {FormatBytes(value.TotalBytes)}";
        });
        try
        {
            _downloadedPath = await new UpdateDownloadService(_client).DownloadVerifiedAsync(_selection, _paths.Updates, progress, 3, _downloadCancellation.Token);
            DownloadProgress.Value = 100; InstallButton.IsEnabled = true; SetStatus("✓", LocalizationService.Get("DownloadVerified"));
        }
        catch (OperationCanceledException) { SetStatus("!", LocalizationService.Get("DownloadCanceled")); }
        catch (Exception ex) { _downloadedPath = null; SetStatus("✗", $"{LocalizationService.Get("DownloadFailedRetry")}: {ErrorText(ex)}"); }
        finally { _downloadCancellation.Dispose(); _downloadCancellation = null; SetBusy(false); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _downloadCancellation?.Cancel();
    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_selection is null || _downloadedPath is null || !File.Exists(_downloadedPath)) return;
        var strategy = _selection.Kind == ReleaseAssetKind.Setup ? LocalizationService.Get("SetupSilentStrategy") : LocalizationService.Get("PackageUpdaterStrategy");
        if (System.Windows.MessageBox.Show($"{strategy}\n\n{LocalizationService.Get("ConfirmInstallUpdate")}", LocalizationService.Get("UpdateTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { ((App)System.Windows.Application.Current).InstallUpdate(_downloadedPath, _selection.Kind); }
        catch (Exception ex) { SetStatus("✗", $"{LocalizationService.Get("InstallLaunchFailed")}: {ex.Message}"); }
    }

    private void SetBusy(bool busy, bool downloading = false)
    {
        _busy = busy; CheckButton.IsEnabled = !busy; DownloadButton.IsEnabled = !busy && _selection is not null; CancelButton.IsEnabled = busy && downloading;
        if (busy) InstallButton.IsEnabled = false; else InstallButton.IsEnabled = _downloadedPath is not null;
    }
    private void ClearUpdate() { _selection = null; _downloadedPath = null; DownloadButton.IsEnabled = InstallButton.IsEnabled = false; }
    private void SetStatus(string symbol, string message) => StatusText.Text = $"{symbol} {message}";
    private static string ErrorText(Exception exception) => exception is UpdateOperationException updateError
        ? LocalizationService.Get($"UpdateError{updateError.Code}") : exception.Message;
    private static string FormatBytes(long bytes) => bytes < 1024 ? $"{bytes:N0} B" : bytes < 1024 * 1024 ? $"{bytes / 1024d:N1} KB" : $"{bytes / 1024d / 1024d:N1} MB";
}
