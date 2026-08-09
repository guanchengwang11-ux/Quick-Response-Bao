using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QuickResponseBao.App.Controls;
using QuickResponseBao.App.ViewModels;
using QuickResponseBao.App.Views.Pages;
using QuickResponseBao.Core.Interfaces;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

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

            var first = new TextBox { Name = "First", Text = "risk", Width = 240 };
            var second = new Button { Name = "Second", Content = "Next", Width = 120 };
            var focusPanel = new StackPanel { Width = 400, Height = 200 }; focusPanel.Children.Add(first); focusPanel.Children.Add(second);
            using (var focusWindow = Show(focusPanel))
            {
                Assert.True(first.Focus());
                Assert.True(first.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                Assert.True(second.IsKeyboardFocused);
                Assert.True(second.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                Assert.True(first.IsKeyboardFocused);
            }

            var committed = new ResponseLibraryFilterState();
            var filterPanel = new ColumnFilterPanel { Width = 400, Height = 540, PreviewCountProvider = (state, _) => Task.FromResult(state.Summary?.Matches("Issue report") == true ? 1 : 0) };
            var closeRequests = 0; filterPanel.CloseRequested += (_, _) => closeRequests++;
            filterPanel.Configure(ResponseFilterField.Summary, committed, matchCount: 3);
            using (var filterWindow = Show(filterPanel))
            {
                var operatorBox = (ComboBox)filterPanel.FindName("TextOperatorBox");
                var valueBox = (TextBox)filterPanel.FindName("TextValueBox");
                operatorBox.SelectedItem = operatorBox.Items.Cast<OperatorOption<TextFilterOperator>>().Single(x => x.Value == TextFilterOperator.Contains);
                valueBox.Text = "issue";
                PumpUntil(() => filterPanel.LastPreviewCount == 1);
                filterPanel.ApplyDraft();
                Assert.Equal(new TextColumnFilter("issue", TextFilterOperator.Contains), committed.Summary);
                Assert.Equal(1, closeRequests);
                filterPanel.Configure(ResponseFilterField.Summary, committed, matchCount: 1);
                valueBox.Text = "draft only";
                filterPanel.CancelDraft();
                Assert.Equal("issue", committed.Summary!.Value);
                Assert.Equal(2, closeRequests);
                filterPanel.Configure(ResponseFilterField.Summary, committed, matchCount: 1);
                filterPanel.ClearCurrentFilter();
                Assert.Null(committed.Summary);
                Assert.Equal(3, closeRequests);
            }

            var rows = new[] { Response("Issue one"), Response("Other"), Response("ISSUE two") };
            var viewModel = new MainViewModel(new MemoryRepository(rows), new SearchService());
            viewModel.ApplySnapshot(rows);
            var library = new ResponseLibraryPage(viewModel, _ => { }, _ => { }) { Width = 1180, Height = 680 };
            library.RefreshAsync().GetAwaiter().GetResult();
            using (var libraryWindow = Show(library))
            {
                var search = (TextBox)library.FindName("SearchBox");
                Assert.True(search.Focus()); search.Text = "risk";
                Assert.True(search.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                Assert.False(search.IsKeyboardFocused);
                Assert.True(Keyboard.FocusedElement is UIElement focused && focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
                Assert.True(search.IsKeyboardFocused);

                search.Text = string.Empty;
                var filterButton = (Button)library.FindName("SummaryFilterButton");
                var popup = (Popup)library.FindName("FilterPopup");
                var panel = (ColumnFilterPanel)library.FindName("ColumnFilterPanel");
                var gridControl = (DataGrid)library.FindName("ResponsesGrid");
                filterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(popup.IsOpen);
                PumpUntil(() => ((ComboBox)panel.FindName("TextOperatorBox")).Items.Count > 0);
                var textOperator = (ComboBox)panel.FindName("TextOperatorBox");
                textOperator.SelectedItem = textOperator.Items.Cast<OperatorOption<TextFilterOperator>>().Single(x => x.Value == TextFilterOperator.Contains);
                ((TextBox)panel.FindName("TextValueBox")).Text = "issue";
                PumpUntil(() => panel.LastPreviewCount == 2);
                panel.ApplyDraft();
                libraryWindow.Window.UpdateLayout();
                Assert.False(popup.IsOpen); Assert.Equal(2, gridControl.Items.Count); Assert.True(library.LastFilterApplyDuration.TotalMilliseconds < 100);

                filterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => popup.IsOpen && ((ComboBox)panel.FindName("TextOperatorBox")).Items.Count > 0);
                panel.CancelDraft(); Assert.False(popup.IsOpen); Assert.Equal(2, gridControl.Items.Count);
                filterButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => popup.IsOpen && ((ComboBox)panel.FindName("TextOperatorBox")).Items.Count > 0);
                panel.ClearCurrentFilter(); libraryWindow.Window.UpdateLayout();
                Assert.False(popup.IsOpen); Assert.Equal(3, gridControl.Items.Count); Assert.True(library.LastFilterClearDuration.TotalMilliseconds < 100);
                Console.WriteLine($"Library filter: open={library.LastFilterOpenDuration.TotalMilliseconds:F2}ms apply={library.LastFilterApplyDuration.TotalMilliseconds:F2}ms clear={library.LastFilterClearDuration.TotalMilliseconds:F2}ms preview={panel.LastPreviewCount}");
            }

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
    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        Assert.True(condition());
    }
    private static void RunSta(Action action)
    {
        Exception? failure = null; var thread = new Thread(() => { try { SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)); action(); } catch (Exception ex) { failure = ex; } finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) return match; if (FindVisualChild<T>(child) is { } nested) return nested; } return null; }
    private static int CountVisualChildren<T>(DependencyObject parent) where T : DependencyObject { var count = parent is T ? 1 : 0; for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) count += CountVisualChildren<T>(VisualTreeHelper.GetChild(parent, i)); return count; }
    private static QuickResponse Response(string summary) => new() { Summary = summary, Content = "Body", Category = "General", Language = "English" };
    private sealed class MemoryRepository(IReadOnlyList<QuickResponse> rows) : IQuickResponseRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<QuickResponse>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows);
        public Task<QuickResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(rows.FirstOrDefault(x => x.Id == id));
        public Task UpsertAsync(QuickResponse response, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task IncrementUsageAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BatchOperationResult> SetEnabledAsync(IReadOnlyCollection<Guid> ids, bool enabled, CancellationToken cancellationToken = default) => Task.FromResult(new BatchOperationResult(ids.Count, ids.Count, 0, []));
        public Task<BatchOperationResult> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult(new BatchOperationResult(ids.Count, ids.Count, 0, []));
        public Task<BatchOperationResult> MoveToCategoryAsync(IReadOnlyCollection<Guid> ids, string category, CancellationToken cancellationToken = default) => Task.FromResult(new BatchOperationResult(ids.Count, ids.Count, 0, []));
    }
    private sealed class WindowScope(Window window) : IDisposable { public Window Window { get; } = window; public void Dispose() => Window.Close(); }
}
