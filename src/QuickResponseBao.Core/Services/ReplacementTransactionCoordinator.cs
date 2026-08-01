namespace QuickResponseBao.Core.Services;

public enum ReplacementFailure { None, ContextInvalid, DeleteFailed, PasteFailed }
public sealed record InputInjectionResult(int ExpectedCount, int SentCount, int ErrorCode)
{
    public bool Success => ExpectedCount > 0 && SentCount == ExpectedCount;
}
public sealed record ReplacementTransactionResult(
    bool ContextValid,
    bool DeletionSucceeded,
    bool PasteSucceeded,
    bool RollbackAttempted,
    bool RollbackSucceeded,
    ReplacementFailure Failure);

public sealed class ReplacementTransactionCoordinator
{
    public async Task<ReplacementTransactionResult> ExecuteAsync(
        int rawTypedCharacterCount,
        string rawTypedText,
        Func<Task<bool>> validateContext,
        Func<int, Task<InputInjectionResult>> delete,
        Func<Task<InputInjectionResult>> paste,
        Func<string, Task<InputInjectionResult>> restore)
    {
        if (rawTypedCharacterCount <= 0 || rawTypedText.Length != rawTypedCharacterCount || !await validateContext())
            return new(false, false, false, false, false, ReplacementFailure.ContextInvalid);

        var deletion = await delete(rawTypedCharacterCount);
        if (!deletion.Success)
        {
            var deletedCharacters = Math.Clamp((deletion.SentCount + 1) / 2, 0, rawTypedCharacterCount);
            var rollback = deletedCharacters == 0 ? null : await restore(rawTypedText[^deletedCharacters..]);
            return new(true, false, false, rollback is not null, rollback?.Success == true, ReplacementFailure.DeleteFailed);
        }

        var pasted = await paste();
        if (pasted.Success)
            return new(true, true, true, false, false, ReplacementFailure.None);

        var fullRollback = await restore(rawTypedText);
        return new(true, true, false, true, fullRollback.Success, ReplacementFailure.PasteFailed);
    }
}

public static class CandidateTargetPolicy
{
    public static bool IsSameTarget(nint capturedWindow, uint capturedProcessId, nint currentWindow, uint currentProcessId) =>
        capturedWindow != 0 && capturedProcessId != 0 && capturedWindow == currentWindow && capturedProcessId == currentProcessId;
}

public enum ResponseInsertionMode { Insert, ReplaceTypedSearchText }
public static class ResponseInsertionModePolicy
{
    public static ResponseInsertionMode Resolve(bool replaceTypedSearchText) =>
        replaceTypedSearchText ? ResponseInsertionMode.ReplaceTypedSearchText : ResponseInsertionMode.Insert;
}
