using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Updates;
using QuickResponseBao.Infrastructure.ImportExport;
using Microsoft.Win32;
using System.Windows.Threading;
using QuickResponseBao.Infrastructure.Diagnostics;

namespace QuickResponseBao.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _diagnosticTimer;
    private bool _initialized;
    private UpdateWindow? _updateWindow;
    private App Runtime => (App)System.Windows.Application.Current;
    private static string T(string key) => LocalizationService.Get(key);
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent(); DataContext = _viewModel = viewModel;
        DashboardVersionValue.Text = ApplicationVersion.Current; AboutVersionValue.Text = $"{T("Version")} {ApplicationVersion.Current}"; RefreshPageTitle();
        _diagnosticTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _diagnosticTimer.Tick += (_, _) => RefreshDiagnostics(); Loaded += (_, _) => _diagnosticTimer.Start(); Closed += (_, _) => _diagnosticTimer.Stop();
    }
    public async Task InitializeAsync(AppSettings settings)
    {
        _viewModel.Settings = settings; WhitelistText.Text = string.Join(Environment.NewLine, settings.AllowedProcesses);
        SelectTag(ThemeBox, settings.Theme); SelectTag(UpdateBehaviorBox, settings.AutoDownloadUpdates ? "Download" : "Notify");
        StartupUpdateCheckBox.IsChecked = settings.CheckUpdatesOnStartup; PrereleaseCheckBox.IsChecked = settings.IncludePrereleaseUpdates;
        _initialized = true;
        await _viewModel.RefreshAsync(); UpdateListenerDisplay();
    }
    public Task RefreshAsync() => _viewModel.RefreshAsync();
    public void OpenSettings() { MainTabs.SelectedIndex = 6; RefreshPageTitle(); }
    public void AddResponse() => Add_Click(this, new RoutedEventArgs());

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ResponseEditorWindow { Owner = this };
        if (editor.ShowDialog() == true) { await Runtime.Repository.UpsertAsync(editor.Response); await ChangedAsync(T("ResponseSaved")); }
    }
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } selected) return;
        var editor = new ResponseEditorWindow(selected) { Owner = this };
        if (editor.ShowDialog() == true) { await Runtime.Repository.UpsertAsync(editor.Response); await ChangedAsync(T("ResponseUpdated")); }
    }
    private async void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return;
        var copy = new QuickResponse { Summary = $"{x.Summary} {T("CopySuffix")}", Content = x.Content, Keywords = [.. x.Keywords], Category = x.Category, Language = x.Language, IsEnabled = x.IsEnabled, SortOrder = x.SortOrder };
        await Runtime.Repository.UpsertAsync(copy); await ChangedAsync(T("ResponseDuplicated"));
    }
    private async void ToggleResponse_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return; x.IsEnabled = !x.IsEnabled;
        await Runtime.Repository.UpsertAsync(x); await ChangedAsync(T(x.IsEnabled ? "ResponseEnabled" : "ResponseDisabled"));
    }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return;
        if (System.Windows.MessageBox.Show(string.Format(T("ConfirmDeleteResponse"), x.Summary), T("Delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Runtime.Repository.DeleteAsync(x.Id); await ChangedAsync(T("ResponseDeleted"));
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = T("ImportFileFilter") };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            FeedbackText.Text = T("Loading"); LibraryToolbar.IsEnabled = false;
            if (Path.GetExtension(dialog.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                var excel = new ExcelQuickResponseService(); var preview = await excel.PreviewAsync(dialog.FileName);
                var mapping = new ImportPreviewWindow(preview, excel.SuggestMapping(preview.Headers)) { Owner = this };
                if (mapping.ShowDialog() != true) return;
                var outcome = await excel.ImportAsync(dialog.FileName, mapping.Mapping);
                var persisted = 0; var failures = outcome.Result.Failures.ToList();
                foreach (var item in outcome.Items)
                {
                    try { await Runtime.Repository.UpsertAsync(item.Response); persisted++; }
                    catch (Exception ex) { failures.Add(new ImportFailure(item.RowNumber, ex.Message)); }
                }
                var result = outcome.Result with { Succeeded = persisted, Failed = failures.Count, Failures = failures };
                await Runtime.ReloadCacheAsync(); await _viewModel.RefreshAsync(); ShowImportResult(result); return;
            }
            var files = new QuickResponseFileService(); var items = Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? await files.ImportJsonAsync(dialog.FileName) : await files.ImportCsvAsync(dialog.FileName);
            foreach (var item in items) await Runtime.Repository.UpsertAsync(item);
            await ChangedAsync(string.Format(T("ImportSucceededCount"), items.Count));
        }
        catch (Exception ex) { ReportError("Import failed", T("ImportFailed"), ex); }
        finally { LibraryToolbar.IsEnabled = true; }
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = T("ExportFileFilter"), FileName = "quick-responses.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            LibraryToolbar.IsEnabled = false; FeedbackText.Text = T("Loading");
            var items = await Runtime.Repository.GetAllAsync(); var files = new QuickResponseFileService(); var extension = Path.GetExtension(dialog.FileName);
            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) await new ExcelQuickResponseService().ExportAsync(dialog.FileName, items, _viewModel.Settings.Language != "en-US");
            else if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)) await files.ExportCsvAsync(dialog.FileName, items);
            else await files.ExportJsonAsync(dialog.FileName, items);
            FeedbackText.Text = string.Format(T("ExportSucceededCount"), items.Count);
        }
        catch (Exception ex) { ReportError("Export failed", T("ExportFailed"), ex); }
        finally { LibraryToolbar.IsEnabled = true; }
    }
    private void ShowImportResult(DetailedImportResult result)
    {
        var details = string.Join(Environment.NewLine, result.Failures.Take(10).Select(x => $"#{x.RowNumber}: {x.Reason}"));
        var text = $"{T("Total")}: {result.Total}\n{T("Succeeded")}: {result.Succeeded}\n{T("Failed")}: {result.Failed}\n{T("Skipped")}: {result.Skipped}";
        if (details.Length > 0) text += $"\n\n{details}";
        FeedbackText.Text = text.ReplaceLineEndings(" · "); System.Windows.MessageBox.Show(text, T("ImportComplete"));
    }

    private IReadOnlyList<Guid> SelectedIds() => ResponsesGrid.SelectedItems.Cast<QuickResponse>().Select(x => x.Id).Distinct().ToList();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => ResponsesGrid.SelectAll();
    private void ClearSelection_Click(object sender, RoutedEventArgs e) => ResponsesGrid.UnselectAll();
    private async void BatchEnable_Click(object sender, RoutedEventArgs e) => await RunBatchAsync(ids => Runtime.Repository.SetEnabledAsync(ids, true));
    private async void BatchDisable_Click(object sender, RoutedEventArgs e) => await RunBatchAsync(ids => Runtime.Repository.SetEnabledAsync(ids, false));
    private async void BatchDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedIds().Count == 0) { FeedbackText.Text = T("NoSelection"); return; }
        if (System.Windows.MessageBox.Show(T("ConfirmBatchDelete"), T("BatchDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunBatchAsync(ids => Runtime.Repository.DeleteManyAsync(ids));
    }
    private async void BatchMove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedIds().Count == 0) { FeedbackText.Text = T("NoSelection"); return; }
        var categories = await Runtime.CategoryRepository.GetCategoriesAsync(); var choice = new CategoryChoiceWindow(categories) { Owner = this };
        if (choice.ShowDialog() == true && choice.SelectedCategory is { } category)
            await RunBatchAsync(ids => Runtime.Repository.MoveToCategoryAsync(ids, category.Name));
    }
    private async Task RunBatchAsync(Func<IReadOnlyCollection<Guid>, Task<BatchOperationResult>> operation)
    {
        var ids = SelectedIds(); if (ids.Count == 0) { FeedbackText.Text = T("NoSelection"); return; }
        LibraryToolbar.IsEnabled = false; ResponsesGrid.IsEnabled = false; FeedbackText.Text = T("Loading");
        try
        {
            var result = await operation(ids); await Runtime.ReloadCacheAsync(); await _viewModel.RefreshAsync();
            var message = $"{T("BatchComplete")}: {T("ActualProcessed")}: {result.Processed}, {T("Failed")}: {result.Failed}";
            FeedbackText.Text = message;
        }
        catch (Exception ex) { ReportError("Batch operation failed", T("OperationFailed"), ex); }
        finally { LibraryToolbar.IsEnabled = true; ResponsesGrid.IsEnabled = true; }
    }
    private async void Categories_Click(object sender, RoutedEventArgs e)
    {
        new CategoryManagerWindow(Runtime.CategoryRepository) { Owner = this }.ShowDialog(); await ChangedAsync(T("Succeeded"));
    }
    private async void Backups_Click(object sender, RoutedEventArgs e)
    {
        var manager = new BackupManagerWindow(Runtime.BackupService) { Owner = this }; manager.ShowDialog();
        if (manager.DatabaseRestored) await ChangedAsync(T("RestoreSucceeded"));
    }
    private async Task ChangedAsync(string feedback) { await Runtime.ReloadCacheAsync(); await _viewModel.RefreshAsync(); FeedbackText.Text = feedback; }

    private void ToggleListener_Click(object sender, RoutedEventArgs e) { if (Runtime.Listener.IsRunning) Runtime.PauseListener(); else Runtime.TryStartListener(); UpdateListenerDisplay(); }
    private void UpdateListenerDisplay()
    {
        if (Runtime.Listener?.IsRunning == true) { ListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerEnabled"); DashboardListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerEnabled"); ListenerButton.SetResourceReference(ContentControl.ContentProperty, "Pause"); ListenerStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush"); }
        else { ListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerPaused"); DashboardListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerPaused"); ListenerButton.SetResourceReference(ContentControl.ContentProperty, "Resume"); ListenerStatus.SetResourceReference(TextBlock.ForegroundProperty, "WarningBrush"); }
    }
    private async void SwitchLanguage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.Language = _viewModel.Settings.Language == "en-US" ? "zh-CN" : "en-US";
        LocalizationService.Apply(_viewModel.Settings.Language); _updateWindow?.RefreshLocalization();
        AboutVersionValue.Text = $"{T("Version")} {ApplicationVersion.Current}"; RefreshPageTitle();
        await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = T("LanguageUpdated");
    }
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.AllowedProcesses = WhitelistText.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        _viewModel.Settings.CheckUpdatesOnStartup = StartupUpdateCheckBox.IsChecked == true;
        _viewModel.Settings.AutoDownloadUpdates = SelectedTag(UpdateBehaviorBox) == "Download";
        _viewModel.Settings.NotifyOnlyForUpdates = !_viewModel.Settings.AutoDownloadUpdates;
        _viewModel.Settings.IncludePrereleaseUpdates = PrereleaseCheckBox.IsChecked == true;
        SaveSettingsButton.IsEnabled = false; FeedbackText.Text = T("Loading");
        try { await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = T("SettingsSaved"); }
        catch (Exception ex) { ReportError("Settings save failed", T("OperationFailed"), ex); }
        finally { SaveSettingsButton.IsEnabled = true; }
    }
    private void OpenRepository_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/guanchengwang11-ux/Quick-Response-Bao") { UseShellExecute = true });
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false; FeedbackText.Text = T("CheckingUpdates");
        try
        {
            await ShowUpdateWindowAsync();
            FeedbackText.Text = T("UpdateWindowOpened");
        }
        catch (Exception ex) { ReportError("Update check failed", T("UpdateCheckFailed"), ex); }
        finally { CheckUpdatesButton.IsEnabled = true; }
    }

    public async Task ShowUpdateWindowAsync(UpdateCheckResult? initial = null, bool automaticDownload = false)
    {
        if (_updateWindow?.IsVisible == true) { _updateWindow.Activate(); return; }
        var current = ApplicationVersion.Current;
        _updateWindow = new UpdateWindow(Runtime.UpdatesClient, Runtime.Paths, current, _viewModel.Settings.IncludePrereleaseUpdates, initial);
        if (IsVisible) _updateWindow.Owner = this;
        _updateWindow.Closed += (_, _) => _updateWindow = null; _updateWindow.Show();
        if (automaticDownload) await _updateWindow.StartAutomaticDownloadAsync();
    }

    private async void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || SelectedTag(ThemeBox) is not { } theme) return;
        _viewModel.Settings.Theme = theme; ThemeBox.IsEnabled = false;
        try { await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = T("ThemeApplied"); }
        catch (Exception ex) { ReportError("Theme save failed", T("OperationFailed"), ex); }
        finally { ThemeBox.IsEnabled = true; }
    }

    private void RefreshDiagnostics()
    {
        if (!_initialized || MainTabs.SelectedItem != DiagnosticsTab) return;
        var snapshot = Runtime.GetDiagnosticSnapshot();
        ForegroundProcessValue.Text = Empty(snapshot.ForegroundProcess); WindowTitleValue.Text = Empty(snapshot.WindowTitle);
        FocusProcessValue.Text = Empty(snapshot.FocusProcess);
        WhitelistStateValue.Text = YesNo(snapshot.IsWhitelisted); HookStateValue.Text = YesNo(snapshot.HookRunning); TextInputStateValue.Text = YesNo(snapshot.TextInputDetected);
        TextInputUnknownValue.Text = YesNo(snapshot.TextInputUnknown); PasswordFieldValue.Text = YesNo(snapshot.PasswordFieldDetected); UiAutomationValue.Text = YesNo(snapshot.UiAutomationUnavailable);
        BufferLengthValue.Text = snapshot.SearchBufferLength.ToString(); PositionMethodValue.Text = T($"Position{snapshot.CandidatePosition}");
        LastPasteValue.Text = snapshot.LastPasteSucceeded is null ? T("NotTested") : YesNo(snapshot.LastPasteSucceeded.Value); LastFailureValue.Text = Empty(snapshot.LastFailureReason);
        CapturedTargetWindowValue.Text = Empty(snapshot.CapturedTargetWindow);
        ConfirmationTargetWindowValue.Text = Empty(snapshot.ConfirmationTargetWindow);
        FocusRestoreValue.Text = snapshot.LastFocusRestored is null ? T("NotTested") : YesNo(snapshot.LastFocusRestored.Value);
        DeletedCharacterCountValue.Text = snapshot.LastDeletedCharacterCount.ToString();
        DeletionSucceededValue.Text = snapshot.LastDeletionSucceeded is null ? T("NotTested") : YesNo(snapshot.LastDeletionSucceeded.Value);
        ReplacementMethodValue.Text = Empty(snapshot.LastReplacementMethod);
        PasteSentCountValue.Text = snapshot.LastPasteSentCount?.ToString() ?? T("NotTested");
        PasteErrorCodeValue.Text = snapshot.LastPasteErrorCode?.ToString() ?? T("NotTested");
        PasteInputSizeValue.Text = snapshot.LastPasteInputSize?.ToString() ?? T("NotTested");
        PasteTargetProcessValue.Text = Empty(snapshot.LastPasteTargetProcess);
        PastePermissionValue.Text = snapshot.LastPasteSamePermissionLevel is null ? T("Unavailable") : YesNo(snapshot.LastPasteSamePermissionLevel.Value);
        ClipboardRestoredValue.Text = snapshot.LastClipboardRestored is null ? T("NotTested") : YesNo(snapshot.LastClipboardRestored.Value);
        LogDirectoryValue.Text = snapshot.LogDirectory;
    }

    private async void TestCandidate_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticActions.IsEnabled = false; FeedbackText.Text = T("SwitchToTargetHint");
        try { WindowState = WindowState.Minimized; await Task.Delay(2000); Runtime.TestCandidateWindow(); FeedbackText.Text = T("Succeeded"); }
        catch (Exception ex) { ReportError("Candidate test failed", T("OperationFailed"), ex); }
        finally { DiagnosticActions.IsEnabled = true; }
    }
    private async void TestPaste_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(T("PasteTestConfirm"), T("Diagnostics"), MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        DiagnosticActions.IsEnabled = false; FeedbackText.Text = T("SwitchToTargetHint"); WindowState = WindowState.Minimized;
        try { await Task.Delay(2000); await Runtime.TestPasteAsync(); FeedbackText.Text = T("Succeeded"); }
        catch (Exception ex) { ReportError("Paste test failed", T("OperationFailed"), ex); }
        finally { DiagnosticActions.IsEnabled = true; RefreshDiagnostics(); }
    }
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", Runtime.Paths.Logs) { UseShellExecute = true }); FeedbackText.Text = T("Succeeded"); }
        catch (Exception ex) { ReportError("Open logs failed", T("OperationFailed"), ex); }
    }
    private async void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON|*.json", FileName = $"QuickResponseBao-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json" };
        if (dialog.ShowDialog(this) != true) return;
        DiagnosticActions.IsEnabled = false; FeedbackText.Text = T("Loading");
        try
        {
            var value = Runtime.GetDiagnosticSnapshot();
            await new SafeDiagnosticReportService().ExportAsync(dialog.FileName, value, ApplicationVersion.Current); FeedbackText.Text = T("DiagnosticsExported");
        }
        catch (Exception ex) { ReportError("Diagnostic export failed", T("OperationFailed"), ex); }
        finally { DiagnosticActions.IsEnabled = true; }
    }

    private static string? SelectedTag(System.Windows.Controls.ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString();
    private static void SelectTag(System.Windows.Controls.ComboBox box, string value) => box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => string.Equals(x.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase));
    private static string Empty(string value) => string.IsNullOrWhiteSpace(value) ? T("Unavailable") : value;
    private static string YesNo(bool value) => T(value ? "Yes" : "No");

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: not null } button && int.TryParse(button.Tag.ToString(), out var index))
        { MainTabs.SelectedIndex = index; RefreshPageTitle(); if (index == 5) RefreshDiagnostics(); }
    }

    private void RefreshPageTitle()
    {
        var keys = new[] { "Dashboard", "Library", "Categories", "ImportExport", "ApplicationWhitelist", "Diagnostics", "Settings", "About" };
        PageTitle.Text = T(keys[Math.Clamp(MainTabs.SelectedIndex, 0, keys.Length - 1)]);
        var buttons = new[] { DashboardNav, LibraryNav, CategoriesNav, ImportExportNav, WhitelistNav, DiagnosticsNav, SettingsNav, AboutNav };
        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index].Background = System.Windows.Media.Brushes.Transparent;
            buttons[index].FontWeight = index == MainTabs.SelectedIndex ? FontWeights.SemiBold : FontWeights.Normal;
            if (index == MainTabs.SelectedIndex) buttons[index].SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "SelectionBrush");
        }
    }

    private async void CaptureProcess_Click(object sender, RoutedEventArgs e)
    {
        CaptureProcessButton.IsEnabled = false; WindowState = WindowState.Minimized;
        try
        {
            for (var remaining = 3; remaining > 0; remaining--)
            { FeedbackText.Text = string.Format(T("CaptureCountdown"), remaining); await Task.Delay(1000); }
            var processName = Runtime.Listener.InspectEnvironment().ProcessName;
            if (string.IsNullOrWhiteSpace(processName)) { FeedbackText.Text = T("CaptureApplicationFailed"); return; }
            await AddAllowedProcessAsync(processName, true);
        }
        catch (Exception ex) { ReportError("Foreground process capture failed", T("OperationFailed"), ex); }
        finally { CaptureProcessButton.IsEnabled = true; WindowState = WindowState.Normal; Activate(); }
    }

    private void RefreshProcesses_Click(object sender, RoutedEventArgs e)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            try { if (process.MainWindowHandle != 0) names.Add($"{process.ProcessName}.exe"); }
            catch { }
            finally { process.Dispose(); }
        }
        RunningProcessesBox.ItemsSource = names.OrderBy(x => x).ToList();
        if (RunningProcessesBox.Items.Count > 0) RunningProcessesBox.SelectedIndex = 0;
    }

    private async void AddSelectedProcess_Click(object sender, RoutedEventArgs e)
    {
        if (RunningProcessesBox.SelectedItem is string processName) await AddAllowedProcessAsync(processName, false);
    }

    private async Task AddAllowedProcessAsync(string processName, bool captured)
    {
        var values = WhitelistText.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (ProcessWhitelist.Contains(values, processName)) { FeedbackText.Text = T("ApplicationAlreadyAllowed"); return; }
        values.Add(processName); WhitelistText.Text = string.Join(Environment.NewLine, values);
        await SaveWhitelistCoreAsync(); FeedbackText.Text = captured ? string.Format(T("ApplicationCaptured"), processName) : T("WhitelistSaved");
    }

    private async void SaveWhitelist_Click(object sender, RoutedEventArgs e) => await SaveWhitelistCoreAsync();
    private async Task SaveWhitelistCoreAsync()
    {
        _viewModel.Settings.AllowedProcesses = WhitelistText.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = T("WhitelistSaved");
    }

    private void CopyFeedback_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(FeedbackText.Text)) System.Windows.Clipboard.SetText(FeedbackText.Text);
    }

    private void ReportError(string context, string localizedPrefix, Exception exception)
    {
        FeedbackText.Text = $"{localizedPrefix}: {exception.Message}";
        _ = Runtime.LogSafeErrorAsync(context, exception);
    }
}
