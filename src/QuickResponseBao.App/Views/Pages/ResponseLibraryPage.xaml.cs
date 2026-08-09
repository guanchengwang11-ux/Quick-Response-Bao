using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;

namespace QuickResponseBao.App.Views.Pages;

public partial class ResponseLibraryPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel;
    private readonly Action<string> _feedback;
    private readonly Action<string> _navigate;
    private readonly ListCollectionView _libraryView;
    private int _searchVersion;
    private bool _updatingFilters;
    private int _seenDataVersion = -1;
    private readonly ResponseLibraryFilterState _filterState = new();

    private IQuickResponseRepository Repository => _viewModel.Repository;
    private ICategoryRepository CategoryRepository => (ICategoryRepository)_viewModel.Repository;

    public ResponseLibraryPage(MainViewModel viewModel, Action<string> feedback, Action<string> navigate)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _feedback = feedback;
        _navigate = navigate;
        _libraryView = new ListCollectionView(_viewModel.Responses) { Filter = MatchesFilters };
        ResponsesGrid.ItemsSource = _libraryView;
        ColumnFilterPanel.Applied += (_, _) => ApplyFilters();
        ColumnFilterPanel.CloseRequested += (_, _) => FilterPopup.IsOpen = false;
        InitializeStaticFilters();
    }

    public async Task RefreshAsync()
    {
        LoadingOverlay.Visibility = _viewModel.IsLoaded ? Visibility.Collapsed : Visibility.Visible;
        await _viewModel.EnsureLoadedAsync();
        LoadingOverlay.Visibility = Visibility.Collapsed;
        if (_seenDataVersion != _viewModel.DataVersion) { RefreshFilterOptions(); _seenDataVersion = _viewModel.DataVersion; }
        _libraryView.Refresh();
        UpdateEmptyState();
    }

    public void AddResponse() => Add_Click(this, new RoutedEventArgs());
    public IReadOnlyList<QuickResponse> GetFilteredResponses() => _libraryView.Cast<QuickResponse>().ToList();
    public IReadOnlyList<QuickResponse> GetSelectedResponses() => ResponsesGrid.SelectedItems.Cast<QuickResponse>().ToList();

    private void InitializeStaticFilters()
    {
        _updatingFilters = true;
        StatusFilter.ItemsSource = new[]
        {
            new FilterOption(LocalizationService.Get("AllStatuses"), null),
            new FilterOption(LocalizationService.Get("EnabledOnly"), "enabled"),
            new FilterOption(LocalizationService.Get("DisabledOnly"), "disabled")
        };
        StatusFilter.SelectedIndex = 0;
        _updatingFilters = false;
    }

    private void RefreshFilterOptions()
    {
        _updatingFilters = true;
        var categoryValue = (CategoryFilter.SelectedItem as FilterOption)?.Value;
        var languageValue = (LanguageFilter.SelectedItem as FilterOption)?.Value;
        var categories = _viewModel.Responses.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x);
        CategoryFilter.ItemsSource = new[] { new FilterOption(LocalizationService.Get("AllCategories"), null) }
            .Concat(categories.Select(x => new FilterOption(x, x))).ToList();
        LanguageFilter.ItemsSource = new[] { new FilterOption(LocalizationService.Get("AllLanguages"), null) }
            .Concat(_viewModel.Responses.Select(x => x.Language).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Select(x => new FilterOption(x, x))).ToList();
        CategoryFilter.SelectedItem = CategoryFilter.Items.Cast<FilterOption>().FirstOrDefault(x => x.Value == categoryValue) ?? CategoryFilter.Items[0];
        LanguageFilter.SelectedItem = LanguageFilter.Items.Cast<FilterOption>().FirstOrDefault(x => x.Value == languageValue) ?? LanguageFilter.Items[0];
        _updatingFilters = false;
    }

    private bool MatchesFilters(object value)
    {
        if (value is not QuickResponse response) return false;
        return _filterState.Matches(response);
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var version = ++_searchVersion;
        await Task.Delay(150);
        if (version != _searchVersion) return;
        _viewModel.SearchText = SearchBox.Text; _filterState.GlobalSearch = SearchBox.Text; ApplyFilters();
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFilters || _libraryView is null) return;
        _filterState.Categories.Clear(); _filterState.Languages.Clear(); _filterState.Statuses.Clear();
        if ((CategoryFilter.SelectedItem as FilterOption)?.Value is { } category) _filterState.Categories.Add(category);
        if ((LanguageFilter.SelectedItem as FilterOption)?.Value is { } language) _filterState.Languages.Add(language);
        if ((StatusFilter.SelectedItem as FilterOption)?.Value is { } status) _filterState.Statuses.Add(status == "enabled");
        ApplyFilters();
    }

    private void FilterHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } button || !Enum.TryParse<ResponseFilterField>(tag, out var field)) return;
        var values = field switch { ResponseFilterField.Category => _viewModel.Responses.Select(x => x.Category), ResponseFilterField.Language => _viewModel.Responses.Select(x => x.Language), ResponseFilterField.Status => new[] { "enabled", "disabled" }, _ => [] };
        ColumnFilterPanel.Configure(field, _filterState, values.Where(x => !string.IsNullOrWhiteSpace(x))); FilterPopup.PlacementTarget = button; FilterPopup.IsOpen = true; e.Handled = true;
    }

    private void ApplyFilters()
    {
        using (_libraryView.DeferRefresh()) { }
        UpdateEmptyState(); UpdateActiveFilters(); UpdateFilterButtons();
    }

    private void ClearFilterChip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ResponseFilterField field) return;
        _filterState.Clear(field);
        _updatingFilters = true;
        if (field == ResponseFilterField.Category) CategoryFilter.SelectedIndex = 0;
        if (field == ResponseFilterField.Language) LanguageFilter.SelectedIndex = 0;
        if (field == ResponseFilterField.Status) StatusFilter.SelectedIndex = 0;
        _updatingFilters = false;
        ApplyFilters();
    }
    private void ClearAllFilters_Click(object sender, RoutedEventArgs e) { _filterState.ClearAll(); ResetLegacyFilterSelection(); ApplyFilters(); }
    private void ResetLegacyFilterSelection() { _updatingFilters = true; CategoryFilter.SelectedIndex = LanguageFilter.SelectedIndex = StatusFilter.SelectedIndex = 0; _updatingFilters = false; }
    private void UpdateActiveFilters()
    {
        var chips = new List<ActiveFilterChip>();
        if (_filterState.Summary?.IsActive == true) chips.Add(new(ResponseFilterField.Summary, $"{LocalizationService.Get("Summary")}: {_filterState.Summary.Value}"));
        if (_filterState.Categories.Count > 0) chips.Add(new(ResponseFilterField.Category, $"{LocalizationService.Get("Category")}: {string.Join(", ", _filterState.Categories)}"));
        if (_filterState.Keywords?.IsActive == true) chips.Add(new(ResponseFilterField.Keywords, $"{LocalizationService.Get("Keywords")}: {_filterState.Keywords.Value}"));
        if (_filterState.Languages.Count > 0) chips.Add(new(ResponseFilterField.Language, $"{LocalizationService.Get("Language")}: {string.Join(", ", _filterState.Languages)}"));
        if (_filterState.Statuses.Count > 0) chips.Add(new(ResponseFilterField.Status, $"{LocalizationService.Get("Status")}: {string.Join(", ", _filterState.Statuses.Select(x => LocalizationService.Get(x ? "Enabled" : "Disabled")))}"));
        if (_filterState.UsageCount is { } usage) chips.Add(new(ResponseFilterField.UsageCount, $"{LocalizationService.Get("UsageCount")}: {LocalizationService.Get(usage.Operator.ToString())} {usage.Value}{(usage.SecondValue is null ? "" : $"–{usage.SecondValue}")}"));
        if (_filterState.LastUsed is { IsActive: true } last) chips.Add(new(ResponseFilterField.LastUsed, $"{LocalizationService.Get("LastUsed")}: {LocalizationService.Get(last.Period.ToString())}"));
        ActiveFiltersItems.ItemsSource = chips; ActiveFiltersBar.Visibility = chips.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void UpdateFilterButtons()
    {
        foreach (var (button, field) in FilterButtons()) { button.SetResourceReference(Control.ForegroundProperty, _filterState.IsActive(field) ? "QrbAccentBrush" : "QrbTextPrimaryBrush"); button.SetResourceReference(Control.BackgroundProperty, _filterState.IsActive(field) ? "QrbSelectionBrush" : "QrbSurfaceBrush"); }
    }
    private IEnumerable<(Button Button, ResponseFilterField Field)> FilterButtons() => [(SummaryFilterButton, ResponseFilterField.Summary), (CategoryColumnFilterButton, ResponseFilterField.Category), (KeywordsFilterButton, ResponseFilterField.Keywords), (LanguageColumnFilterButton, ResponseFilterField.Language), (StatusColumnFilterButton, ResponseFilterField.Status), (UsageFilterButton, ResponseFilterField.UsageCount), (LastUsedFilterButton, ResponseFilterField.LastUsed)];

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ResponseEditorWindow { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;
        await Repository.UpsertAsync(editor.Response);
        await ChangedAsync("ResponseSaved");
    }

    private async void EditMenu_Click(object sender, RoutedEventArgs e) => await EditAsync(MenuResponse(sender));

    private async Task EditAsync(QuickResponse? response)
    {
        if (response is null) return;
        var editor = new ResponseEditorWindow(response) { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;
        await Repository.UpsertAsync(editor.Response);
        await ChangedAsync("ResponseUpdated");
    }

    private async void DuplicateMenu_Click(object sender, RoutedEventArgs e)
    {
        if (MenuResponse(sender) is not { } source) return;
        var copy = new QuickResponse
        {
            Summary = $"{source.Summary} {LocalizationService.Get("CopySuffix")}", Content = source.Content,
            Keywords = [.. source.Keywords], Category = source.Category, Language = source.Language,
            IsEnabled = source.IsEnabled, SortOrder = source.SortOrder
        };
        await Repository.UpsertAsync(copy);
        await ChangedAsync("ResponseDuplicated");
    }

    private async void ToggleMenu_Click(object sender, RoutedEventArgs e)
    {
        if (MenuResponse(sender) is not { } response) return;
        response.IsEnabled = !response.IsEnabled;
        await Repository.UpsertAsync(response);
        await ChangedAsync(response.IsEnabled ? "ResponseEnabled" : "ResponseDisabled");
    }

    private async void MoveMenu_Click(object sender, RoutedEventArgs e)
    {
        if (MenuResponse(sender) is not { } response) return;
        var category = await ChooseCategoryAsync();
        if (category is null) return;
        await Repository.MoveToCategoryAsync([response.Id], category.Name);
        await ChangedAsync("BatchComplete");
    }

    private async void DeleteMenu_Click(object sender, RoutedEventArgs e)
    {
        if (MenuResponse(sender) is not { } response) return;
        var message = string.Format(LocalizationService.Get("ConfirmDeleteResponse"), response.Summary);
        if (!UiDialogService.Confirm(Window.GetWindow(this), LocalizationService.Get("Delete"), message)) return;
        await Repository.DeleteAsync(response.Id);
        await ChangedAsync("ResponseDeleted");
    }

    private void ResponsesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResponsesGrid.SelectedItem is QuickResponse response) _ = EditAsync(response);
    }

    private void ResponsesGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = FindVisualChild<ScrollViewer>(ResponsesGrid);
        if (viewer is null || viewer.ScrollableHeight <= 0) return;
        viewer.ScrollToVerticalOffset(Math.Clamp(viewer.VerticalOffset - (e.Delta / 3d), 0, viewer.ScrollableHeight));
        e.Handled = true;
    }

    private void ResponsesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = ResponsesGrid.SelectedItems.Count;
        SelectionToolbar.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectionCountText.Text = string.Format(LocalizationService.Get("SelectedCountFormat"), count);
    }

    private void SelectAllCheck_Checked(object sender, RoutedEventArgs e) => ResponsesGrid.SelectAll();
    private void SelectAllCheck_Unchecked(object sender, RoutedEventArgs e) => ResponsesGrid.UnselectAll();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => ResponsesGrid.SelectAll();
    private void ClearSelection_Click(object sender, RoutedEventArgs e) => ResponsesGrid.UnselectAll();
    private async void BatchEnable_Click(object sender, RoutedEventArgs e) => await RunBatchAsync(ids => Repository.SetEnabledAsync(ids, true));
    private async void BatchDisable_Click(object sender, RoutedEventArgs e) => await RunBatchAsync(ids => Repository.SetEnabledAsync(ids, false));

    private async void BatchMove_Click(object sender, RoutedEventArgs e)
    {
        var category = await ChooseCategoryAsync();
        if (category is not null) await RunBatchAsync(ids => Repository.MoveToCategoryAsync(ids, category.Name));
    }

    private async void BatchDelete_Click(object sender, RoutedEventArgs e)
    {
        if (!UiDialogService.Confirm(Window.GetWindow(this), LocalizationService.Get("BatchDelete"), LocalizationService.Get("ConfirmBatchDelete"))) return;
        await RunBatchAsync(ids => Repository.DeleteManyAsync(ids));
    }

    private async Task RunBatchAsync(Func<IReadOnlyCollection<Guid>, Task<BatchOperationResult>> operation)
    {
        var ids = ResponsesGrid.SelectedItems.Cast<QuickResponse>().Select(x => x.Id).ToList();
        if (ids.Count == 0) { _feedback(LocalizationService.Get("NoSelection")); return; }
        RootPanel.IsEnabled = false;
        try
        {
            var result = await operation(ids);
            var message = $"{LocalizationService.Get("BatchComplete")}: {LocalizationService.Get("ActualProcessed")}: {result.Processed}, {LocalizationService.Get("Failed")}: {result.Failed}";
            await ChangedAsync(message, false);
        }
        catch (Exception ex) { _feedback($"{LocalizationService.Get("OperationFailed")}: {ex.Message}"); }
        finally { RootPanel.IsEnabled = true; }
    }

    private async Task<CategoryInfo?> ChooseCategoryAsync()
    {
        var categories = await CategoryRepository.GetCategoriesAsync();
        var choice = new CategoryChoiceWindow(categories) { Owner = Window.GetWindow(this) };
        return choice.ShowDialog() == true ? choice.SelectedCategory : null;
    }

    private async Task ChangedAsync(string messageOrKey, bool isKey = true)
    {
        if (System.Windows.Application.Current is App app) await app.ReloadCacheAsync();
        await _viewModel.RefreshAsync(); await RefreshAsync();
        _feedback(isKey ? LocalizationService.Get(messageOrKey) : messageOrKey);
    }

    private void ImportExport_Click(object sender, RoutedEventArgs e) => _navigate(ShellRoutes.ImportExport);
    private static QuickResponse? MenuResponse(object sender) => (sender as FrameworkElement)?.DataContext as QuickResponse;

    private void UpdateEmptyState() => EmptyState.Visibility = _libraryView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++) { var child = VisualTreeHelper.GetChild(parent, index); if (child is T match) return match; if (FindVisualChild<T>(child) is { } nested) return nested; }
        return null;
    }
    public sealed class FilterOption(string label, string? value)
    {
        public string Label { get; } = label;
        public string? Value { get; } = value;
        public override string ToString() => Label;
    }
    public sealed record ActiveFilterChip(ResponseFilterField Field, string Label);
}
