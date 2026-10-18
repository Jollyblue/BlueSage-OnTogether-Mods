using System;

namespace BlueSage.QoLTweaks.Core;

internal static class PlayerRenderSaverPolicy
{
    // Retired 2026-10-17 (Blue): hiding far/overflow avatars caused visual glitching. While true,
    // nothing is hidden, the controller never starts, and the menu does not offer it. Flip to bring
    // the dormant feature back. static readonly, not const: a constant true makes every guarded
    // early return leave dead code (CS0162), which the seal's -warnaserror build refuses.
    public static readonly bool Retired = true;
    public const string RetiredMessage =
        "Player Render Saver is retired for now: it caused visual glitches. Nothing is hidden; every player draws normally.";

    public const int MinRadiusMeters = 10;
    public const int DefaultRadiusMeters = 45;
    public const int MaxRadiusMeters = 250;
    public const int MinMaxVisiblePlayers = 1;
    public const int DefaultMaxVisiblePlayers = 32;
    public const int MaxMaxVisiblePlayers = 128;
    public const float RefreshIntervalSeconds = 1f;
    public const int DistanceHysteresisMeters = 6;
    public const int RankHysteresisPlayers = 2;

    public static int ClampRadiusMeters(int configured)
    {
        if (configured < MinRadiusMeters)
        {
            return MinRadiusMeters;
        }

        if (configured > MaxRadiusMeters)
        {
            return MaxRadiusMeters;
        }

        return configured;
    }

    public static int ClampMaxVisiblePlayers(int configured)
    {
        if (configured < MinMaxVisiblePlayers)
        {
            return MinMaxVisiblePlayers;
        }

        if (configured > MaxMaxVisiblePlayers)
        {
            return MaxMaxVisiblePlayers;
        }

        return configured;
    }

    public static bool ShouldHideRemotePlayer(float distanceMeters, int distanceRank, int radiusMeters, int maxVisiblePlayers)
    {
        return ShouldHideRemotePlayer(distanceMeters, distanceRank, radiusMeters, maxVisiblePlayers, false);
    }

    public static bool ShouldHideRemotePlayer(float distanceMeters, int distanceRank, int radiusMeters, int maxVisiblePlayers, bool currentlyHidden)
    {
        int radius = ClampRadiusMeters(radiusMeters);
        int visibleCap = ClampMaxVisiblePlayers(maxVisiblePlayers);
        int rank = Math.Max(0, distanceRank);

        // A hidden player must move materially closer before reappearing. This prevents
        // render/animation thrash when a crowd hovers around either threshold.
        if (currentlyHidden)
        {
            return distanceMeters > Math.Max(MinRadiusMeters, radius - DistanceHysteresisMeters) ||
                   rank >= Math.Max(1, visibleCap - RankHysteresisPlayers);
        }

        // Either budget may cull a remote avatar: the distance budget is our fog-of-war,
        // while the rank budget places a hard ceiling on expensive visible avatars.
        return distanceMeters > radius + DistanceHysteresisMeters || rank >= visibleCap;
    }
}
