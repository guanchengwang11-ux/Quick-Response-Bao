using System.Xml.Linq;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Storage;

namespace QuickResponseBao.UnitTests;

public sealed class ThemeAndLocalizationTests
{
    private static readonly string[] RequiredBrushes = ["PrimaryBrush", "HighlightBrush", "SurfaceBrush", "SurfaceAltBrush", "CanvasBrush", "TextBrush", "MutedTextBrush", "BorderBrush", "ControlBrush", "InputBrush", "SelectionBrush", "DisabledBrush", "DisabledTextBrush", "SuccessBrush", "WarningBrush", "ErrorBrush", "CardBlueBrush", "CardGreenBrush", "CardAmberBrush"];

    [Fact] public void LightTheme_ContainsAllRequiredResources() => AssertPalette("Light.xaml");
    [Fact] public void DarkTheme_ContainsAllRequiredResources() => AssertPalette("Dark.xaml");

    [Fact]
    public void ChineseAndEnglishResourceKeys_AreCompleteAndIdentical()
    {
        var english = Keys(Path.Combine(Root(), "src", "QuickResponseBao.App", "Resources", "Strings.en-US.xaml"));
        var chinese = Keys(Path.Combine(Root(), "src", "QuickResponseBao.App", "Resources", "Strings.zh-CN.xaml"));
        Assert.NotEmpty(english); Assert.Equal(english, chinese);
    }

    [Fact]
    public async Task ThemePreference_IsPersistedBySettingsStore()
    {
        using var workspace = new TestWorkspace(); var store = new JsonSettingsStore(workspace.Paths);
        await store.SaveAsync(new AppSettings { Theme = ThemeMode.Dark }); var loaded = await store.LoadAsync();
        Assert.Equal(ThemeMode.Dark, loaded.Theme);
    }

    [Theory][InlineData("System", true, false)][InlineData("System", false, true)][InlineData("Light", false, false)][InlineData("Dark", true, true)]
    public void FollowSystemTheme_ResolvesExpectedState(string preference, bool systemLight, bool expectedDark) => Assert.Equal(expectedDark, ThemeMode.ResolveDark(preference, systemLight));

    private static void AssertPalette(string file)
    {
        var keys = Keys(Path.Combine(Root(), "src", "QuickResponseBao.App", "Resources", "Themes", file));
        Assert.All(RequiredBrushes, key => Assert.Contains(key, keys));
    }
    private static string[] Keys(string path)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        return XDocument.Load(path).Root!.Elements().Select(element => (string?)element.Attribute(x + "Key")).Where(key => key is not null).Cast<string>().OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
