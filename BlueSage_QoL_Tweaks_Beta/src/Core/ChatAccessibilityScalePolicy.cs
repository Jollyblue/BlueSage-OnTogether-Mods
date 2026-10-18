using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal static class ChatAccessibilityScalePolicy
{
    public const int MinScalePercent = 75;
    public const int DefaultScalePercent = 100;
    public const int MaxScalePercent = 200;

    public static int ClampScalePercent(int configured)
    {
        if (configured < MinScalePercent)
        {
            return MinScalePercent;
        }

        if (configured > MaxScalePercent)
        {
            return MaxScalePercent;
        }

        return configured;
    }

    public static float NormalizeScale(int configured)
    {
        return ClampScalePercent(configured) / 100f;
    }

    public static bool TryParseScaleToken(string token, out int percent)
    {
        percent = DefaultScalePercent;
        string value = (token ?? string.Empty).Trim();
        if (string.Equals(value, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "status", StringComparison.OrdinalIgnoreCase) ||
            value == "?")
        {
            return false;
        }

        value = value.TrimEnd('%');
        if (value.Contains(".") && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float multiplier))
        {
            percent = ClampScalePercent((int)Math.Round(multiplier * 100f));
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested))
        {
            percent = ClampScalePercent(requested);
            return true;
        }

        return false;
    }

    public static bool TryCalculateScreenClampDelta(
        float left,
        float right,
        float bottom,
        float top,
        float screenWidth,
        float screenHeight,
        float margin,
        out float deltaX,
        out float deltaY)
    {
        deltaX = 0f;
        deltaY = 0f;
        if (screenWidth <= 0f || screenHeight <= 0f)
        {
            return false;
        }

        float safeMargin = Math.Max(0f, margin);
        float maxRight = Math.Max(safeMargin, screenWidth - safeMargin);
        float maxTop = Math.Max(safeMargin, screenHeight - safeMargin);

        if (left < safeMargin)
        {
            deltaX = safeMargin - left;
        }
        else if (right > maxRight)
        {
            deltaX = maxRight - right;
        }

        if (bottom < safeMargin)
        {
            deltaY = safeMargin - bottom;
        }
        else if (top > maxTop)
        {
            deltaY = maxTop - top;
        }

        return Math.Abs(deltaX) > 0.001f || Math.Abs(deltaY) > 0.001f;
    }
}
