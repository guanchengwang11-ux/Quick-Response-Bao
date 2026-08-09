namespace QuickResponseBao.UnitTests;

public sealed class DashboardUiTests
{
    [Fact]
    public void Dashboard_HasFourRestrainedMetricsRecentItemsAndQuickActions()
    {
        var xaml = Read();
        Assert.Contains("<UniformGrid Columns=\"4\"", xaml, StringComparison.Ordinal);
        Assert.Equal(4, xaml.Split("Style=\"{DynamicResource QrbCardStyle}\" Margin=\"6\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("RecentResponses", xaml, StringComparison.Ordinal);
        Assert.Contains("QuickActions", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_ExposesAllThreeQuickActions()
    {
        var xaml = Read();
        Assert.Contains("AddResponseAction", xaml, StringComparison.Ordinal);
        Assert.Contains("ImportResponsesAction", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenDiagnostics", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_UsesSemanticResourcesAndNoHardcodedPalette()
    {
        var xaml = Read();
        Assert.DoesNotMatch("#[0-9a-fA-F]{6,8}", xaml);
        Assert.Contains("QrbSuccessSurfaceBrush", xaml, StringComparison.Ordinal);
        Assert.Contains("QrbWarningSurfaceBrush", xaml, StringComparison.Ordinal);
    }

    private static string Read() => File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "Views", "Pages", "DashboardPage.xaml"));
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
