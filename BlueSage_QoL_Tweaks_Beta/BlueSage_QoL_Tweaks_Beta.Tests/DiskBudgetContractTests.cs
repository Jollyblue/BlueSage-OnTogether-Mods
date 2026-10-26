using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5d (Blue, 2026-10-23): "Advisor must approve we've IMPROVED not REGRESSED or repeated past mistakes."
// Every source file that writes to disk is named here with the rule that keeps it small. A new writer fails this
// test until someone states its ceiling, so a growing file can never ship by accident again (the 0.2.6 audit
// folder reached 27 MB on Blue's PC, the host journal wrote a row on every join and leave).
public sealed class DiskBudgetContractTests
{
    private static readonly string Root = FindProjectRoot();

    private static readonly Regex WriterPattern = new Regex(
        @"FileMode\.Append|File\.AppendAll|File\.WriteAll|FileMode\.Create|File\.Copy\(|File\.Replace\(|new StreamWriter\(",
        RegexOptions.Compiled);

    // Path relative to src -> why it cannot grow without bound.
    private static readonly Dictionary<string, string> KnownWriters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AvatarSlotStore.cs"] = "whole-file rewrite of nine avatar slots (stays in config: the player's own creations)",
        ["ChalkboardPersistenceController.cs"] = "whole-file rewrite of one binary chalkboard (stays in config: the player's own creations)",
        ["CommunityBanListController.cs"] = "whole-file rewrite of the small ban-list attestation in trackers/",
        ["Core/AuditSnapshotPolicy.cs"] = "manual snapshot, newest 8 MiB of the log, two kept per session",
        ["HostJournalFile.cs"] = "append, one 512 KiB page; full pages are zipped into archive/",
        ["ModSuiteStorage.cs"] = "the one-time move and the retention pass: copy, zip, owned-path deletes only",
        ["SessionAuditController.cs"] = "optional (off by default), parts capped; sessions zipped after 3 sessions / 3 days / 24 MiB",
        ["SessionRunMarkerController.cs"] = "whole-file rewrite of one marker",
        ["WorkRelaxLedgerStore.cs"] = "whole-file rewrite; rows join into stretches and older days fold into daily rows",
    };

