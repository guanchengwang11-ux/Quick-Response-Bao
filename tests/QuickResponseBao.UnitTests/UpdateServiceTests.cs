using System.Net;
using System.Security.Cryptography;
using System.Text;
using QuickResponseBao.Infrastructure.Updates;

namespace QuickResponseBao.UnitTests;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qrb-update-{Guid.NewGuid():N}");
    public UpdateServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Check_IgnoresDraftRelease()
    {
        using var client = JsonClient(Releases(Release("v9.0.0", draft: true), Release("v1.1.0")));
        var result = await new GitHubUpdateService(client).CheckAsync("1.0.0");
        Assert.True(result.IsUpdateAvailable); Assert.Equal("1.1.0", result.LatestVersion);
    }

    [Fact]
    public async Task Check_IgnoresPrereleaseByDefault()
    {
        using var client = JsonClient(Releases(Release("v2.0.0-beta.1", prerelease: true), Release("v1.2.0")));
        var result = await new GitHubUpdateService(client).CheckAsync("1.0.0");
        Assert.Equal("1.2.0", result.LatestVersion); Assert.False(result.Update!.IsPrerelease);
    }

    [Fact]
    public async Task Check_IncludesPrereleaseWhenEnabled()
    {
        using var client = JsonClient(Releases(Release("v2.0.0-beta.1", prerelease: true), Release("v1.2.0")));
        var result = await new GitHubUpdateService(client).CheckAsync("1.0.0", includePrerelease: true);
        Assert.Equal("2.0.0-beta.1", result.LatestVersion); Assert.True(result.Update!.IsPrerelease);
    }

    [Theory][InlineData("1.2.0")][InlineData("1.3.0")]
    public async Task Check_ReportsNoUpdateWhenCurrentIsEqualOrNewer(string current)
    {
        using var client = JsonClient(Releases(Release("v1.2.0")));
        var result = await new GitHubUpdateService(client).CheckAsync(current);
        Assert.False(result.IsUpdateAvailable); Assert.Equal("1.2.0", result.LatestVersion);
    }

    [Fact]
    public void AssetSelector_PrefersExactX64SetupAsset()
    {
        var update = Update([Asset("Quick-Response-Bao-Portable-1.2.0-x64.zip"), Asset("checksums.txt"), Asset("Quick-Response-Bao-Setup-1.2.0-x64.exe")]);
        var selected = ReleaseAssetSelector.Select(update);
        Assert.Equal(ReleaseAssetKind.Setup, selected.Kind); Assert.Equal("Quick-Response-Bao-Setup-1.2.0-x64.exe", selected.Asset.Name);
    }

    [Fact]
    public void AssetSelector_PrefersPackageForPortableInstallation()
    {
        var update = Update([Asset("Quick-Response-Bao-Portable-1.2.0-x64.zip"), Asset("checksums.txt"), Asset("Quick-Response-Bao-Setup-1.2.0-x64.exe")]);
        Assert.Equal(ReleaseAssetKind.Package, ReleaseAssetSelector.Select(update, preferSetup: false).Kind);
    }

    [Fact]
    public void AssetSelector_ThrowsWhenNoSupportedAssetExists()
    {
        var update = Update([Asset("checksums.txt"), Asset("source-code.zip")]);
        Assert.Throws<UpdateOperationException>(() => ReleaseAssetSelector.Select(update));
    }

    [Fact]
    public async Task Sha256_VerificationSucceedsForMatchingHash()
    {
        var path = Path.Combine(_root, "valid.bin"); await File.WriteAllTextAsync(path, "verified");
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("verified")));
        Assert.True(await UpdateDownloadService.VerifySha256Async(path, expected));
    }

    [Fact]
    public async Task Sha256_MismatchBlocksInstallAndDeletesDamagedFile()
    {
        var bytes = Encoding.UTF8.GetBytes("damaged"); var selection = Selection(bytes, new string('0', 64));
        using var client = new HttpClient(new DelegateHandler((request, _, _) => Task.FromResult(Response(request.RequestUri!.AbsolutePath.EndsWith("checksums.txt")
            ? Encoding.UTF8.GetBytes($"{new string('0', 64)}  package.zip") : bytes))));
        await Assert.ThrowsAsync<UpdateOperationException>(() => new UpdateDownloadService(client).DownloadVerifiedAsync(selection, _root));
        Assert.False(File.Exists(Path.Combine(_root, "package.zip"))); Assert.False(File.Exists(Path.Combine(_root, "package.zip.partial")));
    }

    [Fact]
    public async Task Download_CancellationRemovesPartialFile()
    {
        var bytes = Encoding.UTF8.GetBytes("content"); var hash = Convert.ToHexString(SHA256.HashData(bytes)); var selection = Selection(bytes, hash);
        using var client = new HttpClient(new DelegateHandler((request, _, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("checksums.txt")
            ? Response(Encoding.UTF8.GetBytes($"{hash}  package.zip"))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new CancelableStream()) })));
        using var cancellation = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UpdateDownloadService(client).DownloadVerifiedAsync(selection, _root, token: cancellation.Token));
        Assert.False(File.Exists(Path.Combine(_root, "package.zip.partial")));
    }

    [Fact]
    public async Task Download_RetriesTransientFailureAndThenSucceeds()
    {
        var bytes = Encoding.UTF8.GetBytes("retry success"); var hash = Convert.ToHexString(SHA256.HashData(bytes)); var assetAttempts = 0; var selection = Selection(bytes, hash);
        using var client = new HttpClient(new DelegateHandler((request, _, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("checksums.txt")) return Task.FromResult(Response(Encoding.UTF8.GetBytes($"{hash}  package.zip")));
            assetAttempts++; return Task.FromResult(assetAttempts < 3 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Response(bytes));
        }));
        var path = await new UpdateDownloadService(client).DownloadVerifiedAsync(selection, _root, maximumAttempts: 3);
        Assert.Equal(3, assetAttempts); Assert.True(File.Exists(path));
    }

    private static HttpClient JsonClient(string json) => new(new DelegateHandler((_, _, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })));
    private static string Releases(params string[] releases) => $"[{string.Join(',', releases)}]";
    private static string Release(string tag, bool draft = false, bool prerelease = false) => $"{{\"tag_name\":\"{tag}\",\"html_url\":\"https://example.test/{tag}\",\"body\":\"notes\",\"draft\":{draft.ToString().ToLowerInvariant()},\"prerelease\":{prerelease.ToString().ToLowerInvariant()},\"assets\":[{{\"name\":\"checksums.txt\",\"size\":10,\"browser_download_url\":\"https://example.test/checksums.txt\"}},{{\"name\":\"Quick-Response-Bao-Portable-{tag.TrimStart('v')}-x64.zip\",\"size\":10,\"browser_download_url\":\"https://example.test/package.zip\"}}]}}";
    private static ReleaseAsset Asset(string name) => new(name, new Uri($"https://example.test/{name}"), 100);
    private static UpdateInfo Update(IReadOnlyList<ReleaseAsset> assets) => new("1.2.0", "notes", new Uri("https://example.test/release"), assets, false);
    private static SelectedUpdateAsset Selection(byte[] bytes, string hash) => new(new ReleaseAsset("package.zip", new Uri("https://example.test/package.zip"), bytes.Length), new ReleaseAsset("checksums.txt", new Uri("https://example.test/checksums.txt"), 80), ReleaseAssetKind.Package);
    private static HttpResponseMessage Response(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class DelegateHandler(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private int _calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request, Interlocked.Increment(ref _calls), cancellationToken);
    }

    private sealed class CancelableStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => 1; public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
