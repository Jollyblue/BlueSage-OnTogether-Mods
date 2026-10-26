namespace BlueSage.QoLTweaks.Core;

internal readonly struct NetworkCarriedRequestDecision
{
    internal NetworkCarriedRequestDecision(bool accepted, string surface, string reason)
    {
        Accepted = accepted;
        Surface = surface;
        Reason = reason;
    }

    internal bool Accepted { get; }
    internal string Surface { get; }
    internal string Reason { get; }
}

// Round 4 Stage I (WS4 closure, Blue 2026-10-20). A Helper request that arrives over the game connection (a chat
// RPC the host intercepts before relay) is bound to the player by the AUTHORITATIVE network sender of that RPC,
// never by anything written inside the packet: exactly one live roster row must own that sender, the row's Steam
// ID must be a SteamID64, and when the transport exposes the Steam-authenticated ID it must agree. A sender
// missing from Steam's lobby list (the reconnect case this path exists for) is accepted and named in the journal
// surface, so the host journal, the abuse monitor Blue chose, shows it on every row.
internal static class NetworkCarriedRequestPolicy
{
    internal const string Surface = "helper relay (game connection)";
    internal const string ReconnectedIssuerSurface = "helper relay (game connection, issuer not in Steam list)";

    internal const string RefusedSurface = "helper relay (game connection) refused";

    // claimedSteamIdLiveRows: how many LIVE rows in the lobby carry the sender's Steam ID. Exactly one, or a second
    // client claiming a Helper's ID could act under it (advisor, I gate): the same rule the ban path applies.
    internal static NetworkCarriedRequestDecision Decide(int senderRowMatches, string claimedSteamId, int claimedSteamIdLiveRows, ulong transportSteamId, bool senderInSteamLobby)
    {
        if (senderRowMatches <= 0)
        {
            return new NetworkCarriedRequestDecision(false, string.Empty, "the network sender has no live roster row");
        }

        if (senderRowMatches > 1)
        {
            return new NetworkCarriedRequestDecision(false, string.Empty, "the network sender matches more than one roster row");
        }

        if (!ModerationProtocolPolicy.IsSteamId64(claimedSteamId))
        {
            return new NetworkCarriedRequestDecision(false, string.Empty, "the sender's roster row has no SteamID64");
        }

        if (claimedSteamIdLiveRows <= 0)
        {
            return new NetworkCarriedRequestDecision(false, string.Empty, "no live row carries the sender's Steam ID");
        }

        if (claimedSteamIdLiveRows > 1)
        {
            return new NetworkCarriedRequestDecision(false, string.Empty, "more than one live row carries the sender's Steam ID");
        }

        if (transportSteamId != 0UL && !string.Equals(transportSteamId.ToString(), claimedSteamId, System.StringComparison.Ordinal))
        {
            return new NetworkCarriedRequestDecision(false, string.Empty, "the transport Steam ID does not match the sender's roster row");
        }

        return new NetworkCarriedRequestDecision(true, senderInSteamLobby ? Surface : ReconnectedIssuerSurface, string.Empty);
    }
}
