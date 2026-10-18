using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HostModerationSurfacePolicyTests
{
    [Test]
    public void Host_loss_disables_destructive_action_immediately()
    {
        HostModerationSurfaceDecision decision =
            HostModerationSurfacePolicy.Evaluate(
                isHost: false,
                exactTargetReady: true,
                isSelf: false,
                confirmationArmed: true);

        Assert.Multiple(() =>
        {
            Assert.That(decision.CanExecuteHostRemove, Is.False);
            Assert.That(decision.CanArmHostRemove, Is.False);
            Assert.That(decision.CanInspectNativeActions, Is.True);
        });
    }

    [Test]
    public void Exact_nonself_host_target_requires_separate_confirmation()
    {
        HostModerationSurfaceDecision preview =
            HostModerationSurfacePolicy.Evaluate(true, true, false, false);
        HostModerationSurfaceDecision confirmed =
            HostModerationSurfacePolicy.Evaluate(true, true, false, true);

        Assert.Multiple(() =>
        {
            Assert.That(preview.CanArmHostRemove, Is.True);
            Assert.That(preview.CanExecuteHostRemove, Is.False);
            Assert.That(confirmed.CanExecuteHostRemove, Is.True);
        });
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void Self_or_ambiguous_target_never_enables_host_remove(
        bool exact,
        bool self)
    {
        HostModerationSurfaceDecision decision =
            HostModerationSurfacePolicy.Evaluate(true, exact, self, true);

        Assert.That(decision.CanExecuteHostRemove, Is.False);
    }
}
