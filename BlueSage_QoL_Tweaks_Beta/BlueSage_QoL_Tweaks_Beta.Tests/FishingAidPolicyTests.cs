using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Accessibility fishing aid (Blue, 2026-10-17): for players with sore or unsteady hands. Default OFF.
// After one cast by hand it repeats that cast, answers bites, plays the mini-game, and recasts.
// The zone test mirrors vanilla FishingManager.TryClickFish exactly, so it only clicks when the game
// itself would count a hit (a miss would shrink the zone).
public sealed class FishingAidPolicyTests
{
    [Test]
    public void Fishing_aid_is_off_for_fresh_installs()
    {
        Assert.That(FishingAidPolicy.DefaultEnabled, Is.False);
    }

    [TestCase(100f, 90f, 30f, true, TestName = "Inside_a_normal_zone")]
    [TestCase(85f, 90f, 30f, false, TestName = "Before_a_normal_zone")]
    [TestCase(125f, 90f, 30f, false, TestName = "After_a_normal_zone")]
    [TestCase(90f, 90f, 30f, true, TestName = "Exactly_on_the_zone_start")]
    [TestCase(120f, 90f, 30f, true, TestName = "Exactly_on_the_zone_end")]
    [TestCase(355f, 340f, 40f, true, TestName = "Wrapping_zone_before_360")]
    [TestCase(10f, 340f, 40f, true, TestName = "Wrapping_zone_after_0")]
    [TestCase(30f, 340f, 40f, false, TestName = "Past_a_wrapping_zone")]
    [TestCase(300f, 340f, 40f, false, TestName = "Before_a_wrapping_zone")]
    public void Zone_test_matches_the_games_own_hit_rule(float fish, float zone, float degree, bool expected)
    {
        Assert.That(FishingAidPolicy.IsFishInZone(fish, zone, degree), Is.EqualTo(expected));
    }

