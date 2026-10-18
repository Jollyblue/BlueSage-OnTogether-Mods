#nullable enable

using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

// Accessibility fishing aid (Blue, 2026-10-17): helps players with sore or unsteady hands. Default OFF.
// Equip the rod with the game's fishing key near water and it finds water, casts, answers the bite
// after a human-like delay, plays the reel mini-game, and recasts. A cast by hand sets a preferred
// spot. Every decision here is pure so it can be tested.
// An aim point as an x/z offset from the player (no ValueTuple on net46).
internal readonly struct CastOffset
{
    internal CastOffset(float x, float z)
    {
        X = x;
        Z = z;
    }

    internal float X { get; }
    internal float Z { get; }

    internal void Deconstruct(out float x, out float z)
    {
        x = X;
        z = Z;
    }
}

internal static class FishingAidPolicy
{
    internal const bool DefaultEnabled = false;
    // Round 3 (Blue, live UAT 2026-10-18): 0.7 s reaction, 7 clicks per second and 3.5 s release are
    // the defaults for every player. These keys are new in 0.2.5, so updating 0.2.4 users get them too;
    // a saved choice is never overwritten.
    internal const float DefaultReactionSeconds = 0.7f;
    internal const float MinReactionSeconds = 0.1f;
    internal const float MaxReactionSeconds = 1.5f;
    internal const float DefaultClicksPerSecond = 7f;
    internal const float MinClicksPerSecond = 3f;
    internal const float MaxClicksPerSecond = 15f;
    // After a catch the game holds the fish on screen until Release/Recycle (or its own timeout);
    // the aid presses Release after this many seconds so the next cast comes sooner.
    internal const float DefaultReleaseSeconds = 3.5f;
    internal const float MinReleaseSeconds = 0.2f;
    internal const float MaxReleaseSeconds = 5f;
    // Pause after a catch or a miss before casting again, so it reads like a person fishing.
    internal const float RecastDelaySeconds = 1f;
    // Blue (2026-10-17): at most 20 quick checks per search, then advise instead of searching on.
    internal const int MaxWaterChecks = 20;
    private const float ShortRange = 0.35f;
    private const float MediumRange = 0.6f;
    private const float LongRange = 1f;
    // Positive degrees turn left, negative turn right (seen from above, facing forward).
    // Round 3: the search covers all directions (water behind the player counts too); vanilla's cast
    // turns the player toward the chosen spot. Medium ring every 30 degrees, front first, then a short
    // and a long ring on the gaps: 20 checks, every heading within 15 degrees of one.
    private static readonly float[] MediumRingDegrees = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
    private static readonly float[] ShortRingDegrees = { 15f, -15f, 105f, -105f };
    private static readonly float[] LongRingDegrees = { 45f, -45f, 135f, -135f };
    // Walking further than this from where the player last cast by hand forgets that cast.
    internal const float MaxMetersFromCastSpot = 2f;
    internal const float ReelClickDebounceSeconds = 0.12f;

    // Mirrors FishingManager.TryClickFish: zone end = zone start + degree, wrapping past 360.
    internal static bool IsFishInZone(float fishDegrees, float zoneStartDegrees, float zoneDegree)
    {
        float zoneEnd = zoneStartDegrees + zoneDegree;
        return zoneEnd >= 360f
            ? fishDegrees >= zoneStartDegrees || fishDegrees <= zoneEnd - 360f
            : fishDegrees >= zoneStartDegrees && fishDegrees <= zoneEnd;
    }

    internal static bool ShouldForgetCast(bool hasRecordedCast, float metersFromCastSpot)
    {
        return hasRecordedCast && metersFromCastSpot > MaxMetersFromCastSpot;
    }

    // hasBait is false only when the player owns no bait at all: with Reusable Consumables on, owned
    // bait is put back after every bite and never runs out.
    internal static bool ShouldStartCast(
        bool enabled,
        bool rodOut,
        bool stateIsNone,
        bool fishShowing,
        bool isIdle,
        bool hasBait,
        float secondsIdle,
        bool lastAttemptFoundNoWater)
    {
        // After a search found no water the aid waits for the player: re-equip the rod (new search)
        // or cast once by hand (that spot is reused).
        return enabled && rodOut && stateIsNone && !fishShowing && isIdle && hasBait &&
               !lastAttemptFoundNoWater && secondsIdle >= RecastDelaySeconds;
    }

