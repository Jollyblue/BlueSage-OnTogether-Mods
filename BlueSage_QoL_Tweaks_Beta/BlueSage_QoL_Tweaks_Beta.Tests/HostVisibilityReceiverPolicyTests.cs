using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HostVisibilityReceiverPolicyTests
{
    [Test]
    public void LocalOwnerNeverConsumesItsOwnRemoteVisibilityHeartbeat()
    {
        Assert.That(HostVisibilityReceiverPolicy.ShouldConsumeRemoteOwnerState("76561198000000001", "76561198000000001"), Is.False);
    }

    [Test]
    public void SeparatePublicQolObserverMayConsumeTheOwnerHeartbeat()
    {
        Assert.That(HostVisibilityReceiverPolicy.ShouldConsumeRemoteOwnerState("76561198000000001", "76561198000000002"), Is.True);
    }

    [TestCase("", "76561198000000002")]
    [TestCase("76561198000000001", "")]
    public void MissingIdentityFailsClosed(string ownerSteamId, string localSteamId)
    {
        Assert.That(HostVisibilityReceiverPolicy.ShouldConsumeRemoteOwnerState(ownerSteamId, localSteamId), Is.False);
    }
}
