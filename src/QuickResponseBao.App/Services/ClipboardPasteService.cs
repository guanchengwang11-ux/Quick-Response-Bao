using System.Runtime.InteropServices;
using System.Windows;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;
using QuickResponseBao.Infrastructure.Windows;

namespace QuickResponseBao.App.Services;

public sealed class ClipboardPasteService
{
    public async Task<PasteOperationResult> PasteAsync(string text, bool preserve, bool restore, int restoreDelayMs)
    {
        var target = PasteTargetInspector.Capture();
        System.Windows.IDataObject? original = null;
        var captured = false;
        if (preserve)
        {
            try { original = CaptureClipboard(); captured = true; }
            catch (ExternalException) { }
        }

        SetClipboardTextWithRetry(text);
        var sent = SendPaste();
        if (!sent.Success)
            throw new PasteShortcutException(sent.SentCount, sent.ErrorCode, sent.InputSize, target);
        await Task.Delay(PasteShortcutInput.RestoreDelay(restoreDelayMs));
        var restored = RestoreClipboard(restore, original, captured);
        return new PasteOperationResult(true, restored, "Clipboard + SendInput Ctrl+V", target, sent);
    }

    public async Task<ResponseInsertionResult> ReplaceAsync(
        CandidateConfirmationContext context,
        string text,
        bool preserve,
        bool restore,
        int restoreDelayMs,
        Func<bool> isSafeTarget)
    {
        var focus = await CandidateTargetWindow.ValidateAndRestoreAsync(context);
        if (!focus.IsValid || context.RawTypedCharacterCount <= 0 || context.RawTypedText.Length != context.RawTypedCharacterCount || !isSafeTarget())
            throw new ResponseReplacementException(new ResponseInsertionResult(focus, context.RawTypedCharacterCount,
                false, false, false, false, null, "Backspace + Clipboard + Ctrl+V", ReplacementFailure.ContextInvalid, null, null));

        System.Windows.IDataObject? original = null;
        var captured = false;
        if (preserve)
        {
            try { original = CaptureClipboard(); captured = true; }
            catch (ExternalException) { }
        }

        var target = PasteTargetInspector.Capture();
        PasteSendResult? pasteSend = null;
        Exception? clipboardError = null;
        var coordinator = new ReplacementTransactionCoordinator();
        var outcome = await coordinator.ExecuteAsync(context.RawTypedCharacterCount, context.RawTypedText,
            () => Task.FromResult(focus.IsValid && isSafeTarget()),
            async count =>
            {
                var result = NativeInputSender.Send(PasteShortcutInput.CreateBackspaces(count));
                if (result.Success) await Task.Delay(80);
                return result;
            },
            () =>
            {
                try
                {
                    SetClipboardTextWithRetry(text); pasteSend = SendPaste();
                    return Task.FromResult(new InputInjectionResult(PasteShortcutInput.EventCount, checked((int)pasteSend.SentCount), pasteSend.ErrorCode));
                }
                catch (Exception ex)
                {
                    clipboardError = ex; return Task.FromResult(new InputInjectionResult(PasteShortcutInput.EventCount, 0, Marshal.GetLastPInvokeError()));
                }
            },
            raw => Task.FromResult(NativeInputSender.Send(PasteShortcutInput.CreateUnicodeText(raw))));

        bool? restored = restore ? false : null;
        if (outcome.PasteSucceeded)
        {
            await Task.Delay(PasteShortcutInput.RestoreDelay(restoreDelayMs));
            restored = RestoreClipboard(restore, original, captured);
            var paste = new PasteOperationResult(true, restored, "Backspace + Clipboard + SendInput Ctrl+V", target, pasteSend!);
            return new ResponseInsertionResult(focus, context.RawTypedCharacterCount, true, true, false, false,
                restored, "Backspace + Clipboard + Ctrl+V", ReplacementFailure.None, target, paste);
        }

        if (outcome.Failure == ReplacementFailure.DeleteFailed)
            restored = restore ? true : null;
        else if (outcome.RollbackAttempted && outcome.RollbackSucceeded)
            restored = RestoreClipboard(restore, original, captured);
        var failed = new ResponseInsertionResult(focus, context.RawTypedCharacterCount, outcome.DeletionSucceeded,
            false, outcome.RollbackAttempted, outcome.RollbackSucceeded, restored, "Backspace + Clipboard + Ctrl+V", outcome.Failure, target, null);
        throw new ResponseReplacementException(failed, clipboardError);
    }

    private static System.Windows.IDataObject? CaptureClipboard()
    {
        var source = System.Windows.Clipboard.GetDataObject();
        if (source is null) return null;
        var snapshot = new System.Windows.DataObject();
        foreach (var format in source.GetFormats())
        {
            try
            {
                var data = source.GetData(format, true);
                if (data is not null) snapshot.SetData(format, data);
            }
            catch (ExternalException) { }
            catch (NotSupportedException) { }
        }
        return snapshot;
    }

    private static void SetClipboardTextWithRetry(string text)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { System.Windows.Clipboard.SetText(text, System.Windows.TextDataFormat.UnicodeText); return; }
            catch (ExternalException) when (attempt < 4) { Thread.Sleep(30 * (attempt + 1)); }
        }
    }

    private static bool? RestoreClipboard(bool restore, System.Windows.IDataObject? original, bool captured)
    {
        if (!restore) return null;
        if (original is null)
        {
            if (!captured) return false;
            try { System.Windows.Clipboard.Clear(); return true; }
            catch (ExternalException) { return false; }
        }
        try { System.Windows.Clipboard.SetDataObject(original, true); return true; }
        catch (ExternalException) { return false; }
    }

    private static PasteSendResult SendPaste()
    {
        var result = NativeInputSender.Send(PasteShortcutInput.Create());
        return new PasteSendResult(result.Success, checked((uint)result.SentCount), result.ErrorCode, PasteShortcutInput.StructureSize);
    }
}

public sealed record PasteSendResult(bool Success, uint SentCount, int ErrorCode, int InputSize);
public sealed record PasteOperationResult(bool PasteSent, bool? ClipboardRestored, string Method, PasteTargetInfo Target, PasteSendResult SendResult);
public sealed record ResponseInsertionResult(
    CandidateTargetFocusResult Focus,
    int DeletedCharacterCount,
    bool DeletionSucceeded,
    bool PasteSucceeded,
    bool RollbackAttempted,
    bool RollbackSucceeded,
    bool? ClipboardRestored,
    string ReplacementMethod,
    ReplacementFailure Failure,
    PasteTargetInfo? Target,
    PasteOperationResult? Paste);

public sealed class ResponseReplacementException(ResponseInsertionResult result, Exception? inner = null)
    : Exception($"Response replacement failed. reason={result.Failure}, deleted={result.DeletionSucceeded}, paste={result.PasteSucceeded}, rollback={result.RollbackAttempted}/{result.RollbackSucceeded}, focus={result.Focus.FocusRestored}", inner)
{
    public ResponseInsertionResult Result { get; } = result;
}

public sealed class PasteShortcutException(uint sentCount, int errorCode, int inputSize, PasteTargetInfo target)
    : Exception($"Paste shortcut send failed. sent={sentCount}/4, error={errorCode}, inputSize={inputSize}, target={target.ProcessName}, samePermission={target.SamePermissionLevel?.ToString() ?? "unknown"}")
{
    public uint SentCount { get; } = sentCount;
    public int ErrorCode { get; } = errorCode;
    public int InputSize { get; } = inputSize;
    public PasteTargetInfo Target { get; } = target;
}
