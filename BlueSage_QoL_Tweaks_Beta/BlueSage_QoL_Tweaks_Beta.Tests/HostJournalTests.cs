using System;
using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage D (Blue, 2026-10-19: "the host should always have audit and logs available alongside helper actions";
// Steam IDs are public and players should know who they truly play with). One host journal: action rows (who did
// what to whom, with the RECONNECTED/LOADING label and the surface it came from) and status rows (host/guest, lobby
// valid, Steam count/limit, live roster, transport-verified count, host health) feed a 50-row ring for the Host
// Tools box and a versioned, size-capped local JSONL file that is always on while the local player hosts.
[TestFixture]
public sealed class HostJournalTests
{
    private const long T0 = 638_000_000_000_000_000L;

    [Test]
    public void Action_rows_serialize_as_versioned_jsonl_with_escaped_text()
    {
        HostJournalRow row = HostJournalRow.ForAction(new DateTime(2026, 10, 20, 4, 50, 0, DateTimeKind.Utc).Ticks, "76561198000000001", "HOST", "ID card", "ban",
            "76561198000000002", "Na\"me\nwith newline", "PlayerID(7)", "RECONNECTED", true, "Native ban applied.");
        string line = HostJournalFormat.ToJsonLine(row);
        Assert.Multiple(() =>
        {
            Assert.That(line, Does.StartWith("{\"v\":1,\"kind\":\"action\",\"utc\":\"2026-10-20T04:50:00Z\""));
            Assert.That(line, Does.Contain("\"actor\":\"76561198000000001\",\"actorRole\":\"HOST\",\"surface\":\"ID card\",\"action\":\"ban\""));
            Assert.That(line, Does.Contain("\"targetSteamId\":\"76561198000000002\",\"targetName\":\"Na\\\"me\\nwith newline\",\"targetPurrNetId\":\"PlayerID(7)\",\"label\":\"RECONNECTED\",\"success\":true"));
            Assert.That(line, Does.EndWith("}"));
            Assert.That(line, Does.Not.Contain("\n"), "One row per line.");
        });
    }

    [Test]
    public void Status_rows_serialize_and_key_on_everything_but_the_clock()
    {
        HostJournalRow a = HostJournalRow.ForStatus(T0, true, true, 12, 111, 11, 9, "healthy");
        HostJournalRow b = HostJournalRow.ForStatus(T0 + TimeSpan.TicksPerMinute, true, true, 12, 111, 11, 9, "healthy");
        HostJournalRow c = HostJournalRow.ForStatus(T0, true, true, 13, 111, 11, 9, "healthy");
        string line = HostJournalFormat.ToJsonLine(a);
        Assert.Multiple(() =>
        {
            Assert.That(line, Does.Contain("\"kind\":\"status\""));
            Assert.That(line, Does.Contain("\"host\":true,\"lobbyValid\":true,\"steamCount\":12,\"steamLimit\":111,\"liveRoster\":11,\"transportVerified\":9,\"hostHealth\":\"healthy\""));
            Assert.That(a.StatusKey(), Is.EqualTo(b.StatusKey()), "The clock is not part of the change key.");
            Assert.That(a.StatusKey(), Is.EqualTo(c.StatusKey()), "Round 5d: a count change alone is not a change; the heartbeat row carries the counts.");
            Assert.That(a.StatusKey(), Is.Not.EqualTo(HostJournalRow.ForStatus(T0, true, false, 12, 111, 11, 9, "healthy").StatusKey()));
        });
    }

    [Test]
    public void Display_lines_are_short_and_name_the_player()
    {
        HostJournalRow action = HostJournalRow.ForAction(new DateTime(2026, 10, 20, 4, 50, 0, DateTimeKind.Utc).Ticks, "76561198000000001", "HELPER", "Report", "ban",
            "76561198000000002", "Cozy", "PlayerID(7)", "LOADING", false, "Refused: protected victim.");
        HostJournalRow status = HostJournalRow.ForStatus(T0, true, true, 12, 111, 11, 9, "healthy");
        Assert.Multiple(() =>
        {
            Assert.That(HostJournalFormat.ToDisplayLine(action), Does.Contain("BAN").And.Contain("HELPER").And.Contain("Report").And.Contain("Cozy").And.Contain("76561198000000002").And.Contain("LOADING").And.Contain("refused"));
            Assert.That(HostJournalFormat.ToDisplayLine(status), Does.Contain("host").And.Contain("12/111").And.Contain("in game 11").And.Contain("matched 9").And.Contain("healthy"));
        });
    }

    [Test]
    public void Ring_keeps_the_newest_fifty_rows_newest_first()
    {
        HostJournalRing ring = new HostJournalRing();
        for (int index = 0; index < 60; index++)
        {
            ring.Add(HostJournalRow.ForStatus(T0 + index, true, true, index, 111, 0, 0, "healthy"));
        }

        var snapshot = ring.SnapshotNewestFirst();
        Assert.Multiple(() =>
        {
            Assert.That(ring.Capacity, Is.EqualTo(50));
            Assert.That(snapshot.Count, Is.EqualTo(50));
            Assert.That(snapshot[0].SteamCount, Is.EqualTo(59));
            Assert.That(snapshot[49].SteamCount, Is.EqualTo(10));
        });
    }

    [Test]
    public void Status_rows_are_written_on_change_or_heartbeat_only()
    {
        long now = T0;
        long heartbeat = HostJournalStatusPolicy.HeartbeatTicks;
        Assert.Multiple(() =>
        {
            Assert.That(HostJournalStatusPolicy.ShouldWrite(null, "k1", 0, now), Is.True, "First status always writes.");
            Assert.That(HostJournalStatusPolicy.ShouldWrite("k1", "k1", now, now + heartbeat / 2), Is.False, "Unchanged inside the heartbeat window is noise.");
            Assert.That(HostJournalStatusPolicy.ShouldWrite("k1", "k2", now, now + 1), Is.True, "Any change writes at once.");
            Assert.That(HostJournalStatusPolicy.ShouldWrite("k1", "k1", now, now + heartbeat), Is.True, "The heartbeat proves the journal is alive.");
            Assert.That(heartbeat, Is.EqualTo(TimeSpan.FromMinutes(5).Ticks));
            // Advisor at the D gate: the host-health tick is also 5 minutes of realtime, so a strict comparison would
            // miss every other tick and double the effective heartbeat. 15 seconds of slack (deliberate pin change).
            Assert.That(HostJournalStatusPolicy.ShouldWrite("k1", "k1", now, now + heartbeat - TimeSpan.FromSeconds(10).Ticks), Is.True, "A tick that lands a few seconds early still counts as the heartbeat.");
            Assert.That(HostJournalStatusPolicy.ShouldWrite("k1", "k1", now, now + heartbeat - TimeSpan.FromSeconds(30).Ticks), Is.False);
        });
    }

    [Test]
    public void File_appends_lines_durably_and_rotates_once_at_the_size_cap()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-host-journal-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, HostJournalFile.FileName);
        try
        {
            string line = HostJournalFormat.ToJsonLine(HostJournalRow.ForStatus(T0, true, true, 1, 111, 1, 1, "healthy"));
            long cap = (line.Length + 1) * 3 + 5;
            Assert.Multiple(() =>
            {
                Assert.That(HostJournalFile.Append(path, line, cap), Is.False);
                Assert.That(HostJournalFile.Append(path, line, cap), Is.False);
                Assert.That(HostJournalFile.Append(path, line, cap), Is.False);
                Assert.That(File.ReadAllLines(path), Has.Length.EqualTo(3));
                Assert.That(HostJournalFile.Append(path, line, cap), Is.True, "The fourth line would pass the cap, so the file rotates first.");
                Assert.That(File.ReadAllLines(path), Has.Length.EqualTo(1));
                Assert.That(File.ReadAllLines(path + ".prev"), Has.Length.EqualTo(3), "The previous file is kept once.");
                Assert.That(Directory.GetFiles(dir), Has.Length.EqualTo(2), "Exactly the live file and one previous file.");
                Assert.That(File.ReadAllText(path), Does.EndWith("\n"));
            });

            for (int index = 0; index < 3; index++)
            {
                HostJournalFile.Append(path, line, cap);
            }

            Assert.That(File.ReadAllLines(path + ".prev"), Has.Length.EqualTo(3).Or.Length.EqualTo(1), "A second rotation replaces the previous file; nothing older is kept.");
            Assert.That(Directory.GetFiles(dir), Has.Length.EqualTo(2));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // Wiring: every host action through TryExecuteHostAction (commands, menu, helper relay, the routed vanilla
    // buttons) writes an action row; host health ticks and identity-coverage transitions write status rows (on change
    // plus heartbeat); the Host Tools tab shows the ring; guests write nothing.
    [Test]
    public void Journal_is_wired_to_host_actions_status_sources_and_the_host_tools_box()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string controller = File.ReadAllText(Path.Combine(root, "src", "HostJournalController.cs")).Replace("\r\n", "\n");
        string moderation = File.ReadAllText(Path.Combine(root, "src", "SteamIdModerationController.cs")).Replace("\r\n", "\n");
        string router = File.ReadAllText(Path.Combine(root, "src", "Patches", "NativeBanRoutePatch.cs")).Replace("\r\n", "\n");
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        string evidence = File.ReadAllText(Path.Combine(root, "src", "PlayerIdentityEvidenceController.cs")).Replace("\r\n", "\n");
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs")).Replace("\r\n", "\n");
        int execute = moderation.IndexOf("internal static bool TryExecuteHostAction(", StringComparison.Ordinal);
        string executeBody = moderation.Substring(execute, moderation.IndexOf("\n    }\n", execute, StringComparison.Ordinal) - execute);
        int health = plugin.IndexOf("private string RunHostHealthCheck(string reason)", StringComparison.Ordinal);
        string healthBody = plugin.Substring(health, plugin.IndexOf("\n    }\n", health, StringComparison.Ordinal) - health);
        Assert.Multiple(() =>
        {
            // Advisor at the D gate: action rows are not re-gated (TryExecuteHostAction already proved lobby ownership;
            // a second predicate could only lose rows), and status rows gate on the network host, the same signal the
            // ban router uses, not the stricter Steam-owner check that fails closed on a bad Steam read.
            Assert.That(controller, Does.Contain("NetworkManager.main != null && NetworkManager.main.isHost"), "Status rows: whenever the local player hosts (Blue's ruling); guests write nothing.");
            Assert.That(controller, Does.Not.Contain("CanUseLobbySafety"));
            Assert.That(controller, Does.Contain("Write(row, flushToDisk: true);"), "Action rows are the record: forced to disk.");
            Assert.That(controller, Does.Contain("Write(row, flushToDisk: false);"), "Status rows can arrive dozens of times in a join wave: no per-row fsync (WS2, no hitches).");
            Assert.That(controller, Does.Contain("internal static void RecordHostHealth(bool lobbyValid, string hostHealth)"), "One owner for the counts and the Steam limit (the coverage snapshot); health adds only its text.");
            Assert.That(controller, Does.Contain("HostJournalFile.DefaultMaxBytes"));
            Assert.That(controller, Does.Contain("ModSuitePaths.Current.HostJournalPath"), "Round 5d: BlueSageQoLModSuite/trackers/host-journal.jsonl.");
            Assert.That(controller, Does.Contain("ModSuitePaths.Current.HostJournalArchiveDirectory"), "Full pages are zipped into the archive.");
            Assert.That(controller, Does.Contain("HostJournalStatusPolicy.ShouldWrite("));
            Assert.That(executeBody, Does.Contain("HostJournalController.RecordAction(action, issuerSteamId, ownerSteamId, targetSteamId, success, message);"));
            Assert.That(moderation, Does.Contain("HostJournalController.NoteTarget("), "Rows carry the target's name, PurrNet id and label.");
            Assert.That(moderation, Does.Contain("HostJournalController.BeginSurface(\"helper relay\");"));
            Assert.That(router, Does.Contain("HostJournalController.BeginSurface(surface);"));
            Assert.That(router, Does.Contain("HostJournalController.EndSurface();"));
            Assert.That(healthBody, Does.Contain("HostJournalController.RecordHostHealth("));
            Assert.That(healthBody, Does.Contain("if (isManualRun || healthChanged)"), "Log diet: automatic ticks log only on change.");
            Assert.That(healthBody, Does.Contain("string healthKey = issueText;"), "The member counts are not part of the health log key; they live in the journal.");
            Assert.That(healthBody, Does.Contain("HostJournalController.RecordHostHealth(input.LobbyValid, issueText);"));
            Assert.That(evidence, Does.Contain("HostJournalController.RecordCoverage("), "The WS3 transport line becomes a status row.");
            Assert.That(menu, Does.Contain("DrawHostJournalBox();"));
            Assert.That(menu, Does.Contain("GUILayout.Label(\"Host journal\", _labelStyle);"), "Stage H: no row count in the title; two rings behind it");
            Assert.That(menu.IndexOf("DrawNativeBanRecords();", StringComparison.Ordinal), Is.LessThan(menu.IndexOf("DrawHostJournalBox();", StringComparison.Ordinal)), "Inside the host-only block.");
        });
    }

    [Test]
    public void Journal_file_lives_under_the_audit_root_with_a_versioned_name()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HostJournalFile.FileName, Is.EqualTo("host-journal-v1.jsonl"));
            Assert.That(HostJournalFile.DefaultMaxBytes, Is.EqualTo(512L * 1024), "Round 5d: 512 KiB plus one previous file.");
            Assert.That(HostJournalFile.Append(Path.Combine(Path.GetTempPath(), "bluesage-hj-" + Guid.NewGuid().ToString("N"), "sub", HostJournalFile.FileName), "{}", 1024), Is.False, "Missing folders are created.");
        });
    }

    [Test]
    public void Status_rows_append_without_a_per_row_fsync_and_still_land_in_order()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bluesage-host-journal-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, HostJournalFile.FileName);
        try
        {
            HostJournalFile.Append(path, "{\"a\":1}", 1024, flushToDisk: true);
            HostJournalFile.Append(path, "{\"b\":2}", 1024, flushToDisk: false);
            HostJournalFile.Append(path, "{\"c\":3}", 1024, flushToDisk: false);
            Assert.That(File.ReadAllLines(path), Is.EqualTo(new[] { "{\"a\":1}", "{\"b\":2}", "{\"c\":3}" }));
            string file = File.ReadAllText(Path.Combine(FindRoot(), "src", "HostJournalFile.cs"));
            Assert.That(file, Does.Contain("if (flushToDisk)"), "Only the caller decides whether a row is forced to disk.");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static string FindRoot()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return root;
    }
}
