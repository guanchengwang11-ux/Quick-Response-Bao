using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.ImportExport;

namespace QuickResponseBao.App.Views.Pages;

public partial class ImportExportPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel; private readonly Action<string> _feedback;
    private App Runtime => (App)System.Windows.Application.Current;
    public ImportExportPage(MainViewModel viewModel, Action<string> feedback) { InitializeComponent(); _viewModel = viewModel; _feedback = feedback; }
    public Task RefreshAsync() => Task.CompletedTask;
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = LocalizationService.Get("ImportFileFilter") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            ExcelImportOutcome outcome;
            if (Path.GetExtension(dialog.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                var excel = new ExcelQuickResponseService(); var preview = await excel.PreviewAsync(dialog.FileName);
                var mapping = new ImportPreviewWindow(preview, excel.SuggestMapping(preview.Headers)) { Owner = Window.GetWindow(this) };
                if (mapping.ShowDialog() != true) return; outcome = await excel.ImportAsync(dialog.FileName, mapping.Mapping);
            }
            else
            {
                var files = new QuickResponseFileService(); outcome = Path.GetExtension(dialog.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)
                    ? await files.ImportJsonOutcomeAsync(dialog.FileName) : await files.ImportCsvOutcomeAsync(dialog.FileName);
            }
            var result = await new QuickResponseImportCoordinator(Runtime.Repository).PersistAsync(outcome);
            await Runtime.ReloadCacheAsync(); await _viewModel.RefreshAsync();
            _feedback($"{LocalizationService.Get("Succeeded")}: {result.Succeeded}; {LocalizationService.Get("Failed")}: {result.Failed}; {LocalizationService.Get("Skipped")}: {result.Skipped}");
        }
        catch (Exception ex) { _feedback($"{LocalizationService.Get("ImportFailed")}: {ex.Message}"); await Runtime.LogSafeErrorAsync("Import failed", ex); }
    }
    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = LocalizationService.Get("ExportFileFilter"), FileName = "quick-responses.xlsx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            var items = await Runtime.Repository.GetAllAsync(); var files = new QuickResponseFileService(); var extension = Path.GetExtension(dialog.FileName);
            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) await new ExcelQuickResponseService().ExportAsync(dialog.FileName, items, _viewModel.Settings.Language != "en-US");
            else if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)) await files.ExportCsvAsync(dialog.FileName, items); else await files.ExportJsonAsync(dialog.FileName, items);
            _feedback(string.Format(LocalizationService.Get("ExportSucceededCount"), items.Count));
        }
        catch (Exception ex) { _feedback($"{LocalizationService.Get("ExportFailed")}: {ex.Message}"); await Runtime.LogSafeErrorAsync("Export failed", ex); }
    }
}
