using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuickResponseBao.Infrastructure.Windows;

public static class PasteShortcutInput
{
    public const uint KeyboardType = 1;
    public const ushort ControlKey = 0x11;
    public const ushort VKey = 0x56;
    public const uint KeyUp = 0x0002;
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

    private static NativeInput Keyboard(ushort key, uint flags) => new()
    {
        Type = KeyboardType,
        Data = new NativeInputUnion { Keyboard = new NativeKeyboardInput { VirtualKey = key, Flags = flags } }
    };
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
