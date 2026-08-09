using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickResponseBao.Infrastructure.Windows;

public sealed record TopLevelWindowInfo(nint Handle, string ClassName, string Title, bool IsVisible, int Left, int Top, int Right, int Bottom)
{
    public override string ToString() =>
        $"HWND=0x{Handle:X}; class='{ClassName}'; title='{Title}'; visible={IsVisible}; rect={Left},{Top},{Right},{Bottom}";
}

public static class TopLevelWindowEnumerator
{
    public static IReadOnlyList<TopLevelWindowInfo> CurrentProcessWindows() => ForProcess(Environment.ProcessId);

    public static IReadOnlyList<TopLevelWindowInfo> ForProcess(int processId)
    {
        var result = new List<TopLevelWindowInfo>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner != processId) return true;
            var className = new StringBuilder(256); var title = new StringBuilder(512);
            GetClassName(window, className, className.Capacity); GetWindowText(window, title, title.Capacity);
            GetWindowRect(window, out var rect);
            result.Add(new TopLevelWindowInfo(window, className.ToString(), title.ToString(), IsWindowVisible(window),
                rect.Left, rect.Top, rect.Right, rect.Bottom));
            return true;
        }, 0);
        return result;
    }

    private delegate bool EnumWindowsProc(nint window, nint parameter);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out int processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder className, int maximumCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder title, int maximumCount);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
}
