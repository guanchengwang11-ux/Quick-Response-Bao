using System.Diagnostics;
using System.Windows.Threading;

namespace QuickResponseBao.App.Services;

public sealed record UiStallSnapshot(double P50Milliseconds, double P95Milliseconds, double MaximumMilliseconds, int SampleCount,
    int Over16Milliseconds, int Over33Milliseconds, int Over50Milliseconds, int Over100Milliseconds, int Over250Milliseconds);

public sealed class UiResponsivenessMonitor : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(16);
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<double> _stalls = [];
    private double _expected = Interval.TotalMilliseconds;

    public UiResponsivenessMonitor(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = Interval };
        _timer.Tick += Tick; _timer.Start();
    }

    public event EventHandler<double>? SignificantStall;
    public UiStallSnapshot Snapshot() { var values = _stalls.OrderBy(x => x).ToArray(); return new(Percentile(values, .5), Percentile(values, .95), values.LastOrDefault(), values.Length, Count(16), Count(33), Count(50), Count(100), Count(250)); }
    private void Tick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalMilliseconds; var stall = Math.Max(0, now - _expected); _expected = now + Interval.TotalMilliseconds;
        if (stall < 1) return; _stalls.Add(stall); if (_stalls.Count > 10_000) _stalls.RemoveRange(0, 1_000); if (stall >= 50) SignificantStall?.Invoke(this, stall);
    }
    private static double Percentile(double[] values, double value) => values.Length == 0 ? 0 : values[(int)Math.Clamp(Math.Ceiling(values.Length * value) - 1, 0, values.Length - 1)];
    private int Count(double threshold) => _stalls.Count(value => value > threshold);
    public void Dispose() { _timer.Stop(); GC.SuppressFinalize(this); }
}
