using System.Runtime.InteropServices;
using System.Windows;

namespace QuickResponseBao.App.Services;

public sealed class ClipboardPasteService
{
    public async Task PasteAsync(string text, bool preserve, bool restore, int restoreDelayMs)
    {
        System.Windows.IDataObject? original = null;
        if (preserve)
        {
            try { original = CaptureClipboard(); }
            catch (ExternalException) { }
        }

        SetClipboardTextWithRetry(text);
        SendPaste();
        await Task.Delay(Math.Clamp(restoreDelayMs, 100, 5000));
        if (restore && original is not null)
        {
            try { System.Windows.Clipboard.SetDataObject(original, true); }
            catch (ExternalException) { }
        }
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

    private static void SendPaste()
    {
        var inputs = new[]
        {
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = 0x11 } } },
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = 0x56 } } },
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = 0x56, Flags = 2 } } },
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = 0x11, Flags = 2 } } }
        };
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            throw new InvalidOperationException($"Ctrl+V could not be sent ({Marshal.GetLastWin32Error()}).");
    }

    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, ScanCode; public uint Flags, Time; public nuint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
