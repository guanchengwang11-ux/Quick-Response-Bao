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
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfInjected = 0x10;
    private const uint LlmhfInjected = 0x01;
    private const int WmLeftButtonDown = 0x0201;
    private readonly SearchPhraseBuffer _buffer = new(64);
    private readonly HookProc _callback;
    private readonly MouseHookProc _mouseCallback;
    private nint _hook;
    private nint _mouseHook;
    private nint _lastForegroundWindow;
    private nint _cachedWindow;
    private DateTime _cachedAt;
    private InputEnvironmentInfo? _cachedEnvironment;
    private AppSettings _settings;

    public GlobalKeyboardListener(AppSettings settings)
    {
        _settings = settings;
        _callback = HookCallback;
        _mouseCallback = MouseHookCallback;
    }

    public event EventHandler<CandidateSearchContext>? SearchTextChanged;
    public event EventHandler? SearchCancelled;
    public event EventHandler<NavigationKey>? NavigationRequested;
    public bool IsRunning => _hook != 0;
    public bool SuggestionsVisible { get; set; }
    public int BufferLength => _buffer.Length;
    public nint CandidateWindowHandle { get; set; }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void Start()
    {
        if (_hook != 0) return;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback,
            module is null ? 0 : GetModuleHandle(module.ModuleName), 0);
        if (_hook == 0) throw new InvalidOperationException($"Keyboard hook could not be installed ({Marshal.GetLastWin32Error()}).");
        _mouseHook = SetWindowsMouseHookEx(WhMouseLl, _mouseCallback,
            module is null ? 0 : GetModuleHandle(module.ModuleName), 0);
        if (_mouseHook == 0)
        {
            var error = Marshal.GetLastWin32Error(); UnhookWindowsHookEx(_hook); _hook = 0;
            throw new InvalidOperationException($"Mouse safety hook could not be installed ({error}).");
        }
    }

    public void Stop()
    {
        if (_hook != 0) UnhookWindowsHookEx(_hook);
        if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
        _hook = 0;
        _mouseHook = 0;
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
        return InspectEnvironmentCore(window);
    }

    private InputEnvironmentInfo InspectEnvironmentCached(nint window)
    {
        if (_cachedEnvironment is not null && window == _cachedWindow && DateTime.UtcNow - _cachedAt < TimeSpan.FromMilliseconds(750))
            return _cachedEnvironment;
        _cachedWindow = window; _cachedAt = DateTime.UtcNow; return _cachedEnvironment = InspectEnvironmentCore(window);
    }

    private InputEnvironmentInfo InspectEnvironmentCore(nint window)
    {
        if (window == 0 || window == GetShellWindow()) return InputEnvironmentInfo.Empty;
        var thread = GetWindowThreadProcessId(window, out var processId);
        var processName = GetProcessName(processId);
        var titleLength = GetWindowTextLength(window); var title = new StringBuilder(Math.Max(1, titleLength + 1)); GetWindowText(window, title, title.Capacity);
        var gui = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        var hasGuiInfo = GetGUIThreadInfo(thread, ref gui);
        var focusProcess = hasGuiInfo && gui.Focus != 0 ? GetWindowProcessName(gui.Focus) : string.Empty;
        var whitelisted = ProcessWhitelist.Contains(_settings.AllowedProcesses, processName) || ProcessWhitelist.Contains(_settings.AllowedProcesses, focusProcess);
        var assessment = AssessInput(gui.Focus, hasGuiInfo, processName, focusProcess);
        return new InputEnvironmentInfo(processName, focusProcess, title.ToString(), whitelisted,
            assessment.TextInputState, assessment.PasswordFieldDetected, assessment.UiAutomationUnavailable, assessment.SecureSystemProcess);
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code < 0 || (wParam != WmKeyDown && wParam != WmSysKeyDown))
            return CallNextHookEx(_hook, code, wParam, lParam);

        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        if (ShouldIgnoreInjectedKeyboard(data.flags)) return CallNextHookEx(_hook, code, wParam, lParam);

        var foreground = GetForegroundWindow();
        if (_lastForegroundWindow != 0 && foreground != _lastForegroundWindow && _buffer.Length > 0) Reset();
        _lastForegroundWindow = foreground;
        var environment = InspectEnvironmentCached(foreground);
        if (!ShouldMonitor(environment))
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
                _buffer.Backspace();
                Publish(foreground, environment);
                break;
            case VirtualKey.Space:
                if (_buffer.AppendSpace()) Publish(foreground, environment);
                break;
            default:
                if (TryGetLetter(data.vkCode, out var letter))
                {
                    _buffer.AppendLetter(letter);
                    Publish(foreground, environment);
                }
                else if (key is not VirtualKey.Shift and not VirtualKey.Control and not VirtualKey.Menu)
                {
                    if (_buffer.Length > 0) Reset();
                }
                break;
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void Publish(nint targetWindow, InputEnvironmentInfo environment)
    {
        if (_buffer.IsReady(_settings.MinimumTriggerLength))
        {
            GetWindowThreadProcessId(targetWindow, out var processId);
            SearchTextChanged?.Invoke(this, new CandidateSearchContext(_buffer.Value, _buffer.RawTypedCharacterCount,
                targetWindow, processId, environment.ProcessName, DateTimeOffset.UtcNow, _buffer.RawTypedText));
        }
        else SearchCancelled?.Invoke(this, EventArgs.Empty);
    }

    private nint MouseHookCallback(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && wParam == WmLeftButtonDown)
        {
            var data = Marshal.PtrToStructure<MouseLlHookStruct>(lParam);
            if ((data.Flags & LlmhfInjected) == 0)
            {
                var clickedRoot = GetAncestor(WindowFromPoint(data.Point), 2);
                if (clickedRoot != CandidateWindowHandle && _buffer.RawTypedCharacterCount > 0) Reset();
            }
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private static InputAssessment AssessInput(nint focus, bool hasGuiInfo, string topProcess, string focusProcess)
    {
        if (IsSecureSystemProcess(topProcess) || IsSecureSystemProcess(focusProcess))
            return new(TextInputDetectionState.Unknown, false, false, true);
        if (hasGuiInfo && focus != 0)
        {
            var className = new StringBuilder(128); GetClassName(focus, className, className.Capacity);
            var nativeEdit = className.ToString().Contains("Edit", StringComparison.OrdinalIgnoreCase) || className.ToString().Contains("Rich", StringComparison.OrdinalIgnoreCase);
            if (nativeEdit && (GetWindowLong(focus, -16) & 0x20) != 0)
                return new(TextInputDetectionState.NotDetected, true, false, false);
            if (nativeEdit) return new(TextInputDetectionState.Detected, false, false, false);
        }

        try
        {
            var task = Task.Run(() =>
            {
                var element = AutomationElement.FocusedElement;
                if (element is null) return new InputAssessment(TextInputDetectionState.Unknown, false, false, false);
                if (element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) is true)
                    return new InputAssessment(TextInputDetectionState.NotDetected, true, false, false);
                var detected = element.TryGetCurrentPattern(ValuePattern.Pattern, out _) || element.TryGetCurrentPattern(TextPattern.Pattern, out _) ||
                    element.Current.ControlType == ControlType.Edit || element.Current.ControlType == ControlType.Document;
                return new InputAssessment(detected ? TextInputDetectionState.Detected : TextInputDetectionState.NotDetected, false, false, false);
            });
            if (!task.Wait(150)) return new(TextInputDetectionState.Unknown, false, true, false);
            return task.Result;
        }
        catch { return new(TextInputDetectionState.Unknown, false, true, false); }
    }

    public static bool IsSecureSystemProcess(string processName) => processName is not null &&
        new[] { "LogonUI.exe", "winlogon.exe", "CredentialUIBroker.exe", "consent.exe" }
            .Contains(processName, StringComparer.OrdinalIgnoreCase);

    public static bool ShouldMonitor(InputEnvironmentInfo environment) => environment.IsWhitelisted &&
        !environment.PasswordFieldDetected && !environment.SecureSystemProcess;

    public static bool ShouldIgnoreInjectedKeyboard(uint flags) => (flags & LlkhfInjected) != 0;

    private static string GetWindowProcessName(nint window)
    {
        GetWindowThreadProcessId(window, out var processId); return GetProcessName(processId);
    }

    private static string GetProcessName(uint processId)
    {
        try { using var process = Process.GetProcessById((int)processId); return $"{process.ProcessName}.exe"; }
        catch { return string.Empty; }
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
            VirtualKey.Return => NavigationKey.ConfirmEnter,
            VirtualKey.Tab => NavigationKey.ConfirmTab,
            VirtualKey.Escape => NavigationKey.Cancel,
            _ => NavigationKey.None
        };
        return navigation != NavigationKey.None;
    }

    public void Dispose() { Stop(); GC.SuppressFinalize(this); }

    private delegate nint HookProc(int code, nint wParam, nint lParam);
    private delegate nint MouseHookProc(int code, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct { public uint vkCode, scanCode, flags, time; public nuint dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size; public uint Flags; public nint Active, Focus, Capture, MenuOwner, MoveSize, CaretWindow;
        public NativeRect CaretRect;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseLlHookStruct { public NativePoint Point; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    private enum VirtualKey : uint
    {
        Back = 0x08, Tab = 0x09, Return = 0x0D, Shift = 0x10, Control = 0x11, Menu = 0x12,
        Space = 0x20,
        Escape = 0x1B, PageUp = 0x21, PageDown = 0x22, Up = 0x26, Down = 0x28
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] private static extern nint SetWindowsMouseHookEx(int idHook, MouseHookProc callback, nint module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
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

public enum NavigationKey { None, Up, Down, PageUp, PageDown, ConfirmEnter, ConfirmTab, Cancel }
public sealed record InputEnvironmentInfo(string ProcessName, string FocusProcessName, string WindowTitle, bool IsWhitelisted,
    TextInputDetectionState TextInputState, bool PasswordFieldDetected, bool UiAutomationUnavailable, bool SecureSystemProcess)
{
    public bool TextInputDetected => TextInputState == TextInputDetectionState.Detected;
    public static InputEnvironmentInfo Empty { get; } = new(string.Empty, string.Empty, string.Empty, false,
        TextInputDetectionState.Unknown, false, true, false);
}
internal sealed record InputAssessment(TextInputDetectionState TextInputState, bool PasswordFieldDetected, bool UiAutomationUnavailable, bool SecureSystemProcess);
