using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.Infrastructure.Windows;

public sealed class GlobalKeyboardListener : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfInjected = 0x10;
    private readonly StringBuilder _buffer = new(64);
    private readonly HookProc _callback;
    private nint _hook;
    private AppSettings _settings;

    public GlobalKeyboardListener(AppSettings settings)
    {
        _settings = settings;
        _callback = HookCallback;
    }

    public event EventHandler<string>? SearchTextChanged;
    public event EventHandler? SearchCancelled;
    public event EventHandler<NavigationKey>? NavigationRequested;
    public bool IsRunning => _hook != 0;
    public bool SuggestionsVisible { get; set; }
    public int BufferLength => _buffer.Length;

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void Start()
    {
        if (_hook != 0) return;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback,
            module is null ? 0 : GetModuleHandle(module.ModuleName), 0);
        if (_hook == 0) throw new InvalidOperationException($"Keyboard hook could not be installed ({Marshal.GetLastWin32Error()}).");
    }

    public void Stop()
    {
        if (_hook != 0) UnhookWindowsHookEx(_hook);
        _hook = 0;
        Reset();
    }

    public void Reset()
    {
        _buffer.Clear();
        SearchCancelled?.Invoke(this, EventArgs.Empty);
    }

    public InputEnvironmentInfo InspectEnvironment()
    {
        var window = GetForegroundWindow();
        if (window == 0 || window == GetShellWindow()) return new InputEnvironmentInfo(string.Empty, string.Empty, false, false);
        GetWindowThreadProcessId(window, out var processId);
        var processName = string.Empty;
        try { using var process = Process.GetProcessById((int)processId); processName = $"{process.ProcessName}.exe"; } catch { }
        var titleLength = GetWindowTextLength(window); var title = new StringBuilder(Math.Max(1, titleLength + 1)); GetWindowText(window, title, title.Capacity);
        var whitelisted = ProcessWhitelist.Contains(_settings.AllowedProcesses, processName);
        return new InputEnvironmentInfo(processName, title.ToString(), whitelisted, DetectTextInputEnvironment(window));
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code < 0 || (wParam != WmKeyDown && wParam != WmSysKeyDown))
            return CallNextHookEx(_hook, code, wParam, lParam);

        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        if ((data.flags & LlkhfInjected) != 0) return CallNextHookEx(_hook, code, wParam, lParam);

        if (!IsAllowedForegroundProcess() || IsSecureInput())
        {
            if (_buffer.Length > 0) Reset();
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        var key = (VirtualKey)data.vkCode;
        if (SuggestionsVisible && TryNavigation(key, out var navigation))
        {
            NavigationRequested?.Invoke(this, navigation);
            return 1;
        }

        switch (key)
        {
            case VirtualKey.Escape:
                Reset();
                break;
            case VirtualKey.Back:
                if (_buffer.Length > 0) _buffer.Length--;
                Publish();
                break;
            default:
                if (TryGetLetter(data.vkCode, out var letter))
                {
                    if (_buffer.Length == 64) _buffer.Remove(0, 1);
                    _buffer.Append(letter);
                    Publish();
                }
                else if (key is not VirtualKey.Shift and not VirtualKey.Control and not VirtualKey.Menu)
                {
                    if (_buffer.Length > 0) Reset();
                }
                break;
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void Publish()
    {
        if (_buffer.Length >= _settings.MinimumTriggerLength)
            SearchTextChanged?.Invoke(this, _buffer.ToString());
        else SearchCancelled?.Invoke(this, EventArgs.Empty);
    }

    private bool IsAllowedForegroundProcess()
    {
        var window = GetForegroundWindow();
        if (window == 0 || window == GetShellWindow()) return false;
        GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var name = $"{process.ProcessName}.exe";
            return ProcessWhitelist.Contains(_settings.AllowedProcesses, name);
        }
        catch { return false; }
    }

    private static bool IsSecureInput()
    {
        var foreground = GetForegroundWindow();
        var thread = GetWindowThreadProcessId(foreground, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(thread, ref info) || info.Focus == 0) return true;
        if ((GetWindowLong(info.Focus, -16) & 0x20) != 0) return true;
        try
        {
            var element = AutomationElement.FocusedElement;
            return element?.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is true;
        }
        catch { return true; }
    }

    private static bool DetectTextInputEnvironment(nint foreground)
    {
        var thread = GetWindowThreadProcessId(foreground, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(thread, ref info) || info.Focus == 0 || (GetWindowLong(info.Focus, -16) & 0x20) != 0) return false;
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element?.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is true) return false;
            if (element is not null && (element.TryGetCurrentPattern(ValuePattern.Pattern, out _) || element.TryGetCurrentPattern(TextPattern.Pattern, out _))) return true;
            var type = element?.Current.ControlType; if (type == ControlType.Edit || type == ControlType.Document) return true;
        }
        catch { }
        var className = new StringBuilder(128); GetClassName(info.Focus, className, className.Capacity);
        return className.ToString().Contains("Edit", StringComparison.OrdinalIgnoreCase) || className.ToString().Contains("Rich", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetLetter(uint virtualKey, out char letter)
    {
        letter = default;
        if (virtualKey < 0x41 || virtualKey > 0x5A) return false;
        var shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
        var caps = (GetKeyState(0x14) & 1) != 0;
        letter = (char)(virtualKey + (shift ^ caps ? 0 : 32));
        return true;
    }

    private static bool TryNavigation(VirtualKey key, out NavigationKey navigation)
    {
        navigation = key switch
        {
            VirtualKey.Up => NavigationKey.Up,
            VirtualKey.Down => NavigationKey.Down,
            VirtualKey.PageUp => NavigationKey.PageUp,
            VirtualKey.PageDown => NavigationKey.PageDown,
            VirtualKey.Return => NavigationKey.Confirm,
            VirtualKey.Tab => NavigationKey.Confirm,
            VirtualKey.Escape => NavigationKey.Cancel,
            _ => NavigationKey.None
        };
        return navigation != NavigationKey.None;
    }

    public void Dispose() { Stop(); GC.SuppressFinalize(this); }

    private delegate nint HookProc(int code, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct { public uint vkCode, scanCode, flags, time; public nuint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size; public uint Flags; public nint Active, Focus, Capture, MenuOwner, MoveSize, CaretWindow;
        public NativeRect CaretRect;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private enum VirtualKey : uint
    {
        Back = 0x08, Tab = 0x09, Return = 0x0D, Shift = 0x10, Control = 0x11, Menu = 0x12,
        Escape = 0x1B, PageUp = 0x21, PageDown = 0x22, Up = 0x26, Down = 0x28
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int maximumCount);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder className, int maximumCount);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? moduleName);
}

public enum NavigationKey { None, Up, Down, PageUp, PageDown, Confirm, Cancel }
public sealed record InputEnvironmentInfo(string ProcessName, string WindowTitle, bool IsWhitelisted, bool TextInputDetected);
