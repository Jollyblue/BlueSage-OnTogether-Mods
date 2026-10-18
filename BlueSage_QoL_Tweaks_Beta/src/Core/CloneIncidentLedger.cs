#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal sealed class CloneIncident
{
    internal string Id { get; set; } = string.Empty;
    internal string VictimSteamId { get; set; } = string.Empty;
    internal string SuspectSteamId { get; set; } = string.Empty;
    internal string VictimLabel { get; set; } = string.Empty;
    internal string SuspectLabel { get; set; } = string.Empty;
    internal string MatchType { get; set; } = string.Empty;
    internal long FirstObservedUnixSeconds { get; set; }
    internal long LastObservedUnixSeconds { get; set; }
    internal string StableId { get; set; } = string.Empty;
    internal CloneIncidentState State { get; set; } = CloneIncidentState.Open;
    internal long ClosedUnixSeconds { get; set; }
    internal string ClosedBySteamId { get; set; } = string.Empty;
}

internal enum CloneIncidentState
{
    Open,
    Resolved,
    DismissedLocal,
    StaleAbsent,
    NoLongerMatching,
}

internal static class CloneIncidentLedger
{
    private const int MaximumIncidents = 64;
    private const long MaximumIncidentAgeSeconds = 7200;
    private static readonly List<CloneIncident> Incidents = new List<CloneIncident>();
    private static string _lobbyKey = string.Empty;
    private static int _nextIncident = 1;

    // Existing moderation and identity surfaces intentionally see only open
    // incidents. Closed incidents remain in bounded history until explicitly
    // cleared or pruned, but can no longer protect/highlight/target a player.
    internal static IReadOnlyList<CloneIncident> Snapshot => Incidents.Where(item => item.State == CloneIncidentState.Open).ToArray();
    internal static IReadOnlyList<CloneIncident> HistorySnapshot => Incidents.ToArray();

    internal static void Reset(string lobbyKey)
    {
        string normalized = (lobbyKey ?? string.Empty).Trim();
        _lobbyKey = normalized;
        _nextIncident = 1;
        Incidents.Clear();
    }

    internal static CloneIncident Record(
        string victimSteamId,
        string suspectSteamId,
        string matchType,
        string victimLabel,
        string suspectLabel,
        long observedUnixSeconds)
    {
        if (!ModerationProtocolPolicy.IsSteamId64(victimSteamId) ||
            !ModerationProtocolPolicy.IsSteamId64(suspectSteamId) ||
            string.Equals(victimSteamId, suspectSteamId, StringComparison.Ordinal))
        {
            return null!;
        }

        Prune(observedUnixSeconds);
        CloneIncident? existing = Incidents.LastOrDefault(item =>
            item.State == CloneIncidentState.Open &&
            string.Equals(item.VictimSteamId, victimSteamId, StringComparison.Ordinal) &&
            string.Equals(item.SuspectSteamId, suspectSteamId, StringComparison.Ordinal));
        if (existing != null)
        {
            existing.MatchType = MergeMatch(existing.MatchType, matchType);
            existing.VictimLabel = SanitizeLabel(victimLabel);
            existing.SuspectLabel = SanitizeLabel(suspectLabel);
            existing.LastObservedUnixSeconds = observedUnixSeconds;
            return existing;
        }

        var incident = new CloneIncident
        {
            Id = "C" + _nextIncident++.ToString("000"),
            VictimSteamId = victimSteamId,
            SuspectSteamId = suspectSteamId,
            VictimLabel = SanitizeLabel(victimLabel),
            SuspectLabel = SanitizeLabel(suspectLabel),
            MatchType = NormalizeMatch(matchType),
            FirstObservedUnixSeconds = observedUnixSeconds,
            LastObservedUnixSeconds = observedUnixSeconds,
            StableId = BuildStableId(victimSteamId, suspectSteamId),
            State = CloneIncidentState.Open,
        };
        Incidents.Add(incident);
        while (Incidents.Count > MaximumIncidents)
        {
            Incidents.RemoveAt(0);
        }

        return incident;
    }

