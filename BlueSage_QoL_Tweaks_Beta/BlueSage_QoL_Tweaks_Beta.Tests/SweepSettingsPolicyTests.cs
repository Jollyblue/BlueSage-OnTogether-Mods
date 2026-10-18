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
    public void DefaultIntervalMinutes_UsesBlues_fifteen_minute_cadence_for_fresh_installs()
    {
        // 2026-10-17 (Blue): fresh installs clean up every 15 minutes, his live UAT setting.
        Assert.That(SweepSettingsPolicy.DefaultIntervalMinutes, Is.EqualTo(15));
    }

    // Review 2026-10-17: fresh installs bound 15 and the old one-time safety migration rewrote it to
    // 10. A fresh install (no config file when the plugin starts) keeps today's default; only real
    // older configs go through the historical migrations.
    [Test]
    public void Fresh_install_keeps_the_fifteen_minute_default()
    {
        Assert.That(
            SweepSettingsPolicy.ResolveStartupIntervalMinutes(SweepSettingsPolicy.DefaultIntervalMinutes, freshInstall: true, legacyDefaultsMigrated: false, tenMinuteSafetyMigrated: false),
            Is.EqualTo(15));
    }

    [Test]
    public void Old_unmigrated_config_still_gets_the_historical_safety_migration()
    {
        Assert.That(SweepSettingsPolicy.ResolveStartupIntervalMinutes(30, freshInstall: false, legacyDefaultsMigrated: false, tenMinuteSafetyMigrated: false), Is.EqualTo(10));
    }

    [Test]
    public void Migrated_config_keeps_the_players_choice()
    {
        Assert.That(SweepSettingsPolicy.ResolveStartupIntervalMinutes(45, freshInstall: false, legacyDefaultsMigrated: true, tenMinuteSafetyMigrated: true), Is.EqualTo(45));
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
}
