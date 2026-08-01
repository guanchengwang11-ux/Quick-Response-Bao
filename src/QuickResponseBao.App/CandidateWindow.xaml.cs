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

    public CandidateWindow() { InitializeComponent(); }
    public event EventHandler<QuickResponse>? Confirmed;

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
                Background = i == _selected ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 240, 254)) : System.Windows.Media.Brushes.Transparent,
                BorderBrush = i == _selected ? (System.Windows.Media.Brush)FindResource("PrimaryBrush") : System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(i == _selected ? 1 : 0)
            };
            border.MouseEnter += (_, _) => { _selected = index; Rebuild(); };
            border.MouseLeftButtonUp += (_, _) => Confirmed?.Invoke(this, response);
            ItemsPanel.Children.Add(border);
        }
    }

    private TextBlock CreateHighlighted(string text, bool bold, double size = 13)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 1, 0, 3) };
        foreach (var part in _highlight.Split(text, _query)) block.Inlines.Add(new Run(part.Text) { Foreground = part.IsMatch ? (System.Windows.Media.Brush)FindResource("HighlightBrush") : (System.Windows.Media.Brush)FindResource("TextBrush"), FontWeight = part.IsMatch ? FontWeights.Bold : block.FontWeight });
        return block;
    }

    private void PositionNearCaret()
    {
        var point = NativeCaret.GetPosition();
        var source = PresentationSource.FromVisual(this);
        var scaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1;
        var scaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1;
        var area = SystemParameters.WorkArea;
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
        public static System.Windows.Point GetPosition()
        {
            var foreground = GetForegroundWindow(); GetWindowThreadProcessId(foreground, out _);
            var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
            if (GetGUIThreadInfo(0, ref info) && info.CaretWindow != 0)
            {
                var p = new NativePoint { X = info.CaretRect.Left, Y = info.CaretRect.Bottom };
                ClientToScreen(info.CaretWindow, ref p); return new System.Windows.Point(p.X, p.Y);
            }
            GetWindowRect(foreground, out var rect); return new System.Windows.Point(rect.Right - 540, rect.Bottom - 520);
        }
        [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo { public int Size; public uint Flags; public nint Active, Focus, Capture, MenuOwner, MoveSize, CaretWindow; public NativeRect CaretRect; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref NativePoint point);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect rect);
    }
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(nint window, int index, int value);
}
