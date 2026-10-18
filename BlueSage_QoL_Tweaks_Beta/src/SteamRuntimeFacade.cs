using Steamworks;

namespace BlueSage.QoLTweaks;

internal sealed class SteamRuntimeSnapshot
{
    internal static readonly SteamRuntimeSnapshot Empty =
        new SteamRuntimeSnapshot(false, CSteamID.Nil, CSteamID.Nil, false);

    internal SteamRuntimeSnapshot(
        bool steamWasReady,
        CSteamID lobby,
        CSteamID localUser,
        bool localWasOwner)
    {
        SteamWasReady = steamWasReady;
        Lobby = lobby;
        LocalUser = localUser;
        LocalWasOwner = localWasOwner;
    }

    internal bool SteamWasReady { get; }
    internal CSteamID Lobby { get; }
    internal CSteamID LocalUser { get; }
    internal bool LocalWasOwner { get; }
}

internal static class SteamRuntimeFacade
{
    private static readonly object Sync = new object();
    private static SteamRuntimeSnapshot _lastKnown = SteamRuntimeSnapshot.Empty;

    internal static void CaptureLive()
    {
        if (PluginShutdownController.IsShuttingDown)
        {
            return;
        }

        try
        {
            if (!SteamManager.Initialized ||
                !Plugin.TryGetRescueLobby(out CSteamID lobby) ||
                lobby == CSteamID.Nil)
            {
                return;
            }

            CSteamID localUser = SteamUser.GetSteamID();
            if (localUser == CSteamID.Nil)
            {
                return;
            }

            lock (Sync)
            {
                _lastKnown = new SteamRuntimeSnapshot(
                    true,
                    lobby,
                    localUser,
                    SteamMatchmaking.GetLobbyOwner(lobby) == localUser);
            }
        }
        catch
        {
            // Keep the last fully validated runtime tuple. Shutdown never probes again.
        }
    }

    internal static SteamRuntimeSnapshot SnapshotForShutdown()
    {
        lock (Sync)
        {
            return _lastKnown;
        }
    }
}
