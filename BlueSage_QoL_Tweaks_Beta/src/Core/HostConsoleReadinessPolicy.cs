namespace BlueSage.QoLTweaks.Core;

internal enum HostConsoleOverallState
{
    Ready,
    Degraded,
    Unavailable
}

internal sealed class HostConsoleSnapshot
{
    internal HostConsoleSnapshot(
        HostConsoleOverallState overallState,
        bool isHost,
        bool rosterConverged,
        bool cleanupHealthy,
        bool reconnectHealthy,
        bool playerLimitHealthy,
        bool providerRequired,
        bool providerHealthy,
        bool relayRequired,
        bool relayHealthy,
        string hostState,
        string rosterState,
        string cleanupState,
        string reconnectState,
        string playerLimitState,
        string providerState,
        string relayState,
        string reason)
    {
        OverallState = overallState;
        IsHost = isHost;
        RosterConverged = rosterConverged;
        CleanupHealthy = cleanupHealthy;
        ReconnectHealthy = reconnectHealthy;
        PlayerLimitHealthy = playerLimitHealthy;
        ProviderRequired = providerRequired;
        ProviderHealthy = providerHealthy;
        RelayRequired = relayRequired;
        RelayHealthy = relayHealthy;
        HostState = hostState;
        RosterState = rosterState;
        CleanupState = cleanupState;
        ReconnectState = reconnectState;
        PlayerLimitState = playerLimitState;
        ProviderState = providerState;
        RelayState = relayState;
        Reason = reason;
    }

    internal HostConsoleOverallState OverallState { get; }
    internal bool IsHost { get; }
    internal bool RosterConverged { get; }
    internal bool CleanupHealthy { get; }
    internal bool ReconnectHealthy { get; }
    internal bool PlayerLimitHealthy { get; }
    internal bool ProviderRequired { get; }
    internal bool ProviderHealthy { get; }
    internal bool RelayRequired { get; }
    internal bool RelayHealthy { get; }
    internal string HostState { get; }
    internal string RosterState { get; }
    internal string CleanupState { get; }
    internal string ReconnectState { get; }
    internal string PlayerLimitState { get; }
    internal string ProviderState { get; }
    internal string RelayState { get; }
    internal string Reason { get; }
}

internal static class HostConsoleReadinessPolicy
{
    internal static HostConsoleSnapshot Resolve(
        bool isHost,
        bool rosterConverged,
        bool cleanupHealthy,
        bool reconnectHealthy,
        bool playerLimitHealthy,
        bool providerRequired,
        bool providerHealthy,
        bool relayRequired,
        bool relayHealthy)
    {
        string providerState = OptionalState(
            providerRequired,
            providerHealthy);
        string relayState = OptionalState(
            relayRequired,
            relayHealthy);

        if (!isHost)
        {
            return Snapshot(
                HostConsoleOverallState.Unavailable,
                isHost,
                rosterConverged,
                cleanupHealthy,
                reconnectHealthy,
                playerLimitHealthy,
                providerRequired,
                providerHealthy,
                relayRequired,
                relayHealthy,
                providerState,
                relayState,
                "Current client is not the verified lobby host.");
        }

        string reason = FirstRequiredFailure(
            rosterConverged,
            cleanupHealthy,
            reconnectHealthy,
            playerLimitHealthy,
            providerRequired,
            providerHealthy,
            relayRequired,
            relayHealthy);
        HostConsoleOverallState state = string.IsNullOrEmpty(reason)
            ? HostConsoleOverallState.Ready
            : HostConsoleOverallState.Degraded;

        return Snapshot(
            state,
            isHost,
            rosterConverged,
            cleanupHealthy,
            reconnectHealthy,
            playerLimitHealthy,
            providerRequired,
            providerHealthy,
            relayRequired,
            relayHealthy,
            providerState,
            relayState,
            string.IsNullOrEmpty(reason)
                ? "All required host lanes are ready."
                : reason);
    }

    private static HostConsoleSnapshot Snapshot(
        HostConsoleOverallState state,
        bool host,
        bool roster,
        bool cleanup,
        bool reconnect,
        bool playerLimit,
        bool providerRequired,
        bool providerHealthy,
        bool relayRequired,
        bool relayHealthy,
        string provider,
        string relay,
        string reason) =>
        new HostConsoleSnapshot(
            state,
            host,
            roster,
            cleanup,
            reconnect,
            playerLimit,
            providerRequired,
            providerHealthy,
            relayRequired,
            relayHealthy,
            OnOff(host),
            OnOff(roster),
            OnOff(cleanup),
            OnOff(reconnect),
            OnOff(playerLimit),
            provider,
            relay,
            reason);

    private static string FirstRequiredFailure(
        bool roster,
        bool cleanup,
        bool reconnect,
        bool playerLimit,
        bool providerRequired,
        bool providerHealthy,
        bool relayRequired,
        bool relayHealthy)
    {
        if (!roster) return "Verified roster is not converged.";
        if (!cleanup) return "Automatic cleanup is not healthy.";
        if (!reconnect) return "Reconnect protection is not healthy.";
        if (!playerLimit) return "PlayerLimit host integration is not healthy.";
        if (providerRequired && !providerHealthy)
            return "Required host capability provider is not healthy.";
        if (relayRequired && !relayHealthy)
            return "Required Discord relay is not healthy.";
        return string.Empty;
    }

    private static string OptionalState(bool required, bool healthy)
    {
        if (required) return healthy ? "Ready" : "Degraded";
        return healthy ? "Ready (optional)" : "Optional / off";
    }

    private static string OnOff(bool value) =>
        value ? "Ready" : "Degraded";
}
