namespace QuickResponseBao.App.Services;

public sealed record LibraryVisualMetrics(int ItemCount, int RealizedRowCount, double ActualHeight, double ViewportHeight,
    double ExtentHeight, double ScrollableHeight, double VerticalOffset);
