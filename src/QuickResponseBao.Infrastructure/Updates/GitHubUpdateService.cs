using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.Infrastructure.Updates;

public sealed record UpdateInfo(string Version, string Notes, Uri Page, IReadOnlyList<ReleaseAsset> Assets, bool IsPrerelease);
public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size);
public sealed record UpdateCheckResult(string CurrentVersion, string? LatestVersion, UpdateInfo? Update)
{
    public bool IsUpdateAvailable => Update is not null;
}
public enum ReleaseAssetKind { Package, Setup }
public sealed record SelectedUpdateAsset(ReleaseAsset Asset, ReleaseAsset Checksums, ReleaseAssetKind Kind);
public sealed class UpdateOperationException(string code, string message) : Exception(message) { public string Code { get; } = code; }

public sealed class GitHubUpdateService(HttpClient client)
{
    internal const string ReleasesEndpoint = "https://api.github.com/repos/guanchengwang11-ux/Quick-Response-Bao/releases?per_page=30";

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, bool includePrerelease = false, CancellationToken token = default)
    {
        if (!SemanticVersion.TryParse(currentVersion, out var current)) throw new ArgumentException("The current application version is invalid.", nameof(currentVersion));
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesEndpoint);
        request.Headers.UserAgent.ParseAdd($"QuickResponseBao/{current}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var releases = await response.Content.ReadFromJsonAsync<IReadOnlyList<GitHubRelease>>(cancellationToken: token)
            ?? throw new InvalidDataException("GitHub returned an empty release response.");
        var candidates = releases
            .Where(x => !x.Draft && (includePrerelease || !x.Prerelease))
            .Select(x => (Release: x, Parsed: SemanticVersion.TryParse(x.TagName, out var parsed) ? parsed : (SemanticVersion?)null))
            .Where(x => x.Parsed is not null && (includePrerelease || x.Parsed.Value.Prerelease is null))
            .OrderByDescending(x => x.Parsed!.Value).ToList();
        if (candidates.Count == 0) return new UpdateCheckResult(current.ToString(), null, null);
        var latest = candidates[0];
        if (latest.Parsed!.Value.CompareTo(current) <= 0) return new UpdateCheckResult(current.ToString(), latest.Parsed.Value.ToString(), null);
        var release = latest.Release;
        var assets = release.Assets.Select(x => new ReleaseAsset(x.Name, new Uri(x.BrowserDownloadUrl), x.Size)).ToList();
        var update = new UpdateInfo(latest.Parsed.Value.ToString(), release.Body ?? string.Empty, new Uri(release.HtmlUrl), assets, release.Prerelease || latest.Parsed.Value.Prerelease is not null);
        return new UpdateCheckResult(current.ToString(), update.Version, update);
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] IReadOnlyList<GitHubAsset> Assets);
    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}

public static partial class ReleaseAssetSelector
{
    public static SelectedUpdateAsset Select(UpdateInfo update, bool preferSetup = true)
    {
        var checksums = update.Assets.SingleOrDefault(x => x.Name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase))
            ?? throw new UpdateOperationException("MissingChecksums", "The release does not contain checksums.txt; installation is blocked.");
        var escaped = Regex.Escape(update.Version);
        var setupPatterns = new[]
        {
            $"^Quick-Response-Bao-Setup-v?{escaped}-x64\\.exe$",
            "^Quick-Response-Bao-Setup-x64\\.exe$"
        };
        var packagePatterns = new[]
        {
            $"^Quick-Response-Bao-(?:Update|Portable)-v?{escaped}-x64\\.zip$",
            "^Quick-Response-Bao-(?:Update|Portable|win)-x64\\.zip$"
        };
        var setup = Match(update.Assets, setupPatterns); var package = Match(update.Assets, packagePatterns);
        if (preferSetup && setup is not null) return new SelectedUpdateAsset(setup, checksums, ReleaseAssetKind.Setup);
        if (package is not null) return new SelectedUpdateAsset(package, checksums, ReleaseAssetKind.Package);
        if (setup is not null) return new SelectedUpdateAsset(setup, checksums, ReleaseAssetKind.Setup);
        throw new UpdateOperationException("NoAsset", "No supported x64 Setup.exe or standard update ZIP asset was found in the release.");
    }

    private static ReleaseAsset? Match(IEnumerable<ReleaseAsset> assets, IEnumerable<string> patterns) =>
        patterns.Select(pattern => assets.SingleOrDefault(x => Regex.IsMatch(x.Name, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            .FirstOrDefault(x => x is not null);
}
