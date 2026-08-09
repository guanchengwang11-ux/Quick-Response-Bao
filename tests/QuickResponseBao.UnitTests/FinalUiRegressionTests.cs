using System.Diagnostics;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.UnitTests;

public sealed class FinalUiRegressionTests
{
    [Fact]
    public void HighContrast_UsesWindowsSystemColorsForEveryCriticalSemanticRole()
    {
        var xaml = Read("src", "QuickResponseBao.App", "Resources", "HighContrastTheme.xaml");
        foreach (var key in new[] { "QrbBackgroundBrush", "QrbTextPrimaryBrush", "QrbFocusRingBrush", "QrbSelectionBrush", "QrbInputBrush", "QrbErrorBrush" }) Assert.Contains($"x:Key=\"{key}\"", xaml);
        Assert.Contains("SystemColors.HighlightColorKey", xaml); Assert.Contains("SystemColors.WindowTextColorKey", xaml);
    }

    [Fact]
    public void AppManifest_EnablesPerMonitorV2AndWindowsTenCompatibility()
    {
        var manifest = Read("src", "QuickResponseBao.App", "app.manifest"); var project = Read("src", "QuickResponseBao.App", "QuickResponseBao.App.csproj");
        Assert.Contains("PerMonitorV2", project); Assert.Contains("8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a", manifest);
    }

    [Fact]
    public void CandidateWindow_PreservesFastKeyboardContractAndNoActivate()
    {
        var xaml = Read("src", "QuickResponseBao.App", "CandidateWindow.xaml"); var code = Read("src", "QuickResponseBao.App", "CandidateWindow.xaml.cs");
        Assert.Contains("ShowActivated=\"False\"", xaml); Assert.Contains("ConfirmEnter", code); Assert.Contains("ConfirmTab", code); Assert.Contains("PageUp", code); Assert.Contains("PageDown", code); Assert.Contains("BringIntoView", code); Assert.Contains("0x08000000", code);
    }

    [Fact]
    public void MainWindow_RemainsShellSized()
    {
        var source = Read("src", "QuickResponseBao.App", "MainWindow.xaml.cs");
        Assert.DoesNotContain("ExcelQuickResponseService", source);
        Assert.DoesNotContain("Sqlite", source);
    }

    [Fact]
    public void SettingsRows_UseResponsiveTwoColumnAdapter()
    {
        var controls = Read("src", "QuickResponseBao.App", "Resources", "Controls.xaml"); var adapter = Read("src", "QuickResponseBao.App", "Controls", "ResponsiveGrid.cs");
        Assert.Contains("ResponsiveGrid.IsSettingRow", controls); Assert.Contains("GridUnitType.Star", adapter); Assert.Contains("GridLength.Auto", adapter);
    }

    [Fact]
    public void MainPages_DoNotHardCodeHexColors()
    {
        var pages = Directory.GetFiles(Path.Combine(Root(), "src", "QuickResponseBao.App", "Views", "Pages"), "*.xaml");
        Assert.All(pages, path => Assert.DoesNotMatch("#[0-9A-Fa-f]{6,8}", File.ReadAllText(path)));
    }

    [Fact]
    public void SearchTenThousandResponses_RemainsInteractive()
    {
        var responses = Enumerable.Range(0, 10_000).Select(index => new QuickResponse { Summary = $"Account support {index}", Content = "How to solve this account problem quickly", Keywords = ["account", "support"], Category = "Customer care", UsageCount = index % 20 }).ToArray();
        var service = new SearchService(); var timer = Stopwatch.StartNew();
        var result = service.Search(responses, "account support", new SearchOptions(true, true, true, true, false, true, 30)); timer.Stop();
        Assert.Equal(30, result.Count); Assert.True(timer.Elapsed < TimeSpan.FromSeconds(3), $"10,000-response search took {timer.Elapsed}.");
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));
    private static string Root() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
}
