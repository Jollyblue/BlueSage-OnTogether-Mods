using System;
using BlueSage.QoLTweaks.Contracts;

namespace BlueSage.QoLTweaks.Core;

internal static class ClientCapabilitySnapshotController
{
    internal static bool TryResolve(
        BlueSageIdentityTruthSnapshotV1 identity,
        ulong lobbyKey,
        string compatiblePayload,
        string rescueReceiverPayload,
        long nowUnixSeconds,
        out BlueSageClientCapabilitySnapshotV1 snapshot)
    {
        snapshot = default;
        ClientCapabilitySnapshot resolved = ClientCapabilitySnapshotPolicy.Resolve(
            identity.SteamId,
            lobbyKey,
            identity.LobbyEpoch,
            identity.EvidenceGeneration,
            exactCurrentIdentity:
                identity.SteamId != 0UL &&
                identity.PurrNetId != 0UL &&
                identity.LobbyEpoch > 0L &&
                identity.EvidenceGeneration > 0L,
            compatibleHeartbeatFresh:
                CompatibleClientPresencePolicy.IsFresh(
                    compatiblePayload,
                    nowUnixSeconds),
            rescueReceiverFresh:
                RescueReceiverCapabilityPolicy.IsFreshCompatible(
                    rescueReceiverPayload,
                    nowUnixSeconds));
        if (!resolved.IsCurrentExactIdentity)
        {
            return false;
        }

        long capturedUtcTicks = DateTime.UtcNow.Ticks;
        snapshot = new BlueSageClientCapabilitySnapshotV1(
            resolved.SteamId,
            resolved.LobbyKey,
            resolved.LobbyEpoch,
            resolved.EvidenceGeneration,
            resolved.CompatibleClientPresent,
            resolved.RescueReceiverReady,
            resolved.StatusText,
            capturedUtcTicks,
            capturedUtcTicks + TimeSpan.TicksPerSecond);
        return true;
    }
}
