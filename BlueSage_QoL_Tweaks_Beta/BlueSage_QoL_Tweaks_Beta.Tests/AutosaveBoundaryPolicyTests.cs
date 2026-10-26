using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage G (Marbles, 2026-10-20): the game autosaves every 180 s on the main thread. A BlueSage sweep that
// starts right before or right after that frame stacks two stalls into one. No fixed interval avoids it (900 s is
// 5 x 180 s), so the guard reads the game's own save timer at the moment the sweep is due.
public sealed class AutosaveBoundaryPolicyTests
{
    [Test]
    public void Defaults_match_the_games_180_second_autosave()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutosaveBoundaryPolicy.AutosavePeriodSeconds, Is.EqualTo(180f));
            Assert.That(AutosaveBoundaryPolicy.BeforeWindowSeconds, Is.EqualTo(30f));
            Assert.That(AutosaveBoundaryPolicy.AfterWindowSeconds, Is.EqualTo(20f));
        });
    }

    [TestCase(150f)]
    [TestCase(160f)]
    [TestCase(179.9f)]
    [TestCase(180f)]
    public void Defers_when_the_autosave_is_about_to_fire(float saveTimerSeconds)
    {
        Assert.That(AutosaveBoundaryPolicy.ShouldDefer(saveTimerSeconds), Is.True);
    }

    [TestCase(0f)]
    [TestCase(5f)]
    [TestCase(19.9f)]
    public void Defers_right_after_an_autosave_while_the_backup_coroutine_still_writes(float saveTimerSeconds)
    {
        Assert.That(AutosaveBoundaryPolicy.ShouldDefer(saveTimerSeconds), Is.True);
    }

    [TestCase(20f)]
    [TestCase(60f)]
    [TestCase(149.9f)]
    public void Runs_in_the_quiet_middle_of_the_autosave_period(float saveTimerSeconds)
    {
        Assert.That(AutosaveBoundaryPolicy.ShouldDefer(saveTimerSeconds), Is.False);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    public void Unknown_timer_never_blocks_the_sweep(float saveTimerSeconds)
    {
        Assert.That(AutosaveBoundaryPolicy.ShouldDefer(saveTimerSeconds), Is.False);
    }

    [Test]
    public void Custom_windows_are_honoured()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AutosaveBoundaryPolicy.ShouldDefer(90f, periodSeconds: 100f, beforeSeconds: 10f, afterSeconds: 5f), Is.True);
            Assert.That(AutosaveBoundaryPolicy.ShouldDefer(4f, periodSeconds: 100f, beforeSeconds: 10f, afterSeconds: 5f), Is.True);
            Assert.That(AutosaveBoundaryPolicy.ShouldDefer(50f, periodSeconds: 100f, beforeSeconds: 10f, afterSeconds: 5f), Is.False);
        });
    }
}
