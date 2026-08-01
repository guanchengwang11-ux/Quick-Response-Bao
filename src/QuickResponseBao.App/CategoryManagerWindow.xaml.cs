using System.Collections.ObjectModel;
using System.Windows;
using QuickResponseBao.App.Services;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.App;

public partial class CategoryManagerWindow : Window
{
    private readonly ICategoryRepository _repository;
    private readonly ObservableCollection<CategoryInfo> _categories = [];
    public CategoryManagerWindow(ICategoryRepository repository) { InitializeComponent(); _repository = repository; CategoriesGrid.DataContext = _categories; Loaded += async (_, _) => await RefreshAsync(); }
    private CategoryInfo? Selected => CategoriesGrid.SelectedItem as CategoryInfo;
    private async Task RefreshAsync() { _categories.Clear(); foreach (var item in await _repository.GetCategoriesAsync()) _categories.Add(item); }
    private async Task RunAsync(Func<Task> action, string success)
    {
        ActionPanel.IsEnabled = false; Feedback.Text = LocalizationService.Get("Loading");
        try { await action(); await RefreshAsync(); Feedback.Text = success; }
        catch (Exception ex) { Feedback.Text = $"{LocalizationService.Get("OperationFailed")}: {ex.Message}"; }
        finally { ActionPanel.IsEnabled = true; }
    }
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var prompt = new TextPromptWindow { Owner = this }; if (prompt.ShowDialog() != true) return;
        await RunAsync(() => _repository.AddCategoryAsync(prompt.Value), LocalizationService.Get("Succeeded"));
    }
    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } item) { Feedback.Text = LocalizationService.Get("NoSelection"); return; }
        var prompt = new TextPromptWindow(item.Name) { Owner = this }; if (prompt.ShowDialog() != true) return;
        await RunAsync(() => _repository.RenameCategoryAsync(item.Id, prompt.Value), LocalizationService.Get("Succeeded"));
    }
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } item) { Feedback.Text = LocalizationService.Get("NoSelection"); return; }
        var count = await _repository.CountResponsesAsync(item.Id);
        var message = count > 0 ? LocalizationService.Get("MoveAndDeleteCategory") : LocalizationService.Get("ConfirmDeleteCategory");
        if (System.Windows.MessageBox.Show(message, LocalizationService.Get("Categories"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync(() => _repository.DeleteCategoryAsync(item.Id, count > 0), LocalizationService.Get("Succeeded"));
    }
    private async void Up_Click(object sender, RoutedEventArgs e) => await MoveAsync(-1);
    private async void Down_Click(object sender, RoutedEventArgs e) => await MoveAsync(1);
    private async Task MoveAsync(int change)
    {
        if (Selected is not { } item) { Feedback.Text = LocalizationService.Get("NoSelection"); return; }
        var index = _categories.IndexOf(item); var target = index + change;
        if (target < 0 || target >= _categories.Count) return; _categories.Move(index, target);
        await RunAsync(() => _repository.ReorderCategoriesAsync(_categories.Select(x => x.Id).ToList()), LocalizationService.Get("Succeeded"));
        CategoriesGrid.SelectedItem = _categories.FirstOrDefault(x => x.Id == item.Id);
    }
}
