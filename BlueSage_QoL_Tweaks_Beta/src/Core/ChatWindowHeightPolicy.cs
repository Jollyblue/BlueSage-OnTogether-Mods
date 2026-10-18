using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal static class ChatWindowHeightPolicy
{
    public const int MinHeightPercent = 100;
    public const int DefaultHeightPercent = 100;
    public const int EnabledHeightPercent = 125;
    public const int MaxHeightPercent = 200;

    public static int ClampHeightPercent(int configured)
    {
        if (configured < MinHeightPercent)
        {
            return MinHeightPercent;
        }

        if (configured > MaxHeightPercent)
        {
            return MaxHeightPercent;
        }

        return configured;
    }

    public static float NormalizeHeight(int configured)
    {
        return ClampHeightPercent(configured) / 100f;
    }

    public static bool TryParseHeightToken(string token, out int percent)
    {
        percent = DefaultHeightPercent;
        string value = (token ?? string.Empty).Trim();
        if (string.Equals(value, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "disable", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "enabled", StringComparison.OrdinalIgnoreCase))
        {
            percent = EnabledHeightPercent;
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
            percent = ClampHeightPercent((int)Math.Round(multiplier * 100f));
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested))
        {
            percent = ClampHeightPercent(requested);
            return true;
        }

        return false;
    }

    public static float CalculateAnchoredYForBottomLockedStretch(float originalY, float originalHeight, float newHeight, float pivotY)
    {
        float safePivotY = pivotY < 0f ? 0f : pivotY > 1f ? 1f : pivotY;
        return originalY + Math.Max(0f, newHeight - originalHeight) * safePivotY;
    }
}
