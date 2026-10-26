using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxLedgerStoreContractTests
{
    [Test]
    public void Load_rejects_a_primary_ledger_owned_by_another_profile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            const string selectedKey = "p_1111111111111111111111111111111111111111111111111111111111111111";
            const string otherKey = "p_2222222222222222222222222222222222222222222222222222222222222222";
            File.WriteAllText(
                Path.Combine(directory, selectedKey + ".json"),
                WorkRelaxLedgerCodec.Serialize(new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = otherKey }));

            WorkRelaxLedgerLoadResult result = WorkRelaxLedgerStore.ForLedgerDirectory(directory, selectedKey).Load();

            Assert.Multiple(() =>
            {
                Assert.That(result.Ledger, Is.Null);
                Assert.That(result.RecoveredFromBackup, Is.False);
                Assert.That(result.PrimaryError, Does.Contain("different local profile"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Load_rejects_a_backup_ledger_owned_by_another_profile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            const string selectedKey = "p_3333333333333333333333333333333333333333333333333333333333333333";
            const string otherKey = "p_4444444444444444444444444444444444444444444444444444444444444444";
            string path = Path.Combine(directory, selectedKey + ".json");
            File.WriteAllText(path, "{ corrupt-primary");
            File.WriteAllText(
                path + ".bak",
                WorkRelaxLedgerCodec.Serialize(new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = otherKey }));

            WorkRelaxLedgerLoadResult result = WorkRelaxLedgerStore.ForLedgerDirectory(directory, selectedKey).Load();

            Assert.Multiple(() =>
            {
                Assert.That(result.Ledger, Is.Null);
                Assert.That(result.RecoveredFromBackup, Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo("{ corrupt-primary"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Load_recovers_a_valid_backup_when_the_primary_is_missing()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            const string profileKey = "p_5555555555555555555555555555555555555555555555555555555555555555";
            string path = Path.Combine(directory, profileKey + ".json");
            File.WriteAllText(
                path + ".bak",
                WorkRelaxLedgerCodec.Serialize(new WorkRelaxLedger
                {
                    SchemaVersion = 1,
                    ProfileKey = profileKey,
                    LastMode = "Relax"
                }));

            WorkRelaxLedgerLoadResult result = WorkRelaxLedgerStore.ForLedgerDirectory(directory, profileKey).Load();

            Assert.Multiple(() =>
            {
                Assert.That(result.Ledger, Is.Not.Null);
                Assert.That(result.Ledger!.LastMode, Is.EqualTo("Relax"));
                Assert.That(result.RecoveredFromBackup, Is.True);
                Assert.That(WorkRelaxLedgerCodec.Deserialize(File.ReadAllText(path)).ProfileKey, Is.EqualTo(profileKey));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void FromConfigPath_writes_below_the_BlueSage_owned_ledger_directory()
    {
        string configPath = Path.Combine(Path.GetTempPath(), "BlueSageWorkRelaxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configPath);

        try
        {
            const string profileKey = "p_dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
            var ledger = new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = profileKey };

            WorkRelaxLedgerStore.FromConfigPath(configPath, profileKey).Save(ledger);

            Assert.That(
                File.Exists(Path.Combine(configPath, "BlueSageWorkRelax", profileKey + ".json")),
                Is.True);
        }
        finally
        {
            Directory.Delete(configPath, recursive: true);
        }
    }

    [Test]
    public void Load_uses_valid_backup_and_reports_recovery_when_primary_is_corrupt()
    {
        string directory = Path.Combine(Path.GetTempPath(), "BlueSageWorkRelaxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            const string profileKey = "p_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var ledger = new WorkRelaxLedger
            {
                SchemaVersion = 1,
                ProfileKey = profileKey,
                LastMode = "Relax"
            };
            ledger.AppendEvent(new WorkRelaxLedgerEvent
            {
                EventId = "event-backup",
                UtcTicks = 638896320000000000L,
                MetricFamily = "ActivityCount",
                MetricKey = "ReadFocusCount",
                Amount = 1d,
                Mode = "Relax",
                Source = "NativePostfix"
            });
            string path = Path.Combine(directory, profileKey + ".json");
            File.WriteAllText(path, "{ definitely-not-valid-json");
            File.WriteAllText(path + ".bak", WorkRelaxLedgerCodec.Serialize(ledger));

            var store = WorkRelaxLedgerStore.ForLedgerDirectory(directory, profileKey);
            WorkRelaxLedgerLoadResult result = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(result.Ledger, Is.Not.Null);
                Assert.That(result.Ledger!.ProfileKey, Is.EqualTo(profileKey));
                Assert.That(result.Ledger.Events.Single().EventId, Is.EqualTo("event-backup"));
                Assert.That(result.RecoveredFromBackup, Is.True);
                Assert.That(result.PrimaryError, Is.Not.Empty);
                Assert.That(WorkRelaxLedgerCodec.Deserialize(File.ReadAllText(path)).ProfileKey, Is.EqualTo(profileKey));
                Assert.That(WorkRelaxLedgerCodec.Deserialize(File.ReadAllText(path)).Recovery.RecoveryCount, Is.EqualTo(1));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Save_creates_primary_then_replaces_it_atomically_with_a_last_known_good_backup()
    {
        string directory = Path.Combine(Path.GetTempPath(), "BlueSageWorkRelaxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            const string profileKey = "p_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var store = WorkRelaxLedgerStore.ForLedgerDirectory(directory, profileKey);
            var first = new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = profileKey, LastMode = "Work" };
            var second = new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = profileKey, LastMode = "Relax" };

            store.Save(first);
            store.Save(second);

            string path = Path.Combine(directory, profileKey + ".json");
            Assert.Multiple(() =>
            {
                Assert.That(WorkRelaxLedgerCodec.Deserialize(File.ReadAllText(path)).LastMode, Is.EqualTo("Relax"));
                Assert.That(WorkRelaxLedgerCodec.Deserialize(File.ReadAllText(path + ".bak")).LastMode, Is.EqualTo("Work"));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Round 5d full review (critical): in BlueSageQoLModSuite/trackers the file is "work-relax-p_<id>.json"; Save used
    // to compare the file name with the profile key and refused every save, so history was lost on quit.
    [Test]
    public void Save_and_load_work_in_the_new_folder_with_the_work_relax_file_prefix()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            const string profileKey = "p_cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
            var store = WorkRelaxLedgerStore.ForLedgerDirectory(directory, profileKey, ModSuiteLayoutPolicy.WorkRelaxFilePrefix);
            store.Save(new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = profileKey, LastMode = "Work" });
            store.Save(new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = profileKey, LastMode = "Relax" });

            string path = Path.Combine(directory, "work-relax-" + profileKey + ".json");
            WorkRelaxLedgerLoadResult loaded = WorkRelaxLedgerStore.ForLedgerDirectory(directory, profileKey, ModSuiteLayoutPolicy.WorkRelaxFilePrefix).Load();
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), "Saved under the prefixed name.");
                Assert.That(File.Exists(path + ".bak"));
                Assert.That(loaded.Ledger?.LastMode, Is.EqualTo("Relax"), "What was saved loads back.");
                Assert.That(() => store.Save(new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = "p_" + new string('d', 64) }),
                    Throws.InvalidOperationException, "Another profile's ledger is still refused.");
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "BlueSageWorkRelaxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
