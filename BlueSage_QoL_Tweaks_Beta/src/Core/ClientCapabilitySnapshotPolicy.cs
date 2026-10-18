namespace BlueSage.QoLTweaks.Core;

internal readonly struct ClientCapabilitySnapshot
{
    internal ClientCapabilitySnapshot(
        ulong steamId,
        ulong lobbyKey,
        long lobbyEpoch,
        long evidenceGeneration,
        bool isCurrentExactIdentity,
        bool compatibleClientPresent,
        bool rescueReceiverReady,
        string statusText)
    {
        SteamId = steamId;
        LobbyKey = lobbyKey;
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        IsCurrentExactIdentity = isCurrentExactIdentity;
        CompatibleClientPresent = compatibleClientPresent;
        RescueReceiverReady = rescueReceiverReady;
        StatusText = statusText ?? string.Empty;
    }

    internal ulong SteamId { get; }
    internal ulong LobbyKey { get; }
    internal long LobbyEpoch { get; }
    internal long EvidenceGeneration { get; }
    internal bool IsCurrentExactIdentity { get; }
    internal bool CompatibleClientPresent { get; }
    internal bool RescueReceiverReady { get; }
    internal string StatusText { get; }
}

internal static class ClientCapabilitySnapshotPolicy
{
    internal static ClientCapabilitySnapshot Resolve(
        ulong steamId,
        ulong lobbyKey,
        long lobbyEpoch,
        long evidenceGeneration,
        bool exactCurrentIdentity,
        bool compatibleHeartbeatFresh,
        bool rescueReceiverFresh)
    {
        bool current = exactCurrentIdentity &&
                       steamId != 0UL &&
                       lobbyKey != 0UL &&
                       lobbyEpoch > 0L &&
                       evidenceGeneration > 0L;
        bool compatible = current && compatibleHeartbeatFresh;
        bool rescue = current && rescueReceiverFresh;
        string status = !current
            ? "Exact current player identity is unavailable."
            : compatible && rescue
                ? "Compatible QoL and rescue receiver ready."
                : rescue
                    ? "Rescue receiver ready; general QoL presence is not current."
                    : compatible
                        ? "Compatible QoL present; rescue receiver is not ready."
                        : "No fresh compatible QoL heartbeat.";

        return new ClientCapabilitySnapshot(
            steamId,
            lobbyKey,
            lobbyEpoch,
            evidenceGeneration,
            current,
            compatible,
            rescue,
            status);
    }
}