    [Test]
    public void Every_disk_writer_is_named_with_its_ceiling()
    {
        string src = Path.Combine(Root, "src");
        string[] writers = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => WriterPattern.IsMatch(File.ReadAllText(path)))
            .Select(path => path.Substring(src.Length + 1).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] unknown = writers.Where(path => !KnownWriters.ContainsKey(path)).ToArray();
        string[] gone = KnownWriters.Keys.Where(path => !writers.Contains(path, StringComparer.OrdinalIgnoreCase)).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(unknown, Is.Empty, "A new file writer needs a stated ceiling here (and a cap in code) before it ships.");
            Assert.That(gone, Is.Empty, "A listed writer no longer writes; remove it from the list.");
        });
    }

    [Test]
    public void Growing_writers_use_the_shared_budget_constants()
    {
        string journal = Read("src", "HostJournalFile.cs");
        string audit = Read("src", "SessionAuditController.cs");
        string snapshot = Read("src", "Core", "AuditSnapshotPolicy.cs");
        string plugin = Read("src", "Plugin.cs");
        Assert.Multiple(() =>
        {
            Assert.That(journal, Does.Contain("DefaultMaxBytes = LocalFileBudgetPolicy.HostJournalMaxBytes"));
            Assert.That(audit, Does.Contain("LocalFileBudgetPolicy.AuditChatPartBytes"));
            Assert.That(audit, Does.Contain("LocalFileBudgetPolicy.AuditMirrorPartBytes"));
            Assert.That(audit, Does.Contain("ModSuitePaths.RunRetentionInBackground(_sessionDirectory)"));
            Assert.That(audit, Does.Contain("internal static string AuditRoot => ModSuitePaths.Current.Logs;"));
            Assert.That(audit, Does.Not.Contain("MaxRetainedSessions = 12"), "The 0.2.6 ceilings (12 sessions, 64 MiB mirror parts) are gone.");
            Assert.That(audit + journal + snapshot, Does.Not.Contain("\"BlueSageAudit\""), "No writer uses the old folder directly.");
            Assert.That(snapshot, Does.Contain("MaxRetainedSnapshots = LocalFileBudgetPolicy.AuditSnapshotsPerSession"));
            Assert.That(plugin, Does.Match(@"EnableLocalAuditExports\s*=\s*Config\.Bind\([^;]+,\s*false,"), "The local audit is optional: off unless the player turns it on.");
            Assert.That(plugin, Does.Contain("LocalFileBudgetPolicy.MigrateLocalAudit("));
            Assert.That(plugin, Does.Contain("ConfigMigrationPolicy.LocalAuditOffV6Migration"));
        });
    }

    [Test]
    public void Host_journal_status_rows_follow_real_changes_not_every_join_and_leave()
    {
        var a = BlueSage.QoLTweaks.Core.HostJournalRow.ForStatus(0, true, true, 12, 111, 11, 9, "healthy");
        var moreGuests = BlueSage.QoLTweaks.Core.HostJournalRow.ForStatus(0, true, true, 40, 111, 39, 38, "healthy");
        var invalid = BlueSage.QoLTweaks.Core.HostJournalRow.ForStatus(0, true, false, 40, 111, 39, 38, "healthy");
        var unhealthy = BlueSage.QoLTweaks.Core.HostJournalRow.ForStatus(0, true, true, 12, 111, 11, 9, "LobbyOwnerInvalid");
        Assert.Multiple(() =>
        {
            Assert.That(moreGuests.StatusKey(), Is.EqualTo(a.StatusKey()), "Player counts ride on the 5-minute heartbeat, not on every join.");
            Assert.That(invalid.StatusKey(), Is.Not.EqualTo(moreGuests.StatusKey()));
            Assert.That(unhealthy.StatusKey(), Is.Not.EqualTo(a.StatusKey()));
        });
    }

    [Test]
    public void An_oversized_journal_from_an_older_version_keeps_only_its_newest_rows_on_rotation()
    {
        string folder = Path.Combine(Path.GetTempPath(), "bluesage-hj-tail-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, HostJournalFile.FileName);
        try
        {
            Directory.CreateDirectory(folder);
            const long cap = 1024;
            File.WriteAllText(path, string.Concat(Enumerable.Range(0, 200).Select(i => "{\"row\":" + i.ToString("000") + "}\n")));
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(cap * 2), "Fixture: a journal written under the old, larger cap.");

            Assert.That(HostJournalFile.Append(path, "{\"row\":\"new\"}", cap, flushToDisk: false), Is.True);
            string previous = File.ReadAllText(path + ".prev");
            Assert.Multiple(() =>
            {
                Assert.That(new FileInfo(path + ".prev").Length, Is.LessThanOrEqualTo(cap));
                Assert.That(previous, Does.StartWith("{\"row\":"), "The kept tail starts at a whole line.");
                Assert.That(previous, Does.EndWith("{\"row\":199}\n"), "The newest old rows are the ones kept.");
                Assert.That(File.ReadAllText(path), Is.EqualTo("{\"row\":\"new\"}\n"));
            });
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { }
        }
    }

    [Test]
    public void A_full_journal_page_is_zipped_into_the_archive_and_a_fresh_page_starts()
    {
        string folder = Path.Combine(Path.GetTempPath(), "bluesage-hj-zip-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "trackers", "host-journal.jsonl");
        string archive = Path.Combine(folder, "archive");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Concat(Enumerable.Range(0, 100).Select(i => "{\"row\":" + i.ToString("000") + "}\n")));
            long oldLength = new FileInfo(path).Length;

            Assert.That(HostJournalFile.Append(path, "{\"row\":\"new\"}", 1024, flushToDisk: false, archiveDirectory: archive), Is.True);
            string zip = Directory.GetFiles(archive, "*-host-journal.zip").Single();
            using var packed = System.IO.Compression.ZipFile.OpenRead(zip);
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(path), Is.EqualTo("{\"row\":\"new\"}\n"), "A fresh page.");
                Assert.That(File.Exists(path + ".prev"), Is.False, "No .prev beside the live file any more.");
                Assert.That(packed.Entries.Single().FullName, Does.EndWith("-host-journal.jsonl"));
                Assert.That(packed.Entries.Single().Length, Is.EqualTo(oldLength), "The whole old page is in the zip.");
            });
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { }
        }
    }

    [Test]
    public void The_host_tools_now_line_follows_live_counts_while_the_file_stays_gated()
    {
        string controller = Read("src", "HostJournalController.cs");
        int latest = controller.IndexOf("_latestStatus = row;", StringComparison.Ordinal);
        int gate = controller.IndexOf("HostJournalStatusPolicy.ShouldWrite(", StringComparison.Ordinal);
        Assert.That(latest, Is.GreaterThan(0).And.LessThan(gate), "The in-memory status refreshes on every count change; only the ring and file wait for a change or the heartbeat.");
    }

    [Test]
    public void The_session_line_shows_how_big_our_files_are()
    {
        string line = BlueSage.QoLTweaks.Core.SessionSummaryPolicy.Format("6h", 360, true, 41, 86, 6, 709 * 1024, 72, 0, 72, 35.8, 966.6, 9, 0,
            journalBytes: 300 * 1024, folderBytes: 3 * 1024 * 1024);
        Assert.That(line, Does.EndWith("; warnings=9; errors=0; journalKB=300; folderMB=3."));
        string controller = Read("src", "SessionSummaryController.cs");
        Assert.That(controller, Does.Contain("journalBytes:").And.Contain("folderBytes:").And.Contain("ModSuitePaths.Current.Root"));
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new DirectoryNotFoundException("Could not find the BlueSage_QoL_Tweaks_Beta project root.");
        }

        return directory.FullName;
    }
}
