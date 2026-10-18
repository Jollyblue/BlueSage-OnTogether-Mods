#nullable enable
using System;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct CloneIncidentNotice
{
    internal CloneIncidentNotice(string victimSuffix, string suspectSuffix, string matchType, string stableId)
    {
        VictimSuffix = victimSuffix;
        SuspectSuffix = suspectSuffix;
        MatchType = matchType;
        StableId = stableId;
    }

    internal string VictimSuffix { get; }
    internal string SuspectSuffix { get; }
    internal string MatchType { get; }
    internal string StableId { get; }
}

internal static class CloneIncidentMessagePolicy
{
    private static readonly Regex AlertPattern = new Regex(
        @"^\[Clone Shield\].*? was potentially cloned by .*?\. Evidence IDs: victim \.\.\.(?<victim>\d{6}); suspect \.\.\.(?<suspect>\d{6}); match (?<match>[^\r\n.]{1,80})\. Be aware of the player list, Steam ID, and profile; verify identity in case this alert is incorrect\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryParseAlert(string text, out CloneIncidentNotice notice)
    {
        notice = default;
        Match match = AlertPattern.Match((text ?? string.Empty).Trim());
        if (!match.Success) return false;
        notice = new CloneIncidentNotice(
            match.Groups["victim"].Value,
            match.Groups["suspect"].Value,
            NormalizeMatch(match.Groups["match"].Value),
            string.Empty);
        return true;
    }

    private static string NormalizeMatch(string value)
    {
        string clean = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(clean) ? "clone" : clean;
    }

}