    // Blue (2026-10-17): no first cast needed. Equip the rod with the game's fishing key (H) near
    // water and the aid finds water and casts by itself.
    [Test]
    public void Aid_casts_without_a_hand_cast_once_the_rod_is_out_and_the_player_stands_still()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.ShouldStartCast(true, rodOut: true, stateIsNone: true, fishShowing: false, isIdle: true, hasBait: true, secondsIdle: 2f, lastAttemptFoundNoWater: false), Is.True);
            Assert.That(FishingAidPolicy.ShouldStartCast(false, true, true, false, true, true, 2f, false), Is.False, "Off.");
            Assert.That(FishingAidPolicy.ShouldStartCast(true, false, true, false, true, true, 2f, false), Is.False, "Rod put away.");
            Assert.That(FishingAidPolicy.ShouldStartCast(true, true, false, false, true, true, 2f, false), Is.False, "Already fishing.");
            Assert.That(FishingAidPolicy.ShouldStartCast(true, true, true, true, true, true, 2f, false), Is.False, "Still showing the catch.");
            Assert.That(FishingAidPolicy.ShouldStartCast(true, true, true, false, false, true, 2f, false), Is.False, "Walking.");
            Assert.That(FishingAidPolicy.ShouldStartCast(true, true, true, false, true, false, 2f, false), Is.False, "No bait owned at all.");
            Assert.That(FishingAidPolicy.ShouldStartCast(true, true, true, false, true, true, 0.2f, false), Is.False, "Short pause between casts.");
            Assert.That(FishingAidPolicy.ShouldStartCast(true, true, true, false, true, true, 60f, true), Is.False, "After a search finds no water it waits for the player (re-equip or cast by hand).");
        });
    }

    // Blue (2026-10-17): at most ~20 quick checks. Medium distance first: straight ahead, then
    // alternating left/right in 20 degree steps; then short, medium, long distances with small 10
    // degree pivots from right to left; then advise "no water nearby" instead of searching on.
    [Test]
    // Round 3: search order and full-circle coverage are pinned in FishingAidRound3Tests.
    public void Water_search_is_capped_at_twenty_quick_checks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.MaxWaterChecks, Is.EqualTo(20));
            Assert.That(FishingAidPolicy.CandidateCastOffsets(0f, 1f, 10f, 1f).Count, Is.EqualTo(20));
        });
    }

    [Test]
    public void Search_never_repeats_an_aim_point()
    {
        var offsets = FishingAidPolicy.CandidateCastOffsets(0f, 1f, 10f, 1f);
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (CastOffset o in offsets)
        {
            Assert.That(seen.Add(o.X.ToString("0.00") + "," + o.Z.ToString("0.00")), Is.True);
        }
    }

    [Test]
    public void Zero_forward_vector_still_searches()
    {
        Assert.That(FishingAidPolicy.CandidateCastOffsets(0f, 0f, 8f, 1f).Count, Is.GreaterThan(0));
    }

    private static float Sin(float degrees) => (float)System.Math.Sin(degrees * System.Math.PI / 180d);

    private static float Cos(float degrees) => (float)System.Math.Cos(degrees * System.Math.PI / 180d);

    private static void AssertOffset(CastOffset actual, float x, float z, string because)
    {
        Assert.That(actual.X, Is.EqualTo(x).Within(0.01f), because + " (x)");
        Assert.That(actual.Z, Is.EqualTo(z).Within(0.01f), because + " (z)");
    }

    [Test]
    public void Walking_away_forgets_the_recorded_cast()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.ShouldForgetCast(hasRecordedCast: true, metersFromCastSpot: 2.5f), Is.True);
            Assert.That(FishingAidPolicy.ShouldForgetCast(true, 1.0f), Is.False);
            Assert.That(FishingAidPolicy.ShouldForgetCast(false, 50f), Is.False);
        });
    }

    [Test]
    public void Bite_is_answered_after_the_reaction_delay()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.ShouldAnswerBite(true, stateIsFishing: true, biteAlert: true, secondsSinceBite: 0.5f, reactionSeconds: 0.4f), Is.True);
            Assert.That(FishingAidPolicy.ShouldAnswerBite(true, true, true, 0.2f, 0.4f), Is.False, "Too soon to react.");
            Assert.That(FishingAidPolicy.ShouldAnswerBite(true, true, false, 5f, 0.4f), Is.False, "No bite.");
            Assert.That(FishingAidPolicy.ShouldAnswerBite(false, true, true, 5f, 0.4f), Is.False, "Off.");
        });
    }

    [Test]
    public void Reel_rounds_click_only_inside_the_zone_and_never_twice_in_a_row()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.ShouldClickReelRound(true, counter: 3, fishInZone: true, secondsSinceLastClick: 1f), Is.True);
            Assert.That(FishingAidPolicy.ShouldClickReelRound(true, 2, true, 1f), Is.True);
            Assert.That(FishingAidPolicy.ShouldClickReelRound(true, 3, false, 1f), Is.False, "A miss would shrink the zone.");
            Assert.That(FishingAidPolicy.ShouldClickReelRound(true, 3, true, 0.05f), Is.False, "Debounce.");
            Assert.That(FishingAidPolicy.ShouldClickReelRound(true, 1, true, 1f), Is.False, "Last round uses its own rhythm.");
        });
    }

    [Test]
    public void Last_round_clicks_at_the_chosen_speed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.ShouldClickLastRound(true, counter: 1, secondsSinceLastClick: 0.11f, clicksPerSecond: 10f), Is.True);
            Assert.That(FishingAidPolicy.ShouldClickLastRound(true, 1, 0.05f, 10f), Is.False);
            Assert.That(FishingAidPolicy.ShouldClickLastRound(true, 2, 1f, 10f), Is.False);
        });
    }

    [TestCase(0.01f, 0.1f)]
    [TestCase(0.4f, 0.4f)]
    [TestCase(9f, 1.5f)]
    public void Reaction_delay_is_clamped(float requested, float expected)
    {
        Assert.That(FishingAidPolicy.ClampReactionSeconds(requested), Is.EqualTo(expected).Within(0.0001f));
    }

    [TestCase(1f, 3f)]
    [TestCase(10f, 10f)]
    [TestCase(99f, 15f)]
    public void Last_round_speed_is_clamped(float requested, float expected)
    {
        Assert.That(FishingAidPolicy.ClampClicksPerSecond(requested), Is.EqualTo(expected).Within(0.0001f));
    }
}
