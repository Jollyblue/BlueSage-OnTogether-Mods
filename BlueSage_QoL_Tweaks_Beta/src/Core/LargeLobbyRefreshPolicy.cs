using System;

namespace BlueSage.QoLTweaks.Core;

internal static class LargeLobbyRefreshPolicy
{
    // Player-list badges are read-only presentation. Keep their periodic work
    // below the frame-critical path while exact actions still capture fresh
    // identity truth on demand and fail closed when it is unavailable.
    internal const float BadgePollIntervalSeconds = 2f;

    internal static float BadgePollIntervalSecondsForRoster(
        int activeRowCount)
    {
        // Keep ordinary lobbies as responsive as the established release.
        // At 24+ visible rows, badge rendering becomes background display
        // work; back it off without touching exact moderation/action truth.
        if (activeRowCount >= 48)
        {
            return 6f;
        }

        if (activeRowCount >= 24)
        {
            return 4f;
        }

        return BadgePollIntervalSeconds;
    }

    internal static readonly TimeSpan RosterSnapshotLifetime =
        TimeSpan.FromSeconds(2);
}
