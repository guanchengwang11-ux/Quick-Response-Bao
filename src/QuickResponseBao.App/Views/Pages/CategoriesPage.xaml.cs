using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;

namespace QuickResponseBao.App.Views.Pages;

public partial class CategoriesPage : Page, IRefreshablePage
{
    private readonly Action<string> _feedback;
    private App Runtime => (App)System.Windows.Application.Current;
    public CategoriesPage(Action<string> feedback) { InitializeComponent(); _feedback = feedback; }
    public Task RefreshAsync() => Task.CompletedTask;
    private void Open_Click(object sender, RoutedEventArgs e)
    { new CategoryManagerWindow(Runtime.CategoryRepository) { Owner = Window.GetWindow(this) }.ShowDialog(); _feedback(LocalizationService.Get("Succeeded")); }
}
