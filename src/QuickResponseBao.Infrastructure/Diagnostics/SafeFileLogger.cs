using QuickResponseBao.Infrastructure.Storage;

namespace QuickResponseBao.Infrastructure.Diagnostics;

public sealed class SafeFileLogger(AppPaths paths)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task WriteAsync(string eventName, Exception? exception = null)
    {
        await _gate.WaitAsync();
        try
        {
            var file = Path.Combine(paths.Logs, $"quick-response-bao-{DateTime.UtcNow:yyyyMMdd}.log");
            var safeError = exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message.ReplaceLineEndings(" ")}";
            await File.AppendAllTextAsync(file, $"{DateTimeOffset.Now:O} | {eventName}{safeError}{Environment.NewLine}");
        }
        finally { _gate.Release(); }
    }
}
