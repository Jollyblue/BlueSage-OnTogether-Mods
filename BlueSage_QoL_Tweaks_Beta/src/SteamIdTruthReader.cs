using System;
using PurrNet;
using PurrNet.Steam;

namespace BlueSage.QoLTweaks;

// The only place BlueSage reads PurrNet's Steam-authenticated connection SteamID. Host only: a
// client holds a single connection (to the host), so it has no transport truth about other players.
internal static class SteamIdTruthReader
{
    internal static bool IsTransportTruthAvailable()
    {
        try
        {
            NetworkManager manager = NetworkManager.main;
            return manager != null &&
                   manager.isServer &&
                   manager.transport is SteamTransport;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static ulong ReadTransportSteamId(PlayerID playerId)
    {
        try
        {
            return PurrSteamUtils.TryGetSteamID(playerId, out ulong steamId) ? steamId : 0UL;
        }
        catch (Exception)
        {
            return 0UL;
        }
    }
}
