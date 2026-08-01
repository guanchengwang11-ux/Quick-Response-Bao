using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Updates;
using QuickResponseBao.Infrastructure.ImportExport;
using Microsoft.Win32;

namespace QuickResponseBao.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private App Runtime => (App)System.Windows.Application.Current;
    private static string T(string key) => LocalizationService.Get(key);
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = _viewModel = viewModel; }
    public async Task InitializeAsync(AppSettings settings)
    {
        _viewModel.Settings = settings; WhitelistText.Text = string.Join(Environment.NewLine, settings.AllowedProcesses);
        await _viewModel.RefreshAsync(); UpdateListenerDisplay();
    }
    public Task RefreshAsync() => _viewModel.RefreshAsync();
    public void OpenSettings() => MainTabs.SelectedIndex = 2;
    public void AddResponse() => Add_Click(this, new RoutedEventArgs());

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ResponseEditorWindow { Owner = this };
        if (editor.ShowDialog() == true) { await Runtime.Repository.UpsertAsync(editor.Response); await ChangedAsync("Response saved."); }
    }
    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } selected) return;
        var editor = new ResponseEditorWindow(selected) { Owner = this };
        if (editor.ShowDialog() == true) { await Runtime.Repository.UpsertAsync(editor.Response); await ChangedAsync("Response updated."); }
    }
    private async void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return;
        var copy = new QuickResponse { Summary = $"{x.Summary} (copy)", Content = x.Content, Keywords = [.. x.Keywords], Category = x.Category, Language = x.Language, IsEnabled = x.IsEnabled, SortOrder = x.SortOrder };
        await Runtime.Repository.UpsertAsync(copy); await ChangedAsync("Response duplicated.");
    }
    private async void ToggleResponse_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return; x.IsEnabled = !x.IsEnabled;
        await Runtime.Repository.UpsertAsync(x); await ChangedAsync(x.IsEnabled ? "Response enabled." : "Response disabled.");
    }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResponse is not { } x) return;
        if (System.Windows.MessageBox.Show($"Delete ‘{x.Summary}’?", "Quick Response Bao", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Runtime.Repository.DeleteAsync(x.Id); await ChangedAsync("Response deleted.");
    }
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Excel, CSV or JSON|*.xlsx;*.csv;*.json|Excel workbook|*.xlsx|CSV|*.csv|JSON|*.json" };
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
            await ChangedAsync($"Imported {items.Count} responses.");
        }
        catch (Exception ex) { FeedbackText.Text = $"Import failed: {ex.Message}"; }
        finally { LibraryToolbar.IsEnabled = true; }
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel workbook|*.xlsx|JSON|*.json|CSV|*.csv", FileName = "quick-responses.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            LibraryToolbar.IsEnabled = false; FeedbackText.Text = T("Loading");
            var items = await Runtime.Repository.GetAllAsync(); var files = new QuickResponseFileService(); var extension = Path.GetExtension(dialog.FileName);
            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) await new ExcelQuickResponseService().ExportAsync(dialog.FileName, items, _viewModel.Settings.Language != "en-US");
            else if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)) await files.ExportCsvAsync(dialog.FileName, items);
            else await files.ExportJsonAsync(dialog.FileName, items);
            FeedbackText.Text = $"Exported {items.Count} responses.";
        }
        catch (Exception ex) { FeedbackText.Text = $"Export failed: {ex.Message}"; }
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
            FeedbackText.Text = message; System.Windows.MessageBox.Show(message, T("BatchComplete"));
        }
        catch (Exception ex) { FeedbackText.Text = $"{T("OperationFailed")}: {ex.Message}"; }
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
        if (Runtime.Listener?.IsRunning == true) { ListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerEnabled"); ListenerButton.SetResourceReference(ContentControl.ContentProperty, "Pause"); ListenerStatus.Foreground = System.Windows.Media.Brushes.ForestGreen; }
        else { ListenerStatus.SetResourceReference(TextBlock.TextProperty, "ListenerPaused"); ListenerButton.SetResourceReference(ContentControl.ContentProperty, "Resume"); ListenerStatus.Foreground = System.Windows.Media.Brushes.DarkGoldenrod; }
    }
    private async void SwitchLanguage_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.Language = _viewModel.Settings.Language == "en-US" ? "zh-CN" : "en-US";
        LocalizationService.Apply(_viewModel.Settings.Language); await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = "Language updated.";
    }
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Settings.AllowedProcesses = WhitelistText.Text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        try { await Runtime.SaveSettingsAsync(_viewModel.Settings); FeedbackText.Text = "Settings saved."; }
        catch (Exception ex) { FeedbackText.Text = $"Settings could not be saved: {ex.Message}"; }
    }
    private void OpenRepository_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/guanchengwang11-ux/Quick-Response-Bao") { UseShellExecute = true });
    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        FeedbackText.Text = "Checking for updates…";
        try
        {
            var update = await new GitHubUpdateService(new HttpClient()).CheckAsync("1.0.0");
            FeedbackText.Text = update is null ? (_viewModel.Settings.Language == "en-US" ? "You’re using the latest version." : "当前已经是最新版本。") : $"Version {update.Version} is available.";
        }
        catch (Exception ex) { FeedbackText.Text = $"Update check failed: {ex.Message}"; }
    }
}
