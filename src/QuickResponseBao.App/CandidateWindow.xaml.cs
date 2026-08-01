using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using QuickResponseBao.Core.Models;
using QuickResponseBao.Core.Services;

namespace QuickResponseBao.App;

public partial class CandidateWindow : Window
{
    private readonly TextHighlightService _highlight = new();
    private IReadOnlyList<SearchResult> _results = [];
    private int _selected;
    private string _query = string.Empty;
    public CandidatePositionMethod LastPositionMethod { get; private set; } = CandidatePositionMethod.CurrentMonitorBottomRight;

    public CandidateWindow() { InitializeComponent(); }
    public event EventHandler<QuickResponse>? Confirmed;
    public event EventHandler<CandidatePositionMethod>? PositionMethodChanged;

    public void ShowResults(string query, IReadOnlyList<SearchResult> results)
    {
        _query = query; _results = results; _selected = 0;
        Rebuild(); PositionNearCaret();
        if (!IsVisible) Show();
    }

    public void Navigate(Infrastructure.Windows.NavigationKey key)
    {
        if (_results.Count == 0) return;
        if (key == Infrastructure.Windows.NavigationKey.Cancel) { Hide(); return; }
        if (key == Infrastructure.Windows.NavigationKey.Confirm) { Confirmed?.Invoke(this, _results[_selected].Response); return; }
        var change = key switch
        {
            Infrastructure.Windows.NavigationKey.Up => -1,
            Infrastructure.Windows.NavigationKey.Down => 1,
            Infrastructure.Windows.NavigationKey.PageUp => -5,
            Infrastructure.Windows.NavigationKey.PageDown => 5,
            _ => 0
        };
        _selected = Math.Clamp(_selected + change, 0, _results.Count - 1); Rebuild();
    }

    private void Rebuild()
    {
        ItemsPanel.Children.Clear();
        for (var i = 0; i < _results.Count; i++)
        {
            var index = i; var response = _results[i].Response;
            var stack = new StackPanel();
            stack.Children.Add(CreateHighlighted(response.Summary, true));
            stack.Children.Add(CreateHighlighted(response.Content, false));
            if (response.Keywords.Count > 0) stack.Children.Add(CreateHighlighted(string.Join(" · ", response.Keywords), false, 11));
            var border = new Border
            {
                Child = stack, Padding = new Thickness(12, 9, 12, 9), Margin = new Thickness(2), CornerRadius = new CornerRadius(5),
                Background = System.Windows.Media.Brushes.Transparent, BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(i == _selected ? 1 : 0)
            };
            if (i == _selected) { border.SetResourceReference(Border.BackgroundProperty, "SelectionBrush"); border.SetResourceReference(Border.BorderBrushProperty, "PrimaryBrush"); }
            border.MouseEnter += (_, _) => { _selected = index; Rebuild(); };
            border.MouseLeftButtonUp += (_, _) => Confirmed?.Invoke(this, response);
            ItemsPanel.Children.Add(border);
        }
    }

    private TextBlock CreateHighlighted(string text, bool bold, double size = 13)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 1, 0, 3) };
        foreach (var part in _highlight.Split(text, _query))
        {
            var run = new Run(part.Text) { FontWeight = part.IsMatch ? FontWeights.Bold : block.FontWeight };
            run.SetResourceReference(TextElement.ForegroundProperty, part.IsMatch ? "HighlightBrush" : "TextBrush"); block.Inlines.Add(run);
        }
        return block;
    }

    private void PositionNearCaret()
    {
        var position = NativeCaret.GetPosition(); LastPositionMethod = position.Method; PositionMethodChanged?.Invoke(this, position.Method); var point = position.Point;
        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1;
        var area = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = Math.Clamp(point.X * scaleX, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp((point.Y + 24) * scaleY, area.Top, Math.Max(area.Top, area.Bottom - 500));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e); var handle = new WindowInteropHelper(this).Handle;
        SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x00000080);
    }

    private static class NativeCaret
    {
        public static (System.Windows.Point Point, CandidatePositionMethod Method) GetPosition()
        {
            var foreground = GetForegroundWindow(); GetWindowThreadProcessId(foreground, out _);
            var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
            if (GetGUIThreadInfo(0, ref info) && info.CaretWindow != 0)
            {
                var p = new NativePoint { X = info.CaretRect.Left, Y = info.CaretRect.Bottom };
                ClientToScreen(info.CaretWindow, ref p); return (new System.Windows.Point(p.X, p.Y), CandidatePositionMethod.Caret);
            }
            if (foreground != 0 && GetWindowRect(foreground, out var rect) && rect.Right > rect.Left && rect.Bottom > rect.Top)
                return (new System.Windows.Point(rect.Right - 540, rect.Bottom - 520), CandidatePositionMethod.WindowBottomRight);
            var monitor = MonitorFromWindow(foreground, 2);
            var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor != 0 && GetMonitorInfo(monitor, ref monitorInfo))
                return (new System.Windows.Point(monitorInfo.WorkArea.Right - 540, monitorInfo.WorkArea.Bottom - 520), CandidatePositionMethod.CurrentMonitorBottomRight);
            return (new System.Windows.Point(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 540,
                SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 520), CandidatePositionMethod.CurrentMonitorBottomRight);
        }
        [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo { public int Size; public uint Flags; public nint Active, Focus, Capture, MenuOwner, MoveSize, CaretWindow; public NativeRect CaretRect; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, WorkArea; public uint Flags; }
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref NativePoint point);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
        [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    }
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(nint window, int index, int value);
}
