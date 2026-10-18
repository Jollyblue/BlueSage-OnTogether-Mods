using System;

namespace BlueSage.QoLTweaks.Core;

internal sealed class PerformanceOverlayPosition
{
    public PerformanceOverlayPosition(double offsetX, double offsetY)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public double OffsetX { get; }
    public double OffsetY { get; }
}

internal static class PerformanceOverlayPositionPolicy
{
    public static PerformanceOverlayPosition ClampToScreen(double offsetX, double offsetY, double overlayWidth, double overlayHeight, double screenWidth, double screenHeight)
    {
        double maximumX = Math.Max(0d, PositiveFiniteOrZero(screenWidth) - PositiveFiniteOrZero(overlayWidth));
        double maximumY = Math.Max(0d, PositiveFiniteOrZero(screenHeight) - PositiveFiniteOrZero(overlayHeight));
        return new PerformanceOverlayPosition(ClampFinite(offsetX, maximumX), ClampFinite(offsetY, maximumY));
    }

    private static double PositiveFiniteOrZero(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) || value <= 0d ? 0d : value;

    private static double ClampFinite(double value, double maximum) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0d : Math.Max(0d, Math.Min(maximum, value));
}
