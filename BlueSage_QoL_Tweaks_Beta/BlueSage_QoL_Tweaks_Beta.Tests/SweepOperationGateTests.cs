using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SweepOperationGateTests
{
    [Test]
    public void Overlapping_requests_are_rejected_until_the_active_operation_completes()
    {
        SweepOperationGate gate = new();
        int firstSequence = 0;

        Assert.Multiple(() =>
        {
            Assert.That(gate.TryStart("automatic timer", out firstSequence), Is.True);
            Assert.That(firstSequence, Is.EqualTo(1));
            Assert.That(gate.TryStart("manual hotkey", out int rejectedSequence), Is.False);
            Assert.That(rejectedSequence, Is.EqualTo(1));
            Assert.That(gate.IsRunning, Is.True);
            Assert.That(gate.ActiveReason, Is.EqualTo("automatic timer"));
        });

        gate.Complete(firstSequence);

        Assert.Multiple(() =>
        {
            Assert.That(gate.IsRunning, Is.False);
            Assert.That(gate.TryStart("manual hotkey", out int secondSequence), Is.True);
            Assert.That(secondSequence, Is.EqualTo(2));
        });
    }

    [Test]
    public void Stale_completion_cannot_release_a_newer_operation()
    {
        SweepOperationGate gate = new();
        Assert.That(gate.TryStart("first", out int firstSequence), Is.True);
        gate.Complete(firstSequence);
        Assert.That(gate.TryStart("second", out int secondSequence), Is.True);

        gate.Complete(firstSequence);

        Assert.Multiple(() =>
        {
            Assert.That(gate.IsRunning, Is.True);
            Assert.That(gate.ActiveSequence, Is.EqualTo(secondSequence));
            Assert.That(gate.ActiveReason, Is.EqualTo("second"));
        });
    }
}
