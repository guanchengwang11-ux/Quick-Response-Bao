using System.Xml.Linq;

namespace QuickResponseBao.UnitTests;

public sealed class UiInfrastructureTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void DesignTokens_ContainApprovedSpacingRadiusHeightAndMotionScales()
    {
        var keys = Keys(Resource("DesignTokens.xaml"));
        var expected = new[]
        {
            "QrbSpace4", "QrbSpace8", "QrbSpace12", "QrbSpace16", "QrbSpace20", "QrbSpace24", "QrbSpace32",
            "QrbRadius6", "QrbRadius8", "QrbRadius10", "QrbRadius12",
            "QrbControlHeight32", "QrbControlHeight36", "QrbControlHeight40", "QrbControlHeight44",
            "QrbMotionFast", "QrbMotionNormal", "QrbMotionSlow", "QrbFocusRingThickness"
        };
        Assert.All(expected, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void Typography_ContainsCompleteSemanticHierarchy()
    {
        var keys = Keys(Resource("Typography.xaml"));
        Assert.All(new[] { "QrbDisplayTextStyle", "QrbPageTitleTextStyle", "QrbSectionTitleTextStyle", "QrbBodyStrongTextStyle", "QrbBodyTextStyle", "QrbCaptionTextStyle" },
            key => Assert.Contains(key, keys));
    }

    [Fact]
    public void ControlResources_ContainButtonsInputsCardsStatusAndFeedback()
    {
        var keys = Keys(Resource("Controls.xaml"));
        var expected = new[]
        {
            "QrbPrimaryButtonStyle", "QrbSecondaryButtonStyle", "QrbGhostButtonStyle", "QrbDangerButtonStyle", "QrbIconButtonStyle", "QrbAsyncButtonStyle",
            "QrbTextBoxStyle", "QrbSearchBoxStyle", "QrbComboBoxStyle", "QrbToggleSwitchStyle", "QrbCheckBoxStyle",
            "QrbCardStyle", "QrbStatusBadgeStyle", "QrbEmptyStateStyle", "QrbInfoBarStyle", "QrbFocusVisualStyle"
        };
        Assert.All(expected, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void FluentIcons_HaveOnlyApprovedSemanticSizes()
    {
        var keys = Keys(Resource("Icons.xaml"));
        Assert.Contains("QrbIcon16Style", keys);
        Assert.Contains("QrbIcon20Style", keys);
        Assert.Contains("QrbIcon24Style", keys);
    }

    [Fact]
    public void WpfUi_IsPinnedAndCompetingUiFrameworksAreNotReferenced()
    {
        var project = XDocument.Load(Path.Combine(Root(), "src", "QuickResponseBao.App", "QuickResponseBao.App.csproj"));
        var packages = project.Descendants("PackageReference").Select(x => ((string?)x.Attribute("Include"), (string?)x.Attribute("Version"))).ToArray();
        Assert.Contains(packages, x => x.Item1 == "WPF-UI" && x.Item2 == "4.3.0");
        Assert.DoesNotContain(packages, x => x.Item1 is "MahApps.Metro" or "MaterialDesignThemes" or "HandyControl");
    }

    [Fact]
    public void AppResources_IntegrateWpfUiBeforeQrbOverrides()
    {
        var app = File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "App.xaml"));
        Assert.Contains("<ui:ThemesDictionary", app, StringComparison.Ordinal);
        Assert.Contains("<ui:ControlsDictionary", app, StringComparison.Ordinal);
        Assert.Contains("Resources/LightTheme.xaml", app, StringComparison.Ordinal);
        Assert.Contains("Resources/Theme.xaml", app, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicLanguageSwitch_UpdatesLegacyAndSemanticFontResources()
    {
        var service = File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "Services", "LocalizationService.cs"));
        Assert.Contains("AppFontFamily", service, StringComparison.Ordinal);
        Assert.Contains("QrbFontFamily", service, StringComparison.Ordinal);
        Assert.Contains("Segoe UI, Microsoft YaHei UI", service, StringComparison.Ordinal);
        Assert.Contains("Microsoft YaHei UI, Segoe UI", service, StringComparison.Ordinal);
    }

    [Fact]
    public void FuturePageSkeletons_UseSemanticDynamicResourcesWithoutHexColors()
    {
        var pages = Directory.GetFiles(Path.Combine(Root(), "src", "QuickResponseBao.App", "Views", "Pages"), "*.xaml");
        Assert.Equal(8, pages.Length);
        Assert.All(pages, page =>
        {
            var text = File.ReadAllText(page);
            Assert.Contains("DynamicResource Qrb", text, StringComparison.Ordinal);
            Assert.Contains("DynamicResource", text, StringComparison.Ordinal);
            Assert.DoesNotMatch("#[0-9a-fA-F]{6,8}", text);
        });
    }

    [Fact]
    public void HighContrastTheme_MapsCoreTokensToWindowsSystemResources()
    {
        var text = File.ReadAllText(Resource("HighContrastTheme.xaml"));
        Assert.Contains("SystemColors.WindowColorKey", text, StringComparison.Ordinal);
        Assert.Contains("SystemColors.WindowTextColorKey", text, StringComparison.Ordinal);
        Assert.Contains("SystemColors.HighlightColorKey", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeService_DelegatesToWpfUiAndRetainsHighContrastFallback()
    {
        var service = File.ReadAllText(Path.Combine(Root(), "src", "QuickResponseBao.App", "Services", "ThemeService.cs"));
        Assert.Contains("ApplicationThemeManager.Apply", service, StringComparison.Ordinal);
        Assert.Contains("ApplicationThemeManager.IsSystemHighContrast", service, StringComparison.Ordinal);
        Assert.Contains("SystemPreferenceChanged", service, StringComparison.Ordinal);
        Assert.Contains("HighContrastTheme.xaml", service, StringComparison.Ordinal);
    }

    [Fact]
    public void WpfUiLicense_IsDistributedAndListedInNotices()
    {
        var license = Path.Combine(Root(), "licenses", "WPF-UI-LICENSE.txt");
        var bundledNotices = Path.Combine(Root(), "licenses", "WPF-UI-THIRD-PARTY-NOTICES.txt");
        Assert.True(File.Exists(license));
        Assert.True(File.Exists(bundledNotices));
        Assert.Contains("MIT License", File.ReadAllText(license), StringComparison.Ordinal);
        Assert.Contains("fluentui-system-icons", File.ReadAllText(bundledNotices), StringComparison.Ordinal);
        Assert.Contains("WPF UI 4.3.0", File.ReadAllText(Path.Combine(Root(), "THIRD-PARTY-NOTICES.md")), StringComparison.Ordinal);
    }

    private static string Resource(string name) => Path.Combine(Root(), "src", "QuickResponseBao.App", "Resources", name);
    private static string[] Keys(string path) => XDocument.Load(path).Descendants().Select(element => (string?)element.Attribute(Xaml + "Key"))
        .Where(key => key is not null).Cast<string>().ToArray();

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
