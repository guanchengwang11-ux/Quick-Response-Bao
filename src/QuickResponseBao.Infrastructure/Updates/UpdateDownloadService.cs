using System.Buffers;
using System.Net;
using System.Security.Cryptography;

namespace QuickResponseBao.Infrastructure.Updates;

public sealed record UpdateDownloadProgress(string FileName, long DownloadedBytes, long TotalBytes)
{
    public int Percentage => TotalBytes <= 0 ? 0 : (int)Math.Clamp(DownloadedBytes * 100 / TotalBytes, 0, 100);
}

public sealed class UpdateDownloadService(HttpClient client)
{
    public async Task<string> DownloadVerifiedAsync(
        SelectedUpdateAsset selection,
        string updatesDirectory,
        IProgress<UpdateDownloadProgress>? progress = null,
        int maximumAttempts = 3,
        CancellationToken token = default)
    {
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        Directory.CreateDirectory(updatesDirectory);
        var fileName = SafeFileName(selection.Asset.Name);
        var destination = Path.Combine(updatesDirectory, fileName);
        var partial = $"{destination}.partial";
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();
            TryDelete(partial);
            try
            {
                var checksumsText = await DownloadTextAsync(selection.Checksums.DownloadUrl, token);
                await File.WriteAllTextAsync(Path.Combine(updatesDirectory, "checksums.txt"), checksumsText, token);
                var expected = FindChecksum(checksumsText, fileName);
                await DownloadFileAsync(selection.Asset, partial, progress, token);
                if (!await VerifySha256Async(partial, expected, token))
                {
                    TryDelete(partial); TryDelete(destination);
                    throw new UpdateOperationException("ChecksumMismatch", $"SHA-256 verification failed for '{fileName}'. The damaged file was deleted.");
                }
                File.Move(partial, destination, true);
                return destination;
            }
            catch (OperationCanceledException) { TryDelete(partial); throw; }
            catch (Exception ex) when (ex is InvalidDataException or UpdateOperationException) { TryDelete(partial); throw; }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                lastError = ex; TryDelete(partial);
                if (attempt < maximumAttempts) await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), token);
            }
        }
        throw new HttpRequestException($"Update download failed after {maximumAttempts} attempts.", lastError);
    }

    public static async Task<bool> VerifySha256Async(string path, string expectedHash, CancellationToken token = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
        return actual.Equals(expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string FindChecksum(string text, string fileName)
    {
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts[0].Length == 64 && parts[0].All(Uri.IsHexDigit) &&
                parts[1].TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase)) return parts[0];
        }
        throw new UpdateOperationException("ChecksumEntryMissing", $"checksums.txt does not contain a valid SHA-256 entry for '{fileName}'.");
    }

    private async Task<string> DownloadTextAsync(Uri uri, CancellationToken token)
    {
        using var request = CreateRequest(uri); using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode(); return await response.Content.ReadAsStringAsync(token);
    }

    private async Task DownloadFileAsync(ReleaseAsset asset, string path, IProgress<UpdateDownloadProgress>? progress, CancellationToken token)
    {
        using var request = CreateRequest(asset.DownloadUrl);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? asset.Size;
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = ArrayPool<byte>.Shared.Rent(81920); long downloaded = 0;
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token); if (read == 0) break;
                await output.WriteAsync(buffer.AsMemory(0, read), token); downloaded += read;
                progress?.Report(new UpdateDownloadProgress(asset.Name, downloaded, total));
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static HttpRequestMessage CreateRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri); request.Headers.UserAgent.ParseAdd("QuickResponseBao/1.0.0"); return request;
    }
    private static string SafeFileName(string name) => Path.GetFileName(name) == name && !string.IsNullOrWhiteSpace(name)
        ? name : throw new InvalidDataException("The release asset has an unsafe file name.");
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
