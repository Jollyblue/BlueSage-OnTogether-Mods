using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct WorkRelaxOverlayRect
{
    internal WorkRelaxOverlayRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    internal float X { get; }
    internal float Y { get; }
    internal float Width { get; }
    internal float Height { get; }
    internal float Right => X + Width;
    internal float Bottom => Y + Height;
}

internal readonly struct WorkRelaxOverlayLayout
{
    internal WorkRelaxOverlayLayout(
        WorkRelaxOverlayRect qolMenu,
        WorkRelaxOverlayRect styleMenu,
        WorkRelaxOverlayRect metrics,
        WorkRelaxOverlayRect work,
        WorkRelaxOverlayRect relax,
        WorkRelaxOverlayRect close,
        float width,
        float height,
        bool wrapped)
    {
        QolMenu = qolMenu;
        StyleMenu = styleMenu;
        Metrics = metrics;
        Work = work;
        Relax = relax;
        Close = close;
        Width = width;
        Height = height;
        Wrapped = wrapped;
    }

    internal WorkRelaxOverlayRect QolMenu { get; }
    internal WorkRelaxOverlayRect StyleMenu { get; }
    internal WorkRelaxOverlayRect Metrics { get; }
    internal WorkRelaxOverlayRect Work { get; }
    internal WorkRelaxOverlayRect Relax { get; }
    internal WorkRelaxOverlayRect Close { get; }
    internal float Width { get; }
    internal float Height { get; }
    internal bool Wrapped { get; }
}

internal static class WorkRelaxOverlayLayoutPolicy
{
    private const float CloseWidth = 24f;
    private const float SelectorWidth = 108f;
    // The metrics bar carries the live FPS/PING/RAM readout. Keep enough width
    // for its longest normal label before allowing optional mode controls to
    // share the row; otherwise the readout clips at the default 320-pixel size.
    private const float MinimumInlineMetricsWidth = 240f;

    // Round 3b (Blue, 2026-10-18): click-to-open buttons for players who prefer them over a hotkey.
    internal const string QolMenuLabel = "QoL Menu";
    internal const string StyleMenuLabel = "Style Helper";

    internal static WorkRelaxOverlayLayout Resolve(float width, float rowHeight)
    {
        float safeWidth = Math.Max(0f, width);
        float safeHeight = Math.Max(0f, rowHeight);
        float closeWidth = Math.Min(CloseWidth, safeWidth);
        float contentWidth = Math.Max(0f, safeWidth - closeWidth);
        bool wrapped = contentWidth < MinimumInlineMetricsWidth + SelectorWidth;

        // The menu row sits above the stat bar, apart from WORK / RELAX; everything else moves down a row.
        // Blue, UAT 2026-10-18: it uses the same columns as WORK / RELAX (left of the close button), so the
        // buttons line up instead of overhanging into the close column.
        float menuHalf = contentWidth / 2f;
        WorkRelaxOverlayRect qolMenu = new(0f, 0f, menuHalf, safeHeight);
        WorkRelaxOverlayRect styleMenu = new(menuHalf, 0f, contentWidth - menuHalf, safeHeight);
        float statsY = safeHeight;

        WorkRelaxOverlayRect close = new(contentWidth, statsY, closeWidth, safeHeight);
        if (wrapped)
        {
            float segmentWidth = contentWidth / 2f;
            return new WorkRelaxOverlayLayout(
                qolMenu,
                styleMenu,
                new WorkRelaxOverlayRect(0f, statsY, contentWidth, safeHeight),
                new WorkRelaxOverlayRect(0f, statsY + safeHeight, segmentWidth, safeHeight),
                new WorkRelaxOverlayRect(segmentWidth, statsY + safeHeight, contentWidth - segmentWidth, safeHeight),
                close,
                safeWidth,
                safeHeight * 3f,
                true);
        }

        float metricsWidth = contentWidth - SelectorWidth;
        float segmentInlineWidth = SelectorWidth / 2f;
        return new WorkRelaxOverlayLayout(
            qolMenu,
            styleMenu,
            new WorkRelaxOverlayRect(0f, statsY, metricsWidth, safeHeight),
            new WorkRelaxOverlayRect(metricsWidth, statsY, segmentInlineWidth, safeHeight),
            new WorkRelaxOverlayRect(metricsWidth + segmentInlineWidth, statsY, segmentInlineWidth, safeHeight),
            close,
            safeWidth,
            safeHeight * 2f,
            false);
    }

    internal static WorkRelaxOverlayLayout ResolveForViewport(
        float configuredWidth,
        float viewportWidth,
        float rowHeight)
    {
        float safeViewportWidth = Math.Max(0f, viewportWidth);
        float effectiveWidth = Math.Min(Math.Max(0f, configuredWidth), safeViewportWidth);
        return Resolve(effectiveWidth, rowHeight);
    }
}
