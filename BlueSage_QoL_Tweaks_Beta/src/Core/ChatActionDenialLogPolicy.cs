using System;

namespace BlueSage.QoLTweaks.Core;

internal static class ChatActionDenialLogPolicy
{
    internal const int MaximumReasonLength = 180;
    internal const string FallbackReason = "unspecified safety rejection";
    internal const string UnknownReason = "unclassified safety rejection";

    internal static string CategorizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return FallbackReason;
        }

        string normalized = reason.Replace("\r", " ").Replace("\n", " ");
        if (Contains(normalized, "player-sender binding"))
        {
            return "sender binding unavailable";
        }

        if (Contains(normalized, "exact sender tuple changed") ||
            Contains(normalized, "tuple did not agree"))
        {
            return "sender evidence changed or disagreed";
        }

        if (Contains(normalized, "field was malformed"))
        {
            return "sender evidence malformed";
        }

        if (Contains(normalized, "clipboard record was too large"))
        {
            return "sender evidence exceeded the safety bound";
        }

        if (Contains(normalized, "bounded copy payload"))
        {
            return "bounded copy payload unavailable";
        }

        if (Contains(normalized, "safe http(s) link"))
        {
            return "safe link unavailable";
        }

        if (Contains(normalized, "modifier"))
        {
            return "invalid modifier combination";
        }

        return UnknownReason;
    }

    private static bool Contains(string value, string fragment) =>
        value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
}
