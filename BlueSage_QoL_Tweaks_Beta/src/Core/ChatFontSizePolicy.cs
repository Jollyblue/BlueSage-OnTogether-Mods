using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal static class ChatFontSizePolicy
{
    public const int MinFontSizePercent = 75;
    public const int DefaultFontSizePercent = 100;
    public const int EnabledFontSizePercent = 125;
    public const int MaxFontSizePercent = 200;

    public static int ClampFontSizePercent(int configured)
    {
        if (configured < MinFontSizePercent)
        {
            return MinFontSizePercent;
        }

        if (configured > MaxFontSizePercent)
        {
            return MaxFontSizePercent;
        }

        return configured;
    }

    public static float NormalizeFontSize(int configured)
    {
        return ClampFontSizePercent(configured) / 100f;
    }

    public static bool TryParseFontSizeToken(string token, out int percent)
    {
        percent = DefaultFontSizePercent;
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
            percent = EnabledFontSizePercent;
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
            percent = ClampFontSizePercent((int)Math.Round(multiplier * 100f));
            return true;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested))
        {
            percent = ClampFontSizePercent(requested);
            return true;
        }

        return false;
    }
}
