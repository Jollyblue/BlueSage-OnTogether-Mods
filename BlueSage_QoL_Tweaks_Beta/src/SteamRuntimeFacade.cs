using BlueSage.QoLTweaks.Core;
using Steamworks;
using UnityEngine;

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
    // Round 4 Stage E #10: the shutdown snapshot only needs a recent tuple; probing Steam every frame was waste.
    internal const float CaptureIntervalSeconds = 1f;
    private static float _lastCaptureAt = -1f;

    internal static void CaptureLive()
    {
        if (PluginShutdownController.IsShuttingDown)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (!NativeLookupCooldownPolicy.ShouldScan(_lastCaptureAt, now, CaptureIntervalSeconds))
        {
            return;
        }

        _lastCaptureAt = now;
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
