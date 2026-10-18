using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HostConsoleReadinessPolicyTests
{
    [Test]
    public void Ready_requires_host_and_converged_required_lanes()
    {
        HostConsoleSnapshot snapshot = HostConsoleReadinessPolicy.Resolve(
            isHost: true,
            rosterConverged: true,
            cleanupHealthy: true,
            reconnectHealthy: true,
            playerLimitHealthy: true,
            providerRequired: false,
            providerHealthy: false,
            relayRequired: false,
            relayHealthy: false);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.OverallState, Is.EqualTo(HostConsoleOverallState.Ready));
            Assert.That(snapshot.Reason, Is.EqualTo("All required host lanes are ready."));
            Assert.That(snapshot.ProviderState, Is.EqualTo("Optional / off"));
            Assert.That(snapshot.RelayState, Is.EqualTo("Optional / off"));
        });
    }

    [Test]
    public void Missing_host_truth_is_unavailable()
    {
        HostConsoleSnapshot snapshot = HostConsoleReadinessPolicy.Resolve(
            false, true, true, true, true, false, true, false, true);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.OverallState, Is.EqualTo(HostConsoleOverallState.Unavailable));
            Assert.That(snapshot.Reason, Does.Contain("verified lobby host"));
        });
    }

    [TestCase(false, true, true, "roster")]
    [TestCase(true, false, true, "cleanup")]
    [TestCase(true, true, false, "reconnect")]
    public void First_unhealthy_required_lane_is_named_without_identity(
        bool roster,
        bool cleanup,
        bool reconnect,
        string expectedReason)
    {
        HostConsoleSnapshot snapshot = HostConsoleReadinessPolicy.Resolve(
            true, roster, cleanup, reconnect, true, false, true, false, true);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.OverallState, Is.EqualTo(HostConsoleOverallState.Degraded));
            Assert.That(snapshot.Reason.ToLowerInvariant(), Does.Contain(expectedReason));
            Assert.That(snapshot.Reason, Does.Not.Contain("7656"));
            Assert.That(snapshot.Reason, Does.Not.Contain("\\"));
        });
    }

    [Test]
    public void Required_optional_lane_blocks_only_when_marked_required()
    {
        HostConsoleSnapshot optional = HostConsoleReadinessPolicy.Resolve(
            true, true, true, true, true, false, false, false, false);
        HostConsoleSnapshot required = HostConsoleReadinessPolicy.Resolve(
            true, true, true, true, true, true, false, false, false);

        Assert.Multiple(() =>
        {
            Assert.That(optional.OverallState, Is.EqualTo(HostConsoleOverallState.Ready));
            Assert.That(required.OverallState, Is.EqualTo(HostConsoleOverallState.Degraded));
            Assert.That(required.Reason.ToLowerInvariant(), Does.Contain("provider"));
        });
    }
}
