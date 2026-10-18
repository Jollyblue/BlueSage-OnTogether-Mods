using System;
using System.Reflection;
using System.Threading;
using BlueSage.QoLTweaks.Core;
using BlueSage.QoLTweaks.Contracts;

namespace BlueSage.QoLTweaks;

internal static class HostConsoleController
{
    private static readonly long RefreshIntervalTicks =
        TimeSpan.FromSeconds(1).Ticks;
    private static HostConsoleSnapshot _lastSnapshot;
    private static long _lastCaptureUtcTicks;
    private static long _capabilityNonce;

    internal static BlueSageHostCapabilitySnapshotV1 LastCapabilitySnapshot { get; private set; }
    internal static bool HasCapabilitySnapshot { get; private set; }

    internal static int LastVerifiedRosterCount { get; private set; }

    internal static string LastRosterSummary { get; private set; } =
        "Roster has not been checked yet.";

    internal static string LastPlayerLimitSummary { get; private set; } =
        "PlayerLimit not checked.";

    internal static PlayerLimitCompanionStatus LastPlayerLimitStatus { get; private set; } =
        PlayerLimitCompanionStatus.NotLoaded();

    internal static HostConsoleSnapshot Capture()
    {
        long nowUtcTicks = DateTime.UtcNow.Ticks;
        long elapsedTicks = nowUtcTicks - _lastCaptureUtcTicks;
        if (_lastSnapshot != null &&
            elapsedTicks >= 0L &&
            elapsedTicks < RefreshIntervalTicks)
        {
            return _lastSnapshot;
        }

        LobbySafetyAccessState access =
            SteamIdModerationController.GetLocalAccessState(
                out bool isHost,
                out _);
        _ = PlayerIdentityEvidenceController.GetVerifiedRoster(
            out string rosterSummary);
        IdentityResyncCoverage coverage =
            PlayerIdentityEvidenceController.GetCachedIdentityResyncCoverage();
        PlayerLimitCompanionStatus playerLimit =
            Plugin.GetPlayerLimitCompanionStatus();

        LastVerifiedRosterCount = coverage.VerifiedRows;
        LastRosterSummary = rosterSummary;
        LastPlayerLimitSummary = playerLimit.ToDiagnosticText();
        LastPlayerLimitStatus = playerLimit;

        bool playerLimitHealthy =
            !playerLimit.IsLoaded ||
            (playerLimit.EffectiveMaxLobbySize.GetValueOrDefault() > 0 &&
             playerLimit.EffectiveDefaultLobbySize.GetValueOrDefault() > 0 &&
             playerLimit.EffectiveShiftSkipRate.GetValueOrDefault() > 0 &&
             playerLimit.ChatRelayPatchEnabled.HasValue);

        bool providerRequired =
            BlueSageExtensionRegistry.HasLiveHostCapabilityProvider();
        long capabilityNonce = Interlocked.Increment(ref _capabilityNonce);
        if (capabilityNonce <= 0)
        {
            Interlocked.Exchange(ref _capabilityNonce, 1);
            capabilityNonce = 1;
        }
        BlueSageHostCapabilitySnapshotV1 capabilitySnapshot = default;
        HasCapabilitySnapshot = providerRequired &&
            BlueSageExtensionRegistry.TryReadHostCapability(
                isHost,
                capabilityNonce,
                nowUtcTicks,
                out capabilitySnapshot);
        LastCapabilitySnapshot = HasCapabilitySnapshot
            ? capabilitySnapshot
            : default;

        _lastSnapshot = HostConsoleReadinessPolicy.Resolve(
            isHost:
                access == LobbySafetyAccessState.Host && isHost,
            rosterConverged: coverage.Ready,
            cleanupHealthy: Plugin.HostCleanupLaneHealthy,
            reconnectHealthy: Plugin.HostReconnectLaneHealthy,
            playerLimitHealthy: playerLimitHealthy,
            providerRequired: providerRequired,
            providerHealthy: HasCapabilitySnapshot &&
                capabilitySnapshot.State == BlueSageHostCapabilityStateV1.Ready,
            relayRequired: false,
            relayHealthy: false);
        _lastCaptureUtcTicks = nowUtcTicks;
        return _lastSnapshot;
    }

    internal static HostToolsPresentation CapturePresentation()
    {
        HostConsoleSnapshot snapshot = Capture();
        return HostToolsPresentationPolicy.Resolve(
            snapshot,
            LastVerifiedRosterCount,
            LastPlayerLimitStatus,
            Plugin.EnableAutoSweep?.Value == true,
            Plugin.ManualSweepAllowed,
            Plugin.EnableReconnectGuard?.Value == true);
    }

    internal static bool TryExecuteCapability(
        BlueSageHostCapabilityActionV1 action,
        out string message)
    {
        LobbySafetyAccessState access =
            SteamIdModerationController.GetLocalAccessState(
                out bool isHost,
                out _);
        long nowUtcTicks = DateTime.UtcNow.Ticks;
        long nonce = Interlocked.Increment(ref _capabilityNonce);
        if (access != LobbySafetyAccessState.Host || !isHost || nonce <= 0 ||
            !BlueSageExtensionRegistry.TryExecuteHostCapability(
                action,
                isHost: true,
                nonce,
                nowUtcTicks,
                out BlueSageHostCapabilityReceiptV1 receipt))
        {
            message = "Private host action unavailable; live host and provider checks failed closed.";
            return false;
        }

        message = receipt.Message;
        _lastCaptureUtcTicks = 0;
        return receipt.Succeeded;
    }

    internal static string AuditNativeModerationUi()
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;
        Type rowType = typeof(PlayerItemController);
        bool mute = rowType.GetMethod("ButtonMutePlayer", flags) != null;
        bool ignore = rowType.GetMethod("ButtonIgnorePlayer", flags) != null;
        bool report = rowType.GetMethod("ButtonReport", flags) != null;
        bool hostRemove = rowType.GetMethod("ButtonBan", flags) != null;
        return
            "Game moderation buttons: Mute=" + OnOff(mute) +
            ", Ignore=" + OnOff(ignore) +
            ", Report=" + OnOff(report) +
            ", Host Remove=" + OnOff(hostRemove) +
            ". Read-only check complete; no player action was taken.";
    }

    private static string OnOff(bool value) =>
        value ? "ready" : "unavailable";
}
