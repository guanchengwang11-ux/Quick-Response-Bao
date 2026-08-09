using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App.Views.Pages;

public partial class CategoriesPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel;
    private readonly Action<string> _feedback;
    private readonly ObservableCollection<CategoryRow> _rows = [];
    private bool _loaded;
    private ICategoryRepository Categories => (ICategoryRepository)_viewModel.Repository;

    public CategoriesPage(MainViewModel viewModel, Action<string> feedback)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _feedback = feedback;
        CategoriesGrid.DataContext = _rows;
    }

    public Task RefreshAsync() => RefreshAsync(false);

    private async Task RefreshAsync(bool force)
    {
        if (_loaded && !force) return;
        await _viewModel.EnsureLoadedAsync();
        var categories = await Categories.GetCategoriesAsync();
        var responses = _viewModel.Responses;
        _rows.Clear();
        foreach (var category in categories.OrderBy(x => x.SortOrder))
            _rows.Add(new CategoryRow(category, responses.Count(x => x.Category.Equals(category.Name, StringComparison.OrdinalIgnoreCase))));
        _loaded = true;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new TextPromptWindow { Owner = Window.GetWindow(this) };
        if (prompt.ShowDialog() != true) return;
        await RunAsync(async () => await Categories.AddCategoryAsync(prompt.Value));
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Row(sender) is not { } row) return;
        var prompt = new TextPromptWindow(row.Category.Name) { Owner = Window.GetWindow(this) };
        if (prompt.ShowDialog() != true) return;
        await RunAsync(async () => await Categories.RenameCategoryAsync(row.Category.Id, prompt.Value));
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Row(sender) is not { } row) return;
        var dialog = new CategoryDeleteWindow(row.Category.Name, row.ResponseCount) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        await RunAsync(async () => await Categories.DeleteCategoryAsync(row.Category.Id, row.ResponseCount > 0));
    }

    private async void Up_Click(object sender, RoutedEventArgs e) => await MoveAsync(Row(sender), -1);
    private async void Down_Click(object sender, RoutedEventArgs e) => await MoveAsync(Row(sender), 1);

    private async Task MoveAsync(CategoryRow? row, int delta)
    {
        if (row is null) return;
        var index = _rows.IndexOf(row);
        var target = index + delta;
        if (target < 0 || target >= _rows.Count) return;
        _rows.Move(index, target);
        await RunAsync(async () => await Categories.ReorderCategoriesAsync(_rows.Select(x => x.Category.Id).ToList()), false);
    }

    private async Task RunAsync(Func<Task> action, bool refresh = true)
    {
        CategoriesGrid.IsEnabled = AddCategoryButton.IsEnabled = false;
        try
        {
            await action();
            if (refresh)
            {
                await _viewModel.RefreshAsync();
                if (System.Windows.Application.Current is App app) app.SynchronizeRuntimeSearchCache(_viewModel.Responses);
                await RefreshAsync(true);
            }
            _feedback(LocalizationService.Get("Succeeded"));
        }
        catch (Exception ex) { _feedback($"{LocalizationService.Get("OperationFailed")}: {ex.Message}"); }
        finally { CategoriesGrid.IsEnabled = AddCategoryButton.IsEnabled = true; }
    }

    private static CategoryRow? Row(object sender) => (sender as FrameworkElement)?.DataContext as CategoryRow;
    public sealed record CategoryRow(CategoryInfo Category, int ResponseCount);
}
