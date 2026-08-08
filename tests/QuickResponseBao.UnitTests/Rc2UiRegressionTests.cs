using System.Xml.Linq;

namespace QuickResponseBao.UnitTests;

public sealed class Rc2UiRegressionTests
{
    [Fact]
    public void MainNavigation_ContainsEightExplicitPages()
    {
        var document = XDocument.Load(PathAt("src", "QuickResponseBao.App", "MainWindow.xaml"));
        XNamespace ui = "http://schemas.lepo.co/wpfui/2022/xaml";
        var tags = document.Descendants(ui + "NavigationViewItem")
            .Select(item => (string?)item.Attribute("TargetPageTag"))
            .Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray();
        Assert.Equal(new[] { "Dashboard", "Library", "Categories", "ImportExport", "Applications", "Diagnostics", "Settings", "About" }, tags);
    }

    [Fact]
    public void DiagnosticRows_DoNotExceedDeclaredGridRows()
    {
        var document = XDocument.Load(PathAt("src", "QuickResponseBao.App", "Views", "Pages", "DiagnosticsPage.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var grid = document.Descendants(presentation + "Grid").Single(element => element.Element(presentation + "Grid.RowDefinitions") is not null);
        var rowCount = grid.Element(presentation + "Grid.RowDefinitions")!.Elements(presentation + "RowDefinition").Count();
        var assignedRows = grid.Elements().Select(element => int.TryParse((string?)element.Attribute("Grid.Row"), out var row) ? row : 0);
        Assert.Equal(10, rowCount);
        Assert.True(assignedRows.Max() < rowCount);
    }

    [Fact]
    public void ApplicationAndInstaller_UseGeneratedIcon()
    {
        Assert.True(File.Exists(PathAt("assets", "branding", "QuickResponseBao.ico")));
        Assert.Contains("QuickResponseBao.ico", File.ReadAllText(PathAt("src", "QuickResponseBao.App", "QuickResponseBao.App.csproj")));
        Assert.Contains("SetupIconFile=..\\assets\\branding\\QuickResponseBao.ico", File.ReadAllText(PathAt("installer", "QuickResponseBao.iss")));
    }

    private static string PathAt(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), Path.Combine(parts));
    }
}
