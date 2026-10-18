#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal sealed class PlayerLookupCandidate
{
    internal string SteamId { get; set; } = string.Empty;
    internal string DisplayNameRaw { get; set; } = string.Empty;
    internal string DisplayNamePlain { get; set; } = string.Empty;
    internal string SteamPersona { get; set; } = string.Empty;
    internal string Role { get; set; } = string.Empty;
    internal bool IsLocal { get; set; }
}

internal static class PlayerLookupPolicy
{
    internal const int MinimumPartialSteamIdDigits = 4;
    internal const int MaximumQueryCharacters = 96;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(30);

    internal static IReadOnlyList<PlayerLookupCandidate> FindMatches(
        string? query,
        IEnumerable<PlayerLookupCandidate>? candidates,
        out string error)
    {
        string clean = (query ?? string.Empty).Trim().TrimStart('@');
        if (clean.Length == 0)
        {
            error = "Enter part of a display name or Steam persona, 4+ SteamID digits, _host, _self, or a regex pattern.";
            return Array.Empty<PlayerLookupCandidate>();
        }

        if (clean.Length > MaximumQueryCharacters)
        {
            error = "Player lookup is limited to 96 characters.";
            return Array.Empty<PlayerLookupCandidate>();
        }

        PlayerLookupCandidate[] available = (candidates ?? Array.Empty<PlayerLookupCandidate>())
            .Where(candidate => candidate != null && ModerationProtocolPolicy.IsSteamId64(candidate.SteamId))
            .GroupBy(candidate => candidate.SteamId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        if (string.Equals(clean, "_host", StringComparison.OrdinalIgnoreCase))
        {
            return Finish(available.Where(candidate => Contains(candidate.Role, "HOST")), out error);
        }

        if (string.Equals(clean, "_self", StringComparison.OrdinalIgnoreCase))
        {
            return Finish(available.Where(candidate => candidate.IsLocal), out error);
        }

        bool digitsOnly = clean.All(char.IsDigit);
        if (digitsOnly)
        {
            if (clean.Length < MinimumPartialSteamIdDigits)
            {
                error = "Use at least 4 SteamID digits for a read-only lookup.";
                return Array.Empty<PlayerLookupCandidate>();
            }

            return Finish(available.Where(candidate => candidate.SteamId.IndexOf(clean, StringComparison.Ordinal) >= 0), out error);
        }

        var matches = available.Where(candidate =>
            Contains(candidate.DisplayNamePlain, clean) ||
            Contains(candidate.DisplayNameRaw, clean) ||
            Contains(candidate.SteamPersona, clean)).ToList();

        Regex? regex = null;
        try
        {
            regex = new Regex(clean, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            // Invalid patterns remain harmless literal-only lookups.
        }

        var matchedIds = new HashSet<string>(matches.Select(candidate => candidate.SteamId), StringComparer.Ordinal);
        var budget = Stopwatch.StartNew();
        foreach (PlayerLookupCandidate candidate in available)
        {
            if (regex == null || matchedIds.Contains(candidate.SteamId) || budget.ElapsedMilliseconds >= 50) continue;
            try
            {
                if (regex.IsMatch(candidate.DisplayNamePlain ?? string.Empty) || regex.IsMatch(candidate.SteamPersona ?? string.Empty))
                {
                    matches.Add(candidate);
                    matchedIds.Add(candidate.SteamId);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // One expensive row consumes the lookup's regex budget; keep literal matches and stop regex work.
                regex = null;
            }
        }

        return Finish(matches, out error);
    }

    private static IReadOnlyList<PlayerLookupCandidate> Finish(
        IEnumerable<PlayerLookupCandidate> matches,
        out string error)
    {
        PlayerLookupCandidate[] result = matches
            .GroupBy(candidate => candidate.SteamId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        error = result.Length == 0 ? "No verified current player matches that query." : string.Empty;
        return result;
    }

    private static bool Contains(string? value, string query)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value!.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

}