    internal static bool TryResolveIncident(string incidentId, out CloneIncident incident)
    {
        incident = Incidents.LastOrDefault(item => item.State == CloneIncidentState.Open &&
            string.Equals(item.Id, (incidentId ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))!;
        return incident != null;
    }

    internal static bool TryResolveStableIncident(string stableId, out CloneIncident incident)
    {
        incident = Incidents.LastOrDefault(item => item.State == CloneIncidentState.Open &&
            string.Equals(item.StableId, (stableId ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))!;
        return incident != null;
    }

    internal static bool MarkResolved(string incidentId, string actorSteamId, long closedUnixSeconds)
    {
        if (!TryResolveIncident(incidentId, out CloneIncident incident)) return false;
        Close(incident, CloneIncidentState.Resolved, actorSteamId, closedUnixSeconds);
        return true;
    }

    internal static bool MarkResolvedPair(string victimSteamId, string suspectSteamId, string actorSteamId, long closedUnixSeconds)
    {
        CloneIncident? incident = Incidents.LastOrDefault(item => item.State == CloneIncidentState.Open &&
            string.Equals(item.VictimSteamId, victimSteamId, StringComparison.Ordinal) &&
            string.Equals(item.SuspectSteamId, suspectSteamId, StringComparison.Ordinal));
        if (incident == null) return false;
        Close(incident, CloneIncidentState.Resolved, actorSteamId, closedUnixSeconds);
        return true;
    }

    internal static bool DismissLocal(string incidentId, long closedUnixSeconds)
    {
        if (!TryResolveIncident(incidentId, out CloneIncident incident)) return false;
        Close(incident, CloneIncidentState.DismissedLocal, string.Empty, closedUnixSeconds);
        return true;
    }

    internal static int DismissAllOpenLocal(long closedUnixSeconds)
    {
        CloneIncident[] open = Incidents.Where(item => item.State == CloneIncidentState.Open).ToArray();
        foreach (CloneIncident incident in open)
        {
            Close(incident, CloneIncidentState.DismissedLocal, string.Empty, closedUnixSeconds);
        }
        return open.Length;
    }

    internal static bool MarkStaleAbsent(string incidentId, long closedUnixSeconds)
    {
        if (!TryResolveIncident(incidentId, out CloneIncident incident)) return false;
        Close(incident, CloneIncidentState.StaleAbsent, string.Empty, closedUnixSeconds);
        return true;
    }

    internal static bool MarkNoLongerMatching(string incidentId, long closedUnixSeconds)
    {
        if (!TryResolveIncident(incidentId, out CloneIncident incident)) return false;
        Close(incident, CloneIncidentState.NoLongerMatching, string.Empty, closedUnixSeconds);
        return true;
    }

    internal static IReadOnlyList<CloneIncident> DismissOpenForSteamId(string steamId, long closedUnixSeconds)
    {
        if (!ModerationProtocolPolicy.IsSteamId64(steamId)) return Array.Empty<CloneIncident>();
        CloneIncident[] matches = Incidents.Where(item => item.State == CloneIncidentState.Open &&
            (string.Equals(item.VictimSteamId, steamId, StringComparison.Ordinal) ||
             string.Equals(item.SuspectSteamId, steamId, StringComparison.Ordinal))).ToArray();
        foreach (CloneIncident incident in matches)
            Close(incident, CloneIncidentState.DismissedLocal, string.Empty, closedUnixSeconds);
        return matches;
    }

    internal static int ClearClosed()
    {
        int before = Incidents.Count;
        Incidents.RemoveAll(item => item.State != CloneIncidentState.Open);
        return before - Incidents.Count;
    }

    internal static void Prune(long nowUnixSeconds)
    {
        if (nowUnixSeconds <= 0) return;
        // Open incidents are live safety state and must never disappear merely
        // because a clock or capacity bound elapsed. Aligned roster scans close
        // them explicitly as absent, no-longer-matching, resolved, or dismissed.
        Incidents.RemoveAll(item => item.State != CloneIncidentState.Open &&
            item.LastObservedUnixSeconds > 0 && nowUnixSeconds - item.LastObservedUnixSeconds > MaximumIncidentAgeSeconds);
        TrimToBound();
    }

    internal static bool IsProtectedVictim(string steamId)
    {
        return Incidents.Any(item => item.State == CloneIncidentState.Open && string.Equals(item.VictimSteamId, steamId, StringComparison.Ordinal));
    }

    internal static CloneIncidentState? GetRoleState(string steamId, out bool isVictim, out bool isSuspect)
    {
        isVictim = Incidents.Any(item => item.State == CloneIncidentState.Open && string.Equals(item.VictimSteamId, steamId, StringComparison.Ordinal));
        isSuspect = Incidents.Any(item => item.State == CloneIncidentState.Open && string.Equals(item.SuspectSteamId, steamId, StringComparison.Ordinal));
        return isVictim || isSuspect ? CloneIncidentState.Open : null;
    }

    internal static string BuildStableId(string victimSteamId, string suspectSteamId)
    {
        // FNV-1a over the exact ordered SteamID64 pair gives every QoL observer
        // the same compact correlation token without exposing mutable local C#
        // numbering as if it were globally authoritative.
        string value = (victimSteamId ?? string.Empty) + ">" + (suspectSteamId ?? string.Empty);
        uint hash = 2166136261;
        for (int index = 0; index < value.Length; index++)
        {
            hash ^= value[index];
            hash *= 16777619;
        }
        return "I" + hash.ToString("X8");
    }

    private static void Close(CloneIncident incident, CloneIncidentState state, string actorSteamId, long closedUnixSeconds)
    {
        incident.State = state;
        incident.ClosedBySteamId = ModerationProtocolPolicy.IsSteamId64(actorSteamId) ? actorSteamId : string.Empty;
        incident.ClosedUnixSeconds = closedUnixSeconds;
    }

    private static void TrimToBound()
    {
        while (Incidents.Count > MaximumIncidents)
        {
            int oldestClosed = Incidents.FindIndex(item => item.State != CloneIncidentState.Open);
            if (oldestClosed < 0) break;
            Incidents.RemoveAt(oldestClosed);
        }
    }

    private static string MergeMatch(string current, string added)
    {
        string cleanAdded = NormalizeMatch(added);
        if (string.IsNullOrEmpty(current)) return cleanAdded;
        if (current.IndexOf(cleanAdded, StringComparison.OrdinalIgnoreCase) >= 0) return current;
        return current + "+" + cleanAdded;
    }

    private static string NormalizeMatch(string value)
    {
        string clean = (value ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(clean) ? "clone" : clean;
    }

    private static string SanitizeLabel(string value)
    {
        string clean = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length > 48 ? clean.Substring(0, 48) : clean;
    }
}
