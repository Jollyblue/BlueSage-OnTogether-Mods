using System;

namespace BlueSage.QoLTweaks.Core;

internal enum MenuDisplayScaleSource
{
    Fallback,
    ReportedDpi,
    Manual
}

internal enum BlueSageMenuSurface
{
    QolMenu,
    StyleHelper
}

internal readonly struct MenuComfortSize
{
    internal MenuComfortSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    internal float Width { get; }

    internal float Height { get; }
}

internal readonly struct MenuDisplayScaleResult
{
    internal MenuDisplayScaleResult(int percent, MenuDisplayScaleSource source)
    {
        Percent = percent;
        Source = source;
    }

    internal int Percent { get; }

    internal MenuDisplayScaleSource Source { get; }

    internal float Scale => Percent / 100f;

    internal float ToLogicalPixels(float physicalPixels) => physicalPixels / Scale;
}

internal static class MenuDisplayScalePolicy
{
    internal const int AutoScalePercent = 0;
    internal const int MinimumManualScalePercent = 75;
    internal const int MaximumScalePercent = 200;
    private const float BaselineDpi = 96f;

    internal static MenuDisplayScaleResult Resolve(int configuredPercent, float reportedDpi)
    {
        if (configuredPercent != AutoScalePercent)
        {
            return new MenuDisplayScaleResult(
                Clamp(configuredPercent, MinimumManualScalePercent, MaximumScalePercent),
                MenuDisplayScaleSource.Manual);
        }

        if (float.IsNaN(reportedDpi) || float.IsInfinity(reportedDpi) || reportedDpi <= 0f)
        {
            return new MenuDisplayScaleResult(100, MenuDisplayScaleSource.Fallback);
        }

        int nearestFivePercent = (int)Math.Round((reportedDpi / BaselineDpi) * 20f) * 5;
        return new MenuDisplayScaleResult(
            Clamp(nearestFivePercent, 100, MaximumScalePercent),
            MenuDisplayScaleSource.ReportedDpi);
    }

    internal static MenuComfortSize GetComfortSize(BlueSageMenuSurface surface)
    {
        return surface == BlueSageMenuSurface.StyleHelper
            ? new MenuComfortSize(1220f, 1310f)
            : new MenuComfortSize(1390f, 1210f);
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }
}
