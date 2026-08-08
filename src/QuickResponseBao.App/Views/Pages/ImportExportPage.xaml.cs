using System.Data;
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
    private readonly MainViewModel _viewModel;
    private readonly Action<string> _feedback;
    private readonly Func<string, Task<IReadOnlyList<QuickResponse>>> _resolveExportScope;
    private readonly ExcelQuickResponseService _excel = new();
    private readonly QuickResponseFileService _files = new();
    private string? _selectedPath;
    private ImportPreview? _preview;
    private ExcelImportOutcome? _pendingOutcome;
    private DetailedImportResult? _result;
    private bool _isExcel;

    public ImportExportPage(MainViewModel viewModel, Action<string> feedback, Func<string, Task<IReadOnlyList<QuickResponse>>> resolveExportScope)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _feedback = feedback;
        _resolveExportScope = resolveExportScope;
        ShowStep(1);
    }

    public Task RefreshAsync() => Task.CompletedTask;

    private async void SelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = LocalizationService.Get("ImportFileFilter") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        await LoadWithFeedbackAsync(dialog.FileName);
    }

    public async Task LoadFileAsync(string path)
    {
        _selectedPath = path;
        SelectedFileText.Text = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        _isExcel = extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        if (_isExcel)
        {
            _preview = await _excel.PreviewAsync(path, 20);
            _pendingOutcome = null;
            PopulateMapping(_preview.Headers, _excel.SuggestMapping(_preview.Headers), true);
        }
        else
        {
            _pendingOutcome = extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? await _files.ImportJsonOutcomeAsync(path) : await _files.ImportCsvOutcomeAsync(path);
            _preview = BuildStandardPreview(_pendingOutcome);
            PopulateMapping(_preview.Headers, _excel.SuggestMapping(_preview.Headers), false);
        }
        PopulatePreview(_preview);
        ShowStep(2);
    }

    private async Task LoadWithFeedbackAsync(string path)
    {
        await RunBusyAsync(async () =>
        {
            await LoadFileAsync(path);
            _feedback(string.Format(LocalizationService.Get("FileReadyForPreview"), Path.GetFileName(path)));
        }, "ImportFailed");
    }

    private async void Validate_Click(object sender, RoutedEventArgs e) => await ValidateCurrentImportAsync();

    public async Task ValidateCurrentImportAsync()
    {
        if (_selectedPath is null) return;
        await RunBusyAsync(async () =>
        {
            if (_isExcel) _pendingOutcome = await _excel.ImportAsync(_selectedPath, CurrentMapping());
            if (_pendingOutcome is null) return;
            var existing = await _viewModel.Repository.GetAllAsync();
            var validation = ImportValidationService.Analyze(_pendingOutcome, existing);
            ValidCountText.Text = validation.Valid.ToString();
            DuplicateCountText.Text = validation.Duplicate.ToString();
            FailedCountText.Text = validation.Failed.ToString();
            ValidationErrorsList.ItemsSource = validation.Details.Select(FormatFailure).ToList();
            ShowStep(3);
        }, "ImportFailed");
    }

    private async void ImportNow_Click(object sender, RoutedEventArgs e) => await ExecuteImportAsync();

    public async Task ExecuteImportAsync()
    {
        if (_pendingOutcome is null) return;
        await RunBusyAsync(async () =>
        {
            _result = await new QuickResponseImportCoordinator(_viewModel.Repository).PersistAsync(_pendingOutcome);
            if (System.Windows.Application.Current is App app) await app.ReloadCacheAsync();
            await _viewModel.RefreshAsync();
            ImportedResultText.Text = _result.Succeeded.ToString();
            DuplicateResultText.Text = _result.DuplicateSkipped.ToString();
            FailedResultText.Text = _result.Failed.ToString();
            ResultErrorsList.ItemsSource = _result.Failures.Concat(_result.SkippedDetails ?? []).Select(FormatFailure).ToList();
            ShowStep(4);
            _feedback(LocalizationService.Get("ImportComplete"));
        }, "ImportFailed");
    }

    private async void Template_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Quick-Response-Bao-Import-Template.xlsx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        await RunBusyAsync(async () =>
        {
            await _excel.ExportAsync(dialog.FileName, [], _viewModel.Settings.Language != "en-US");
            _feedback(string.Format(LocalizationService.Get("TemplateSaved"), dialog.FileName));
        }, "ExportFailed");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var format = SelectedTag(ExportFormatBox) ?? "xlsx";
        var scope = SelectedTag(ExportScopeBox) ?? "all";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = format switch { "csv" => "CSV|*.csv", "json" => "JSON|*.json", _ => "Excel|*.xlsx" },
            FileName = $"quick-responses.{format}"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        await RunBusyAsync(async () =>
        {
            var items = await _resolveExportScope(scope);
            if (IncludeDisabledBox.IsChecked != true) items = items.Where(x => x.IsEnabled).ToList();
            if (format == "xlsx") await _excel.ExportAsync(dialog.FileName, items, _viewModel.Settings.Language != "en-US");
            else if (format == "csv") await _files.ExportCsvAsync(dialog.FileName, items);
            else await _files.ExportJsonAsync(dialog.FileName, items);
            ExportPathText.Text = string.Format(LocalizationService.Get("ExportSavedPath"), dialog.FileName);
            _feedback(string.Format(LocalizationService.Get("ExportSucceededCount"), items.Count));
        }, "ExportFailed");
    }

    private void PopulatePreview(ImportPreview preview)
    {
        var table = new DataTable();
        foreach (var header in preview.Headers) table.Columns.Add(header);
        foreach (var row in preview.Rows.Take(20))
            table.Rows.Add(preview.Headers.Select(header => row.Values.TryGetValue(header, out var value) ? value : string.Empty).ToArray());
        PreviewGrid.ItemsSource = table.DefaultView;
    }

    private void PopulateMapping(IReadOnlyList<string> headers, ImportFieldMapping mapping, bool editable)
    {
        foreach (var (box, field) in MappingBoxes())
        {
            box.ItemsSource = new[] { string.Empty }.Concat(headers).ToList();
            box.SelectedItem = mapping.Get(field) ?? string.Empty;
            box.IsEnabled = editable;
        }
    }

    private ImportFieldMapping CurrentMapping()
    {
        var columns = new Dictionary<QuickResponseField, string>();
        foreach (var (box, field) in MappingBoxes())
            if (box.SelectedItem?.ToString() is { Length: > 0 } value) columns[field] = value;
        if (!columns.ContainsKey(QuickResponseField.Summary) || !columns.ContainsKey(QuickResponseField.Content))
            throw new InvalidDataException(LocalizationService.Get("RequiredMapping"));
        return new ImportFieldMapping(columns);
    }

    private IEnumerable<(System.Windows.Controls.ComboBox Box, QuickResponseField Field)> MappingBoxes() =>
    [
        (SummaryMap, QuickResponseField.Summary), (ContentMap, QuickResponseField.Content),
        (KeywordsMap, QuickResponseField.Keywords), (CategoryMap, QuickResponseField.Category),
        (LanguageMap, QuickResponseField.Language), (EnabledMap, QuickResponseField.IsEnabled),
        (SortOrderMap, QuickResponseField.SortOrder)
    ];

    private static ImportPreview BuildStandardPreview(ExcelImportOutcome outcome)
    {
        var headers = new[] { "Summary", "Content", "Keywords", "Category", "Language", "IsEnabled", "SortOrder" };
        var rows = outcome.Items.Take(20).Select(item => new ImportPreviewRow(item.RowNumber, new Dictionary<string, string>
        {
            ["Summary"] = item.Response.Summary, ["Content"] = item.Response.Content,
            ["Keywords"] = string.Join("; ", item.Response.Keywords), ["Category"] = item.Response.Category,
            ["Language"] = item.Response.Language, ["IsEnabled"] = item.Response.IsEnabled.ToString(),
            ["SortOrder"] = item.Response.SortOrder.ToString()
        })).ToList();
        return new ImportPreview(headers, rows, outcome.Result.Total);
    }

    private string FormatFailure(ImportFailure failure)
    {
        var reason = LocalizationService.Get(failure.Reason);
        if (reason == failure.Reason) reason = failure.Reason;
        if (failure.ReferenceRow is { } row) reason = string.Format(reason, row);
        return $"#{failure.RowNumber}: {reason}";
    }

    private void ShowStep(int step)
    {
        Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;
        var indicators = new[] { Step1Indicator, Step2Indicator, Step3Indicator, Step4Indicator };
        for (var index = 0; index < indicators.Length; index++)
        {
            indicators[index].SetResourceReference(Border.BackgroundProperty, index + 1 == step ? "QrbSelectionBrush" : "QrbSurfaceSecondaryBrush");
            indicators[index].SetResourceReference(Border.BorderBrushProperty, index + 1 == step ? "QrbFocusRingBrush" : "QrbBorderBrush");
            indicators[index].BorderThickness = new Thickness(1);
        }
    }

    private async Task RunBusyAsync(Func<Task> action, string errorKey)
    {
        RootPanel.IsEnabled = false;
        try { await action(); }
        catch (Exception ex)
        {
            _feedback($"{LocalizationService.Get(errorKey)}: {ex.Message}");
            if (System.Windows.Application.Current is App app) await app.LogSafeErrorAsync(errorKey, ex);
        }
        finally { RootPanel.IsEnabled = true; }
    }

    private static string? SelectedTag(System.Windows.Controls.ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString();
    private void PreviewGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e) =>
        e.Column.Width = e.PropertyName.Equals("Content", StringComparison.OrdinalIgnoreCase) ? 280 : 135;
    private void ImportExportTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (ImportExportTabs?.SelectedIndex == 1) _feedback(string.Empty); }
    private void BackToStep1_Click(object sender, RoutedEventArgs e) => ShowStep(1);
    private void BackToStep2_Click(object sender, RoutedEventArgs e) => ShowStep(2);
    private void ViewFailed_Click(object sender, RoutedEventArgs e) => ResultErrorsList.Visibility = ResultErrorsList.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void Done_Click(object sender, RoutedEventArgs e) { _selectedPath = null; _pendingOutcome = null; SelectedFileText.Text = string.Empty; ShowStep(1); }
}
