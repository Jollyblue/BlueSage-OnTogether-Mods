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
                Is.EqualTo("A compatibility guard currently blocks manual cleanup. Automatic Cleanup and the game's native cleanup settings are unchanged."));
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

    [TestCase(true, "Asset cleanup started.")]
    [TestCase(false, "Asset cleanup did not start because compatibility blocked cleanup or another cleanup is already in progress.")]
    public void Completion_message_reports_whether_cleanup_really_started(bool started, string expected)
    {
        Assert.That(SweepMenuActionPolicy.BuildCompletionMessage(started), Is.EqualTo(expected));
    }
}
