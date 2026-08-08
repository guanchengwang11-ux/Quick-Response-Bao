using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.App.Views.Pages;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Updates;
using Wpf.Ui.Controls;

namespace QuickResponseBao.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _mainViewModel;
    private readonly ShellViewModel _shellViewModel = new();
    private readonly ShellNavigationService _navigation = new();
    private UpdateWindow? _updateWindow;
    private App Runtime => (App)System.Windows.Application.Current;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _mainViewModel = viewModel;
        RegisterPages();
        UpdateListenerDisplay();
        Loaded += (_, _) => Navigate(ShellRoutes.Dashboard);
    }

    public async Task InitializeAsync(AppSettings settings)
    {
        _mainViewModel.Settings = settings;
        await _mainViewModel.RefreshAsync();
        UpdateListenerDisplay();
    }

    public async Task RefreshAsync()
    {
        await _mainViewModel.RefreshAsync();
        if (_navigation.GetPage(_shellViewModel.CurrentRoute) is IRefreshablePage page) await page.RefreshAsync();
    }

    public void OpenSettings() => Navigate(ShellRoutes.Settings);

    public void AddResponse()
    {
        Navigate(ShellRoutes.Library);
        if (_navigation.TryGetCached<ResponseLibraryPage>(ShellRoutes.Library, out var page)) page!.AddResponse();
    }

    public async Task ShowUpdateWindowAsync(UpdateCheckResult? initial = null, bool automaticDownload = false)
    {
        if (_updateWindow?.IsVisible == true) { _updateWindow.Activate(); return; }
        _updateWindow = new UpdateWindow(Runtime.UpdatesClient, Runtime.Paths, ApplicationVersion.Current,
            _mainViewModel.Settings.IncludePrereleaseUpdates, initial);
        if (IsVisible) _updateWindow.Owner = this;
        _updateWindow.Closed += (_, _) => _updateWindow = null;
        _updateWindow.Show();
        if (automaticDownload) await _updateWindow.StartAutomaticDownloadAsync();
    }

    public void ShowFeedback(string message)
    {
        FeedbackText.Text = message;
        FeedbackHost.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Navigate(string route)
    {
        route = ShellRoutes.Normalize(route);
        _shellViewModel.Navigate(route);
        var page = _navigation.GetPage(route);
        RootNavigation.ReplaceContent(page);
        PageTitle.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, RouteResourceKey(route));
        if (page is IRefreshablePage refreshable) _ = refreshable.RefreshAsync();
    }

    private void RegisterPages()
    {
        _navigation.Register(ShellRoutes.Dashboard, () => new DashboardPage(_mainViewModel, Navigate));
        _navigation.Register(ShellRoutes.Library, () => new ResponseLibraryPage(_mainViewModel, ShowFeedback));
        _navigation.Register(ShellRoutes.Categories, () => new CategoriesPage(ShowFeedback));
        _navigation.Register(ShellRoutes.ImportExport, () => new ImportExportPage(_mainViewModel, ShowFeedback));
        _navigation.Register(ShellRoutes.Applications, () => new ApplicationsPage(ShowFeedback));
        _navigation.Register(ShellRoutes.Diagnostics, () => new DiagnosticsPage(ShowFeedback));
        _navigation.Register(ShellRoutes.Settings, () => new SettingsPage(_mainViewModel, ShowFeedback));
        _navigation.Register(ShellRoutes.About, () => new AboutPage(ShowUpdateWindowAsync, ShowFeedback));
    }

    private void NavigationItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem { TargetPageTag: { Length: > 0 } route }) Navigate(route);
    }

    private void ToggleListener_Click(object sender, RoutedEventArgs e)
    {
        if (Runtime.Listener.IsRunning) Runtime.PauseListener(); else Runtime.TryStartListener();
        UpdateListenerDisplay();
    }

    private void UpdateListenerDisplay()
    {
        var listening = System.Windows.Application.Current is App app && app.Listener?.IsRunning == true;
        _shellViewModel.UpdateListener(listening);
        ListenerStatus.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, listening ? "ListenerEnabled" : "ListenerPaused");
        ListenerStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, listening ? "QrbSuccessBrush" : "QrbWarningBrush");
        ListenerStatusIcon.SetResourceReference(SymbolIcon.ForegroundProperty, listening ? "QrbSuccessBrush" : "QrbWarningBrush");
        ListenerButton.SetResourceReference(ContentControl.ContentProperty, listening ? "Pause" : "Resume");
    }

    private void CopyFeedback_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(FeedbackText.Text)) System.Windows.Clipboard.SetText(FeedbackText.Text);
    }

    private static string RouteResourceKey(string route) => route switch
    {
        ShellRoutes.Library => "Library", ShellRoutes.Categories => "Categories", ShellRoutes.ImportExport => "ImportExport",
        ShellRoutes.Applications => "ApplicationWhitelist", ShellRoutes.Diagnostics => "Diagnostics", ShellRoutes.Settings => "Settings",
        ShellRoutes.About => "About", _ => "Dashboard"
    };
}

public interface IRefreshablePage
{
    Task RefreshAsync();
}
