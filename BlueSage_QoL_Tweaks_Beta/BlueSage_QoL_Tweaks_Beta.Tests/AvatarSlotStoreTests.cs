using System.Collections.Generic;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 WS1 (Blue, 2026-10-19): "allow users to have extra avatar slots without fear of losing their creative
// works". BepInEx ConfigFile.Save truncates and rewrites in place (new StreamWriter(path, append: false)), so a
// crash mid-write could empty all six slots at once. The store is written to a temp file, swapped in atomically,
// keeps three rotating backups, and a file without its end marker is never trusted.
[TestFixture]
public sealed class AvatarSlotStoreTests
{
    private static readonly string[] Names = { "Cozy ♥", "Work", "", "Night | Owl", "Rain;Day", "Last" };
    private static readonly string[] Payloads = { "BSAS1;0;1:2:3;4:5:6", "", "BSAS1;1;;", "", "", "BSAS1;0;9:9:;" };

    [Test]
    public void Store_round_trips_names_payloads_and_selection()
    {
        string text = AvatarSlotStoreFormat.Format(5, Names, Payloads);
        Assert.Multiple(() =>
        {
            Assert.That(AvatarSlotStoreFormat.TryParse(text, 6, out int selected, out string[] names, out string[] payloads), Is.True);
            Assert.That(selected, Is.EqualTo(5));
            Assert.That(names, Is.EqualTo(Names));
            Assert.That(payloads, Is.EqualTo(Payloads));
        });
    }

    [Test]
    public void A_cut_off_file_is_never_trusted()
    {
        string text = AvatarSlotStoreFormat.Format(3, Names, Payloads);
        Assert.Multiple(() =>
        {
            Assert.That(AvatarSlotStoreFormat.TryParse(text.Substring(0, text.Length / 2), 6, out _, out _, out _), Is.False);
            Assert.That(AvatarSlotStoreFormat.TryParse(string.Empty, 6, out _, out _, out _), Is.False);
            Assert.That(AvatarSlotStoreFormat.TryParse("not a store", 6, out _, out _, out _), Is.False);
            Assert.That(AvatarSlotStoreFormat.TryParse(text, 5, out _, out _, out _), Is.False, "A different slot count is a different store.");
        });
    }

    [Test]
    public void Load_prefers_the_main_file_then_the_newest_complete_backup()
    {
        string good = AvatarSlotStoreFormat.Format(4, Names, Payloads);
        string older = AvatarSlotStoreFormat.Format(-1, Names, Payloads);
        Assert.Multiple(() =>
        {
            Assert.That(AvatarSlotStoreFormat.ChooseFirstValid(new[] { good, older, null, null }, 6), Is.EqualTo(0));
            Assert.That(AvatarSlotStoreFormat.ChooseFirstValid(new[] { good.Substring(0, 20), older, null, null }, 6), Is.EqualTo(1), "Truncated main file falls back to backup 1.");
            Assert.That(AvatarSlotStoreFormat.ChooseFirstValid(new[] { null, "junk", null, older }, 6), Is.EqualTo(3));
            Assert.That(AvatarSlotStoreFormat.ChooseFirstValid(new string[] { null, null, null, null }, 6), Is.EqualTo(-1));
        });
    }

    [Test]
    public void Backups_rotate_oldest_first_so_nothing_newer_is_lost()
    {
        string[] files = { "s", "s.bak1", "s.bak2", "s.bak3" };
        Assert.That(AvatarSlotStoreFormat.RotationPlan(files), Is.EqualTo(new[] { new KeyValuePair<string, string>("s.bak2", "s.bak3"), new KeyValuePair<string, string>("s.bak1", "s.bak2"), new KeyValuePair<string, string>("s", "s.bak1") }));
    }

