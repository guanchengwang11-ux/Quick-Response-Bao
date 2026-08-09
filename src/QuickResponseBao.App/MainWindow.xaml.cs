using System.Windows;
using System.Windows.Controls;
using QuickResponseBao.App.Services;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.App.Views.Pages;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Updates;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using System.Diagnostics;

namespace QuickResponseBao.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _mainViewModel;
    private readonly ShellViewModel _shellViewModel = new();
    private readonly ShellNavigationService _navigation = new();
    private UpdateWindow? _updateWindow;
    private readonly UiFeedbackService _feedback = new();
    private readonly DispatcherTimer _feedbackTimer = new();
    private readonly bool _uiTracing = Environment.GetEnvironmentVariable("QRB_UI_TRACE") == "1";
    private Task _lastNavigationTask = Task.CompletedTask;
    private App Runtime => (App)System.Windows.Application.Current;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _mainViewModel = viewModel;
        RegisterPages();
        _feedback.MessageRequested += (_, message) => RenderFeedback(message);
        _feedbackTimer.Tick += (_, _) => { _feedbackTimer.Stop(); FeedbackHost.Visibility = Visibility.Collapsed; };
        UpdateListenerDisplay();
        Loaded += (_, _) => Navigate(ShellRoutes.Dashboard);
        RootNavigation.SizeChanged += (_, _) => ApplyPageViewport(_navigation.GetPage(_shellViewModel.CurrentRoute));
    }

    public async Task InitializeAsync(AppSettings settings)
    {
        _mainViewModel.Settings = settings;
        await _mainViewModel.EnsureLoadedAsync();
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

    public async Task RunUiDiagnosticWalkthroughAsync()
    {
        var routes = new[] { ShellRoutes.Dashboard, ShellRoutes.Library, ShellRoutes.Categories, ShellRoutes.ImportExport, ShellRoutes.Applications, ShellRoutes.Diagnostics, ShellRoutes.Settings, ShellRoutes.About };
        var instances = new Dictionary<string, Page>();
        foreach (var route in routes)
        {
            Navigate(route);
            await _lastNavigationTask;
            var page = _navigation.GetPage(route); instances[route] = page; page.UpdateLayout();
            var viewer = page is ResponseLibraryPage library
                ? FindVisualDescendants<ScrollViewer>(library).OrderByDescending(x => x.ScrollableHeight).FirstOrDefault()
                : FindVisualDescendants<Controls.ScrollablePageLayout>(page).Select(x => x.ScrollViewer).FirstOrDefault(x => x is not null);
            var before = viewer?.VerticalOffset ?? 0;
            if (viewer?.ScrollableHeight > 0) { viewer.ScrollToVerticalOffset(Math.Min(120, viewer.ScrollableHeight)); page.UpdateLayout(); }
            var details = viewer is null ? "scrollViewer=none" : $"scrollable={viewer.ScrollableHeight > 0}; extent={viewer.ExtentHeight:F1}; viewport={viewer.ViewportHeight:F1}; before={before:F1}; after={viewer.VerticalOffset:F1}";
            if (page is ResponseLibraryPage responseLibrary) details += $"; {Format(responseLibrary.CaptureVisualMetrics())}";
            await Runtime.LogSafeEventAsync($"UiRuntime | walkthrough route={route}; {details}");
        }
        foreach (var (route, instance) in instances)
        {
            Navigate(route); await _lastNavigationTask;
            await Runtime.LogSafeEventAsync($"UiRuntime | walkthrough route={route}; cacheReused={ReferenceEquals(instance, _navigation.GetPage(route))}");
        }
        var stalls = Runtime.UiStallSnapshot;
        await Runtime.LogSafeEventAsync($"UiRuntime | stalls p50={stalls.P50Milliseconds:F2}ms; p95={stalls.P95Milliseconds:F2}ms; max={stalls.MaximumMilliseconds:F2}ms; samples={stalls.SampleCount}; over16={stalls.Over16Milliseconds}; over33={stalls.Over33Milliseconds}; over50={stalls.Over50Milliseconds}; over100={stalls.Over100Milliseconds}; over250={stalls.Over250Milliseconds}");
        Navigate(ShellRoutes.Normalize(Environment.GetEnvironmentVariable("QRB_UI_FINAL_ROUTE") ?? ShellRoutes.Dashboard));
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualDescendants<T>(child)) yield return nested;
        }
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
        if (string.IsNullOrWhiteSpace(message)) { FeedbackHost.Visibility = Visibility.Collapsed; return; }
        var failure = message.Contains(LocalizationService.Get("OperationFailed"), StringComparison.OrdinalIgnoreCase) || message.Contains("failed", StringComparison.OrdinalIgnoreCase) || message.Contains("error", StringComparison.OrdinalIgnoreCase);
        _feedback.Show(message, failure ? UiFeedbackSeverity.Error : UiFeedbackSeverity.Success);
    }

    private void RenderFeedback(UiFeedbackMessage message)
    {
        FeedbackText.Text = message.Message; FeedbackDetails.Text = message.Details ?? string.Empty; FeedbackDetails.Visibility = string.IsNullOrWhiteSpace(message.Details) ? Visibility.Collapsed : Visibility.Visible;
        var (brush, symbol) = message.Severity switch { UiFeedbackSeverity.Error => ("QrbErrorBrush", SymbolRegular.ErrorCircle24), UiFeedbackSeverity.Warning => ("QrbWarningBrush", SymbolRegular.Warning24), UiFeedbackSeverity.Information => ("QrbAccentBrush", SymbolRegular.Info24), _ => ("QrbSuccessBrush", SymbolRegular.CheckmarkCircle24) };
        FeedbackAccent.SetResourceReference(Border.BackgroundProperty, brush); FeedbackIcon.SetResourceReference(SymbolIcon.ForegroundProperty, brush); FeedbackIcon.Symbol = symbol;
        FeedbackHost.Visibility = Visibility.Visible; _feedbackTimer.Stop(); _feedbackTimer.Interval = message.DisplayDuration; _feedbackTimer.Start();
    }

    public void Navigate(string route)
    {
        var clock = Stopwatch.StartNew();
        route = ShellRoutes.Normalize(route);
        var cold = !_navigation.TryGetCached<Page>(route, out _);
        TraceNavigation(route, "Click", clock, $"cold={cold}");
        _shellViewModel.Navigate(route);
        var page = _navigation.GetPage(route);
        ApplyPageViewport(page);
        TraceNavigation(route, "Page instance obtained", clock, $"cold={cold}");
        RootNavigation.ReplaceContent(page);
        TraceNavigation(route, "Content assigned", clock);
        PageTitle.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, RouteResourceKey(route));
        _lastNavigationTask = page is IRefreshablePage refreshable
            ? RefreshAndTraceAsync(route, page, refreshable, clock)
            : QueueInteractiveTraceAsync(route, page, clock);
    }

    private async Task RefreshAndTraceAsync(string route, Page page, IRefreshablePage refreshable, Stopwatch clock)
    {
        await refreshable.RefreshAsync(); TraceNavigation(route, "Data available", clock); await QueueInteractiveTraceAsync(route, page, clock);
    }

    private async Task QueueInteractiveTraceAsync(string route, Page page, Stopwatch clock)
    {
        await Dispatcher.InvokeAsync(() => TraceNavigation(route, "Layout completed", clock), DispatcherPriority.Loaded);
        await Dispatcher.InvokeAsync(() =>
        {
            var details = page is ResponseLibraryPage library ? Format(library.CaptureVisualMetrics()) : $"actualHeight={page.ActualHeight:F1}";
            TraceNavigation(route, "Interactive", clock, details);
        }, DispatcherPriority.ContextIdle);
    }

    private void TraceNavigation(string route, string stage, Stopwatch clock, string details = "")
    {
        if (!_uiTracing) return;
        _ = Runtime.LogSafeEventAsync($"UiRuntime | route={route}; stage={stage}; elapsed={clock.Elapsed.TotalMilliseconds:F2}ms; {details}");
    }

    private static string Format(LibraryVisualMetrics value) =>
        $"items={value.ItemCount}; realizedRows={value.RealizedRowCount}; gridHeight={value.ActualHeight:F1}; viewport={value.ViewportHeight:F1}; extent={value.ExtentHeight:F1}; scrollable={value.ScrollableHeight:F1}; offset={value.VerticalOffset:F1}";

    private void ApplyPageViewport(Page page)
    {
        var available = RootNavigation.ActualHeight - AppTitleBar.ActualHeight - PageHeader.ActualHeight - 8;
        if (available > 320) page.MaxHeight = available;
        page.VerticalAlignment = VerticalAlignment.Stretch;
    }

    private void RegisterPages()
    {
        _navigation.Register(ShellRoutes.Dashboard, () => new DashboardPage(_mainViewModel, Navigate, AddResponse));
        _navigation.Register(ShellRoutes.Library, () => new ResponseLibraryPage(_mainViewModel, ShowFeedback, Navigate));
        _navigation.Register(ShellRoutes.Categories, () => new CategoriesPage(_mainViewModel, ShowFeedback));
        _navigation.Register(ShellRoutes.ImportExport, () => new ImportExportPage(_mainViewModel, ShowFeedback, ResolveExportScopeAsync));
        _navigation.Register(ShellRoutes.Applications, () => new ApplicationsPage(ShowFeedback));
        _navigation.Register(ShellRoutes.Diagnostics, () => new DiagnosticsPage(ShowFeedback));
        _navigation.Register(ShellRoutes.Settings, () => new SettingsPage(_mainViewModel, ShowFeedback));
        _navigation.Register(ShellRoutes.About, () => new AboutPage(ShowUpdateWindowAsync, ShowFeedback));
    }

    private void NavigationItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationViewItem { TargetPageTag: { Length: > 0 } route }) Navigate(route);
    }

    private void TogglePane_Click(object sender, RoutedEventArgs e) => RootNavigation.IsPaneOpen = !RootNavigation.IsPaneOpen;

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
    private void CloseFeedback_Click(object sender, RoutedEventArgs e) { _feedbackTimer.Stop(); FeedbackHost.Visibility = Visibility.Collapsed; }

    private async Task<IReadOnlyList<QuickResponse>> ResolveExportScopeAsync(string scope)
    {
        if (_navigation.TryGetCached<ResponseLibraryPage>(ShellRoutes.Library, out var library))
        {
            if (scope == "filtered") return library!.GetFilteredResponses();
            if (scope == "selected") return library!.GetSelectedResponses();
        }
        return scope == "selected" ? [] : await _mainViewModel.Repository.GetAllAsync();
    }

    private static string RouteResourceKey(string route) => route switch
    {
        ShellRoutes.Library => "Library", ShellRoutes.Categories => "Categories", ShellRoutes.ImportExport => "ImportExport",
        ShellRoutes.Applications => "ApplicationWhitelist", ShellRoutes.Diagnostics => "Diagnostics", ShellRoutes.Settings => "Settings",
        ShellRoutes.About => "About", _ => "Dashboard"
    };
}

public interface IRefreshablePage { Task RefreshAsync(); }
