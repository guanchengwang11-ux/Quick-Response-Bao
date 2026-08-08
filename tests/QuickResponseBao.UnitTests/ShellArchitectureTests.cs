namespace QuickResponseBao.UnitTests;

public sealed class ShellArchitectureTests
{
    [Fact]
    public void MainWindow_UsesFluentWindowNavigationViewAndNoHiddenTabControl()
    {
        var xaml = Read("src", "QuickResponseBao.App", "MainWindow.xaml");
        Assert.Contains("<ui:FluentWindow", xaml, StringComparison.Ordinal);
        Assert.Contains("<ui:NavigationView", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationView.MenuItems", xaml, StringComparison.Ordinal);
        Assert.Contains("NavigationView.FooterMenuItems", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("TabControl", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowCodeBehind_HasShellOnlyScale()
    {
        var lines = File.ReadAllLines(Path.Combine(Root(), "src", "QuickResponseBao.App", "MainWindow.xaml.cs"));
        Assert.True(lines.Length < 150, $"MainWindow.xaml.cs has {lines.Length} lines.");
        Assert.DoesNotContain(lines, line => line.Contains("ExcelQuickResponseService", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains("Sqlite", StringComparison.Ordinal));
    }

    [Fact]
    public void ShellNavigation_RegistersAllEightRoutesAndCachesPagesLazily()
    {
        var shell = Read("src", "QuickResponseBao.App", "MainWindow.xaml.cs");
        foreach (var route in new[] { "Dashboard", "Library", "Categories", "ImportExport", "Applications", "Diagnostics", "Settings", "About" })
            Assert.Contains($"ShellRoutes.{route}", shell, StringComparison.Ordinal);
        var service = Read("src", "QuickResponseBao.App", "Services", "ShellNavigationService.cs");
        Assert.Contains("Dictionary<string, Func<Page>>", service, StringComparison.Ordinal);
        Assert.Contains("_cache.TryGetValue", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_UsesFluentSystemIconsAndResponsivePaneLengths()
    {
        var xaml = Read("src", "QuickResponseBao.App", "MainWindow.xaml");
        Assert.Contains("OpenPaneLength=\"232\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CompactPaneLength=\"52\"", xaml, StringComparison.Ordinal);
        Assert.Equal(8, xaml.Split("<ui:NavigationViewItem ", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("Emoji", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
