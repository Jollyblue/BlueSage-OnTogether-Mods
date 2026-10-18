using System;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal static class ChatReadabilityStylePolicy
{
    public const int MinOutlineIntensityPercent = 25;
    public const int DefaultOutlineIntensityPercent = 75;
    public const int MaxOutlineIntensityPercent = 100;
    private const float AbsoluteBlackTolerance = 0.00001f;
    private static readonly Regex ColorTagRegex = new Regex(@"<\s*(?:color\s*=\s*)?(?:#(?<hex>[0-9a-fA-F]{6}(?:[0-9a-fA-F]{2})?)|(?<name>black))\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static int ClampOutlineIntensityPercent(int configured)
    {
        if (configured < MinOutlineIntensityPercent)
        {
            return MinOutlineIntensityPercent;
        }

        if (configured > MaxOutlineIntensityPercent)
        {
            return MaxOutlineIntensityPercent;
        }

        return configured;
    }

    public static float NormalizeOutlineIntensity(int configured)
    {
        return ClampOutlineIntensityPercent(configured) / 100f;
    }

    public static bool ShouldUseLightOutline(float red, float green, float blue)
    {
        return red <= AbsoluteBlackTolerance &&
               green <= AbsoluteBlackTolerance &&
               blue <= AbsoluteBlackTolerance;
    }

    public static bool ContainsTrueBlackRichTextColorTag(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        MatchCollection matches = ColorTagRegex.Matches(text);
        foreach (Match match in matches)
        {
            string name = match.Groups["name"].Value;
            if (string.Equals(name, "black", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string hex = match.Groups["hex"].Value;
            if (hex.Length >= 6 && string.Equals(hex.Substring(0, 6), "000000", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeHexColor(string value, string fallback)
    {
        string normalizedFallback = NormalizeHexColorOrEmpty(fallback);
        string normalized = NormalizeHexColorOrEmpty(value);
        return string.IsNullOrEmpty(normalized) ? normalizedFallback : normalized;
    }

    private static bool IsHexCharacter(char value)
    {
        return (value >= '0' && value <= '9') ||
               (value >= 'a' && value <= 'f') ||
               (value >= 'A' && value <= 'F');
    }

    private static string NormalizeHexColorOrEmpty(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            return string.Empty;
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            if (!IsHexCharacter(normalized[index]))
            {
                return string.Empty;
            }
        }

        return normalized.ToUpperInvariant();
    }
}
