#nullable enable
using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal static class PlayerIdentityEvidencePolicy
{
    internal enum UnresolvedRowState
    {
        Checking,
        LiveSteamCheck,
        LeftStale,
    }

    internal static bool TryResolveExactRosterIndex(
        string steamId,
        IReadOnlyList<string>? rosterSteamIds,
        int playerIdCount,
        int transformCount,
        out int index,
        out string error)
    {
        index = -1;
        error = "The row does not contain a valid SteamID64.";
        if (!ModerationProtocolPolicy.IsSteamId64(steamId) || rosterSteamIds == null)
        {
            return false;
        }

        int matches = 0;
        for (int candidate = 0; candidate < rosterSteamIds.Count; candidate++)
        {
            if (!string.Equals(rosterSteamIds[candidate], steamId, StringComparison.Ordinal)) continue;
            matches++;
            index = candidate;
        }

        if (matches != 1)
        {
            index = -1;
            error = matches == 0
                ? "That Steam ID is no longer in the native player roster."
                : "That Steam ID appears more than once in the native player roster.";
            return false;
        }

        if (index >= playerIdCount || index >= transformCount)
        {
            index = -1;
            error = "The native Steam, network-player, and transform lists are not aligned for this row.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool IsUniqueCurrentLobbyMember(string steamId, IReadOnlyList<string>? lobbyMemberSteamIds)
    {
        if (!ModerationProtocolPolicy.IsSteamId64(steamId) || lobbyMemberSteamIds == null) return false;
        int matches = 0;
        for (int index = 0; index < lobbyMemberSteamIds.Count; index++)
        {
            if (string.Equals(lobbyMemberSteamIds[index], steamId, StringComparison.Ordinal)) matches++;
        }

        return matches == 1;
    }

    internal static bool IsCoverageComplete(
        bool captureReady,
        int missingCurrentSteamMembers,
        int liveNativeOnlyRows,
        int ambiguousRows)
    {
        return captureReady &&
               missingCurrentSteamMembers == 0 &&
               liveNativeOnlyRows == 0 &&
               ambiguousRows == 0;
    }

    internal static bool TryAcquireManualResync(
        long nowUtcTicks,
        long lastAcquiredUtcTicks,
        long cooldownTicks,
        out long acquiredUtcTicks,
        out int retryAfterSeconds)
    {
        long normalizedCooldownTicks = Math.Max(0L, cooldownTicks);
        if (lastAcquiredUtcTicks <= 0L || normalizedCooldownTicks == 0L)
        {
            acquiredUtcTicks = nowUtcTicks;
            retryAfterSeconds = 0;
            return true;
        }

        long elapsedTicks = nowUtcTicks >= lastAcquiredUtcTicks
            ? nowUtcTicks - lastAcquiredUtcTicks
            : 0L;
        if (elapsedTicks >= normalizedCooldownTicks)
        {
            acquiredUtcTicks = nowUtcTicks;
            retryAfterSeconds = 0;
            return true;
        }

        long remainingTicks = normalizedCooldownTicks - elapsedTicks;
        long remainingSeconds = remainingTicks / TimeSpan.TicksPerSecond;
        if (remainingTicks % TimeSpan.TicksPerSecond != 0L) remainingSeconds++;

        acquiredUtcTicks = lastAcquiredUtcTicks;
        retryAfterSeconds = (int)Math.Min(int.MaxValue, Math.Max(1L, remainingSeconds));
        return false;
    }

    internal static IReadOnlyList<string> SelectManualResyncTargets(
        IReadOnlyList<string>? nativeSteamIds,
        IReadOnlyList<string>? lobbyMemberSteamIds,
        IReadOnlyList<string>? verifiedSteamIds)
    {
        var verified = new HashSet<string>(StringComparer.Ordinal);
        if (verifiedSteamIds != null)
        {
            for (int index = 0; index < verifiedSteamIds.Count; index++)
            {
                string steamId = verifiedSteamIds[index] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(steamId)) verified.Add(steamId);
            }
        }

        var targets = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddUnverified(nativeSteamIds, verified, seen, targets);
        AddUnverified(lobbyMemberSteamIds, verified, seen, targets);
        return targets;
    }

    private static void AddUnverified(
        IReadOnlyList<string>? candidates,
        HashSet<string> verified,
        HashSet<string> seen,
        List<string> targets)
    {
        if (candidates == null) return;
        for (int index = 0; index < candidates.Count; index++)
        {
            string steamId = candidates[index] ?? string.Empty;
            if (!ModerationProtocolPolicy.IsSteamId64(steamId) ||
                verified.Contains(steamId) ||
                !seen.Add(steamId))
            {
                continue;
            }
            targets.Add(steamId);
        }
    }

    internal static string DescribeUnresolvedExactRow(
        string steamId,
        IReadOnlyList<string>? currentLobbyMembers,
        string captureError,
        int nativeSteamMatches = 0,
        bool hasUniquePlayerId = false,
        bool hasLiveTransform = false,
        bool? isPurrNetConnected = null)
    {
        if (!ModerationProtocolPolicy.IsSteamId64(steamId))
            return "the row does not contain a valid SteamID64";

        UnresolvedRowState state = ClassifyUnresolvedRow(
            steamId,
            currentLobbyMembers,
            nativeSteamMatches,
            hasUniquePlayerId,
            hasLiveTransform,
            isPurrNetConnected);
        if (state == UnresolvedRowState.LiveSteamCheck)
            return "live/steam-check row; the game/PurrNet row is live but its SteamID is absent from current Steam lobby membership";
        if (state == UnresolvedRowState.LeftStale)
            return "left/stale native-history row; Steam and PurrNet no longer report this player, so BlueSage excludes the lingering game row";

        return string.IsNullOrWhiteSpace(captureError)
            ? "the live identity row is still syncing"
            : captureError;
    }

    internal static UnresolvedRowState ClassifyUnresolvedRow(
        string steamId,
        IReadOnlyList<string>? currentLobbyMembers,
        int nativeSteamMatches,
        bool hasUniquePlayerId,
        bool hasLiveTransform,
        bool? isPurrNetConnected = null)
    {
        if (!ModerationProtocolPolicy.IsSteamId64(steamId)) return UnresolvedRowState.Checking;
        bool exactLiveNative =
            nativeSteamMatches == 1 &&
            hasUniquePlayerId &&
            hasLiveTransform;
        bool currentSteamMember =
            currentLobbyMembers != null &&
            IsUniqueCurrentLobbyMember(
                steamId,
                currentLobbyMembers);
        if (exactLiveNative && !currentSteamMember)
        {
            if (isPurrNetConnected == false)
            {
                return UnresolvedRowState.LeftStale;
            }
            return UnresolvedRowState.LiveSteamCheck;
        }

        if (exactLiveNative &&
            currentSteamMember &&
            isPurrNetConnected == false)
        {
            return UnresolvedRowState.Checking;
        }

        if (nativeSteamMatches == 0 || (nativeSteamMatches == 1 && !hasLiveTransform))
            return UnresolvedRowState.LeftStale;

        return UnresolvedRowState.Checking;
    }

    internal static bool IsVerifiedActiveRow(
        string steamId,
        IReadOnlyList<string>? rosterSteamIds,
        string playerId,
        IReadOnlyList<string>? rosterPlayerIds,
        bool hasLiveTransform,
        IReadOnlyList<string>? lobbyMemberSteamIds)
    {
        if (!hasLiveTransform || !ModerationProtocolPolicy.IsSteamId64(steamId) ||
            rosterSteamIds == null || rosterPlayerIds == null || string.IsNullOrWhiteSpace(playerId) ||
            !IsUniqueCurrentLobbyMember(steamId, lobbyMemberSteamIds))
        {
            return false;
        }

        int steamRows = 0;
        for (int index = 0; index < rosterSteamIds.Count; index++)
            if (string.Equals(rosterSteamIds[index], steamId, StringComparison.Ordinal)) steamRows++;

        int playerRows = 0;
        for (int index = 0; index < rosterPlayerIds.Count; index++)
            if (string.Equals(rosterPlayerIds[index], playerId, StringComparison.Ordinal)) playerRows++;

        return steamRows == 1 && playerRows == 1;
    }
}
