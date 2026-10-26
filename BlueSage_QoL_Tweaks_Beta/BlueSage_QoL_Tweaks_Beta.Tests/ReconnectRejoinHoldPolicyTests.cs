using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Community-reported (2026-10-17): Reconnect Guard rejoined the saved lobby the instant the
// clean menu was confirmed. The host can still be counting the player's old connection at that
// moment, so the rejoin double-counts them -- wrong host/lobby player counts and the known
// non-joinable lobby bug. The rejoin now waits a deliberate, player-adjustable hold first.
public sealed class ReconnectRejoinHoldPolicyTests
{
    [Test]
    public void Default_hold_is_long_enough_for_the_host_to_drop_the_old_connection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReconnectRejoinHoldPolicy.DefaultHoldSeconds, Is.EqualTo(20));
            Assert.That(ReconnectRejoinHoldPolicy.MinimumHoldSeconds, Is.EqualTo(10));
            Assert.That(ReconnectRejoinHoldPolicy.MaximumHoldSeconds, Is.EqualTo(120));
        });
    }

    [TestCase(0, 10)]
    [TestCase(-5, 10)]
    [TestCase(9, 10)]
    [TestCase(10, 10)]
    [TestCase(45, 45)]
    [TestCase(120, 120)]
    [TestCase(9999, 120)]
    public void Hold_is_clamped_so_it_can_never_be_turned_back_into_an_instant_rejoin(int configured, int expected)
    {
        Assert.That(ReconnectRejoinHoldPolicy.ClampHoldSeconds(configured), Is.EqualTo(expected));
    }

    [Test]
    public void Rejoin_waits_until_the_full_hold_has_elapsed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReconnectRejoinHoldPolicy.ShouldJoinNow(heldSeconds: 0f, holdSeconds: 20), Is.False);
            Assert.That(ReconnectRejoinHoldPolicy.ShouldJoinNow(heldSeconds: 19.9f, holdSeconds: 20), Is.False);
            Assert.That(ReconnectRejoinHoldPolicy.ShouldJoinNow(heldSeconds: 20f, holdSeconds: 20), Is.True);
        });
    }

    [Test]
    public void A_bad_saved_value_still_holds_at_least_the_minimum()
    {
        Assert.That(ReconnectRejoinHoldPolicy.ShouldJoinNow(heldSeconds: 5f, holdSeconds: 0), Is.False);
    }

    [Test]
    public void A_single_noisy_frame_does_not_cancel_the_reconnect()
    {
        // Advisor-caught bug (2026-10-17): the hold cancelled on the FIRST frame the clean-menu
        // check read not-ready. That check is noisy -- the original loop needs 3 consecutive ready
        // frames to trust it -- so one flicker silently killed a legitimate reconnect.
        float streak = ReconnectRejoinHoldPolicy.UpdateNotReadyStreak(0f, menuReady: false, deltaSeconds: 0.016f);
        Assert.That(ReconnectRejoinHoldPolicy.ShouldCancelHold(streak), Is.False);

        streak = ReconnectRejoinHoldPolicy.UpdateNotReadyStreak(streak, menuReady: true, deltaSeconds: 0.016f);
        Assert.That(streak, Is.EqualTo(0f), "A ready frame resets the streak.");
    }

    [Test]
    public void Leaving_the_menu_for_real_cancels_once_it_is_sustained()
    {
        float streak = 0f;
        for (int frame = 0; frame < 60; frame++)
        {
            streak = ReconnectRejoinHoldPolicy.UpdateNotReadyStreak(streak, menuReady: false, deltaSeconds: 0.016f);
        }

        Assert.That(ReconnectRejoinHoldPolicy.ShouldCancelHold(streak), Is.False, "~1s of not-ready is still within tolerance.");

        for (int frame = 0; frame < 40; frame++)
        {
            streak = ReconnectRejoinHoldPolicy.UpdateNotReadyStreak(streak, menuReady: false, deltaSeconds: 0.016f);
        }

        Assert.That(ReconnectRejoinHoldPolicy.ShouldCancelHold(streak), Is.True, "~1.6s continuously not-ready means the player really left.");
    }

    // Ultrareview #2-2/#2-3 (2026-10-17): these signals are definitive, not noisy, so they abort
    // the hold at once -- no 1.5s tolerance. EDGE-TRIGGERED (advisor, same day): the game's
    // IsConnecting was only ever logged before, and audit samples show it can read True with no
    // player action, so only a join that STARTS during the hold counts. A value already true when
    // the hold began must never block a legitimate reconnect.
    [Test]
    public void Turning_Reconnect_Guard_off_aborts_the_hold_immediately()
    {
        Assert.That(ReconnectRejoinHoldPolicy.ShouldAbortImmediately(guardEnabled: false, joinInProgressNow: false, joinInProgressAtHoldStart: false), Is.True);
    }

    [Test]
    public void A_join_that_starts_during_the_hold_aborts_it_immediately()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReconnectRejoinHoldPolicy.ShouldAbortImmediately(guardEnabled: true, joinInProgressNow: true, joinInProgressAtHoldStart: false), Is.True);
            Assert.That(ReconnectRejoinHoldPolicy.ShouldAbortImmediately(guardEnabled: true, joinInProgressNow: false, joinInProgressAtHoldStart: false), Is.False);
        });
    }

    [Test]
    public void A_join_flag_already_true_when_the_hold_began_never_blocks_the_reconnect()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReconnectRejoinHoldPolicy.ShouldAbortImmediately(guardEnabled: true, joinInProgressNow: true, joinInProgressAtHoldStart: true), Is.False);
            Assert.That(ReconnectRejoinHoldPolicy.IsSafeToJoinNow(guardEnabled: true, joinInProgressNow: true, joinInProgressAtHoldStart: true, menuReady: true), Is.True);
        });
    }

    [TestCase(true, false, false, true, true)]
    [TestCase(false, false, false, true, false)]
    [TestCase(true, true, false, true, false)]
    [TestCase(true, false, false, false, false)]
    public void Final_check_right_before_joining_requires_guard_on_no_new_player_join_and_a_clean_menu(
        bool guardEnabled, bool joinInProgressNow, bool joinInProgressAtHoldStart, bool menuReady, bool expected)
    {
        Assert.That(ReconnectRejoinHoldPolicy.IsSafeToJoinNow(guardEnabled, joinInProgressNow, joinInProgressAtHoldStart, menuReady), Is.EqualTo(expected));
    }

    [Test]
    public void Hold_notice_tells_the_player_how_long_and_why()
    {
        string notice = ReconnectRejoinHoldPolicy.BuildHoldNotice(20);

        Assert.Multiple(() =>
        {
            Assert.That(notice, Is.EqualTo("Reconnecting in 20s so the host can clear your old spot first."));
            Assert.That(ReconnectRejoinHoldPolicy.BuildHoldNotice(9999), Does.StartWith("Reconnecting in " + ReconnectRejoinHoldPolicy.ClampHoldSeconds(9999) + "s"), "The shown wait is the clamped one.");
        });
    }
}
