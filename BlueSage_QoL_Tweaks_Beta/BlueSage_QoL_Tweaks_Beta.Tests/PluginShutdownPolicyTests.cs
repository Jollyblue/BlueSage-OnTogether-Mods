using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PluginShutdownPolicyTests
{
    [Test]
    public void Destroy_and_application_quit_share_one_idempotent_shutdown_claim()
    {
        var policy = new PluginShutdownPolicy();

        Assert.Multiple(() =>
        {
            Assert.That(policy.TryBegin("application-quit"), Is.True);
            Assert.That(policy.TryBegin("destroy"), Is.False);
            Assert.That(policy.IsShuttingDown, Is.True);
            Assert.That(policy.Trigger, Is.EqualTo("application-quit"));
        });
    }

    [Test]
    public void Steam_and_local_cleanup_are_each_claimed_once_in_order()
    {
        var policy = new PluginShutdownPolicy();
        Assert.That(policy.TryBegin("destroy"), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(policy.TryClaimLocalCleanup(), Is.False, "Local cleanup waits until the Steam phase has finished or been skipped.");
            Assert.That(policy.TryClaimSteamCleanup(), Is.True);
            Assert.That(policy.TryClaimSteamCleanup(), Is.False);
        });

        policy.FinishSteamCleanup();
        Assert.That(policy.TryClaimLocalCleanup(), Is.True);
        Assert.That(policy.TryClaimLocalCleanup(), Is.False);
        policy.FinishLocalCleanup();

        Assert.Multiple(() =>
        {
            Assert.That(policy.IsComplete, Is.True);
            Assert.That(policy.TryBegin("third-entry"), Is.False);
        });
    }

    [Test]
    public void A_failed_or_skipped_Steam_cleanup_cannot_block_local_cleanup()
    {
        var policy = new PluginShutdownPolicy();
        Assert.That(policy.TryBegin("application-quit"), Is.True);
        Assert.That(policy.TryClaimSteamCleanup(), Is.True);

        policy.FinishSteamCleanup();

        Assert.That(policy.TryClaimLocalCleanup(), Is.True);
        policy.FinishLocalCleanup();
        Assert.That(policy.IsComplete, Is.True);
    }
}
