namespace BlueSage.QoLTweaks.Core;

// Reconnect Guard used to rejoin the saved lobby the instant the clean menu was confirmed. The host
// can still be counting the player's old connection at that moment, so an instant rejoin
// double-counts them: wrong host/lobby player counts and, at worst, a lobby nobody can join. The
// client cannot see the host's player list, so it cannot "know" the slot is free -- it waits a
// deliberate hold instead. The floor keeps the hold from ever being set back to instant.
internal static class ReconnectRejoinHoldPolicy
{
    internal const int MinimumHoldSeconds = 10;
    internal const int DefaultHoldSeconds = 20;
    internal const int MaximumHoldSeconds = 120;

    internal static int ClampHoldSeconds(int configuredSeconds)
    {
        if (configuredSeconds < MinimumHoldSeconds)
        {
            return MinimumHoldSeconds;
        }

        return configuredSeconds > MaximumHoldSeconds ? MaximumHoldSeconds : configuredSeconds;
    }

    internal static bool ShouldJoinNow(float heldSeconds, int holdSeconds)
    {
        return heldSeconds >= ClampHoldSeconds(holdSeconds);
    }

    // The clean-menu readiness check is noisy (the teardown loop needs 3 consecutive ready frames to
    // trust it), so the hold cancels only after the menu has been not-ready continuously for this
    // long -- time-based, so it does not depend on frame rate.
    internal const float CancelAfterNotReadySeconds = 1.5f;

    internal static float UpdateNotReadyStreak(float currentStreakSeconds, bool menuReady, float deltaSeconds)
    {
        return menuReady ? 0f : currentStreakSeconds + deltaSeconds;
    }

    internal static bool ShouldCancelHold(float notReadyStreakSeconds)
    {
        return notReadyStreakSeconds >= CancelAfterNotReadySeconds;
    }

    // Definitive signals, unlike the noisy menu check: the player turned Reconnect Guard off, or a
    // join STARTED during the hold (the game reports IsConnecting or a lobby where it did not when
    // the hold began). Edge-triggered on purpose: the game's IsConnecting can read true with no
    // player action, so a value already true at hold start must never block a legitimate reconnect.
    internal static bool ShouldAbortImmediately(bool guardEnabled, bool joinInProgressNow, bool joinInProgressAtHoldStart)
    {
        return !guardEnabled || StartedDuringHold(joinInProgressNow, joinInProgressAtHoldStart);
    }

    // Final check right before JoinByCode, on the same edge-triggered basis.
    internal static bool IsSafeToJoinNow(bool guardEnabled, bool joinInProgressNow, bool joinInProgressAtHoldStart, bool menuReady)
    {
        return guardEnabled && !StartedDuringHold(joinInProgressNow, joinInProgressAtHoldStart) && menuReady;
    }

    private static bool StartedDuringHold(bool now, bool atHoldStart) => now && !atHoldStart;

    internal static string BuildHoldNotice(int holdSeconds)
    {
        return "BlueSage reconnect will rejoin in " + ClampHoldSeconds(holdSeconds) +
               "s, giving the host time to clear your old connection so the lobby's player count stays correct.";
    }
}
