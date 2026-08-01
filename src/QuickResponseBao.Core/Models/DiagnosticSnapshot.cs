namespace QuickResponseBao.Core.Models;

public enum CandidatePositionMethod { Caret, WindowBottomRight, CurrentMonitorBottomRight }

public static class CandidatePositionFallback
{
    public static CandidatePositionMethod Resolve(bool caretAvailable, bool foregroundWindowAvailable) =>
        caretAvailable ? CandidatePositionMethod.Caret :
        foregroundWindowAvailable ? CandidatePositionMethod.WindowBottomRight :
        CandidatePositionMethod.CurrentMonitorBottomRight;
}

public sealed record DiagnosticSnapshot(
    DateTimeOffset CapturedAt,
    string ForegroundProcess,
    string WindowTitle,
    bool IsWhitelisted,
    bool HookRunning,
    bool TextInputDetected,
    int SearchBufferLength,
    CandidatePositionMethod CandidatePosition,
    bool? LastPasteSucceeded,
    bool? LastClipboardRestored,
    string LastFailureReason,
    string LogDirectory);
