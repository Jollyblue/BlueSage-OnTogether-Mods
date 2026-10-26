namespace BlueSage.QoLTweaks.Core;

internal enum NativeBanRoute
{
    // Not the host: vanilla's own gate decides (it disables these buttons for non-hosts); we never touch the click.
    Vanilla,
    // The host clicked with a real SteamID64: route through the host-verified ban (live rows, labels, fail closed).
    HostRoute,
    // The host clicked but the surface carries no usable id: refuse, so vanilla's first-matching-row path never guesses.
    Refuse
}

// Round 4 Stage C, review P1 (2026-10-19): the game's three Ban buttons (ID card, player row, report confirm) ban by
// the first PlayerSteamIDs row matching the id. After a reconnect that is the stale row, so the live player stays.
internal static class NativeBanRoutePolicy
{
    internal static NativeBanRoute Decide(bool isLocalHost, string steamId)
    {
        if (!isLocalHost)
        {
            return NativeBanRoute.Vanilla;
        }

        return ModerationProtocolPolicy.IsSteamId64(steamId) ? NativeBanRoute.HostRoute : NativeBanRoute.Refuse;
    }
}
