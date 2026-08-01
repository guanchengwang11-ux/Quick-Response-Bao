using System.Text.Json;
using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Infrastructure.Diagnostics;

public sealed class SafeDiagnosticReportService
{
    public async Task ExportAsync(string path, DiagnosticSnapshot snapshot, string? applicationVersion, CancellationToken token = default)
    {
        var safeFailure = snapshot.LastFailureReason.ReplaceLineEndings(" "); safeFailure = safeFailure[..Math.Min(300, safeFailure.Length)];
        var report = new
        {
            snapshot.CapturedAt, ApplicationVersion = applicationVersion, OperatingSystem = Environment.OSVersion.VersionString,
            snapshot.ForegroundProcess, WindowTitleLength = snapshot.WindowTitle.Length, snapshot.IsWhitelisted, snapshot.HookRunning,
            snapshot.TextInputDetected, snapshot.SearchBufferLength, CandidatePosition = snapshot.CandidatePosition.ToString(),
            snapshot.LastPasteSucceeded, LastFailureReason = safeFailure,
            Privacy = "No keyboard buffer content, chat content, password, verification code, or clipboard content is included."
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), token);
    }
}
