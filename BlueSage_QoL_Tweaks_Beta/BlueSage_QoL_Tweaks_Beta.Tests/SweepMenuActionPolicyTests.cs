using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SweepMenuActionPolicyTests
{
    [Test]
    public void Compatibility_blocked_cleanup_presents_a_clear_standby_action()
    {
        SweepMenuActionState state = SweepMenuActionPolicy.Resolve(manualSweepAllowed: false);

        Assert.Multiple(() =>
        {
            Assert.That(state.Enabled, Is.False);
            Assert.That(state.Label, Is.EqualTo("BlueSage Cleanup: Standby"));
            Assert.That(
                state.Tooltip,
                Is.EqualTo("Another cleanup mod is already doing this, so BlueSage cleanup is resting. Automatic Cleanup and the game's own cleanup settings are unchanged."));
        });
    }

    [Test]
    public void Manual_cleanup_presents_an_enabled_break_glass_action_without_claiming_automatic_ownership()
    {
        SweepMenuActionState state = SweepMenuActionPolicy.Resolve(manualSweepAllowed: true);

        Assert.Multiple(() =>
        {
            Assert.That(state.Enabled, Is.True);
            Assert.That(state.Label, Is.EqualTo("Run Cleanup Now"));
            Assert.That(
                state.Tooltip,
                Is.EqualTo("Runs one local unused-asset cleanup now, even when Automatic Cleanup is off. It never deletes files, profiles, or mods and does not change the game's timer."));
        });
    }

    // Voice pass (2026-10-23): a refusal answers with the specific notice RunSweep gave, not a generic either/or.
    [TestCase(true, "", "Cleanup started.")]
    [TestCase(true, "Cleanup is already running.", "Cleanup started.")]
    [TestCase(false, "Cleanup is resting because another cleanup mod is already doing it.", "Cleanup is resting because another cleanup mod is already doing it.")]
    [TestCase(false, "Cleanup is already running.", "Cleanup is already running.")]
    [TestCase(false, "Cleanup couldn't start this time. Try again in a moment.", "Cleanup couldn't start this time. Try again in a moment.")]
    [TestCase(false, "", "Cleanup couldn't start this time. Try again in a moment.")]
    [TestCase(false, null, "Cleanup couldn't start this time. Try again in a moment.")]
    public void Completion_message_reports_the_specific_reason(bool started, string? refusalNotice, string expected)
    {
        Assert.That(SweepMenuActionPolicy.BuildCompletionMessage(started, refusalNotice!), Is.EqualTo(expected));
    }

    [Test]
    public void Cleanup_wording_follows_the_glossary()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepMenuActionPolicy.PausedNotice, Is.EqualTo("Cleanup is resting because another cleanup mod is already doing it."));
            Assert.That(SweepMenuActionPolicy.BusyNotice, Is.EqualTo("Cleanup is already running."));
            Assert.That(SweepMenuActionPolicy.RunFailedNotice, Is.EqualTo("Cleanup couldn't finish this time. Try again in a moment."));
            Assert.That(SweepMenuActionPolicy.CompletedNotice, Is.EqualTo("BlueSage cleanup is done."));
            Assert.That(SweepMenuActionPolicy.GameCleanupToast, Is.EqualTo("The game is running its own cleanup; a short pause is normal."));
            foreach (string text in new[] { SweepMenuActionPolicy.StartedMessage, SweepMenuActionPolicy.PausedNotice, SweepMenuActionPolicy.BusyNotice, SweepMenuActionPolicy.StartFailedNotice, SweepMenuActionPolicy.RunFailedNotice, SweepMenuActionPolicy.CompletedNotice, SweepMenuActionPolicy.GameCleanupToast })
            {
                Assert.That(text.ToLowerInvariant(), Does.Not.Contain("sweep").And.Not.Contain("tidy").And.Not.Contain("asset").And.Not.Contain("native").And.Not.Contain("vanilla"), text);
            }
        });
    }

    // NOISE 3 (voice pass 2026-10-23): the game's own cleanup toast follows NotifyOnSweep (off by default).
    [TestCase(false, false, false, TestName = "Game_cleanup_is_quiet_by_default")]
    [TestCase(false, true, true, TestName = "Game_cleanup_toasts_when_cleanup_notices_are_on")]
    [TestCase(true, true, false, TestName = "BlueSage_own_cleanup_never_double_toasts")]
    [TestCase(true, false, false, TestName = "BlueSage_own_cleanup_quiet_setting")]
    public void Game_cleanup_toast_needs_cleanup_notices_on(bool qolCreating, bool notifyOnCleanup, bool expected)
    {
        Assert.That(SweepMenuActionPolicy.ShouldToastGameCleanup(qolCreating, notifyOnCleanup), Is.EqualTo(expected));
    }

    // NOISE 5: a cleanup the player asked for answers when it fails mid-run; the automatic timer stays quiet.
    [TestCase("manual hotkey", true)]
    [TestCase("manual command", true)]
    [TestCase("QoL menu", true)]
    [TestCase("automatic timer", false)]
    public void Run_failure_answers_only_a_cleanup_the_player_asked_for(string trigger, bool expected)
    {
        Assert.That(SweepMenuActionPolicy.ShouldAnnounceRunFailure(trigger), Is.EqualTo(expected));
    }
}
