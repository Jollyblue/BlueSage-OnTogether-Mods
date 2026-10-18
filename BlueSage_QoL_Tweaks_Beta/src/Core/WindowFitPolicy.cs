using System;

namespace BlueSage.QoLTweaks.Core;

internal struct WindowFitResult
{
    public WindowFitResult(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float X { get; }

    public float Y { get; }

    public float Width { get; }

    public float Height { get; }
}

internal static class WindowFitPolicy
{
    public const float ScreenMargin = 10f;
    public const float DefaultWindowWidth = 1390f;
    public const float DefaultWindowHeight = 1210f;
    public const float MinimumUsableWidth = 560f;
    public const float MinimumUsableHeight = 560f;

    public static float GetUsableWidth(float screenWidth) =>
        Math.Max(1f, screenWidth - (ScreenMargin * 2f));

    public static float GetUsableHeight(float screenHeight) =>
        Math.Max(1f, screenHeight - (ScreenMargin * 2f));

    public static WindowFitResult FitInitial(float screenWidth, float screenHeight)
    {
        return FitInitial(BlueSageMenuSurface.QolMenu, screenWidth, screenHeight);
    }

    public static WindowFitResult FitInitial(
        BlueSageMenuSurface surface,
        float screenWidth,
        float screenHeight)
    {
        MenuComfortSize comfort = MenuDisplayScalePolicy.GetComfortSize(surface);
        float usableWidth = GetUsableWidth(screenWidth);
        float usableHeight = GetUsableHeight(screenHeight);
        float fittedWidth = Math.Min(comfort.Width, usableWidth);
        float fittedHeight = Math.Min(comfort.Height, usableHeight);
        float fittedX = Math.Max(ScreenMargin, (screenWidth - fittedWidth) / 2f);
        float fittedY = Math.Max(ScreenMargin, (screenHeight - fittedHeight) / 2f);
        return new WindowFitResult(fittedX, fittedY, fittedWidth, fittedHeight);
    }

    public static WindowFitResult FitForOpen(
        bool hasOpened,
        float x,
        float y,
        float width,
        float height,
        float preferredMinWidth,
        float preferredMinHeight,
        float screenWidth,
        float screenHeight)
    {
        return FitForOpen(
            BlueSageMenuSurface.QolMenu,
            hasOpened,
            x,
            y,
            width,
            height,
            preferredMinWidth,
            preferredMinHeight,
            screenWidth,
            screenHeight);
    }

    public static WindowFitResult FitForOpen(
        BlueSageMenuSurface surface,
        bool hasOpened,
        float x,
        float y,
        float width,
        float height,
        float preferredMinWidth,
        float preferredMinHeight,
        float screenWidth,
        float screenHeight)
    {
        return hasOpened
            ? FitToScreen(x, y, width, height, preferredMinWidth, preferredMinHeight, screenWidth, screenHeight)
            : FitInitial(surface, screenWidth, screenHeight);
    }

    public static WindowFitResult FitToScreen(
        float x,
        float y,
        float width,
        float height,
        float preferredMinWidth,
        float preferredMinHeight,
        float screenWidth,
        float screenHeight)
    {
        return FitToScreen(
            x,
            y,
            width,
            height,
            preferredMinWidth,
            preferredMinHeight,
            screenWidth,
            screenHeight,
            expandToMaximum: false);
    }

    public static WindowFitResult FitToScreen(
        float x,
        float y,
        float width,
        float height,
        float preferredMinWidth,
        float preferredMinHeight,
        float screenWidth,
        float screenHeight,
        bool expandToMaximum)
    {
        float usableWidth = GetUsableWidth(screenWidth);
        float usableHeight = GetUsableHeight(screenHeight);
        float maxWidth = usableWidth;
        float maxHeight = usableHeight;
        float minWidth = Math.Min(preferredMinWidth, maxWidth);
        float minHeight = Math.Min(preferredMinHeight, maxHeight);
        float fittedWidth = expandToMaximum ? maxWidth : Clamp(width, minWidth, maxWidth);
        float fittedHeight = expandToMaximum ? maxHeight : Clamp(height, minHeight, maxHeight);
        float fittedX = ClampToVisibleRange(x, screenWidth, fittedWidth);
        float fittedY = ClampToVisibleRange(y, screenHeight, fittedHeight);

        return new WindowFitResult(fittedX, fittedY, fittedWidth, fittedHeight);
    }

    private static float ClampToVisibleRange(float origin, float screenSize, float windowSize)
    {
        float maximumOrigin = Math.Max(ScreenMargin, screenSize - windowSize - ScreenMargin);
        return Clamp(origin, ScreenMargin, maximumOrigin);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        if (value > maximum)
        {
            return maximum;
        }

        return value;
    }
}

internal static class ReportWindowResizePolicy
{
    public static WindowFitResult MergeGuiWindowResult(
        WindowFitResult guiWindowResult,
        bool hasRequestedSize,
        float requestedWidth,
        float requestedHeight)
    {
        return hasRequestedSize
            ? new WindowFitResult(
                guiWindowResult.X,
                guiWindowResult.Y,
                requestedWidth,
                requestedHeight)
            : guiWindowResult;
    }
}

internal static class ReportWindowLayoutPolicy
{
    public const float HorizontalChromeWidth = 48f;
    public const float RequiredInlineAllocationWidth = 1080f;

    public static bool UseStackedAllocationLayout(float contentWidth) =>
        contentWidth < RequiredInlineAllocationWidth;
}
