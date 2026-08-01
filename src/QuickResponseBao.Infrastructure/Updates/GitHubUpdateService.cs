using System.Net.Http.Json;
using System.Text.Json.Serialization;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.Infrastructure.Updates;

public sealed record UpdateInfo(string Version, string Notes, Uri Page, IReadOnlyList<ReleaseAsset> Assets);
public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size);

public sealed class GitHubUpdateService(HttpClient client)
{
    private const string LatestRelease = "https://api.github.com/repos/guanchengwang11-ux/Quick-Response-Bao/releases/latest";

    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestRelease);
        request.Headers.UserAgent.ParseAdd("QuickResponseBao/1.0.0");
        using var response = await client.SendAsync(request, token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: token)
            ?? throw new InvalidDataException("GitHub returned an empty release response.");
        if (release.Draft || release.Prerelease) return null;
        if (!SemanticVersion.TryParse(currentVersion, out var current) ||
            !SemanticVersion.TryParse(release.TagName, out var latest) || latest.CompareTo(current) <= 0) return null;
        return new UpdateInfo(latest.ToString(), release.Body ?? string.Empty, new Uri(release.HtmlUrl),
            release.Assets.Select(x => new ReleaseAsset(x.Name, new Uri(x.BrowserDownloadUrl), x.Size)).ToList());
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        string? Body,
        bool Draft,
        bool Prerelease,
        IReadOnlyList<GitHubAsset> Assets);
    private sealed record GitHubAsset(string Name, long Size,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
