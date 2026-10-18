using System;

namespace BlueSage.QoLTweaks.Core;

internal sealed class PerformanceOverlaySettings
{
    public PerformanceOverlaySettings(double refreshSeconds, int fontSize, double offsetX, double offsetY, double width)
    {
        RefreshSeconds = refreshSeconds;
        FontSize = fontSize;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Width = width;
    }

    public double RefreshSeconds { get; }
    public int FontSize { get; }
    public double OffsetX { get; }
    public double OffsetY { get; }
    public double Width { get; }
}

internal static class PerformanceOverlaySettingsPolicy
{
    public static PerformanceOverlaySettings Normalize(double refreshSeconds, int fontSize, double offsetX, double offsetY, double width)
    {
        return new PerformanceOverlaySettings(
            ClampFinite(refreshSeconds, 0.25d, 5d, 0.25d),
            fontSize <= 0 ? 13 : Math.Max(8, Math.Min(32, fontSize)),
            ClampFinite(offsetX, -2048d, 2048d, 12d),
            ClampFinite(offsetY, -2048d, 2048d, 174d),
            ClampFinite(width, 320d, 1024d, 320d));
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback)
    {
        return double.IsNaN(value) || double.IsInfinity(value)
            ? fallback
            : Math.Max(minimum, Math.Min(maximum, value));
    }
}
