using QuickResponseBao.Core.Models;
using QuickResponseBao.Infrastructure.Diagnostics;

namespace QuickResponseBao.UnitTests;

public sealed class DiagnosticReportTests
{
    [Fact]
    public async Task Export_OmitsWindowTitleAndNeverContainsInputOrClipboardContent()
    {
        var secretTitle = "Private chat with verification code 123456"; var path = Path.Combine(Path.GetTempPath(), $"qrb-diagnostic-{Guid.NewGuid():N}.json");
        try
        {
            var snapshot = new DiagnosticSnapshot(DateTimeOffset.UtcNow, "Lark.exe", secretTitle, true, true, true, 12, CandidatePositionMethod.Caret, true, string.Empty);
            await new SafeDiagnosticReportService().ExportAsync(path, snapshot, "1.0.0"); var text = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain(secretTitle, text); Assert.Contains("WindowTitleLength", text); Assert.Contains("SearchBufferLength", text);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
