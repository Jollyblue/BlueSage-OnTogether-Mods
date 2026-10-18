namespace BlueSage.QoLTweaks.Core;

internal enum HostToolsTone
{
    Ready,
    Checking,
    Attention,
    Unavailable
}

internal sealed class HostToolsPresentation
{
    internal HostToolsPresentation(
        HostToolsTone tone,
        string heading,
        string meaning,
        string advice,
        string playerSummary,
        string cleanupSummary,
        string reconnectSummary,
        string largeLobbySummary,
        string largeLobbyChatSummary,
        string technicalDetails)
    {
        Tone = tone;
        Heading = heading;
        Meaning = meaning;
        Advice = advice;
        PlayerSummary = playerSummary;
        CleanupSummary = cleanupSummary;
        ReconnectSummary = reconnectSummary;
        LargeLobbySummary = largeLobbySummary;
        LargeLobbyChatSummary = largeLobbyChatSummary;
        TechnicalDetails = technicalDetails;
    }

    internal HostToolsTone Tone { get; }
    internal string Heading { get; }
    internal string Meaning { get; }
    internal string Advice { get; }
    internal string PlayerSummary { get; }
    internal string CleanupSummary { get; }
    internal string ReconnectSummary { get; }
    internal string LargeLobbySummary { get; }
    internal string LargeLobbyChatSummary { get; }
    internal string TechnicalDetails { get; }
}

internal static class HostToolsPresentationPolicy
{
    internal static HostToolsPresentation Resolve(
        HostConsoleSnapshot snapshot,
        int verifiedRosterCount,
        PlayerLimitCompanionStatus playerLimit,
        bool autoCleanupEnabled,
        bool manualCleanupAvailable,
        bool reconnectEnabled)
    {
        string playerSummary = "Players matched safely: " + verifiedRosterCount +
            (snapshot.RosterConverged ? string.Empty : " - checking for updates");
        string cleanupSummary = autoCleanupEnabled
            ? "Automatic cleanup: " + (snapshot.CleanupHealthy ? "Ready" : "Needs attention")
            : "Automatic cleanup: Off by choice" +
              (manualCleanupAvailable ? " - Run Cleanup Now remains available" : string.Empty);
        string reconnectSummary = reconnectEnabled
            ? "Reconnect protection: " + (snapshot.ReconnectHealthy ? "Ready" : "Needs attention")
            : "Reconnect protection: Off by choice";
        string largeLobbySummary = ResolveLargeLobbySummary(snapshot, playerLimit);
        string largeLobbyChatSummary = ResolveLargeLobbyChatSummary(playerLimit);
        string technicalDetails =
            "Host=" + snapshot.HostState +
            "; Roster=" + snapshot.RosterState +
            "; Cleanup=" + snapshot.CleanupState +
            "; Reconnect=" + snapshot.ReconnectState +
            "; " + playerLimit.ToDiagnosticText();

        if (!snapshot.IsHost)
        {
            return New(
                HostToolsTone.Unavailable,
                "Host tools are available to the lobby host",
                "These controls are hidden because this client is not the verified lobby host.",
                "Join or create a lobby as host to use Host Tools.",
                playerSummary,
                cleanupSummary,
                reconnectSummary,
                largeLobbySummary,
                largeLobbyChatSummary,
                technicalDetails);
        }

        if (autoCleanupEnabled && !snapshot.CleanupHealthy)
        {
            return New(
                HostToolsTone.Attention,
                "Some host tools need attention",
                "Automatic cleanup is unavailable. This does not close the lobby.",
                "Use Run Cleanup Now. If this warning returns, turn Automatic Cleanup off and continue with manual cleanup.",
                playerSummary,
                cleanupSummary,
                reconnectSummary,
                largeLobbySummary,
                largeLobbyChatSummary,
                technicalDetails);
        }

        if (reconnectEnabled && !snapshot.ReconnectHealthy)
        {
            return New(
                HostToolsTone.Attention,
                "Some host tools need attention",
                "Reconnect protection is unavailable. Normal hosting can continue.",
                "Return to the main menu before retrying a failed join; restart the game if repeated reconnect attempts stall.",
                playerSummary,
                cleanupSummary,
                reconnectSummary,
                largeLobbySummary,
                largeLobbyChatSummary,
                technicalDetails);
        }

        if (playerLimit.IsLoaded && !snapshot.PlayerLimitHealthy)
        {
            return New(
                HostToolsTone.Attention,
                "Some host tools need attention",
                "Large-lobby capacity could not be confirmed. Ordinary hosting remains available.",
                "Confirm only one PlayerLimitLift is enabled, then restart the game.",
                playerSummary,
                cleanupSummary,
                reconnectSummary,
                largeLobbySummary,
                largeLobbyChatSummary,
                technicalDetails);
        }

        if (!snapshot.RosterConverged)
        {
            return New(
                HostToolsTone.Checking,
                "Checking this lobby...",
                "BlueSage is matching visible player rows to the current lobby.",
                "Wait a few seconds. If checking does not finish, close and reopen the player list. Sensitive player actions remain safely unavailable meanwhile.",
                playerSummary,
                cleanupSummary,
                reconnectSummary,
                largeLobbySummary,
                largeLobbyChatSummary,
                technicalDetails);
        }

        return New(
            HostToolsTone.Ready,
            "Host tools are ready",
            "BlueSage's public host checks are healthy for this lobby.",
            "No action needed. Keep hosting and have fun.",
            playerSummary,
            cleanupSummary,
            reconnectSummary,
            largeLobbySummary,
            largeLobbyChatSummary,
            technicalDetails);
    }

    private static HostToolsPresentation New(
        HostToolsTone tone,
        string heading,
        string meaning,
        string advice,
        string playerSummary,
        string cleanupSummary,
        string reconnectSummary,
        string largeLobbySummary,
        string largeLobbyChatSummary,
        string technicalDetails) =>
        new HostToolsPresentation(
            tone,
            heading,
            meaning,
            advice,
            playerSummary,
            cleanupSummary,
            reconnectSummary,
            largeLobbySummary,
            largeLobbyChatSummary,
            technicalDetails);

    private static string ResolveLargeLobbySummary(
        HostConsoleSnapshot snapshot,
        PlayerLimitCompanionStatus playerLimit)
    {
        if (!playerLimit.IsLoaded)
        {
            return "Large-lobby support: Not installed - standard lobby hosting is available";
        }

        if (!snapshot.PlayerLimitHealthy || !playerLimit.EffectiveMaxLobbySize.HasValue)
        {
            return "Large-lobby support: Needs attention";
        }

        return "Large-lobby support: Ready - up to " +
               playerLimit.EffectiveMaxLobbySize.Value +
               " players";
    }

    private static string ResolveLargeLobbyChatSummary(
        PlayerLimitCompanionStatus playerLimit)
    {
        if (!playerLimit.IsLoaded)
        {
            return "In-game large-lobby chat: Standard game behavior";
        }

        if (!playerLimit.ChatRelayPatchEnabled.HasValue)
        {
            return "In-game large-lobby chat: Needs attention";
        }

        return playerLimit.ChatRelayPatchEnabled.Value
            ? "In-game large-lobby chat: Ready"
            : "In-game large-lobby chat: Off by choice";
    }
}
