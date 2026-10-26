namespace BlueSage.QoLTweaks.Core;

internal enum BanTargetPresence
{
    InSteamLobby,
    Reconnected,
    Absent,
    Ambiguous,
    // Stage C, review P2: in Steam's lobby with no live PurrNet row yet (still loading, or gone a moment ago). A ban is
    // saved without a kick; the game refuses the id when the connection completes and on any later join.
    Loading,
}

// Round 4 WS3/WS4: who a host-side ban can act on. Steam lobby membership was the only presence signal, so a
// player who rejoined through a reconnect mod (connected over PurrNet, missing from Steam's lobby list) looked
// absent and was never removed. Live rows now come from the host's PurrNet connection table; stale history rows
// the player list keeps after a reconnect never count. Blue accepted the small spoof risk ("allow moderation
// always") - every such action is labeled RECONNECTED.
internal static class BanTargetPresencePolicy
{
    internal static BanTargetPresence Classify(int steamLobbyMatches, int panelMatches, int liveRowMatches, bool uniquePlayerId)
    {
        if (steamLobbyMatches == 1 && liveRowMatches == 1 && (uniquePlayerId || panelMatches > 1))
        {
            return BanTargetPresence.InSteamLobby;
        }

        if (steamLobbyMatches == 0 && liveRowMatches == 1)
        {
            return BanTargetPresence.Reconnected;
        }

        if (steamLobbyMatches == 0 && liveRowMatches == 0)
        {
            return BanTargetPresence.Absent;
        }

        if (steamLobbyMatches == 1 && liveRowMatches == 0)
        {
            return BanTargetPresence.Loading;
        }

        return BanTargetPresence.Ambiguous;
    }

    // The pre-round-4 rule, used only when the host's PurrNet connection table cannot be read: exactly the previous
    // Steam-membership checks, so hosts keep today's bans. Moved here from SteamIdModerationController unchanged.
    internal static BanTargetPresence ClassifyWithoutLiveness(bool targetRowFound, int panelMatches, bool uniquePlayerId, int steamLobbyMatches)
    {
        if (targetRowFound && panelMatches == 1 && uniquePlayerId && steamLobbyMatches == 1)
        {
            return BanTargetPresence.InSteamLobby;
        }

        return OfflineBanPolicy.IsConfirmedAbsentFromCurrentLobby(steamLobbyMatches) ? BanTargetPresence.Absent : BanTargetPresence.Ambiguous;
    }
}
