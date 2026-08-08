using System.Windows.Controls;
using QuickResponseBao.App.ViewModels;

namespace QuickResponseBao.App.Views.Pages;

public partial class DashboardPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel;
    private readonly Action<string> _navigate;
    public DashboardPage(MainViewModel viewModel, Action<string> navigate)
    {
        InitializeComponent(); DataContext = _viewModel = viewModel; _navigate = navigate;
    }
    public Task RefreshAsync() => _viewModel.RefreshAsync();
}
