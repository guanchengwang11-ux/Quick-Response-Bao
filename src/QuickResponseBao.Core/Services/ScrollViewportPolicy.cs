namespace QuickResponseBao.Core.Services;

public static class ScrollViewportPolicy
{
    public const double DefaultWheelStep = 48;

    public static bool RequiresScrolling(double extentHeight, double viewportHeight) =>
        extentHeight > viewportHeight && viewportHeight > 0;

    public static double WheelOffset(double currentOffset, int wheelDelta, double scrollableHeight, double step = DefaultWheelStep)
    {
        if (scrollableHeight <= 0) return 0;
        var direction = wheelDelta > 0 ? -1 : wheelDelta < 0 ? 1 : 0;
        return Math.Clamp(currentOffset + direction * step, 0, scrollableHeight);
    }
}