    [Test]
    public void Real_files_survive_a_cut_off_save_by_falling_back_to_the_newest_backup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-avatar-store-" + System.Guid.NewGuid().ToString("N"));
        string main = Path.Combine(dir, "slots.store");
        try
        {
            string[] first = { "One", "", "", "", "", "" };
            string[] second = { "Two", "", "", "", "", "" };
            AvatarSlotStore.Save(main, 3, first, Payloads);
            AvatarSlotStore.Save(main, 4, second, Payloads);
            Assert.That(File.Exists(main + ".bak1"), Is.True, "The previous save rotated into backup 1.");

            // Simulate a crash mid-write of the main file.
            string full = File.ReadAllText(main);
            File.WriteAllText(main, full.Substring(0, full.Length / 3));

            Assert.Multiple(() =>
            {
                Assert.That(AvatarSlotStore.TryLoad(main, 6, out int selected, out string[] names, out _, out string used), Is.True);
                Assert.That(used, Is.EqualTo(main + ".bak1"));
                Assert.That(selected, Is.EqualTo(3));
                Assert.That(names[0], Is.EqualTo("One"));
                Assert.That(File.Exists(main + ".tmp"), Is.False, "No temp file is left behind after a save.");
            });
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // Code review 2026-10-19: saving the same content again used to rotate it into every backup, wiping history.
    [Test]
    public void Saving_unchanged_slots_leaves_the_backups_alone()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-avatar-store-" + System.Guid.NewGuid().ToString("N"));
        string main = Path.Combine(dir, "slots.store");
        try
        {
            string[] first = { "One", "", "", "", "", "" };
            string[] second = { "Two", "", "", "", "", "" };
            AvatarSlotStore.Save(main, 3, first, Payloads);
            AvatarSlotStore.Save(main, 4, second, Payloads);
            string backup = File.ReadAllText(main + ".bak1");
            AvatarSlotStore.Save(main, 4, second, Payloads);
            AvatarSlotStore.Save(main, 4, second, Payloads);
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(main + ".bak1"), Is.EqualTo(backup), "Backup 1 still holds the older save.");
                Assert.That(File.Exists(main + ".bak2"), Is.False, "No identical copies were pushed down the chain.");
            });
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // Code review 2026-10-19: after loading from backup 3 (main, bak1 and bak2 damaged), the next save rotated
    // damaged files over the only good copy. A damaged file must never overwrite a backup.
    [Test]
    public void A_damaged_file_never_overwrites_a_good_backup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-avatar-store-" + System.Guid.NewGuid().ToString("N"));
        string main = Path.Combine(dir, "slots.store");
        try
        {
            Directory.CreateDirectory(dir);
            string good = AvatarSlotStoreFormat.Format(3, new[] { "Only good", "", "", "", "", "" }, Payloads);
            File.WriteAllText(main, "junk");
            File.WriteAllText(main + ".bak1", "junk");
            File.WriteAllText(main + ".bak2", good.Substring(0, good.Length / 2));
            File.WriteAllText(main + ".bak3", good);

            AvatarSlotStore.Save(main, 4, new[] { "New", "", "", "", "", "" }, Payloads);

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(main + ".bak3"), Is.EqualTo(good), "The only good backup survived.");
                Assert.That(AvatarSlotStore.TryLoad(main, 6, out _, out string[] names, out _, out string used), Is.True);
                Assert.That(used, Is.EqualTo(main));
                Assert.That(names[0], Is.EqualTo("New"));
            });
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Test]
    public void Any_existing_store_file_counts_as_data_on_disk()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-avatar-store-" + System.Guid.NewGuid().ToString("N"));
        string main = Path.Combine(dir, "slots.store");
        try
        {
            Directory.CreateDirectory(dir);
            Assert.That(AvatarSlotStore.AnyFileExists(main), Is.False);
            File.WriteAllText(main + ".bak2", "junk");
            Assert.That(AvatarSlotStore.AnyFileExists(main), Is.True);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // Code review 2026-10-19: a store that exists but can't be read (locked, damaged) used to fall through to the
    // legacy migration or an empty start, and the next save overwrote the real slots. Now the session goes
    // read-only and nothing on disk changes. The same holds when the one-time .cfg migration itself fails.
    [Test]
    public void Unreadable_store_or_failed_migration_makes_the_session_read_only()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string bridge = File.ReadAllText(Path.Combine(root, "src", "Patches", "AvatarStyleSlotsPatch.cs")).Replace("\r\n", "\n");
        int start = bridge.IndexOf("private static void EnsureSlotStore()", System.StringComparison.Ordinal);
        string ensure = bridge.Substring(start, bridge.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        int persistStart = bridge.IndexOf("private static bool PersistStore()", System.StringComparison.Ordinal);
        string persist = bridge.Substring(persistStart, bridge.IndexOf("\n    }\n", persistStart, System.StringComparison.Ordinal) - persistStart);
        int anyFile = ensure.IndexOf("AvatarSlotStore.AnyFileExists(_storePath)", System.StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(anyFile, Is.GreaterThan(0));
            Assert.That(anyFile, Is.LessThan(ensure.IndexOf("MigrateFromLegacyConfig();", System.StringComparison.Ordinal)));
            Assert.That(persist, Does.Contain("!_storeWritable"));
            Assert.That(bridge, Does.Contain("_storeWritable = true;"));
            Assert.That(ensure, Does.Not.Contain("nothing on disk was changed"), "Only claim nothing changed where it is true.");
        });
    }

    [Test]
    public void Bridge_persists_through_the_atomic_store_and_reads_the_old_cfg_only_to_migrate()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string bridge = File.ReadAllText(Path.Combine(root, "src", "Patches", "AvatarStyleSlotsPatch.cs"));
        string store = File.ReadAllText(Path.Combine(root, "src", "AvatarSlotStore.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(store, Does.Contain("File.Replace("));
            Assert.That(store, Does.Contain("AvatarSlotStoreFormat.RotationPlan("));
            Assert.That(store, Does.Contain("AvatarSlotStoreFormat.ChooseFirstValid("));
            Assert.That(bridge, Does.Contain("AvatarSlotStore.Save("));
            Assert.That(bridge, Does.Contain("MigrateFromLegacyConfig("));
            Assert.That(bridge, Does.Not.Contain("Payloads[slot - NativeSlotCount].Value ="), "Slot payloads are no longer written through BepInEx ConfigFile.");
        });
    }

    // Round 4 review R1 (2026-10-19): File.WriteAllText never forces the bytes to disk, and copy-based rotation
    // rewrote every backup on each save, so a power loss right after a save could damage all four files at once.
    // The temp file is flushed to disk before the swap, and backups rotate by rename (one file moves, nothing is
    // rewritten); the main file rotates into backup 1 inside the same File.Replace that swaps the new file in.
    [Test]
    public void Store_flushes_the_temp_file_to_disk_and_rotates_backups_by_rename()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string store = File.ReadAllText(Path.Combine(root, "src", "AvatarSlotStore.cs")).Replace("\r\n", "\n");
        int saveStart = store.IndexOf("internal static void Save(string mainPath, int selectedSlot, string legacyConfigHash", System.StringComparison.Ordinal);
        string save = store.Substring(saveStart, store.IndexOf("\n    }\n", saveStart, System.StringComparison.Ordinal) - saveStart);
        Assert.Multiple(() =>
        {
            Assert.That(store, Does.Contain("Flush(true)"), "The temp file is forced to disk before it replaces the main file.");
            Assert.That(store, Does.Not.Contain("File.WriteAllText("), "WriteAllText cannot flush to disk.");
            Assert.That(save, Does.Not.Contain("File.Copy("), "Backups rotate by rename, never by rewriting.");
            Assert.That(save, Does.Contain("MoveOver("));
            Assert.That(save, Does.Contain("File.Replace(tempPath, mainPath, mainComplete ? mainBackup : null)"), "Main rotates into backup 1 inside the atomic swap, and only when complete.");
            Assert.That(store, Does.Not.Match(@"File\.Move\([^;]*,[^;]*,[^;]*\)"), "File.Move(a, b, overwrite) does not exist on net46.");
        });
    }

    [Test]
    public void Four_saves_keep_the_three_previous_versions_newest_first()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-avatar-store-" + System.Guid.NewGuid().ToString("N"));
        string main = Path.Combine(dir, "slots.store");
        try
        {
            for (int version = 1; version <= 4; version++)
            {
                AvatarSlotStore.Save(main, version, new[] { "v" + version, "", "", "", "", "" }, Payloads);
            }

            Assert.Multiple(() =>
            {
                Assert.That(SelectedOf(main), Is.EqualTo(4));
                Assert.That(SelectedOf(main + ".bak1"), Is.EqualTo(3));
                Assert.That(SelectedOf(main + ".bak2"), Is.EqualTo(2));
                Assert.That(SelectedOf(main + ".bak3"), Is.EqualTo(1));
                Assert.That(File.Exists(main + ".tmp"), Is.False);
            });

            AvatarSlotStore.Save(main, 5, new[] { "v5", "", "", "", "", "" }, Payloads);
            Assert.Multiple(() =>
            {
                Assert.That(SelectedOf(main), Is.EqualTo(5));
                Assert.That(SelectedOf(main + ".bak1"), Is.EqualTo(4));
                Assert.That(SelectedOf(main + ".bak3"), Is.EqualTo(2), "The oldest version fell off the end of the chain.");
                Assert.That(Directory.GetFiles(dir), Has.Length.EqualTo(4), "Exactly main plus three backups remain.");
            });
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // Round 4 review R2 (2026-10-19): BSAVSTORE2 records the content hash of the migrated .cfg; a v1 file still
    // loads and reports no recorded hash, so the bridge adopts the current .cfg instead of re-importing it.
    [Test]
    public void Store_v2_records_the_legacy_config_hash_and_v1_files_still_load()
    {
        string v2 = AvatarSlotStoreFormat.Format(5, "ABC123", Names, Payloads);
        string v1 = "BSAVSTORE1\nselected=2\n" + string.Concat(Enumerable.Range(0, 6).Select(i => "name=\npayload=\n")) + "end=6\n";
        Assert.Multiple(() =>
        {
            Assert.That(v2, Does.StartWith("BSAVSTORE2\n"));
            Assert.That(AvatarSlotStoreFormat.TryParse(v2, 6, out int selected, out string hash, out string[] names, out string[] payloads), Is.True);
            Assert.That(selected, Is.EqualTo(5));
            Assert.That(hash, Is.EqualTo("ABC123"));
            Assert.That(names, Is.EqualTo(Names));
            Assert.That(payloads, Is.EqualTo(Payloads));
            Assert.That(AvatarSlotStoreFormat.TryParse(v1, 6, out int v1Selected, out string v1Hash, out _, out _), Is.True, "v1 files written by 7f97ca4 builds still load.");
            Assert.That(v1Selected, Is.EqualTo(2));
            Assert.That(v1Hash, Is.Null, "A v1 file has no recorded hash.");
            Assert.That(AvatarSlotStoreFormat.TryParse(AvatarSlotStoreFormat.Format(5, "", Names, Payloads), 6, out _, out string recordedAbsent, out _, out _), Is.True);
            Assert.That(recordedAbsent, Is.EqualTo(""), "An empty recorded hash means the .cfg was absent, which is different from v1's unknown.");
            Assert.That(AvatarSlotStoreFormat.TryParse(v2.Replace("legacy=ABC123\n", ""), 6, out _, out _, out _, out _), Is.False, "A v2 file without its hash line is cut off.");
        });
    }

    [Test]
    public void Reimport_backs_up_the_loaded_file_to_a_dated_pre_reimport_copy_first()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-avatar-store-" + System.Guid.NewGuid().ToString("N"));
        string main = Path.Combine(dir, "slots.store");
        try
        {
            AvatarSlotStore.Save(main, 3, "OLDHASH", new[] { "Keep me", "", "", "", "", "" }, Payloads);
            string loaded = File.ReadAllText(main);
            string copy = AvatarSlotStore.BackupBeforeReimport(main, main, new System.DateTime(2026, 10, 19, 22, 50, 0));
            Assert.Multiple(() =>
            {
                Assert.That(copy, Is.EqualTo(main + ".pre-reimport-20261019-225000"));
                Assert.That(File.ReadAllText(copy), Is.EqualTo(loaded));
                Assert.That(AvatarSlotStore.TryLoad(main, 6, out _, out string hash, out _, out _, out _), Is.True);
                Assert.That(hash, Is.EqualTo("OLDHASH"));
                Assert.That(AvatarSlotStore.FilesInTrustOrder(main), Does.Not.Contain(copy), "The copy is never loaded as a backup.");
            });
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Test]
    public void Bridge_reconciles_the_legacy_cfg_after_every_successful_load()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string bridge = File.ReadAllText(Path.Combine(root, "src", "Patches", "AvatarStyleSlotsPatch.cs")).Replace("\r\n", "\n");
        int load = bridge.IndexOf("AvatarSlotStore.TryLoad(_storePath", System.StringComparison.Ordinal);
        int reconcile = bridge.IndexOf("ReconcileLegacyConfig(usedFile);", System.StringComparison.Ordinal);
        int backup = bridge.IndexOf("AvatarSlotStore.BackupBeforeReimport(", System.StringComparison.Ordinal);
        int warning = bridge.IndexOf("re-imported from the old settings file", System.StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(load, Is.GreaterThan(0));
            Assert.That(reconcile, Is.GreaterThan(load), "The .cfg is checked after the store loaded.");
            Assert.That(bridge, Does.Contain("LegacyConfigReimportPolicy.Decide("));
            Assert.That(bridge, Does.Contain("LegacyConfigReimportPolicy.ContentHash("));
            Assert.That(backup, Is.GreaterThan(0));
            Assert.That(warning, Is.GreaterThan(backup), "The warning is logged only after the copy exists.");
            Assert.That(bridge, Does.Contain("case LegacyConfigAction.Reimport:"));
        });
    }

    // Round 4 review R10 (2026-10-19): a slot switch saved the store twice (departing slot, then the new
    // selection), pushing a near-duplicate down the backup chain each time. One user action is one save, and the
    // incoming ids are converted once instead of IDs -> data -> IDs -> IDs.
    [Test]
    public void One_slot_switch_is_one_save_and_one_conversion()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string bridge = File.ReadAllText(Path.Combine(root, "src", "Patches", "AvatarStyleSlotsPatch.cs")).Replace("\r\n", "\n");
        string Body(string signature)
        {
            int start = bridge.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            return bridge.Substring(start, bridge.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        }

        string selectExtended = Body("internal static void SelectExtended(");
        string selectNative = Body("internal static bool SelectNativeFromExtended(");
        string persist = Body("private static bool PersistStore()");
        Assert.Multiple(() =>
        {
            Assert.That(selectExtended, Does.Contain("BeginPersistBatch();"));
            Assert.That(selectExtended, Does.Contain("EndPersistBatch();"));
            Assert.That(selectExtended.IndexOf("finally", System.StringComparison.Ordinal), Is.LessThan(selectExtended.IndexOf("EndPersistBatch();", System.StringComparison.Ordinal)), "The batch always closes.");
            Assert.That(selectNative, Does.Contain("BeginPersistBatch();"));
            Assert.That(selectNative, Does.Contain("EndPersistBatch();"));
            Assert.That(selectNative.IndexOf("finally", System.StringComparison.Ordinal), Is.LessThan(selectNative.IndexOf("EndPersistBatch();", System.StringComparison.Ordinal)));
            Assert.That(persist, Does.Contain("_persistBatchDepth > 0"), "Saves inside a batch are deferred to its end.");
            Assert.That(bridge, Does.Contain("private static void SaveExtendedData(CustomizationDataIDs3 ids, int slot)"));
            Assert.That(bridge, Does.Not.Contain("SaveExtendedData(new CustomizationData("));
            Assert.That(bridge, Does.Not.Contain("Serialize(new CustomizationDataIDs3(data))"));
        });
    }

    private static int SelectedOf(string path)
    {
        Assert.That(AvatarSlotStoreFormat.TryParse(File.ReadAllText(path), 6, out int selected, out _, out _), Is.True, path + " must parse.");
        return selected;
    }
}
