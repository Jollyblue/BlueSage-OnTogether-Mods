#nullable enable
using System;

namespace BlueSage.QoLTweaks.Core;

// Round 5 C1 + F (audit of Blue's 37.5 h host, 2026-10-22). Two log families repeated for the same reason:
// Clone Shield wrote "waiting for aligned roster" once a minute for 17.5 h, and the identity-coverage line
// flipped complete=True/False 216 times because a public lobby has someone loading in or dropping out 67%
// of the time. Change detection alone does not diet that; the state must HOLD before it is announced, and a
// persisting bad state gets one reminder per hour. Pure: callers pass their clock, tests pass numbers.
internal enum StableStateLogDecision
{
    None,
    Announce,
    Reminder,
}

internal struct StableStateLogState
{
    internal string? PendingKey;
    internal float PendingSince;
    internal string? AnnouncedKey;
    internal float AnnouncedAt;
    internal float LastReminderAt;

    // Seconds the currently announced key has been in force (for "still waiting for N min" lines).
    internal float AnnouncedAgeSeconds(float now) => AnnouncedKey == null ? 0f : Math.Max(0f, now - AnnouncedAt);
}

internal static class StableStateLogPolicy
{
    internal const float DefaultHoldSeconds = 60f;
    internal const float DefaultReminderSeconds = 3600f;

    internal static StableStateLogDecision Observe(
        ref StableStateLogState state,
        string currentKey,
        bool remindWhileHeld,
        float now,
        float holdSeconds = DefaultHoldSeconds,
        float reminderSeconds = DefaultReminderSeconds)
    {
        currentKey ??= string.Empty;
        if (!string.Equals(state.PendingKey, currentKey, StringComparison.Ordinal))
        {
            state.PendingKey = currentKey;
            state.PendingSince = now;
        }

        if (!string.Equals(state.AnnouncedKey, currentKey, StringComparison.Ordinal))
        {
            // The very first state is announced at once (it is the session's starting truth); every later flip
            // must hold for the full window so a flicker shorter than it logs nothing at all.
            bool firstEver = state.AnnouncedKey == null;
            if (!firstEver && now - state.PendingSince < holdSeconds)
            {
                return StableStateLogDecision.None;
            }

            state.AnnouncedKey = currentKey;
            state.AnnouncedAt = firstEver ? now : state.PendingSince;
            state.LastReminderAt = now;
            return StableStateLogDecision.Announce;
        }

        if (remindWhileHeld && now - state.LastReminderAt >= reminderSeconds)
        {
            state.LastReminderAt = now;
            return StableStateLogDecision.Reminder;
        }

        return StableStateLogDecision.None;
    }

    internal static void Reset(ref StableStateLogState state)
    {
        state = default;
    }
}
