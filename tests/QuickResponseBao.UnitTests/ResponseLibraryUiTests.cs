namespace QuickResponseBao.UnitTests;

public sealed class ResponseLibraryUiTests
{
    [Fact]
    public void Library_ProvidesSearchFiltersAndPrimaryActions()
    {
        var xaml = Read("ResponseLibraryPage.xaml");
        Assert.Contains("QrbSearchBoxStyle", xaml, StringComparison.Ordinal);
        Assert.Contains("CategoryFilter", xaml, StringComparison.Ordinal);
        Assert.Contains("LanguageFilter", xaml, StringComparison.Ordinal);
        Assert.Contains("StatusFilter", xaml, StringComparison.Ordinal);
        Assert.Contains("QrbPrimaryButtonStyle", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_DataGridUsesRecyclingVirtualizationAndStructuredColumns()
    {
        var xaml = Read("ResponseLibraryPage.xaml");
        Assert.Contains("VirtualizationMode=\"Recycling\"", xaml, StringComparison.Ordinal);
        Assert.Contains("EnableRowVirtualization=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("KeywordPreviewControl", xaml, StringComparison.Ordinal);
        Assert.Contains("MoreHorizontal24", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_SelectionToolbarIsContextualAndSupportsAllBatchActions()
    {
        var xaml = Read("ResponseLibraryPage.xaml");
        Assert.Contains("x:Name=\"SelectionToolbar\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"Collapsed\"", xaml, StringComparison.Ordinal);
        foreach (var key in new[] { "BatchEnable", "BatchDisable", "BatchMove", "BatchDelete" })
            Assert.Contains($"DynamicResource {key}", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void KeywordPreview_ShowsAtMostThreeChipsAndOverflowCount()
    {
        var code = Read(Path.Combine("..", "..", "Controls", "KeywordPreviewControl.xaml.cs"));
        Assert.Contains("values.Take(3)", code, StringComparison.Ordinal);
        Assert.Contains("$\"+{remaining}\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_ContainsNoHardcodedPalette()
    {
        Assert.DoesNotMatch("#[0-9a-fA-F]{6,8}", Read("ResponseLibraryPage.xaml"));
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "Views", "Pages", relative));
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
