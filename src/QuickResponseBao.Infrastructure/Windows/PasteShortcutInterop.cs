using System.Diagnostics;
using System.Runtime.InteropServices;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.Infrastructure.Windows;

public static class PasteShortcutInput
{
    public const uint KeyboardType = 1;
    public const ushort ControlKey = 0x11;
    public const ushort VKey = 0x56;
    public const uint KeyUp = 0x0002;
    public const uint Unicode = 0x0004;
    public const ushort BackspaceKey = 0x08;
    public const int EventCount = 4;

    public static NativeInput[] Create() =>
    [
        Keyboard(ControlKey, 0), Keyboard(VKey, 0),
        Keyboard(VKey, KeyUp), Keyboard(ControlKey, KeyUp)
    ];

    public static bool WasFullySent(uint sentCount) => sentCount == EventCount;
    public static int StructureSize => Marshal.SizeOf<NativeInput>();
    public static int ExpectedStructureSize => Environment.Is64BitProcess ? 40 : 28;
    public static TimeSpan RestoreDelay(int milliseconds) => TimeSpan.FromMilliseconds(Math.Clamp(milliseconds, 100, 5000));

    public static NativeInput[] CreateBackspaces(int characterCount)
    {
        if (characterCount <= 0) return [];
        var inputs = new NativeInput[characterCount * 2];
        for (var index = 0; index < characterCount; index++)
        {
            inputs[index * 2] = Keyboard(BackspaceKey, 0);
            inputs[index * 2 + 1] = Keyboard(BackspaceKey, KeyUp);
        }
        return inputs;
    }

    public static NativeInput[] CreateUnicodeText(string text)
    {
        var inputs = new NativeInput[text.Length * 2];
        for (var index = 0; index < text.Length; index++)
        {
            inputs[index * 2] = UnicodeKeyboard(text[index], Unicode);
            inputs[index * 2 + 1] = UnicodeKeyboard(text[index], Unicode | KeyUp);
        }
        return inputs;
    }

    private static NativeInput Keyboard(ushort key, uint flags) => new()
    {
        Type = KeyboardType,
        Data = new NativeInputUnion { Keyboard = new NativeKeyboardInput { VirtualKey = key, Flags = flags } }
    };

    private static NativeInput UnicodeKeyboard(char character, uint flags) => new()
    {
        Type = KeyboardType,
        Data = new NativeInputUnion { Keyboard = new NativeKeyboardInput { ScanCode = character, Flags = flags } }
    };
}

public static class NativeInputSender
{
    public static InputInjectionResult Send(NativeInput[] inputs)
    {
        if (inputs.Length == 0) return new(0, 0, 0);
        Marshal.SetLastPInvokeError(0);
        var sent = SendInput((uint)inputs.Length, inputs, PasteShortcutInput.StructureSize);
        return new(inputs.Length, checked((int)sent), Marshal.GetLastPInvokeError());
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
}

public sealed record CandidateTargetFocusResult(
    bool IsValid,
    nint CapturedWindowHandle,
    nint ConfirmationWindowHandle,
    bool FocusRestored,
    string FailureReason);

public static class CandidateTargetWindow
{
    public static async Task<CandidateTargetFocusResult> ValidateAndRestoreAsync(CandidateConfirmationContext context)
    {
        var confirmationWindow = GetForegroundWindow();
        if (context.TargetWindowHandle == 0 || !IsWindow(context.TargetWindowHandle))
            return new(false, context.TargetWindowHandle, confirmationWindow, false, "TargetWindowClosed");
        GetWindowThreadProcessId(context.TargetWindowHandle, out var processId);
        if (processId != context.TargetProcessId || !SameProcessName(processId, context.TargetProcessName))
            return new(false, context.TargetWindowHandle, confirmationWindow, false, "TargetProcessChanged");

        var restored = confirmationWindow == context.TargetWindowHandle || TryRestore(context.TargetWindowHandle);
        if (confirmationWindow != context.TargetWindowHandle) await Task.Delay(120);
        var currentWindow = GetForegroundWindow(); GetWindowThreadProcessId(currentWindow, out var currentProcessId);
        var valid = restored && CandidateTargetPolicy.IsSameTarget(context.TargetWindowHandle, context.TargetProcessId, currentWindow, currentProcessId);
        return new(valid, context.TargetWindowHandle, confirmationWindow, valid, valid ? string.Empty : "TargetFocusRestoreFailed");
    }

    private static bool SameProcessName(uint processId, string expected)
    {
        try { using var process = Process.GetProcessById((int)processId); return string.Equals($"{process.ProcessName}.exe", expected, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static bool TryRestore(nint target)
    {
        var currentThread = GetCurrentThreadId();
        var foreground = GetForegroundWindow(); var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var targetThread = GetWindowThreadProcessId(target, out _);
        var attachedForeground = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
        var attachedTarget = targetThread != 0 && targetThread != currentThread && AttachThreadInput(currentThread, targetThread, true);
        try { return SetForegroundWindow(target) || GetForegroundWindow() == target; }
        finally
        {
            if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
            if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeInput
{
    public uint Type;
    public NativeInputUnion Data;
}

[StructLayout(LayoutKind.Explicit)]
public struct NativeInputUnion
{
    [FieldOffset(0)] public NativeMouseInput Mouse;
    [FieldOffset(0)] public NativeKeyboardInput Keyboard;
    [FieldOffset(0)] public NativeHardwareInput Hardware;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeMouseInput
{
    public int X;
    public int Y;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeKeyboardInput
{
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeHardwareInput
{
    public uint Message;
    public ushort ParameterLow;
    public ushort ParameterHigh;
}

public sealed record PasteTargetInfo(string ProcessName, bool? CurrentProcessElevated, bool? TargetProcessElevated)
{
    public bool ElevationMismatch => CurrentProcessElevated == false && TargetProcessElevated == true;
    public bool? SamePermissionLevel => CurrentProcessElevated is null || TargetProcessElevated is null
        ? null : CurrentProcessElevated == TargetProcessElevated;
}

public static class PasteTargetInspector
{
    public static PasteTargetInfo Capture()
    {
        var targetName = string.Empty; bool? targetElevated = null;
        var window = GetForegroundWindow(); GetWindowThreadProcessId(window, out var targetId);
        try
        {
            using var target = Process.GetProcessById((int)targetId);
            targetName = $"{target.ProcessName}.exe"; targetElevated = IsElevated(target);
        }
        catch { }
        using var current = Process.GetCurrentProcess();
        return new PasteTargetInfo(targetName, IsElevated(current), targetElevated);
    }

    private static bool? IsElevated(Process process)
    {
        try
        {
            if (!OpenProcessToken(process.Handle, 0x0008, out var token)) return null;
            try
            {
                var elevation = new TokenElevation();
                if (!GetTokenInformation(token, 20, ref elevation, Marshal.SizeOf<TokenElevation>(), out _)) return null;
                return elevation.TokenIsElevated != 0;
            }
            finally { CloseHandle(token); }
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)] private struct TokenElevation { public int TokenIsElevated; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(nint token, int informationClass, ref TokenElevation information, int length, out int returnLength);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
}
