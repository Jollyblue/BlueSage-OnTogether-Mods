using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage_QoL_Tweaks_Beta.Tests;

public sealed class UnifiedHelperPolicyTests
{
    private const string Owner = "76561198000000001";
    private const string OtherOwner = "76561198000000009";

    [Test]
    public void Migration_unions_both_legacy_lists_for_the_same_host()
    {
        string merged = UnifiedHelperPolicy.MergeForOwner(
            Owner,
            "76561198000000002",
            Owner,
            "76561198000000003",
            Owner,
            "76561198000000004,76561198000000003",
            Owner);

        Assert.That(merged, Is.EqualTo("76561198000000002,76561198000000003,76561198000000004"));
    }

    [Test]
    public void Migration_never_imports_permissions_from_another_host()
    {
        string merged = UnifiedHelperPolicy.MergeForOwner(
            Owner,
            string.Empty,
            string.Empty,
            "76561198000000003",
            OtherOwner,
            "76561198000000004",
            Owner);

        Assert.That(merged, Is.EqualTo("76561198000000004"));
    }

    [Test]
    public void Host_is_removed_from_the_helper_whitelist()
    {
        string merged = UnifiedHelperPolicy.MergeForOwner(
            Owner,
            Owner + ",76561198000000002",
            Owner,
            string.Empty,
            Owner,
            string.Empty,
            Owner);

        Assert.That(merged, Is.EqualTo("76561198000000002"));
    }
}
