namespace QuickResponseBao.Core.Models;

public enum CandidatePositionMethod { Caret, WindowBottomRight, ScreenBottomRight }

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
    string LastFailureReason);
