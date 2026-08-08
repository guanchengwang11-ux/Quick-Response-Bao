using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;

namespace QuickResponseBao.App.Views.Pages;

public partial class ResponseLibraryPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel;
    private readonly Action<string> _feedback;
    private App Runtime => (App)System.Windows.Application.Current;
    public ResponseLibraryPage(MainViewModel viewModel, Action<string> feedback)
    { InitializeComponent(); DataContext = _viewModel = viewModel; _feedback = feedback; }
    public Task RefreshAsync() => _viewModel.RefreshAsync();
    public void AddResponse() => Add_Click(this, new RoutedEventArgs());
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ResponseEditorWindow { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;
        await Runtime.Repository.UpsertAsync(editor.Response); await Runtime.ReloadCacheAsync(); await RefreshAsync();
        _feedback(LocalizationService.Get("ResponseSaved"));
    }
}
