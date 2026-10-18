using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RescueReceiverCapabilityPolicyTests
{
    [Test]
    public void CapabilityHeartbeat_UsesStableMemberDataContractAndBoundedCadence()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RescueReceiverCapabilityPolicy.MemberDataKey, Is.EqualTo("bluesage_qol_rescue_receiver_v1"));
            Assert.That(RescueReceiverCapabilityPolicy.PublishIntervalSeconds, Is.GreaterThan(0).And.LessThanOrEqualTo(10));
            Assert.That(RescueReceiverCapabilityPolicy.MaximumAgeSeconds, Is.EqualTo(15));
            Assert.That(
                RescueReceiverCapabilityPolicy.BuildPayload("0.2.0", 1700000000),
                Is.EqualTo("v1|0.2.0|1700000000"));
        });
    }

    [Test]
    public void CapabilityHeartbeat_FreshnessRejectsMalformedStaleAndFarFuturePayloads()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RescueReceiverCapabilityPolicy.IsFresh("v1|0.2.0|1700000000", "0.2.0", 1700000015), Is.True);
            Assert.That(RescueReceiverCapabilityPolicy.IsFresh("v1|0.2.0|1700000000", "0.2.0", 1700000016), Is.False);
            Assert.That(RescueReceiverCapabilityPolicy.IsFresh("v1|0.2.0|1700000002", "0.2.0", 1700000000), Is.True);
            Assert.That(RescueReceiverCapabilityPolicy.IsFresh("v1|0.2.0|1700000003", "0.2.0", 1700000000), Is.False);
            Assert.That(RescueReceiverCapabilityPolicy.IsFresh("v1|0.1.9|1700000000", "0.2.0", 1700000000), Is.False);
            Assert.That(RescueReceiverCapabilityPolicy.IsFresh("not-a-heartbeat", "0.2.0", 1700000000), Is.False);
        });
    }

    [Test]
    public void PluginPublishesHeartbeatFromCoroutineOnlyWhileReceiverIsAvailableAndClearsOnDestroy()
    {
        string root = FindRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string rescuePatch = File.ReadAllText(Path.Combine(root, "src", "Patches", "OptInRescuePatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("StartRescueReceiverCapabilityPublisher();"));
            Assert.That(plugin, Does.Contain("RescueReceiverCapabilityLoop()"));
            Assert.That(plugin, Does.Contain("new WaitForSecondsRealtime(RescueReceiverCapabilityPolicy.PublishIntervalSeconds)"));
            Assert.That(plugin, Does.Contain("Patches.OptInRescuePatch.IsReceiverAvailable"));
            Assert.That(plugin, Does.Contain("SteamMatchmaking.SetLobbyMemberData(lobby, RescueReceiverCapabilityPolicy.MemberDataKey, payload)"));
            Assert.That(plugin, Does.Contain("ClearRescueReceiverCapability();"));
            Assert.That(rescuePatch, Does.Contain("internal static bool IsReceiverAvailable"));
        });
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
