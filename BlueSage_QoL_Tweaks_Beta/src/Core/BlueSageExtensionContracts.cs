using System;

namespace BlueSage.QoLTweaks.Contracts;

public static class BlueSageExtensionAbiV1
{
    public const int Version = 1;
    public const long MaximumContributionLifetimeTicks =
        TimeSpan.TicksPerSecond * 5;
    public const long MaximumRequestFutureSkewTicks =
        TimeSpan.TicksPerSecond * 2;
}

public interface IBlueSageIdentityTruthV1
{
    ulong SteamId { get; }
    ulong PurrNetId { get; }
    long LobbyEpoch { get; }
    long EvidenceGeneration { get; }
}

public readonly struct BlueSageIdentityTruthSnapshotV1 :
    IBlueSageIdentityTruthV1
{
    public BlueSageIdentityTruthSnapshotV1(
        ulong steamId,
        ulong purrNetId,
        long lobbyEpoch,
        long evidenceGeneration,
        long capturedUtcTicks,
        long freshUntilUtcTicks)
    {
        SteamId = steamId;
        PurrNetId = purrNetId;
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        CapturedUtcTicks = capturedUtcTicks;
        FreshUntilUtcTicks = freshUntilUtcTicks;
    }

    public ulong SteamId { get; }
    public ulong PurrNetId { get; }
    public long LobbyEpoch { get; }
    public long EvidenceGeneration { get; }
    public long CapturedUtcTicks { get; }
    public long FreshUntilUtcTicks { get; }
}

public readonly struct BlueSageClientCapabilitySnapshotV1
{
    public BlueSageClientCapabilitySnapshotV1(
        ulong steamId,
        ulong lobbyKey,
        long lobbyEpoch,
        long evidenceGeneration,
        bool compatibleClientPresent,
        bool rescueReceiverReady,
        string statusText,
        long capturedUtcTicks,
        long freshUntilUtcTicks)
    {
        SteamId = steamId;
        LobbyKey = lobbyKey;
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        CompatibleClientPresent = compatibleClientPresent;
        RescueReceiverReady = rescueReceiverReady;
        StatusText = statusText ?? string.Empty;
        CapturedUtcTicks = capturedUtcTicks;
        FreshUntilUtcTicks = freshUntilUtcTicks;
    }

    public ulong SteamId { get; }
    public ulong LobbyKey { get; }
    public long LobbyEpoch { get; }
    public long EvidenceGeneration { get; }
    public bool CompatibleClientPresent { get; }
    public bool RescueReceiverReady { get; }
    public string StatusText { get; }
    public long CapturedUtcTicks { get; }
    public long FreshUntilUtcTicks { get; }
}

public readonly struct BlueSageBadgeContributionRequestV1 :
    IBlueSageIdentityTruthV1
{
    public BlueSageBadgeContributionRequestV1(
        ulong steamId,
        ulong purrNetId,
        long lobbyEpoch,
        long evidenceGeneration,
        long nonce,
        long issuedUtcTicks)
    {
        SteamId = steamId;
        PurrNetId = purrNetId;
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        Nonce = nonce;
        IssuedUtcTicks = issuedUtcTicks;
    }

    public ulong SteamId { get; }
    public ulong PurrNetId { get; }
    public long LobbyEpoch { get; }
    public long EvidenceGeneration { get; }
    public long Nonce { get; }
    public long IssuedUtcTicks { get; }
}

public readonly struct BlueSageBadgeContributionResponseV1
{
    public BlueSageBadgeContributionResponseV1(
        bool contributedBadgePresent,
        long lobbyEpoch,
        long evidenceGeneration,
        long nonce,
        long freshUntilUtcTicks)
    {
        ContributedBadgePresent = contributedBadgePresent;
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        Nonce = nonce;
        FreshUntilUtcTicks = freshUntilUtcTicks;
    }

    public bool ContributedBadgePresent { get; }
    public long LobbyEpoch { get; }
    public long EvidenceGeneration { get; }
    public long Nonce { get; }
    public long FreshUntilUtcTicks { get; }
}

public interface IBlueSageBadgeContributionV1
{
    BlueSageBadgeContributionResponseV1 GetBadgeContribution(
        BlueSageBadgeContributionRequestV1 request);
}

public enum BlueSageDiagnosticsOverlayStateV1
{
    Unavailable,
    Hidden,
    Visible,
}

public enum BlueSageDiagnosticsFreshnessV1
{
    Unavailable,
    Fresh,
    Stale,
}

