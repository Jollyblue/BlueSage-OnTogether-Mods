using System;

namespace BlueSage.QoLTweaks.Core;

/// <summary>
/// Some chat mods send control lines through chat wrapped in a size-zero tag so they are invisible to their own
/// users. Those lines are not player speech. BlueSage never re-publishes one (Discord relay, copy, rewrite).
/// The game still delivers or drops them exactly as before; this only keeps BlueSage's own paths clean.
/// </summary>
internal static class HiddenControlLinePolicy
{
    public const string Marker = "<size=0>GT1|";

    public static bool IsHiddenControlLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.TrimStart().StartsWith(Marker, StringComparison.Ordinal);
    }
}
