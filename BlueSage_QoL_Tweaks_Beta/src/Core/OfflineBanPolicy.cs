using System;

namespace BlueSage.QoLTweaks.Core;

internal static class OfflineBanPolicy
{
    internal static bool IsConfirmedAbsentFromCurrentLobby(int currentLobbyMatches)
    {
        return currentLobbyMatches == 0;
    }

    internal static bool CanSave(
        string ownerSteamId,
        string issuerSteamId,
        string targetSteamId,
        string persistedHelpers,
        bool targetIsProtectedVictim,
        bool targetIsAbsent,
        out string reason)
    {
        reason = "Offline SteamID64 bans require the active host, an exact absent target, and a valid native ban list.";
        if (!ModerationProtocolPolicy.IsSteamId64(ownerSteamId) ||
            !ModerationProtocolPolicy.IsSteamId64(issuerSteamId) ||
            !ModerationProtocolPolicy.IsSteamId64(targetSteamId) ||
            !targetIsAbsent || targetIsProtectedVictim)
        {
            return false;
        }

        if (!string.Equals(ownerSteamId, issuerSteamId, StringComparison.Ordinal))
        {
            reason = "Only the active lobby host can save a SteamID64 that is not in the current lobby.";
            return false;
        }

        if (string.Equals(targetSteamId, ownerSteamId, StringComparison.Ordinal))
        {
            reason = "The host's own SteamID64 cannot be added to the host's native ban list.";
            return false;
        }

        if (Array.IndexOf(LobbyPingAuthorizationPolicy.ParseDelegates(persistedHelpers), targetSteamId) >= 0)
        {
            reason = "Remove this SteamID64 from Helpers before creating an offline native ban.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