public readonly struct BlueSageDiagnosticsSnapshotV1
{
    public BlueSageDiagnosticsSnapshotV1(
        BlueSageDiagnosticsOverlayStateV1 overlayState,
        BlueSageDiagnosticsFreshnessV1 freshness,
        int intervalMilliseconds,
        long capturedUtcTicks,
        long freshUntilUtcTicks)
    {
        OverlayState = overlayState;
        Freshness = freshness;
        IntervalMilliseconds =
            BlueSageDiagnosticsPolicyV1.ClampIntervalMilliseconds(
                intervalMilliseconds);
        CapturedUtcTicks = capturedUtcTicks;
        FreshUntilUtcTicks = freshUntilUtcTicks;
    }

    public BlueSageDiagnosticsOverlayStateV1 OverlayState { get; }
    public BlueSageDiagnosticsFreshnessV1 Freshness { get; }
    public int IntervalMilliseconds { get; }
    public long CapturedUtcTicks { get; }
    public long FreshUntilUtcTicks { get; }
}

public interface IBlueSageDiagnosticsProviderV1
{
    BlueSageDiagnosticsSnapshotV1 GetSnapshot(long nowUtcTicks);
}

public interface IBlueSageDiagnosticsControlV2
{
    bool TrySetOverlayVisible(bool visible);
}

public enum BlueSageHostCapabilityStateV1
{
    Unavailable,
    Ready,
    Degraded,
}

[Flags]
public enum BlueSageHostCapabilityActionMaskV1
{
    None = 0,
    RefreshSnapshot = 1 << 0,
    ResetMovement = 1 << 1,
    RescueLocalPlayer = 1 << 2,
    AuditNativeModeration = 1 << 3,
}

public enum BlueSageHostCapabilityActionV1
{
    RefreshSnapshot,
    ResetMovement,
    RescueLocalPlayer,
    AuditNativeModeration,
}

public readonly struct BlueSageHostCapabilityRequestV1
{
    public BlueSageHostCapabilityRequestV1(
        long lobbyEpoch,
        long evidenceGeneration,
        bool isHost,
        long nonce,
        long issuedUtcTicks)
    {
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        IsHost = isHost;
        Nonce = nonce;
        IssuedUtcTicks = issuedUtcTicks;
    }

    public long LobbyEpoch { get; }
    public long EvidenceGeneration { get; }
    public bool IsHost { get; }
    public long Nonce { get; }
    public long IssuedUtcTicks { get; }
}

public readonly struct BlueSageHostCapabilitySnapshotV1
{
    public BlueSageHostCapabilitySnapshotV1(
        BlueSageHostCapabilityStateV1 state,
        string title,
        string summary,
        long lobbyEpoch,
        long evidenceGeneration,
        long nonce,
        long capturedUtcTicks,
        long freshUntilUtcTicks,
        BlueSageHostCapabilityActionMaskV1 actions)
    {
        State = state;
        Title = title ?? string.Empty;
        Summary = summary ?? string.Empty;
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        Nonce = nonce;
        CapturedUtcTicks = capturedUtcTicks;
        FreshUntilUtcTicks = freshUntilUtcTicks;
        Actions = actions;
    }

    public BlueSageHostCapabilityStateV1 State { get; }
    public string Title { get; }
    public string Summary { get; }
    public long LobbyEpoch { get; }
    public long EvidenceGeneration { get; }
    public long Nonce { get; }
    public long CapturedUtcTicks { get; }
    public long FreshUntilUtcTicks { get; }
    public BlueSageHostCapabilityActionMaskV1 Actions { get; }
}

public readonly struct BlueSageHostCapabilityReceiptV1
{
    public BlueSageHostCapabilityReceiptV1(
        bool succeeded,
        string message,
        long lobbyEpoch,
        long evidenceGeneration,
        long nonce,
        long completedUtcTicks)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
        LobbyEpoch = lobbyEpoch;
        EvidenceGeneration = evidenceGeneration;
        Nonce = nonce;
        CompletedUtcTicks = completedUtcTicks;
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public long LobbyEpoch { get; }
    public long EvidenceGeneration { get; }
    public long Nonce { get; }
    public long CompletedUtcTicks { get; }
}

public interface IBlueSageHostCapabilityProviderV1
{
    BlueSageHostCapabilitySnapshotV1 GetSnapshot(
        BlueSageHostCapabilityRequestV1 request);

    BlueSageHostCapabilityReceiptV1 TryExecute(
        BlueSageHostCapabilityActionV1 action,
        BlueSageHostCapabilityRequestV1 request);
}

public static class BlueSageDiagnosticsPolicyV1
{
    public const int MinimumIntervalMilliseconds = 100;
    public const int MaximumIntervalMilliseconds = 5_000;

    public static int ClampIntervalMilliseconds(int requested)
    {
        if (requested < MinimumIntervalMilliseconds)
        {
            return MinimumIntervalMilliseconds;
        }
        if (requested > MaximumIntervalMilliseconds)
        {
            return MaximumIntervalMilliseconds;
        }
        return requested;
    }
}

public readonly struct BlueSageExtensionRegistrationTokenV1
{
    public BlueSageExtensionRegistrationTokenV1(long generation)
    {
        Generation = generation;
    }

    public long Generation { get; }
    public bool IsValid => Generation > 0;
}
