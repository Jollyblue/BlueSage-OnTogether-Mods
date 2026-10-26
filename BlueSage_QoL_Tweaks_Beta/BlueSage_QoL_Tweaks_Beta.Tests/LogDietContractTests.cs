using NUnit.Framework;
using System;
using System.IO;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 C1 + F (audit of Blue's 37.5 h public host, 2026-10-22): the three repeating log families and the
// 40-minute toast. These pins keep the runtime on the policies the unit tests prove.
[TestFixture]
internal sealed class LogDietContractTests
{
    private static string ReadSource(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
        return File.ReadAllText(Path.Combine(root, "src", relative));
    }

    [Test]
    public void Clone_shield_wait_logging_uses_the_hysteresis_policy_not_a_minute_throttle()
    {
        string source = ReadSource("CloneShieldController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("StableStateLogPolicy.Observe(ref _alignmentLogState, \"waiting\", remindWhileHeld: true"));
            Assert.That(source, Does.Contain("StableStateLogPolicy.Observe(ref _alignmentLogState, \"aligned\", remindWhileHeld: false"));
            Assert.That(source, Does.Contain("Clone Shield still waiting after "));
            Assert.That(source, Does.Contain("StableStateLogPolicy.Reset(ref _alignmentLogState)"), "lobby change forgets the episode");
            Assert.That(source, Does.Not.Contain("_nextAlignmentWaitLogAt"));
            Assert.That(source, Does.Not.Contain("Further identical waits are suppressed for 60 seconds"));
        });
    }

    [Test]
    public void Host_health_heartbeat_line_is_written_on_change_only_without_the_lobby_count()
    {
        string source = ReadSource("Plugin.cs");
        int start = source.IndexOf("private static void RefreshLobbyMetadataHeartbeat(", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0));
        string body = source.Substring(start, Math.Min(2600, source.Length - start));
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("HostHealthHeartbeatLogPolicy.BuildKey(heartbeatCompatOk, refreshedSurfaceKeys, playerLimitText)"));
            Assert.That(body, Does.Contain("HostHealthHeartbeatLogPolicy.ShouldLog(_lastHostHealthHeartbeatLogKey, heartbeatLogKey)"));
            Assert.That(body, Does.Contain("Host Health H1: refreshed Steam lobby heartbeat metadata at"));
        });
    }

    [Test]
    public void Automatic_sweep_toast_is_off_by_default_migrated_once_and_manual_cleanups_still_confirm()
    {
        string plugin = ReadSource("Plugin.cs");
        string policy = ReadSource(Path.Combine("Core", "ConfigMigrationPolicy.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("NotifyOnSweep = Config.Bind(\"Asset Sweep\", \"NotifyOnSweep\", false,"));
            Assert.That(plugin, Does.Contain("SweepSettingsPolicy.MigrateSweepNotification(NotifyOnSweep.Value, freshInstall || sweepNotifyQuietMigrated)"));
            Assert.That(plugin, Does.Contain("ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepNotifyQuietV5Migration)"));
            Assert.That(plugin, Does.Contain("SweepSettingsPolicy.ShouldNotifyOnSweep(NotifyOnSweep.Value, reason)"));
            Assert.That(policy, Does.Contain("SweepNotifyQuietV5Migration = \"sweep-notify-quiet-v5\""));
        });
    }
}
