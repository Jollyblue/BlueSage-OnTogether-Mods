using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5d (Blue, 2026-10-23): one BlueSageQoLModSuite folder, a forced one-time move, zips for older files, and
// "We better not be moving or deleting player files for the vanilla game we are only touching our MOD related files".
// Every test runs on a temporary BepInEx tree and checks that files we do not own are byte-for-byte untouched.
public sealed class ModSuiteStorageTests
{
    private const string ProfileFile = "p_0eefe18b72e6720defb9b801537fe3dced2ccb33e4838678e2187096f6f5eff2.json";
    private static readonly DateTime Now = new DateTime(2026, 10, 23, 17, 45, 0, DateTimeKind.Local);

    private string _root = string.Empty;
    private string _bepinex = string.Empty;
    private string _config = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "bluesage-modsuite-" + Guid.NewGuid().ToString("N"));
        _bepinex = Path.Combine(_root, "BepInEx");
        _config = Path.Combine(_bepinex, "config");
        Directory.CreateDirectory(_config);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Test]
    public void Fresh_install_gets_the_new_folder_and_nothing_else()
    {
        ModSuiteMigrationResult result = ModSuiteMigration.Run(_bepinex, _config, Now, null);
        string suite = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName);
        Assert.Multiple(() =>
        {
            Assert.That(result.LayoutReady, Is.True);
            Assert.That(result.MovedOldFiles, Is.False, "No toast on a fresh install.");
            Assert.That(File.Exists(Path.Combine(suite, ModSuiteLayoutPolicy.MarkerFileName)));
            Assert.That(File.ReadAllText(Path.Combine(suite, "README.txt")), Is.EqualTo(ModSuiteMigration.ReadmeText));
            Assert.That(Directory.Exists(Path.Combine(suite, "trackers")) && Directory.Exists(Path.Combine(suite, "logs")) && Directory.Exists(Path.Combine(suite, "archive")));
            Assert.That(Directory.GetDirectories(_bepinex).Select(Path.GetFileName), Is.EquivalentTo(new[] { "config", ModSuiteLayoutPolicy.RootFolderName }));
        });
    }

    [Test]
    public void Full_old_layout_moves_into_one_folder_and_never_touches_anything_we_do_not_own()
    {
        Dictionary<string, byte[]> notOurs = SeedNotOurs();
        SeedOldLayout(sessions: 5);

        ModSuiteMigrationResult result = ModSuiteMigration.Run(_bepinex, _config, Now, null);
        string trackers = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, "trackers");
        string archive = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, "archive");
        string zip = Path.Combine(archive, "2026-10-23_174500-pre-0.2.7-layout.zip");
        Assert.Multiple(() =>
        {
            Assert.That(result.LayoutReady && result.MovedOldFiles, Is.True, result.Summary);
            Assert.That(File.ReadAllText(Path.Combine(trackers, "work-relax-" + ProfileFile)), Is.EqualTo("ledger"));
            Assert.That(File.ReadAllText(Path.Combine(trackers, "work-relax-" + ProfileFile + ".bak")), Is.EqualTo("ledger-bak"));
            Assert.That(File.ReadAllText(Path.Combine(trackers, "host-journal.jsonl")), Is.EqualTo("journal"));
            Assert.That(File.ReadAllText(Path.Combine(trackers, "session-run-marker.v1")), Is.EqualTo("marker"));
            Assert.That(File.ReadAllText(Path.Combine(trackers, "community-ban-attestation.json")), Is.EqualTo("attest"));
            Assert.That(File.ReadAllText(Path.Combine(trackers, "community-ban-attestation.json.bak")), Is.EqualTo("attest-bak"));
            Assert.That(Directory.Exists(Path.Combine(_bepinex, "BlueSageAudit")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_config, "BlueSageAudit")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_config, "BlueSageWorkRelax")), Is.False);
            AssertUntouched(notOurs);
        });

        using ZipArchive packed = ZipFile.OpenRead(zip);
        string[] names = packed.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("config/BlueSageWorkRelax/" + ProfileFile));
            Assert.That(names, Does.Contain("BlueSageAudit/host-journal/host-journal-v1.jsonl.prev"));
            Assert.That(names.Count(name => name.StartsWith("BlueSageAudit/session-", StringComparison.Ordinal)) / 2, Is.EqualTo(5), "Security review: every old session 0.2.6 would still keep (12 newest, 14 days, about 96 MB) goes into the zip (two files each).");
            Assert.That(names.Any(name => name.Contains("session-20261019")), Is.True);
        });
    }

    [Test]
    public void Running_again_changes_nothing()
    {
        SeedOldLayout(sessions: 2);
        ModSuiteMigration.Run(_bepinex, _config, Now, null);
        string suite = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName);
        string[] before = Directory.GetFiles(suite, "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray();

        ModSuiteMigrationResult again = ModSuiteMigration.Run(_bepinex, _config, Now.AddMinutes(1), null);
        Assert.Multiple(() =>
        {
            Assert.That(again.LayoutReady, Is.True);
            Assert.That(again.MovedOldFiles, Is.False, "The tidy-up toast shows once.");
            Assert.That(Directory.GetFiles(suite, "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray(), Is.EqualTo(before));
        });
    }

    [Test]
    public void A_failure_before_the_marker_changes_nothing_old_and_keeps_the_old_places_in_use()
    {
        Dictionary<string, byte[]> notOurs = SeedNotOurs();
        SeedOldLayout(sessions: 1);
        string blocked = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, "trackers", "work-relax-" + ProfileFile);
        Directory.CreateDirectory(blocked);

        ModSuiteMigrationResult result = ModSuiteMigration.Run(_bepinex, _config, Now, null);
        Assert.Multiple(() =>
        {
            Assert.That(result.LayoutReady, Is.False);
            Assert.That(File.Exists(Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, ModSuiteLayoutPolicy.MarkerFileName)), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(_config, "BlueSageWorkRelax", ProfileFile)), Is.EqualTo("ledger"));
            Assert.That(File.Exists(Path.Combine(_bepinex, "BlueSageAudit", "host-journal", "host-journal-v1.jsonl")));
            Assert.That(ModSuiteLayout.For(_bepinex, _config, legacyMode: true).WorkRelaxDirectory, Is.EqualTo(Path.Combine(_config, "BlueSageWorkRelax")));
            AssertUntouched(notOurs);
        });

        Directory.Delete(blocked);
        Assert.That(ModSuiteMigration.Run(_bepinex, _config, Now.AddMinutes(1), null).LayoutReady, Is.True, "The next start finishes the move.");
    }

    [Test]
    public void Old_files_after_the_move_are_removed_and_anything_new_in_them_is_packed_first()
    {
        SeedOldLayout(sessions: 0);
        ModSuiteMigration.Run(_bepinex, _config, Now, null);
        string trackers = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, "trackers");

        // An older version run after a rollback writes its own ledger back into the old place.
        Directory.CreateDirectory(Path.Combine(_config, "BlueSageWorkRelax"));
        File.WriteAllText(Path.Combine(_config, "BlueSageWorkRelax", ProfileFile), "written-by-rollback");

        ModSuiteMigrationResult result = ModSuiteMigration.Run(_bepinex, _config, Now.AddHours(1), null);
        string zip = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, "archive", "2026-10-23_184500-pre-0.2.7-layout.zip");
        Assert.Multiple(() =>
        {
            Assert.That(result.LayoutReady, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(trackers, "work-relax-" + ProfileFile)), Is.EqualTo("ledger"), "The live tracker is not overwritten.");
            Assert.That(Directory.Exists(Path.Combine(_config, "BlueSageWorkRelax")), Is.False);
            Assert.That(File.Exists(zip), "The rollback's file is kept in the archive.");
        });
        using ZipArchive packed = ZipFile.OpenRead(zip);
        using StreamReader reader = new StreamReader(packed.GetEntry("config/BlueSageWorkRelax/" + ProfileFile)!.Open());
        Assert.That(reader.ReadToEnd(), Is.EqualTo("written-by-rollback"));
    }

    [Test]
    public void Old_sessions_past_what_026_kept_are_the_only_ones_not_zipped()
    {
        string audit = Path.Combine(_bepinex, "BlueSageAudit");
        DateTime nowUtc = Now.ToUniversalTime();
        List<DirectoryInfo> sessions = new List<DirectoryInfo>();
        for (int index = 0; index < 15; index++)
        {
            DirectoryInfo session = Directory.CreateDirectory(Path.Combine(audit, "session-" + index.ToString("00")));
            File.WriteAllText(Path.Combine(session.FullName, "chat-audit.jsonl"), "x");
            session.CreationTimeUtc = nowUtc.AddHours(-index);
            sessions.Add(session);
        }

        DirectoryInfo ancient = Directory.CreateDirectory(Path.Combine(audit, "session-ancient"));
        ancient.CreationTimeUtc = nowUtc.AddDays(-20);
        sessions.Add(ancient);

        List<DirectoryInfo> kept = ModSuiteMigration.SelectOldSessionsToKeep(sessions, nowUtc, out int dropped);
        Assert.Multiple(() =>
        {
            Assert.That(kept.Select(item => item.Name), Is.EqualTo(Enumerable.Range(0, 12).Select(index => "session-" + index.ToString("00"))), "The 12 newest, newest first.");
            Assert.That(dropped, Is.EqualTo(4), "3 past the count, 1 past 14 days.");
        });
    }

    [Test]
    public void A_move_that_keeps_failing_before_the_marker_never_piles_up_zips()
    {
        SeedOldLayout(sessions: 1);
        string marker = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, ModSuiteLayoutPolicy.MarkerFileName);
        Directory.CreateDirectory(marker);
        Assert.That(ModSuiteMigration.Run(_bepinex, _config, Now, null).LayoutReady, Is.False);
        string archive = Path.Combine(_bepinex, ModSuiteLayoutPolicy.RootFolderName, "archive");
        string firstZip = Directory.GetFiles(archive, "*-pre-0.2.7-layout.zip").Single();
        DateTime firstWrite = File.GetLastWriteTimeUtc(firstZip);
        Assert.That(ModSuiteMigration.Run(_bepinex, _config, Now.AddMinutes(1), null).LayoutReady, Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFiles(archive, "*-pre-0.2.7-layout.zip"), Is.EqualTo(new[] { firstZip }), "The first attempt's zip is reused: no second zip.");
            Assert.That(File.GetLastWriteTimeUtc(firstZip), Is.EqualTo(firstWrite), "Not rebuilt either, so a failing move costs no repeated pause at start.");
            Assert.That(File.ReadAllText(Path.Combine(_config, "BlueSageWorkRelax", ProfileFile)), Is.EqualTo("ledger"), "Old files untouched while the move cannot finish.");
        });
    }

    [Test]
    public void After_the_move_trouble_with_old_leftovers_never_sends_the_player_back_to_the_old_folders()
    {
        string storage = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "ModSuiteStorage.cs")).Replace("\r\n", "\n");
        int markerBranch = storage.IndexOf("if (File.Exists(marker))", StringComparison.Ordinal);
        string branch = storage.Substring(markerBranch, storage.IndexOf("\n        }\n", markerBranch, StringComparison.Ordinal) - markerBranch);
        Assert.Multiple(() =>
        {
            Assert.That(branch, Does.Contain("try"));
            Assert.That(branch, Does.Contain("catch (Exception ex)"));
            Assert.That(branch.Substring(branch.IndexOf("catch (Exception ex)", StringComparison.Ordinal)), Does.Contain("new ModSuiteMigrationResult(true, false,"), "Once the marker exists the new folder stays authoritative.");
        });
    }

    [Test]
    public void The_whole_folder_never_stays_over_one_gigabyte_oldest_goes_first_live_files_never()
    {
        long gib = 1024L * 1024L * 1024L;
        long mib = 1024L * 1024L;
        long now = Now.ToUniversalTime().Ticks;
        List<ModSuiteItem> items = new List<ModSuiteItem>
        {
            new ModSuiteItem("archive/2026-10-20_120000-session-logs.zip", now - TimeSpan.TicksPerDay * 3, 400 * mib),
            new ModSuiteItem("archive/2026-10-12_120000-pre-0.2.7-layout.zip", now - TimeSpan.TicksPerDay * 11, 300 * mib),
            new ModSuiteItem("logs/2026-10-21_120000-session", now - TimeSpan.TicksPerDay * 2, 300 * mib),
            new ModSuiteItem("logs/2026-10-23_170000-session", now - TimeSpan.TicksPerHour, 200 * mib, isCurrent: true),
        };

        IList<string> evict = ModSuiteLayoutPolicy.FolderCeilingEvictions(items, trackersBytes: 100 * mib);
        Assert.Multiple(() =>
        {
            Assert.That(evict, Is.EqualTo(new[] { "archive/2026-10-12_120000-pre-0.2.7-layout.zip" }), "1.3 GB: the oldest item goes first, even the rollback copy, and that is enough.");
            Assert.That(ModSuiteLayoutPolicy.FolderCeilingEvictions(items, trackersBytes: 2 * gib), Does.Not.Contain("logs/2026-10-23_170000-session"), "The session being written now is never removed.");
            Assert.That(ModSuiteLayoutPolicy.FolderCeilingEvictions(items.Take(1).ToList(), trackersBytes: 0), Is.Empty, "Under the ceiling nothing goes.");
        });
    }

    [Test]
    public void Retention_enforces_the_one_gigabyte_ceiling_on_disk()
    {
        ModSuiteMigration.Run(_bepinex, _config, Now, null);
        ModSuiteLayout layout = ModSuiteLayout.For(_bepinex, _config, legacyMode: false);
        string current = Path.Combine(layout.Logs, "2026-10-23_170000-session");
        Directory.CreateDirectory(current);
        File.WriteAllText(Path.Combine(current, "2026-10-23_170000-chat.jsonl"), "live");
        string older = Path.Combine(layout.Archive, "2026-10-22_120000-session-logs.zip");
        string newer = Path.Combine(layout.Archive, "2026-10-23_100000-session-logs.zip");
        using (FileStream stream = new FileStream(older, FileMode.CreateNew)) { stream.SetLength(10); }
        using (FileStream stream = new FileStream(newer, FileMode.CreateNew)) { stream.SetLength(10); }

        long folder = Directory.GetFiles(layout.Root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
        string summary = ModSuiteRetention.Run(layout, current, Now.ToUniversalTime().Ticks, ceilingBytes: folder - 5);
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(older), Is.False, "Oldest first.");
            Assert.That(File.Exists(newer), Is.True);
            Assert.That(File.Exists(Path.Combine(current, "2026-10-23_170000-chat.jsonl")), "The live session stays.");
            Assert.That(summary, Does.Contain("over the size ceiling"));
        });
    }

    [Test]
    public void Only_our_own_folders_can_ever_be_deleted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_bepinex, "BlueSageQoLModSuite", "logs", "x"), _bepinex, _config), Is.True);
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_bepinex, "BlueSageAudit", "session-1"), _bepinex, _config), Is.True);
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_config, "BlueSageWorkRelax", ProfileFile), _bepinex, _config), Is.True);
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_bepinex, "LogOutput.log"), _bepinex, _config), Is.False);
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_config, "com.bluesage.ontogether.qoltweaks.beta.cfg"), _bepinex, _config), Is.False);
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_config, "BlueSageChalkboards", "a.bschalk"), _bepinex, _config), Is.False);
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_config, "com.bluesage.ontogether.qol-avatar-styles.store"), _bepinex, _config), Is.False);
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_config, "BlueSage", "CommunityBridge", "x"), _bepinex, _config), Is.False, "Private companion folders are not ours to clean here.");
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_bepinex, "BlueSageQoLModSuite", "..", "plugins", "x.dll"), _bepinex, _config), Is.False, "No escape with ..");
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_bepinex, "BlueSageQoLModSuiteEvil", "x"), _bepinex, _config), Is.False, "A look-alike folder name is not ours.");
            Assert.That(ModSuiteLayoutPolicy.IsDeletableOwnedPath(Path.Combine(_root, "OnTogether_Data", "x"), _bepinex, _config), Is.False);
        });
    }

    [Test]
    public void Retention_packs_older_sessions_into_zips_and_keeps_the_live_one()
    {
        ModSuiteMigration.Run(_bepinex, _config, Now, null);
        ModSuiteLayout layout = ModSuiteLayout.For(_bepinex, _config, legacyMode: false);
        string[] stamps = { "2026-10-23_120000", "2026-10-22_120000", "2026-10-21_220000", "2026-10-21_120000", "2026-10-10_120000" };
        foreach (string stamp in stamps)
        {
            string session = Path.Combine(layout.Logs, stamp + "-session");
            Directory.CreateDirectory(session);
            File.WriteAllText(Path.Combine(session, stamp + "-chat.jsonl"), "chat " + stamp);
        }

        File.WriteAllText(Path.Combine(layout.Archive, "2026-09-01_120000-session-logs.zip"), "old");
        string current = Path.Combine(layout.Logs, "2026-10-10_120000-session");
        string summary = ModSuiteRetention.Run(layout, current, Now.ToUniversalTime().Ticks);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(current), "The live session is never moved, however old its name.");
            Assert.That(Directory.Exists(Path.Combine(layout.Logs, "2026-10-23_120000-session")));
            Assert.That(Directory.Exists(Path.Combine(layout.Logs, "2026-10-22_120000-session")));
            Assert.That(Directory.Exists(Path.Combine(layout.Logs, "2026-10-21_120000-session")), Is.False, "Beyond 3 sessions: packed.");
            Assert.That(File.Exists(Path.Combine(layout.Archive, "2026-10-21_120000-session-logs.zip")));
            Assert.That(File.Exists(Path.Combine(layout.Archive, "2026-09-01_120000-session-logs.zip")), Is.False, "Zips older than 14 days are removed.");
            Assert.That(Directory.Exists(Path.Combine(layout.Logs, "2026-10-21_220000-session")), "3 newest finished sessions stay raw.");
            Assert.That(summary, Does.StartWith("archived 1 sessions, removed 1 old zips"));
        });
    }

    [Test]
    public void Archive_budget_never_drops_the_pre_layout_copy_and_zips_are_never_overwritten()
    {
        long mib = 1024L * 1024L;
        long now = Now.ToUniversalTime().Ticks;
        List<ModSuiteItem> zips = new List<ModSuiteItem>
        {
            new ModSuiteItem("2026-10-01_120000-pre-0.2.7-layout.zip", now - TimeSpan.TicksPerDay * 12, 90 * mib),
            new ModSuiteItem("2026-10-02_120000-session-logs.zip", now - TimeSpan.TicksPerDay * 11, 30 * mib),
            new ModSuiteItem("2026-10-20_120000-session-logs.zip", now - TimeSpan.TicksPerDay * 3, 30 * mib),
        };
        Assert.That(ModSuiteLayoutPolicy.ArchivesToDelete(zips, now), Is.EqualTo(new[] { "2026-10-02_120000-session-logs.zip" }));

        string zip = Path.Combine(_root, "a.zip");
        File.WriteAllText(zip, "existing");
        Assert.That(ModSuiteZip.TryCreate(zip, new List<KeyValuePair<string, string>>(), out string error), Is.False);
        Assert.That(File.ReadAllText(zip), Is.EqualTo("existing"), error);
    }

    [Test]
    public void File_names_carry_the_local_date_time_and_what_they_are()
    {
        DateTime when = new DateTime(2026, 10, 23, 9, 5, 7, DateTimeKind.Local);
        Assert.Multiple(() =>
        {
            Assert.That(ModSuiteLayoutPolicy.SessionFolderName(when), Is.EqualTo("2026-10-23_090507-session"));
            Assert.That(ModSuiteLayoutPolicy.ArchiveFileName(when, "host-journal"), Is.EqualTo("2026-10-23_090507-host-journal.zip"));
            Assert.That(ModSuiteLayoutPolicy.SnapshotFileName(when), Is.EqualTo("2026-10-23_090507-snapshot-game-log.log"));
            Assert.That(ModSuiteLayoutPolicy.IsSessionFolderName("2026-10-23_090507-session"), Is.True);
            Assert.That(ModSuiteLayoutPolicy.IsSessionFolderName("session-20261023T090507Z-abcd1234"), Is.False);
            Assert.That(ModSuiteMigration.ReadmeText, Does.Not.Contain("Exclusive").And.Not.Contain("Inspector").And.Not.Contain("steward").And.Not.Contain("Discord"), "Public folder, public words.");
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("BlueSage_QoL_Tweaks_Beta project root not found.");
    }

    private Dictionary<string, byte[]> SeedNotOurs()
    {
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>
        {
            [Path.Combine(_bepinex, "LogOutput.log")] = new byte[] { 1, 2, 3 },
            [Path.Combine(_config, "com.bluesage.ontogether.qoltweaks.beta.cfg")] = new byte[] { 4 },
            [Path.Combine(_config, "another.mod.cfg")] = new byte[] { 5 },
            [Path.Combine(_config, "com.bluesage.ontogether.qol-avatar-styles.store")] = new byte[] { 6 },
            [Path.Combine(_config, "com.bluesage.ontogether.qol-avatar-styles.cfg")] = new byte[] { 7 },
            [Path.Combine(_config, "BlueSageChalkboards", "board.bschalk")] = new byte[] { 8 },
            [Path.Combine(_config, "BlueSage", "CommunityBridge", "state.json")] = new byte[] { 9 },
            [Path.Combine(_bepinex, "plugins", "Another", "Another.dll")] = new byte[] { 10 },
            [Path.Combine(_root, "OnTogether_Data", "save.dat")] = new byte[] { 11 },
        };
        foreach (KeyValuePair<string, byte[]> file in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.Key)!);
            File.WriteAllBytes(file.Key, file.Value);
        }

        return files;
    }

    private static void AssertUntouched(Dictionary<string, byte[]> files)
    {
        foreach (KeyValuePair<string, byte[]> file in files)
        {
            Assert.That(File.Exists(file.Key), "Not ours, must stay: " + file.Key);
            Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), "Not ours, must be unchanged: " + file.Key);
        }
    }

    private void SeedOldLayout(int sessions)
    {
        string workRelax = Path.Combine(_config, "BlueSageWorkRelax");
        Directory.CreateDirectory(workRelax);
        File.WriteAllText(Path.Combine(workRelax, ProfileFile), "ledger");
        File.WriteAllText(Path.Combine(workRelax, ProfileFile + ".bak"), "ledger-bak");
        File.WriteAllText(Path.Combine(workRelax, ProfileFile + ".tmp"), "half-written");
        string audit = Path.Combine(_bepinex, "BlueSageAudit");
        Directory.CreateDirectory(Path.Combine(audit, "host-journal"));
        File.WriteAllText(Path.Combine(audit, "host-journal", "host-journal-v1.jsonl"), "journal");
        File.WriteAllText(Path.Combine(audit, "host-journal", "host-journal-v1.jsonl.prev"), "journal-prev");
        File.WriteAllText(Path.Combine(audit, "run-marker.v1"), "marker");
        string attest = Path.Combine(_config, "BlueSageAudit");
        Directory.CreateDirectory(attest);
        File.WriteAllText(Path.Combine(attest, "community-ban-source-attestation.json"), "attest");
        File.WriteAllText(Path.Combine(attest, "community-ban-source-attestation.json.bak"), "attest-bak");
        for (int index = 0; index < sessions; index++)
        {
            DateTime started = new DateTime(2026, 10, 19 + index, 12, 0, 0, DateTimeKind.Utc);
            string session = Path.Combine(audit, "session-" + started.ToString("yyyyMMdd") + "T120000Z-abcd123" + index);
            Directory.CreateDirectory(Path.Combine(session, "snapshots"));
            File.WriteAllText(Path.Combine(session, "chat-audit.jsonl"), "chat " + index);
            File.WriteAllText(Path.Combine(session, "bepinex.log"), "log " + index);
            Directory.SetCreationTimeUtc(session, started);
        }
    }
}
