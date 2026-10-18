using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxModeControllerTests
{
    [Test]
    public void SetMode_changes_the_single_active_mode_and_records_the_transition_time()
    {
        var controller = new WorkRelaxModeController(WorkRelaxMode.Work, 1000L, 10d);

        WorkRelaxModeChange change = controller.SetMode(WorkRelaxMode.Relax, 2000L, 20d);

        Assert.Multiple(() =>
        {
            Assert.That(change.Changed, Is.True);
            Assert.That(change.Previous, Is.EqualTo(WorkRelaxMode.Work));
            Assert.That(change.Current, Is.EqualTo(WorkRelaxMode.Relax));
            Assert.That(change.UtcTicks, Is.EqualTo(2000L));
            Assert.That(change.MonotonicSeconds, Is.EqualTo(20d));
            Assert.That(controller.CurrentMode, Is.EqualTo(WorkRelaxMode.Relax));
        });
    }

    [Test]
    public void SetMode_is_idempotent_when_the_requested_mode_is_already_active()
    {
        var controller = new WorkRelaxModeController(WorkRelaxMode.Work, 1000L, 10d);
        controller.SetMode(WorkRelaxMode.Relax, 2000L, 20d);

        WorkRelaxModeChange repeated = controller.SetMode(WorkRelaxMode.Relax, 3000L, 30d);

        Assert.Multiple(() =>
        {
            Assert.That(repeated.Changed, Is.False);
            Assert.That(repeated.Previous, Is.EqualTo(WorkRelaxMode.Relax));
            Assert.That(repeated.Current, Is.EqualTo(WorkRelaxMode.Relax));
            Assert.That(controller.CurrentMode, Is.EqualTo(WorkRelaxMode.Relax));
        });
    }

    [TestCase(0)]
    [TestCase(3)]
    public void Constructor_rejects_values_outside_the_exact_mode_domain(int value)
    {
        Assert.That(
            () => new WorkRelaxModeController((WorkRelaxMode)value, 1000L, 10d),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void SetMode_rejects_values_outside_the_exact_mode_domain_without_mutating_state()
    {
        var controller = new WorkRelaxModeController(WorkRelaxMode.Work, 1000L, 10d);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => controller.SetMode((WorkRelaxMode)99, 2000L, 20d),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(controller.CurrentMode, Is.EqualTo(WorkRelaxMode.Work));
        });
    }
}
