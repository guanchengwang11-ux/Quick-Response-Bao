using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Updates;

namespace QuickResponseBao.UnitTests;

public sealed class V110ReleaseTests
{
    [Fact]
    public void Stable110_IsNewerThanEverySupportedUpgradeBaseline()
    {
        Assert.True(SemanticVersion.TryParse("1.1.0", out var current));
        foreach (var baselineText in new[] { "1.0.0", "1.0.1", "1.0.2" })
        {
            Assert.True(SemanticVersion.TryParse(baselineText, out var baseline));
            Assert.True(current.CompareTo(baseline) > 0, $"1.1.0 must be newer than {baselineText}.");
        }
    }

    [Fact]
    public void AssetSelector_MatchesOfficial110PortableName()
    {
        var update = new UpdateInfo("1.1.0", "notes", new Uri("https://example.test/release"),
        [
            new("Quick-Response-Bao-1.1.0-Portable-x64.zip", new Uri("https://example.test/portable"), 100),
            new("Quick-Response-Bao-Setup-1.1.0-x64.exe", new Uri("https://example.test/setup"), 100),
            new("checksums.txt", new Uri("https://example.test/checksums"), 100)
        ], false);

        var selected = ReleaseAssetSelector.Select(update, preferSetup: false);

        Assert.Equal(ReleaseAssetKind.Package, selected.Kind);
        Assert.Equal("Quick-Response-Bao-1.1.0-Portable-x64.zip", selected.Asset.Name);
    }

    [Fact]
    public void ReleaseMetadata_Uses110AndRequiredPackageNames()
    {
        var root = FindRepositoryRoot();
        Assert.Contains("<Version>1.1.0</Version>", File.ReadAllText(Path.Combine(root, "Directory.Build.props")));
        Assert.Contains("#define MyAppVersion \"1.1.0\"", File.ReadAllText(Path.Combine(root, "installer", "QuickResponseBao.iss")));
        var buildScript = File.ReadAllText(Path.Combine(root, "scripts", "build-release-candidate.ps1"));
        Assert.Contains("Quick-Response-Bao-$Version-Portable-x64.zip", buildScript);
        Assert.Contains("Quick-Response-Bao-Setup-$Version-x64.exe", buildScript);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QuickResponseBao.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
