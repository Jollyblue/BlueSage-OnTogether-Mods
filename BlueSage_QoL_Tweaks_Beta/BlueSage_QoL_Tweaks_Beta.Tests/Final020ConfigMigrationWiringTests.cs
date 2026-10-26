using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class Final020ConfigMigrationWiringTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void ExistingProfilesReceiveEachFinalDefaultMigrationExactlyOnce()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Not.Contain("ConfigMigrationTarget ="));
            Assert.That(plugin, Does.Contain("HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.FinalDefaultsV2Migration)"));
            Assert.That(plugin, Does.Contain("HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.SweepFifteenMinuteV1Migration)"));
            Assert.That(plugin, Does.Contain("HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.SweepTenMinuteSafetyV2Migration)"));
            Assert.That(plugin, Does.Contain("HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.SweepAutomaticOptInV3Migration)"));
            Assert.That(plugin, Does.Contain("MigrateOldDefaultInt(ChatHistoryRows.Value, 100, ChatHistoryLimitPolicy.DefaultRows, finalDefaultsMigrated)"));
            Assert.That(plugin, Does.Contain("MigrateOldDefaultString(BlackChatOutlineColor.Value, \"FFFFFF\", \"C0C0C0\", finalDefaultsMigrated)"));
            Assert.That(plugin, Does.Contain("MigrateOldDefaultBool(Use24HourTime.Value, true, false, finalDefaultsMigrated)"));
            Assert.That(plugin, Does.Contain("MigrateOldDefaultBool(EnableExtendedAvatarStyles.Value, false, true, finalDefaultsMigrated)"));
            Assert.That(plugin, Does.Contain("MigrateOldDefaultBool(EnableChalkboardPersistence.Value, false, true, finalDefaultsMigrated)"));
            Assert.That(plugin, Does.Contain("SweepSettingsPolicy.ResolveStartupIntervalMinutes(SweepIntervalMinutes.Value, freshInstall, sweepDefaultsMigrated, sweepTenMinuteSafetyMigrated, sweepOverlapAvoidanceMigrated)"));
            Assert.That(plugin, Does.Contain("MigrateAutomaticCleanupOptIn(EnableAutoSweep.Value, sweepAutomaticOptInMigrated)"));
            Assert.That(File.ReadAllText(Path.Combine(Root, "src", "Core", "SweepSettingsPolicy.cs")), Does.Contain("MigrateTenMinuteSafetyInterval(migrated, tenMinuteSafetyMigrated)"), "Older configs still get the safety step; fresh installs skip it.");
            Assert.That(plugin, Does.Not.Contain("MigrateTenMinuteSafetyInterval(migrated, sweepAutomaticOptInMigrated)"));
            Assert.That(plugin, Does.Contain("string migrationLedger = ConfigMigrationPolicy.RecordMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.FinalDefaultsV2Migration)"));
            Assert.That(plugin, Does.Contain("migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepFifteenMinuteV1Migration)"));
            Assert.That(plugin, Does.Contain("migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepTenMinuteSafetyV2Migration)"));
            Assert.That(plugin, Does.Contain("migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepAutomaticOptInV3Migration)"));
            Assert.That(plugin, Does.Contain("ConfigMigrationVersion.Value = migrationLedger"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
