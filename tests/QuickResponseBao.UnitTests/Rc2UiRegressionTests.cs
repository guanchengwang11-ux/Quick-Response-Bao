using System.Xml.Linq;

namespace QuickResponseBao.UnitTests;

public sealed class Rc2UiRegressionTests
{
    [Fact]
    public void MainNavigation_ContainsEightExplicitPages()
    {
        var document = XDocument.Load(PathAt("src", "QuickResponseBao.App", "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var tags = document.Descendants(presentation + "Button")
            .Select(button => (string?)button.Attribute("Tag"))
            .Where(tag => int.TryParse(tag, out _)).ToArray();
        Assert.Equal(Enumerable.Range(0, 8).Select(value => value.ToString()), tags);
    }

    [Fact]
    public void DiagnosticRows_DoNotExceedDeclaredGridRows()
    {
        var document = XDocument.Load(PathAt("src", "QuickResponseBao.App", "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var actions = document.Descendants(presentation + "WrapPanel").Single(element => (string?)element.Attribute(x + "Name") == "DiagnosticActions");
        var grid = actions.Parent!;
        var rowCount = grid.Element(presentation + "Grid.RowDefinitions")!.Elements(presentation + "RowDefinition").Count();
        var assignedRows = grid.Elements().Select(element => int.TryParse((string?)element.Attribute("Grid.Row"), out var row) ? row : 0);
        Assert.Equal(21, rowCount);
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
