namespace BlueSage.QoLTweaks.Core;

internal static class ModerationAuthorizationPolicy
{
    internal static bool IsAuthorizedIssuer(
        string senderSteamId,
        string ownerSteamId,
        string persistedDelegates,
        string persistedDelegateOwnerSteamId,
        bool hasLobby)
    {
        return LobbyPingAuthorizationPolicy.IsAuthorized(
            senderSteamId,
            ownerSteamId,
            persistedDelegates,
            persistedDelegateOwnerSteamId,
            hasLobby);
    }

    internal static bool CanBanTarget(
        string targetSteamId,
        string ownerSteamId,
        string persistedDelegates,
        string issuerSteamId,
        bool targetIsProtectedVictim,
        bool targetIsPresent)
    {
        if (!targetIsPresent || targetIsProtectedVictim || !ModerationProtocolPolicy.IsSteamId64(targetSteamId))
        {
            return false;
        }

        if (targetSteamId == ownerSteamId || targetSteamId == issuerSteamId)
        {
            return false;
        }

        string[] delegates = LobbyPingAuthorizationPolicy.ParseDelegates(persistedDelegates);
        return System.Array.IndexOf(delegates, targetSteamId) < 0;
    }
}
