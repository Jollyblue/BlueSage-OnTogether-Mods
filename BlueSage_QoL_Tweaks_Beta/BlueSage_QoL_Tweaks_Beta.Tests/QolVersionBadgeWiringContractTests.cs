using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5c (Blue, 2026-10-23): "[U]" keeps its compact token; the long text reads "QoL vX.Y.Z User" from the
// peer's own published version, or "Pre-indicator QoL User" for 0.2.3-0.2.7 peers. Wiring proofs only; the
// decisions are behavior-tested in CompatibleClientPresencePolicyTests and PlayerBadgePolicyTests.
public sealed class QolVersionBadgeWiringContractTests
{
    [Test]
    public void Version_rides_on_its_own_member_data_key_beside_the_unchanged_v1_heartbeat()
    {
        string publisher = ReadSource(Path.Combine("Patches", "PlayerRoleLabelController.cs"));
        string publish = Slice(publisher, "private static void PublishCompatibleClientHeartbeat()", "private static void ClearCompatibleClientHeartbeat()");
        string clear = Slice(publisher, "private static void ClearCompatibleClientHeartbeat()", "_compatibleLobbyKey = 0;");
        string policy = ReadSource(Path.Combine("Core", "CompatibleClientPresencePolicy.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(publish, Does.Contain("CompatibleClientPresencePolicy.MemberDataKey,"), "The v1 heartbeat still goes out unchanged.");
            Assert.That(publish, Does.Contain("CompatibleClientPresencePolicy.TryBuildVersionPayload("));
            Assert.That(publish, Does.Contain("Plugin.PluginVersion,"), "The local client publishes its own PluginVersion.");
            Assert.That(publish, Does.Contain("CompatibleClientPresencePolicy.VersionMemberDataKey,"));
            Assert.That(publish.IndexOf("CompatibleClientPresencePolicy.VersionMemberDataKey,", StringComparison.Ordinal), Is.LessThan(publish.IndexOf("CompatibleClientPresencePolicy.MemberDataKey,\n                payload);", StringComparison.Ordinal)), "Version first, then the heartbeat: a peer polling in between never sees a fresh heartbeat without a version.");
            Assert.That(publish, Does.Contain("Time.unscaledTime >= _nextVersionPublishAt &&"), "The constant version is written on lobby entry and every 30 s, not every 2 s with the heartbeat.");
            Assert.That(publisher, Does.Contain("private const float VersionPublishIntervalSeconds = 30f;"));
            Assert.That(publish, Does.Contain("_nextVersionPublishAt = 0f;"), "A new lobby republishes the version at once.");
            Assert.That(clear, Does.Contain("CompatibleClientPresencePolicy.VersionMemberDataKey,"), "Unload clears the version key too.");
            string shutdown = Slice(publisher, "snapshot.SteamWasReady &&", "_localHostLabelOverride");
            Assert.That(shutdown, Does.Contain("CompatibleClientPresencePolicy.VersionMemberDataKey,"), "The shutdown path clears the version key with the heartbeat (review finding 1).");
            Assert.That(policy, Does.Contain("payload = ProtocolVersion.ToString(CultureInfo.InvariantCulture) +"), "The v1 payload shape is untouched: 0.2.3-0.2.7 parsers reject a third field.");
            Assert.That(policy, Does.Contain("parts.Length != 2 ||"));
        });
    }

    [Test]
    public void Badge_controller_reads_the_peer_version_and_never_substitutes_the_local_one()
    {
        string controller = ReadSource(Path.Combine("Patches", "PlayerBadgeController.cs"));
        string evidence = Slice(controller, "private static bool TryBuildEvidence(", "private static bool TryReadCreatorContribution(");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("CompatibleClientVersionsBySteamId[steamId] ="), "Read once per member in the presentation pass.");
            Assert.That(controller, Does.Contain("CompatibleClientVersionsBySteamId.Clear();"));
            Assert.That(controller, Does.Contain("CompatibleClientPresencePolicy.VersionMemberDataKey) ?? string.Empty;"));
            Assert.That(evidence, Does.Contain("compatiblePresentationRow: true,\n                compatibleClientVersion: GetCompatibleClientVersion("), "The presentation-only row carries the peer version.");
            Assert.That(evidence, Does.Contain("compatibleClientVersion:\n                compatibleFresh\n                    ? GetCompatibleClientVersion("), "The exact row carries the peer version only while the heartbeat is fresh.");
            Assert.That(controller, Does.Not.Contain("compatibleClientVersion: Plugin.PluginVersion"), "Never label a peer with the local version.");
            Assert.That(controller, Does.Not.Contain("compatibleClientVersion:\n                Plugin.PluginVersion"));
        });
    }

    private static string ReadSource(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", relative)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
