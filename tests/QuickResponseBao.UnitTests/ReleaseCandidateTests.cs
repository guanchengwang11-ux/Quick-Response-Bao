using System.Net;
using System.Text;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Storage;
using QuickResponseBao.Infrastructure.Updates;

namespace QuickResponseBao.UnitTests;

public sealed class ReleaseCandidateTests
{
    [Fact]
    public void RcVersion_ParsesWithPrereleaseIdentifiers()
    {
        Assert.True(SemanticVersion.TryParse("v1.0.0-rc.1", out var value));
        Assert.Equal("1.0.0-rc.1", value.ToString());
    }

    [Fact]
    public void StableVersion_HasHigherPrecedenceThanRc()
    {
        Assert.True(SemanticVersion.TryParse("1.0.0", out var stable));
        Assert.True(SemanticVersion.TryParse("1.0.0-rc.1", out var rc));
        Assert.True(stable.CompareTo(rc) > 0);
    }

    [Fact]
    public async Task PrereleaseDisabled_IgnoresRcRelease()
    {
        using var client = ReleaseClient();
        var result = await new GitHubUpdateService(client).CheckAsync("0.9.0", includePrerelease: false);
        Assert.Equal("0.9.1", result.LatestVersion);
    }

    [Fact]
    public async Task PrereleaseEnabled_AllowsRcRelease()
    {
        using var client = ReleaseClient();
        var result = await new GitHubUpdateService(client).CheckAsync("0.9.0", includePrerelease: true);
        Assert.Equal("1.0.0-rc.1", result.LatestVersion);
    }

    [Fact]
    public void AssetSelector_MatchesRcInstallerNameExactly()
    {
        var update = RcUpdate();
        var selected = ReleaseAssetSelector.Select(update, preferSetup: true);
        Assert.Equal("Quick-Response-Bao-Setup-1.0.0-rc.1-x64.exe", selected.Asset.Name);
        Assert.Equal(ReleaseAssetKind.Setup, selected.Kind);
    }

    [Fact]
    public void AssetSelector_MatchesRcPortableNameExactly()
    {
        var update = RcUpdate();
        var selected = ReleaseAssetSelector.Select(update, preferSetup: false);
        Assert.Equal("Quick-Response-Bao-Portable-1.0.0-rc.1-x64.zip", selected.Asset.Name);
        Assert.Equal(ReleaseAssetKind.Package, selected.Kind);
    }

    [Fact]
    public void ChecksumManifest_ContainsBothReleaseFiles()
    {
        var setupHash = new string('A', 64); var portableHash = new string('B', 64);
        var manifest = $"{setupHash}  Quick-Response-Bao-Setup-1.0.0-rc.1-x64.exe\n{portableHash}  Quick-Response-Bao-Portable-1.0.0-rc.1-x64.zip";
        Assert.Equal(setupHash, UpdateDownloadService.FindChecksum(manifest, "Quick-Response-Bao-Setup-1.0.0-rc.1-x64.exe"));
        Assert.Equal(portableHash, UpdateDownloadService.FindChecksum(manifest, "Quick-Response-Bao-Portable-1.0.0-rc.1-x64.zip"));
    }

    [Fact]
    public void UserDataPaths_AreIsolatedFromInstallDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qrb-paths-{Guid.NewGuid():N}");
        try
        {
            var install = Path.Combine(root, "program"); var paths = new AppPaths(Path.Combine(root, "local-app-data"));
            foreach (var path in new[] { paths.Database, paths.Settings, paths.Backups, paths.Logs })
                Assert.False(Path.GetFullPath(path).StartsWith(Path.GetFullPath(install), StringComparison.OrdinalIgnoreCase));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(true, true, CandidatePositionMethod.Caret)]
    [InlineData(false, true, CandidatePositionMethod.WindowBottomRight)]
    [InlineData(false, false, CandidatePositionMethod.CurrentMonitorBottomRight)]
    public void CandidatePosition_UsesThreeLevelFallback(bool caret, bool window, CandidatePositionMethod expected) =>
        Assert.Equal(expected, CandidatePositionFallback.Resolve(caret, window));

    [Fact]
    public void ProcessWhitelist_MatchesNamesCaseInsensitively()
    {
        Assert.True(ProcessWhitelist.Contains(["LARK.exe", "chrome.exe"], "lark.EXE"));
        Assert.False(ProcessWhitelist.Contains(["LARK.exe"], "telegram.exe"));
    }

    private static UpdateInfo RcUpdate() => new("1.0.0-rc.1", "notes", new Uri("https://example.test/rc"),
    [
        new("Quick-Response-Bao-Portable-1.0.0-rc.1-x64.zip", new Uri("https://example.test/portable"), 100),
        new("Quick-Response-Bao-Setup-1.0.0-rc.1-x64.exe", new Uri("https://example.test/setup"), 100),
        new("checksums.txt", new Uri("https://example.test/checksums"), 100)
    ], true);

    private static HttpClient ReleaseClient()
    {
        const string json = "[{\"tag_name\":\"v1.0.0-rc.1\",\"html_url\":\"https://example.test/rc\",\"body\":\"rc\",\"draft\":false,\"prerelease\":true,\"assets\":[{\"name\":\"checksums.txt\",\"size\":10,\"browser_download_url\":\"https://example.test/checksums\"},{\"name\":\"Quick-Response-Bao-Portable-1.0.0-rc.1-x64.zip\",\"size\":10,\"browser_download_url\":\"https://example.test/portable\"}]},{\"tag_name\":\"v0.9.1\",\"html_url\":\"https://example.test/stable\",\"body\":\"stable\",\"draft\":false,\"prerelease\":false,\"assets\":[{\"name\":\"checksums.txt\",\"size\":10,\"browser_download_url\":\"https://example.test/checksums\"},{\"name\":\"Quick-Response-Bao-Portable-0.9.1-x64.zip\",\"size\":10,\"browser_download_url\":\"https://example.test/portable\"}]}]";
        return new HttpClient(new JsonHandler(json));
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
