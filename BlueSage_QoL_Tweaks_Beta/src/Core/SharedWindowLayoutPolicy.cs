using System;

namespace BlueSage.QoLTweaks.Core;

internal struct SharedWindowPairLayout
{
    public SharedWindowPairLayout(WindowFitResult qolMenu, WindowFitResult styleHelper)
    {
        QolMenu = qolMenu;
        StyleHelper = styleHelper;
    }

    public WindowFitResult QolMenu { get; }

    public WindowFitResult StyleHelper { get; }
}

internal static class SharedWindowLayoutPolicy
{
    public const float PairGap = 10f;
    public const float SideBySideThreshold = 1200f;

    public static SharedWindowPairLayout CreatePair(float screenWidth, float screenHeight)
    {
        float margin = WindowFitPolicy.ScreenMargin;
        float usableWidth = WindowFitPolicy.GetUsableWidth(screenWidth);
        float usableHeight = WindowFitPolicy.GetUsableHeight(screenHeight);

        if (usableWidth >= SideBySideThreshold)
        {
            float width = Math.Max(1f, (usableWidth - PairGap) / 2f);
            float height = Math.Min(WindowFitPolicy.DefaultWindowHeight, usableHeight);
            float y = Math.Max(margin, (screenHeight - height) / 2f);
            var qolMenu = new WindowFitResult(margin, y, width, height);
            var styleHelper = new WindowFitResult(margin + width + PairGap, y, width, height);
            return new SharedWindowPairLayout(qolMenu, styleHelper);
        }

        float stackedHeight = Math.Max(1f, (usableHeight - PairGap) / 2f);
        var top = new WindowFitResult(margin, margin, usableWidth, stackedHeight);
        var bottom = new WindowFitResult(
            margin,
            margin + stackedHeight + PairGap,
            usableWidth,
            stackedHeight);
        return new SharedWindowPairLayout(top, bottom);
    }

    public static bool Overlaps(WindowFitResult first, WindowFitResult second) =>
        first.X < second.X + second.Width &&
        first.X + first.Width > second.X &&
        first.Y < second.Y + second.Height &&
        first.Y + first.Height > second.Y;

    public static bool IsFullyVisible(WindowFitResult window, float screenWidth, float screenHeight)
    {
        float margin = WindowFitPolicy.ScreenMargin;
        return window.X >= margin - 0.1f &&
               window.Y >= margin - 0.1f &&
               window.X + window.Width <= screenWidth - margin + 0.1f &&
               window.Y + window.Height <= screenHeight - margin + 0.1f;
    }
}