    // Aim points (x/z offsets from the player), at most 20, all directions within the rod's range.
    // The game's own CastingRay decides which one is water.
    internal static IReadOnlyList<CastOffset> CandidateCastOffsets(float forwardX, float forwardZ, float maxRange, float minRange)
    {
        float length = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
        float fx = length > 0.0001f ? forwardX / length : 0f;
        float fz = length > 0.0001f ? forwardZ / length : 1f;
        float max = Math.Max(minRange, maxRange);
        var offsets = new List<CastOffset>();
        var seen = new HashSet<long>();

        void Add(float degrees, float rangeFraction)
        {
            if (offsets.Count >= MaxWaterChecks)
            {
                return;
            }

            float distance = Math.Max(minRange, max * rangeFraction);
            double radians = degrees * Math.PI / 180d;
            float dx = (float)(fx * Math.Cos(radians) - fz * Math.Sin(radians));
            float dz = (float)(fx * Math.Sin(radians) + fz * Math.Cos(radians));
            var offset = new CastOffset(dx * distance, dz * distance);
            long key = ((long)Math.Round(offset.X * 100f) << 32) ^ (long)Math.Round(offset.Z * 100f);
            if (seen.Add(key))
            {
                offsets.Add(offset);
            }
        }

        foreach (float degrees in MediumRingDegrees)
        {
            Add(degrees, MediumRange);
        }

        foreach (float degrees in ShortRingDegrees)
        {
            Add(degrees, ShortRange);
        }

        foreach (float degrees in LongRingDegrees)
        {
            Add(degrees, LongRange);
        }

        return offsets;
    }

    internal static bool ShouldAnswerBite(bool enabled, bool stateIsFishing, bool biteAlert, float secondsSinceBite, float reactionSeconds)
    {
        return enabled && stateIsFishing && biteAlert && secondsSinceBite >= ClampReactionSeconds(reactionSeconds);
    }

    internal static bool ShouldClickReelRound(bool enabled, int counter, bool fishInZone, float secondsSinceLastClick)
    {
        return enabled && counter > 1 && fishInZone && secondsSinceLastClick >= ReelClickDebounceSeconds;
    }

    internal static bool ShouldClickLastRound(bool enabled, int counter, float secondsSinceLastClick, float clicksPerSecond)
    {
        return enabled && counter == 1 && secondsSinceLastClick >= 1f / ClampClicksPerSecond(clicksPerSecond);
    }

    internal static float ClampReactionSeconds(float seconds)
    {
        return float.IsNaN(seconds) ? DefaultReactionSeconds : Math.Max(MinReactionSeconds, Math.Min(MaxReactionSeconds, seconds));
    }

    internal static float ClampClicksPerSecond(float clicksPerSecond)
    {
        return float.IsNaN(clicksPerSecond) ? DefaultClicksPerSecond : Math.Max(MinClicksPerSecond, Math.Min(MaxClicksPerSecond, clicksPerSecond));
    }

    internal static float ClampReleaseSeconds(float seconds)
    {
        return float.IsNaN(seconds) ? DefaultReleaseSeconds : Math.Max(MinReleaseSeconds, Math.Min(MaxReleaseSeconds, seconds));
    }

    // Putting the rod away (H, Space, cancel, or the game closing it) ends a fishing session. Only the
    // out-to-away edge counts, so the session resets once, not every frame the rod stays away.
    internal static bool IsRodPutAway(bool rodWasOut, bool rodOut)
    {
        return rodWasOut && !rodOut;
    }

    // Presses the game's own Release/Recycle once per catch, after the player has had a moment to see it.
    internal static bool ShouldAutoRelease(bool enabled, bool fishShowing, bool alreadyReleased, float secondsShowing, float releaseSeconds)
    {
        return enabled && fishShowing && !alreadyReleased && secondsShowing >= ClampReleaseSeconds(releaseSeconds);
    }
}
