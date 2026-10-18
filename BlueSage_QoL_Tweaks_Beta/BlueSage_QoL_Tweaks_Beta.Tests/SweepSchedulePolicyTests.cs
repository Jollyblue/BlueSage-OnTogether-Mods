using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SweepSchedulePolicyTests
{
    [Test]
    public void Initial_deadline_uses_the_configured_interval()
    {
        SweepSchedulePolicy schedule = new();

        schedule.Start(nowSeconds: 100d, configuredMinutes: 10);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.IsInitialized, Is.True);
            Assert.That(schedule.DueSeconds, Is.EqualTo(700d));
            Assert.That(schedule.IsDue(699.999d), Is.False);
            Assert.That(schedule.IsDue(700d), Is.True);
        });
    }

    [Test]
    public void Shorter_interval_pulls_the_existing_deadline_forward_without_restarting_elapsed_time()
    {
        SweepSchedulePolicy schedule = new();
        schedule.Start(nowSeconds: 100d, configuredMinutes: 10);

        schedule.ReconcileInterval(nowSeconds: 220d, configuredMinutes: 5);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.AnchorSeconds, Is.EqualTo(100d));
            Assert.That(schedule.DueSeconds, Is.EqualTo(400d));
            Assert.That(schedule.IsDue(399.999d), Is.False);
            Assert.That(schedule.IsDue(400d), Is.True);
        });
    }

    [Test]
    public void Shorter_interval_that_is_already_overdue_fires_on_the_next_poll()
    {
        SweepSchedulePolicy schedule = new();
        schedule.Start(nowSeconds: 100d, configuredMinutes: 10);

        schedule.ReconcileInterval(nowSeconds: 450d, configuredMinutes: 5);

        Assert.That(schedule.IsDue(450d), Is.True);
    }

    [Test]
    public void Longer_interval_does_not_postpone_the_current_deadline()
    {
        SweepSchedulePolicy schedule = new();
        schedule.Start(nowSeconds: 100d, configuredMinutes: 10);

        schedule.ReconcileInterval(nowSeconds: 220d, configuredMinutes: 60);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.AnchorSeconds, Is.EqualTo(100d));
            Assert.That(schedule.DueSeconds, Is.EqualTo(700d));
            Assert.That(schedule.ConfiguredIntervalMinutes, Is.EqualTo(60));
        });
    }

    [Test]
    public void Completed_automatic_sweep_starts_the_next_interval_from_completion()
    {
        SweepSchedulePolicy schedule = new();
        schedule.Start(nowSeconds: 100d, configuredMinutes: 10);
        schedule.ReconcileInterval(nowSeconds: 220d, configuredMinutes: 60);

        schedule.MarkAutomaticAttemptFinished(nowSeconds: 705d, configuredMinutes: 60);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.AnchorSeconds, Is.EqualTo(705d));
            Assert.That(schedule.DueSeconds, Is.EqualTo(4305d));
            Assert.That(schedule.IsDue(4304.999d), Is.False);
        });
    }

    [Test]
    public void Reset_for_disable_then_enable_creates_one_fresh_deadline()
    {
        SweepSchedulePolicy schedule = new();
        schedule.Start(nowSeconds: 100d, configuredMinutes: 10);

        schedule.Reset();
        schedule.Start(nowSeconds: 1000d, configuredMinutes: 5);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.AnchorSeconds, Is.EqualTo(1000d));
            Assert.That(schedule.DueSeconds, Is.EqualTo(1300d));
        });
    }
}
