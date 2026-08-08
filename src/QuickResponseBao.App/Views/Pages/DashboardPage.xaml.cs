using System.Windows.Controls;
using System.Windows;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;

namespace QuickResponseBao.App.Views.Pages;

public partial class DashboardPage : Page, IRefreshablePage
{
    private readonly MainViewModel _viewModel;
    private readonly Action<string> _navigate;
    private readonly Action _addResponse;

    public DashboardPage(MainViewModel viewModel, Action<string> navigate, Action addResponse)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _navigate = navigate;
        _addResponse = addResponse;
        VersionMetric.Text = ApplicationVersion.Current;
    }
    public async Task RefreshAsync()
    {
        await _viewModel.RefreshAsync();
        var listening = System.Windows.Application.Current is App app && app.Listener?.IsRunning == true;
        ListenerMetric.SetResourceReference(TextBlock.TextProperty, listening ? "Active" : "Paused");
        ListenerMetric.SetResourceReference(TextBlock.ForegroundProperty, listening ? "QrbSuccessBrush" : "QrbWarningBrush");
        ListenerMetricDetail.SetResourceReference(TextBlock.TextProperty, listening ? "ListenerEnabled" : "ListenerPaused");
    }
    private void Add_Click(object sender, RoutedEventArgs e) => _addResponse();
    private void Import_Click(object sender, RoutedEventArgs e) => _navigate(ShellRoutes.ImportExport);
    private void Diagnostics_Click(object sender, RoutedEventArgs e) => _navigate(ShellRoutes.Diagnostics);
}
