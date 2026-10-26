using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SweepSettingsPolicyTests
{
    [Test]
    public void ClampIntervalMinutes_UsesFreshInstallMinimum_WhenConfiguredValueIsTooLow()
    {
        Assert.That(SweepSettingsPolicy.ClampIntervalMinutes(1), Is.EqualTo(5));
    }

    [Test]
    public void ClampIntervalMinutes_AllowsFiveMinuteEmergencyCadence()
    {
        Assert.That(SweepSettingsPolicy.ClampIntervalMinutes(5), Is.EqualTo(5));
    }

    [Test]
    public void DefaultIntervalMinutes_avoids_vanilla_overlap_and_still_sweeps_within_75_minutes()
    {
        // Round 4 Stage G (Blue, 2026-10-20, after Marbles' autosave freeze): the default stays clear of the game's
        // 30-minute cleanup and still adds a BlueSage sweep inside every 75 minutes. 15 was Blue's 2026-10-17 setting.
        Assert.Multiple(() =>
        {
            Assert.That(SweepSettingsPolicy.DefaultIntervalMinutes, Is.EqualTo(40));
            Assert.That(SweepSettingsPolicy.OverlapAvoidanceIntervalMinutes, Is.EqualTo(40));
            Assert.That(SweepSettingsPolicy.DefaultIntervalMinutes, Is.LessThanOrEqualTo(75));
            Assert.That(SweepSettingsPolicy.DefaultIntervalMinutes % 30, Is.Not.EqualTo(0), "must not share vanilla's 30-minute boundary");
        });
    }

    // A fresh install (no config file when the plugin starts) keeps today's default; only real older configs go
    // through the historical migrations.
    [Test]
    public void Fresh_install_keeps_the_forty_minute_default()
    {
        Assert.That(
            SweepSettingsPolicy.ResolveStartupIntervalMinutes(SweepSettingsPolicy.DefaultIntervalMinutes, freshInstall: true, legacyDefaultsMigrated: false, tenMinuteSafetyMigrated: false, overlapAvoidanceMigrated: false),
            Is.EqualTo(40));
    }

    [Test]
    public void Old_unmigrated_config_walks_every_historical_step_and_lands_on_forty()
    {
        // 30 -> 15 (v1) -> 10 (v2 safety) -> 40 (v4 overlap avoidance).
        Assert.That(SweepSettingsPolicy.ResolveStartupIntervalMinutes(30, freshInstall: false, legacyDefaultsMigrated: false, tenMinuteSafetyMigrated: false, overlapAvoidanceMigrated: false), Is.EqualTo(40));
    }

    [Test]
    public void Config_already_through_the_overlap_migration_keeps_the_historical_result()
    {
        Assert.That(SweepSettingsPolicy.ResolveStartupIntervalMinutes(30, freshInstall: false, legacyDefaultsMigrated: false, tenMinuteSafetyMigrated: false, overlapAvoidanceMigrated: true), Is.EqualTo(10));
    }

    [Test]
    public void Migrated_config_keeps_the_players_choice()
    {
        Assert.That(SweepSettingsPolicy.ResolveStartupIntervalMinutes(45, freshInstall: false, legacyDefaultsMigrated: true, tenMinuteSafetyMigrated: true, overlapAvoidanceMigrated: true), Is.EqualTo(45));
    }

    [TestCase(10)]
    [TestCase(15)]
    public void MigrateOverlapAvoidanceInterval_moves_the_two_migration_written_values_to_forty_once(int configured)
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepSettingsPolicy.MigrateOverlapAvoidanceInterval(configured, alreadyMigrated: false), Is.EqualTo(40));
            Assert.That(SweepSettingsPolicy.MigrateOverlapAvoidanceInterval(configured, alreadyMigrated: true), Is.EqualTo(configured), "a 10 or 15 chosen after the migration stays");
        });
    }

    [TestCase(5)]
    [TestCase(20)]
    [TestCase(45)]
    [TestCase(180)]
    public void MigrateOverlapAvoidanceInterval_preserves_every_other_value(int configured)
    {
        Assert.That(SweepSettingsPolicy.MigrateOverlapAvoidanceInterval(configured, alreadyMigrated: false), Is.EqualTo(configured));
    }

    [Test]
    public void Plugin_detects_a_fresh_install_before_binding_config()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("_configFileExistedAtStartup = File.Exists(Config.ConfigFilePath);"));
            Assert.That(plugin, Does.Contain("SweepSettingsPolicy.ResolveStartupIntervalMinutes(SweepIntervalMinutes.Value, freshInstall,"));
            Assert.That(
                plugin.IndexOf("_configFileExistedAtStartup = File.Exists(Config.ConfigFilePath);", System.StringComparison.Ordinal),
                Is.LessThan(plugin.IndexOf("Config.Bind(", System.StringComparison.Ordinal)));
        });
    }

    [Test]
    public void Historical_ten_minute_safety_migration_target_is_unchanged()
    {
        Assert.That(SweepSettingsPolicy.TenMinuteSafetyMigrationInterval, Is.EqualTo(10));
    }

    [Test]
    public void ClampIntervalMinutes_PreservesExistingThirtyMinuteValue()
    {
        Assert.That(SweepSettingsPolicy.ClampIntervalMinutes(30), Is.EqualTo(30));
    }

    [Test]
    public void ClampIntervalMinutes_AllowsSixtyMinuteValue()
    {
        Assert.That(SweepSettingsPolicy.ClampIntervalMinutes(60), Is.EqualTo(60));
    }

    [Test]
    public void ClampIntervalMinutes_UsesMaximum_WhenConfiguredValueIsTooHigh()
    {
        Assert.That(SweepSettingsPolicy.ClampIntervalMinutes(9999), Is.EqualTo(180));
    }

    [TestCase(30)]
    [TestCase(60)]
    public void MigrateLegacyDefaultIntervalMinutes_AdoptsFifteenMinuteDefaultOnce(int legacyDefault)
    {
        Assert.That(
            SweepSettingsPolicy.MigrateLegacyDefaultIntervalMinutes(legacyDefault, alreadyMigrated: false),
            Is.EqualTo(15));
    }

    [Test]
    public void MigrateLegacyDefaultIntervalMinutes_PreservesCustomAndPostMigrationChoices()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepSettingsPolicy.MigrateLegacyDefaultIntervalMinutes(45, alreadyMigrated: false), Is.EqualTo(45));
            Assert.That(SweepSettingsPolicy.MigrateLegacyDefaultIntervalMinutes(30, alreadyMigrated: true), Is.EqualTo(30));
            Assert.That(SweepSettingsPolicy.MigrateLegacyDefaultIntervalMinutes(60, alreadyMigrated: true), Is.EqualTo(60));
        });
    }

    [TestCase(-1, 5)]
    [TestCase(0, 5)]
    [TestCase(181, 180)]
    [TestCase(9999, 180)]
    public void MigrateLegacyDefaultIntervalMinutes_PersistsSafeClampRegardlessOfLedger(
        int configured,
        int expected)
    {
        Assert.That(
            SweepSettingsPolicy.MigrateLegacyDefaultIntervalMinutes(configured, alreadyMigrated: true),
            Is.EqualTo(expected));
    }

    [TestCase(5)]
    [TestCase(15)]
    [TestCase(45)]
    [TestCase(180)]
    public void MigrateTenMinuteSafetyInterval_ForcesEveryUpdaterToTenOnce(int configured)
    {
        Assert.That(
            SweepSettingsPolicy.MigrateTenMinuteSafetyInterval(configured, alreadyMigrated: false),
            Is.EqualTo(10));
    }

    [Test]
    public void MigrateTenMinuteSafetyInterval_PreservesIntentionalPostMigrationChoice()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepSettingsPolicy.MigrateTenMinuteSafetyInterval(5, alreadyMigrated: true), Is.EqualTo(5));
            Assert.That(SweepSettingsPolicy.MigrateTenMinuteSafetyInterval(15, alreadyMigrated: true), Is.EqualTo(15));
            Assert.That(SweepSettingsPolicy.MigrateTenMinuteSafetyInterval(45, alreadyMigrated: true), Is.EqualTo(45));
            Assert.That(SweepSettingsPolicy.MigrateTenMinuteSafetyInterval(180, alreadyMigrated: true), Is.EqualTo(180));
        });
    }

    [Test]
    public void MigrateAutomaticCleanupOptIn_PreservesBothEnabledAndDisabledChoices()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepSettingsPolicy.MigrateAutomaticCleanupOptIn(true, alreadyMigrated: false), Is.True);
            Assert.That(SweepSettingsPolicy.MigrateAutomaticCleanupOptIn(false, alreadyMigrated: false), Is.False);
            Assert.That(SweepSettingsPolicy.MigrateAutomaticCleanupOptIn(false, alreadyMigrated: true), Is.False);
            Assert.That(SweepSettingsPolicy.MigrateAutomaticCleanupOptIn(true, alreadyMigrated: true), Is.True);
        });
    }

    // Round 5 F: the automatic-sweep toast goes quiet once for existing installs; a later opt-in is kept.
    [Test]
    public void MigrateSweepNotification_QuietsOnceThenKeepsTheUsersChoice()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepSettingsPolicy.MigrateSweepNotification(true, alreadyMigrated: false), Is.False, "existing 0.2.6 install goes quiet");
            Assert.That(SweepSettingsPolicy.MigrateSweepNotification(false, alreadyMigrated: false), Is.False);
            Assert.That(SweepSettingsPolicy.MigrateSweepNotification(true, alreadyMigrated: true), Is.True, "turned back on after the migration stays on");
            Assert.That(SweepSettingsPolicy.MigrateSweepNotification(false, alreadyMigrated: true), Is.False);
        });
    }

    [Test]
    public void ShouldNotifyOnSweep_SilencesOnlyAutomaticSweeps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepSettingsPolicy.ShouldNotifyOnSweep(false, "automatic timer"), Is.False);
            Assert.That(SweepSettingsPolicy.ShouldNotifyOnSweep(true, "automatic timer"), Is.True);
            Assert.That(SweepSettingsPolicy.ShouldNotifyOnSweep(false, "manual hotkey"), Is.True, "a cleanup the user triggered always confirms");
            Assert.That(SweepSettingsPolicy.ShouldNotifyOnSweep(false, "manual command"), Is.True);
        });
    }
}
