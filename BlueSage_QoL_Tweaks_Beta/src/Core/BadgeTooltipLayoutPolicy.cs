using System;

namespace BlueSage.QoLTweaks.Core;

internal struct BadgeTooltipLayout
{
    public BadgeTooltipLayout(float x, float y, float width, float height)
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

internal static class BadgeTooltipLayoutPolicy
{
    private const float ScreenMargin = 8f;
    private const float PointerGap = 14f;
    private const float MaximumContentHeight = 240f;

    public static BadgeTooltipLayout Resolve(
        float screenWidth,
        float screenHeight,
        float pointerX,
        float pointerY,
        float requestedWidth,
        float measuredHeight,
        float scale)
    {
        float verticalMargin = GetMargin(screenHeight);
        float availableHeight = Math.Max(0f, screenHeight - (verticalMargin * 2f));
        float horizontalMargin = GetMargin(screenWidth);
        float width = ResolveWidth(screenWidth, requestedWidth);
        float scaledMaximumHeight = MaximumContentHeight * Math.Max(0f, scale);
        float height = Clamp(measuredHeight, 0f, Math.Min(scaledMaximumHeight, availableHeight));

        float minimumX = horizontalMargin;
        float maximumX = Math.Max(minimumX, screenWidth - width - horizontalMargin);
        float leftX = pointerX - width - PointerGap;
        float preferredX = leftX >= minimumX
            ? leftX
            : pointerX + PointerGap;

        float minimumY = verticalMargin;
        float maximumY = Math.Max(minimumY, screenHeight - height - verticalMargin);
        float belowY = pointerY + PointerGap;
        float preferredY = belowY <= maximumY
            ? belowY
            : pointerY - height - PointerGap;

        return new BadgeTooltipLayout(
            Clamp(preferredX, minimumX, maximumX),
            Clamp(preferredY, minimumY, maximumY),
            width,
            height);
    }

    public static float ResolveWidth(float screenWidth, float requestedWidth)
    {
        float horizontalMargin = GetMargin(screenWidth);
        float availableWidth = Math.Max(0f, screenWidth - (horizontalMargin * 2f));
        return Clamp(requestedWidth, 0f, availableWidth);
    }

    private static float GetMargin(float screenSize) =>
        Math.Min(ScreenMargin, Math.Max(0f, screenSize) / 2f);

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
