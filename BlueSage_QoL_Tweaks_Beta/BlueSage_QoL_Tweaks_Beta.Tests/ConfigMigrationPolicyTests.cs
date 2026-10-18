using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ConfigMigrationPolicyTests
{
    [Test]
    public void MigrationLedger_RecognizesLegacyV2WithoutDependingOnPluginVersion()
    {
        Assert.That(
            ConfigMigrationPolicy.HasCompletedMigration("0.2.0-final-defaults-v2", ConfigMigrationPolicy.FinalDefaultsV2Migration),
            Is.True);
        Assert.That(
            ConfigMigrationPolicy.HasCompletedMigration("0.2.1-final-defaults-v2", ConfigMigrationPolicy.FinalDefaultsV2Migration),
            Is.False,
            "Unknown version-derived markers must not be treated as completed migration stages.");
    }

    [Test]
    public void MigrationLedger_RecordsStableStagesOnceAndCarriesLegacyV2Forward()
    {
        string ledger = ConfigMigrationPolicy.RecordMigration(
            "0.2.0-final-defaults-v2",
            ConfigMigrationPolicy.SweepFifteenMinuteV1Migration);

        Assert.Multiple(() =>
        {
            Assert.That(ConfigMigrationPolicy.HasCompletedMigration(ledger, ConfigMigrationPolicy.FinalDefaultsV2Migration), Is.True);
            Assert.That(ConfigMigrationPolicy.HasCompletedMigration(ledger, ConfigMigrationPolicy.SweepFifteenMinuteV1Migration), Is.True);
            Assert.That(ledger, Is.EqualTo("0.2.0-final-defaults-v2;sweep-15-default-v1"));
            Assert.That(
                ConfigMigrationPolicy.RecordMigration(ledger, ConfigMigrationPolicy.SweepFifteenMinuteV1Migration),
                Is.EqualTo(ledger));
        });
    }

    [Test]
    public void MigrationLedger_PreservesUnknownFutureStagesAcrossOlderPluginRuns()
    {
        const string futureLedger = "final-defaults-v2;sweep-15-default-v1;future-stage-v3";

        string afterFinalDefaults = ConfigMigrationPolicy.RecordMigration(
            futureLedger,
            ConfigMigrationPolicy.FinalDefaultsV2Migration);
        string afterSweep = ConfigMigrationPolicy.RecordMigration(
            afterFinalDefaults,
            ConfigMigrationPolicy.SweepFifteenMinuteV1Migration);

        Assert.That(afterSweep, Is.EqualTo(futureLedger));
    }

    [Test]
    public void MigrationLedger_AppendsMissingCurrentStageWithoutDiscardingUnknownTokens()
    {
        const string partialLedger = "final-defaults-v2;future-stage-v3";

        string completed = ConfigMigrationPolicy.RecordMigration(
            partialLedger,
            ConfigMigrationPolicy.SweepFifteenMinuteV1Migration);

        Assert.That(completed, Is.EqualTo("final-defaults-v2;future-stage-v3;sweep-15-default-v1"));
    }

    [Test]
    public void MigrateOldDefaultInt_UpdatesOnlyOldDefaultBeforeMigration()
    {
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultInt(1500, 1500, 3000, alreadyMigrated: false), Is.EqualTo(3000));
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultInt(2000, 2000, 3000, alreadyMigrated: false), Is.EqualTo(3000));
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultInt(60, 60, 30, alreadyMigrated: false), Is.EqualTo(30));
    }

    [Test]
    public void MigrateOldDefaultInt_PreservesCustomOrAlreadyMigratedValues()
    {
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultInt(1800, 1500, 3000, alreadyMigrated: false), Is.EqualTo(1800));
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultInt(1500, 1500, 3000, alreadyMigrated: true), Is.EqualTo(1500));
    }

    [Test]
    public void MigrateOldDefaultString_UpdatesOnlyOldDefaultBeforeMigration()
    {
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultString("click", "click", "ticket", alreadyMigrated: false), Is.EqualTo("ticket"));
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultString("task", "click", "ticket", alreadyMigrated: false), Is.EqualTo("task"));
        Assert.That(ConfigMigrationPolicy.MigrateOldDefaultString("click", "click", "ticket", alreadyMigrated: true), Is.EqualTo("click"));
    }

    [Test]
    public void MigrateOldDefaultBool_UpdatesOnlyOldDefaultBeforeMigration()
    {
        var method = typeof(ConfigMigrationPolicy).GetMethod(
            "MigrateOldDefaultBool",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.That(method, Is.Not.Null, "Boolean release defaults need the same old-default-only migration contract as numeric/string defaults.");
        Assert.That(method!.Invoke(null, new object[] { true, true, false, false }), Is.EqualTo(false));
        Assert.That(method.Invoke(null, new object[] { false, false, true, false }), Is.EqualTo(true));
        Assert.That(method.Invoke(null, new object[] { true, false, true, false }), Is.EqualTo(true));
        Assert.That(method.Invoke(null, new object[] { false, false, true, true }), Is.EqualTo(false));
    }

    [Test]
    public void CommunityBanAnyClientOptInMigration_ResetsLegacyDefaultOnceThenPreservesExplicitChoice()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ConfigMigrationPolicy.MigrateOldDefaultBool(
                    currentValue: true,
                    oldDefault: true,
                    newDefault: false,
                    alreadyMigrated: false),
                Is.False);
            Assert.That(
                ConfigMigrationPolicy.MigrateOldDefaultBool(
                    currentValue: true,
                    oldDefault: true,
                    newDefault: false,
                    alreadyMigrated: true),
                Is.True);
            Assert.That(
                ConfigMigrationPolicy.CurrentMigrationLedger,
                Does.Contain(ConfigMigrationPolicy.CommunityBanAnyClientOptInV1Migration));
        });
    }

    [Test]
    public void ShouldDisableDefaultSpoons_PreservesExplicitOptInsAndDisablesOnlyLegacyDefaultState()
    {
        Assert.That(ConfigMigrationPolicy.ShouldDisableDefaultSpoons(enabled: true, spoonsUpdatedUtc: string.Empty, alreadyMigrated: false), Is.True);
        Assert.That(ConfigMigrationPolicy.ShouldDisableDefaultSpoons(enabled: true, spoonsUpdatedUtc: "not-a-timestamp", alreadyMigrated: false), Is.True);
        Assert.That(ConfigMigrationPolicy.ShouldDisableDefaultSpoons(enabled: true, spoonsUpdatedUtc: "2026-07-02T12:00:00.0000000Z", alreadyMigrated: false), Is.False);
        Assert.That(ConfigMigrationPolicy.ShouldDisableDefaultSpoons(enabled: false, spoonsUpdatedUtc: string.Empty, alreadyMigrated: false), Is.False);
        Assert.That(ConfigMigrationPolicy.ShouldDisableDefaultSpoons(enabled: true, spoonsUpdatedUtc: string.Empty, alreadyMigrated: true), Is.False);
    }
}
