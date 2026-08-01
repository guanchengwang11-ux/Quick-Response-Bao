namespace QuickResponseBao.Core.Models;

public enum CandidatePositionMethod { Caret, WindowBottomRight, CurrentMonitorBottomRight }
public enum TextInputDetectionState { NotDetected, Detected, Unknown }

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
    string FocusProcess,
    string WindowTitle,
    bool IsWhitelisted,
    bool HookRunning,
    bool TextInputDetected,
    bool TextInputUnknown,
    bool PasswordFieldDetected,
    bool UiAutomationUnavailable,
    int SearchBufferLength,
    CandidatePositionMethod CandidatePosition,
    bool? LastPasteSucceeded,
    bool? LastClipboardRestored,
    uint? LastPasteSentCount,
    int? LastPasteErrorCode,
    int? LastPasteInputSize,
    string LastPasteTargetProcess,
    bool? LastPasteSamePermissionLevel,
    string LastFailureReason,
    string LogDirectory);
