using System.Text.Json;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Infrastructure.Storage;

public sealed class JsonSettingsStore(AppPaths paths)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(paths.Settings)) return new AppSettings();
            await using var stream = File.OpenRead(paths.Settings);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken)
                ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            var invalid = $"{paths.Settings}.invalid-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(paths.Settings, invalid, true);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        var temporary = $"{paths.Settings}.tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
        File.Move(temporary, paths.Settings, true);
    }
}
