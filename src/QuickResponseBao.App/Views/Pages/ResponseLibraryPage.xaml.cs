using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App.Views.Pages;

public partial class ResponseLibraryPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel;
    private readonly Action<string> _feedback;
    private readonly Action<string> _navigate;
    private readonly ListCollectionView _libraryView;
    private int _searchVersion;
    private bool _updatingFilters;

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
        InitializeStaticFilters();
    }

    public async Task RefreshAsync()
    {
        await _viewModel.RefreshAsync();
        await RefreshFilterOptionsAsync();
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

    private async Task RefreshFilterOptionsAsync()
    {
        _updatingFilters = true;
        var categoryValue = (CategoryFilter.SelectedItem as FilterOption)?.Value;
        var languageValue = (LanguageFilter.SelectedItem as FilterOption)?.Value;
        var categories = await CategoryRepository.GetCategoriesAsync();
        CategoryFilter.ItemsSource = new[] { new FilterOption(LocalizationService.Get("AllCategories"), null) }
            .Concat(categories.Select(x => new FilterOption(x.Name, x.Name))).ToList();
        LanguageFilter.ItemsSource = new[] { new FilterOption(LocalizationService.Get("AllLanguages"), null) }
            .Concat(_viewModel.Responses.Select(x => x.Language).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Select(x => new FilterOption(x, x))).ToList();
        CategoryFilter.SelectedItem = CategoryFilter.Items.Cast<FilterOption>().FirstOrDefault(x => x.Value == categoryValue) ?? CategoryFilter.Items[0];
        LanguageFilter.SelectedItem = LanguageFilter.Items.Cast<FilterOption>().FirstOrDefault(x => x.Value == languageValue) ?? LanguageFilter.Items[0];
        _updatingFilters = false;
    }

    private bool MatchesFilters(object value)
    {
        if (value is not QuickResponse response) return false;
        var category = (CategoryFilter.SelectedItem as FilterOption)?.Value;
        var language = (LanguageFilter.SelectedItem as FilterOption)?.Value;
        var status = (StatusFilter.SelectedItem as FilterOption)?.Value;
        return (category is null || response.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            && (language is null || response.Language.Equals(language, StringComparison.OrdinalIgnoreCase))
            && (status is null || response.IsEnabled == (status == "enabled"));
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var version = ++_searchVersion;
        await Task.Delay(150);
        if (version != _searchVersion) return;
        _viewModel.SearchText = SearchBox.Text;
        await RefreshAsync();
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFilters || _libraryView is null) return;
        _libraryView.Refresh();
        UpdateEmptyState();
    }

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
        if (System.Windows.MessageBox.Show(message, LocalizationService.Get("Delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Repository.DeleteAsync(response.Id);
        await ChangedAsync("ResponseDeleted");
    }

    private void ResponsesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResponsesGrid.SelectedItem is QuickResponse response) _ = EditAsync(response);
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
        if (System.Windows.MessageBox.Show(LocalizationService.Get("ConfirmBatchDelete"), LocalizationService.Get("BatchDelete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
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
        await RefreshAsync();
        _feedback(isKey ? LocalizationService.Get(messageOrKey) : messageOrKey);
    }

    private void ImportExport_Click(object sender, RoutedEventArgs e) => _navigate(ShellRoutes.ImportExport);
    private static QuickResponse? MenuResponse(object sender) => (sender as FrameworkElement)?.DataContext as QuickResponse;

    private void UpdateEmptyState() => EmptyState.Visibility = _libraryView.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public sealed class FilterOption(string label, string? value)
    {
        public string Label { get; } = label;
        public string? Value { get; } = value;
        public override string ToString() => Label;
    }
}
