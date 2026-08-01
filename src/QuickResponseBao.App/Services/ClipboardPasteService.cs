using System.Runtime.InteropServices;
using System.Windows;
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
        bool? restored = restore ? false : null;
        if (restore && original is not null)
        {
            try { System.Windows.Clipboard.SetDataObject(original, true); restored = true; }
            catch (ExternalException) { restored = false; }
        }
        else if (restore && !captured) restored = false;
        return new PasteOperationResult(true, restored, "Clipboard + SendInput Ctrl+V", target, sent);
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

    private static PasteSendResult SendPaste()
    {
        var inputs = PasteShortcutInput.Create(); var size = PasteShortcutInput.StructureSize;
        Marshal.SetLastPInvokeError(0);
        var count = SendInput((uint)inputs.Length, inputs, size);
        return new PasteSendResult(PasteShortcutInput.WasFullySent(count), count, Marshal.GetLastPInvokeError(), size);
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
}

public sealed record PasteSendResult(bool Success, uint SentCount, int ErrorCode, int InputSize);
public sealed record PasteOperationResult(bool PasteSent, bool? ClipboardRestored, string Method, PasteTargetInfo Target, PasteSendResult SendResult);

public sealed class PasteShortcutException(uint sentCount, int errorCode, int inputSize, PasteTargetInfo target)
    : Exception($"Paste shortcut send failed. sent={sentCount}/4, error={errorCode}, inputSize={inputSize}, target={target.ProcessName}, samePermission={target.SamePermissionLevel?.ToString() ?? "unknown"}")
{
    public uint SentCount { get; } = sentCount;
    public int ErrorCode { get; } = errorCode;
    public int InputSize { get; } = inputSize;
    public PasteTargetInfo Target { get; } = target;
}
