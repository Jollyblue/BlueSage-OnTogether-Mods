using System;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal static class TimestampFormatter
{
    private static readonly Regex HexColorRegex = new Regex("^[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public static string BuildPrefix(DateTime now, bool use24HourTime, int sizePercent, string colorHex)
    {
        int safeSize = Clamp(sizePercent, 40, 100);
        string safeColor = SanitizeColor(colorHex);
        string time = now.ToString(use24HourTime ? "HH:mm" : "h:mm tt");

        return $"<size={safeSize}%><color=#{safeColor}>{time}</color></size> ";
    }

    private static string SanitizeColor(string colorHex)
    {
        string value = (colorHex ?? string.Empty).Trim().TrimStart('#');
        return HexColorRegex.IsMatch(value) ? value.ToUpperInvariant() : "F5EDE1";
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
