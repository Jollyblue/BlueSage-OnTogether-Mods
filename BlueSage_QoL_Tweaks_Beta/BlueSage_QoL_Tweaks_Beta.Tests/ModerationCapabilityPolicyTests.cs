using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ModerationCapabilityPolicyTests
{
    private const string Owner = "76561198000000001";

    [Test]
    public void Current_owner_lease_is_accepted()
    {
        string value = ModerationCapabilityPolicy.Build(Owner, 1_000);
        Assert.That(ModerationCapabilityPolicy.IsFresh(value, Owner, 1_005), Is.True);
    }

    [TestCase("76561198000000000|v3-memberdata|1000")]
    [TestCase("76561198000000001|v1|1000")]
    [TestCase("76561198000000001|v2")]
    [TestCase("76561198000000001|v2|1000")]
    [TestCase("garbage")]
    public void Wrong_owner_version_or_shape_fails_closed(string value)
    {
        Assert.That(ModerationCapabilityPolicy.IsFresh(value, Owner, 1_005), Is.False);
    }

    [Test]
    public void Stale_or_implausibly_future_lease_fails_closed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModerationCapabilityPolicy.IsFresh(ModerationCapabilityPolicy.Build(Owner, 1_000), Owner, 1_021), Is.False);
            Assert.That(ModerationCapabilityPolicy.IsFresh(ModerationCapabilityPolicy.Build(Owner, 1_006), Owner, 1_000), Is.False);
        });
    }
}
