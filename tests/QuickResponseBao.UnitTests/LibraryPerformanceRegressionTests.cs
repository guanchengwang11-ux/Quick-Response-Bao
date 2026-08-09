using System.Collections.Specialized;
using QuickResponseBao.Core.Collections;

namespace QuickResponseBao.UnitTests;

public sealed class LibraryPerformanceRegressionTests
{
    [Fact]
    public void ReplaceRange_RaisesOneResetInsteadOfPerItemNotifications()
    {
        var collection = new BulkObservableCollection<int>(); var changes = new List<NotifyCollectionChangedEventArgs>(); collection.CollectionChanged += (_, args) => changes.Add(args);
        collection.ReplaceRange(Enumerable.Range(0, 10_000));
        Assert.Equal(10_000, collection.Count); Assert.Single(changes); Assert.Equal(NotifyCollectionChangedAction.Reset, changes[0].Action);
    }

    [Fact]
    public void SearchTextSetter_DoesNotReloadRepository()
    {
        var source = Read("src", "QuickResponseBao.App", "ViewModels", "MainViewModel.cs");
        var setter = source[(source.IndexOf("public string SearchText", StringComparison.Ordinal))..source.IndexOf("public QuickResponse?", StringComparison.Ordinal)];
        Assert.DoesNotContain("RefreshAsync", setter); Assert.DoesNotContain("GetAllAsync", setter);
    }

    [Fact]
    public void LibrarySearchRefreshesOnlyCollectionView()
    {
        var source = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml.cs");
        var start = source.IndexOf("SearchBox_TextChanged", StringComparison.Ordinal); var end = source.IndexOf("Filter_SelectionChanged", start, StringComparison.Ordinal); var handler = source[start..end];
        var applyStart = source.IndexOf("private void ApplyFilters", StringComparison.Ordinal); var applyEnd = source.IndexOf("private void ClearFilterChip_Click", applyStart, StringComparison.Ordinal); var apply = source[applyStart..applyEnd];
        Assert.Contains("ApplyFilters", handler); Assert.Contains("DeferRefresh", apply); Assert.DoesNotContain("RefreshAsync", handler); Assert.DoesNotContain("GetAllAsync", handler);
    }

    [Fact]
    public void DataGridPreservesVirtualizationAndExplicitWheelScrolling()
    {
        var xaml = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml"); var code = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml.cs");
        Assert.Contains("VirtualizationMode=\"Recycling\"", xaml); Assert.Contains("EnableRowVirtualization=\"True\"", xaml); Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml); Assert.Contains("ResponsesGrid_PreviewMouseWheel", code); Assert.Contains("ScrollToVerticalOffset", code);
    }

    [Fact]
    public void SecondLibraryEntryUsesLoadedVersionWithoutDatabaseRead()
    {
        var main = Read("src", "QuickResponseBao.App", "ViewModels", "MainViewModel.cs"); var page = Read("src", "QuickResponseBao.App", "Views", "Pages", "ResponseLibraryPage.xaml.cs");
        Assert.Contains("IsLoaded ? Task.CompletedTask", main); Assert.Contains("EnsureLoadedAsync", page); Assert.DoesNotContain("Repository.GetAllAsync", page);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
