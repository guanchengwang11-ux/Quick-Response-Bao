using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using QuickResponseBao.App.Controls;

namespace QuickResponseBao.UnitTests;

public sealed class WpfRuntimeIntegrationTests
{
    [Fact]
    public void NativePageAndDataGridScrollersUseFiniteViewportsAndVirtualizeTenThousandRows()
    {
        RunSta(() =>
        {
            var app = new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/QuickResponseBao;component/Resources/LightTheme.xaml") });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/QuickResponseBao;component/Resources/Theme.xaml") });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/QuickResponseBao;component/Resources/Strings.en-US.xaml") });
            var content = new StackPanel();
            for (var i = 0; i < 100; i++) content.Children.Add(new TextBlock { Text = $"Setting {i}", Height = 32 });
            var page = new ScrollablePageLayout { Content = content, Height = 320, Width = 500 };
            using var pageWindow = Show(page);
            Assert.NotNull(page.ScrollViewer); Assert.True(page.ScrollViewer!.ScrollableHeight > 0);
            page.ScrollViewer.ScrollToVerticalOffset(120); pageWindow.Window.UpdateLayout();
            Assert.True(page.ScrollViewer.VerticalOffset > 0);

            var grid = new DataGrid { Height = 360, Width = 600, EnableRowVirtualization = true, EnableColumnVirtualization = true, ItemsSource = Enumerable.Range(0, 10_000).ToArray() };
            VirtualizingPanel.SetIsVirtualizing(grid, true); VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling);
            using var gridWindow = Show(grid); var viewer = FindVisualChild<ScrollViewer>(grid)!;
            var realized = CountVisualChildren<DataGridRow>(grid);
            Assert.True(viewer.ScrollableHeight > 0); Assert.True(realized < 100, $"Expected a virtualized viewport, but {realized} rows were realized.");
            viewer.ScrollToVerticalOffset(50); gridWindow.Window.UpdateLayout(); Assert.True(viewer.VerticalOffset > 0);

            app.Shutdown();
        });
    }

    private static WindowScope Show(FrameworkElement content)
    {
        var window = new Window { Width = content.Width + 20, Height = content.Height + 40, Content = content, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
        window.Show(); window.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); return new(window);
    }
    private static void RunSta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) return match; if (FindVisualChild<T>(child) is { } nested) return nested; } return null; }
    private static int CountVisualChildren<T>(DependencyObject parent) where T : DependencyObject { var count = parent is T ? 1 : 0; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) count += CountVisualChildren<T>(VisualTreeHelper.GetChild(parent, i)); return count; }
    private sealed class WindowScope(Window window) : IDisposable { public Window Window { get; } = window; public void Dispose() => Window.Close(); }
}
