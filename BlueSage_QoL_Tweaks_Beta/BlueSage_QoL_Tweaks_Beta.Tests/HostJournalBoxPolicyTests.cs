using System;
using System.Collections.Generic;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage H (Blue, 2026-10-20): "50 rows sounds excessive and a UI nightmare for hosts of 60-100 slots".
// Status rows are written on every count change, so in a big lobby they evicted the rare action rows from the shared
// ring within minutes. Now: two rings (actions 100, status 20), an actions-first box with one pinned status line,
// three filters, one-glance rows with the full line on hover, click to copy, "Copy last 20". File format v1 unchanged.
public sealed class HostJournalBoxPolicyTests
{
    private static readonly long T0 = new DateTime(2026, 10, 20, 19, 42, 0, DateTimeKind.Utc).Ticks;

    private static HostJournalRow Action(bool success, string label = "RECONNECTED", string action = "ban") =>
        HostJournalRow.ForAction(T0, "76561198000000001", "HELPER", "Player row", action, "76561198000000002", "Cozy", "PlayerID(7)", label, success, success ? "Banned." : "Refused: protected victim.");

    private static HostJournalRow Status(int steamCount) => HostJournalRow.ForStatus(T0, true, true, steamCount, 112, 85, 0, "ok");

    [Test]
    public void Actions_filter_shows_actions_status_filter_shows_status_refused_shows_only_failed_actions()
    {
        var actions = new List<HostJournalRow> { Action(true), Action(false), Action(true, "", "mute") };
        var status = new List<HostJournalRow> { Status(87), Status(86) };

        Assert.Multiple(() =>
        {
            Assert.That(HostJournalBoxPolicy.Select(actions, status, HostJournalBoxFilter.Actions), Is.EqualTo(actions));
            Assert.That(HostJournalBoxPolicy.Select(actions, status, HostJournalBoxFilter.Status), Is.EqualTo(status));
            IReadOnlyList<HostJournalRow> refused = HostJournalBoxPolicy.Select(actions, status, HostJournalBoxFilter.RefusedOnly);
            Assert.That(refused, Has.Count.EqualTo(1));
            Assert.That(refused[0].Success, Is.False);
        });
    }

    [Test]
    public void Short_lines_read_at_a_glance_and_the_full_line_stays_available()
    {
        string shortAction = HostJournalBoxPolicy.ToShortLine(Action(false));
        string shortMute = HostJournalBoxPolicy.ToShortLine(Action(true, "", "mute"));
        string shortStatus = HostJournalBoxPolicy.ToShortLine(Status(87));

        Assert.Multiple(() =>
        {
            Assert.That(shortAction, Does.StartWith(new DateTime(T0, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm")));
            Assert.That(shortAction, Does.Contain("BAN").And.Contain("HELPER").And.Contain("Cozy").And.Contain("…000002").And.Contain("RECONNECTED").And.Contain("refused"));
            Assert.That(shortAction, Does.Not.Contain("76561198000000002"), "the full Steam ID lives on hover and in the copy, not in the one-glance row");
            Assert.That(shortAction, Does.Not.Contain("Refused: protected victim."), "the reason lives on hover and in the copy");
            Assert.That(shortMute, Does.Contain("MUTE").And.Contain("ok").And.Not.Contain("[]").And.Not.Contain("  ok  ok"));
            Assert.That(shortStatus, Does.Contain("status").And.Contain("87/112").And.Contain("live 85").And.Contain("ok"));
            Assert.That(HostJournalFormat.ToDisplayLine(Action(false)), Does.Contain("76561198000000002").And.Contain("Refused: protected victim."));
        });
    }

    [Test]
    public void Pinned_status_line_names_the_current_picture_and_how_often_it_changed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HostJournalBoxPolicy.PinnedStatusLine(null, 0), Is.EqualTo("No status yet this session."));
            string line = HostJournalBoxPolicy.PinnedStatusLine(Status(87), 12);
            Assert.That(line, Does.StartWith("Now: you're hosting, lobby OK, 87 of 112 players, health ok."));
            Assert.That(line, Does.Not.Contain("live").And.Not.Contain("transport"), "player words only on the pinned line");
            Assert.That(line, Does.Contain("Lobby or health changed 12 times this session."));
            Assert.That(HostJournalBoxPolicy.PinnedStatusLine(Status(87), 1), Does.Contain("Lobby or health changed once this session."));
        });
    }

    [Test]
    public void Copy_block_takes_the_newest_rows_as_full_lines()
    {
        var rows = new List<HostJournalRow>();
        for (int i = 0; i < 30; i++)
        {
            rows.Add(HostJournalRow.ForAction(T0 + i, "1", "HOST", "command or menu", "ban", "76561198000000002", "P" + i, "", "", true, "ok"));
        }

        string block = HostJournalBoxPolicy.CopyBlock(rows, 20);
        string[] lines = block.Split(new[] { "\n" }, StringSplitOptions.None);
        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.EqualTo(20));
            Assert.That(lines[0], Is.EqualTo(HostJournalFormat.ToDisplayLine(rows[0])));
            Assert.That(HostJournalBoxPolicy.CopyBlock(new List<HostJournalRow>(), 20), Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Controller_keeps_two_rings_so_status_rows_never_evict_actions()
    {
        string controller = ReadSource("src", "HostJournalController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("ActionRing = new HostJournalRing(100)"));
            Assert.That(controller, Does.Contain("StatusRing = new HostJournalRing(20)"));
            Assert.That(controller, Does.Contain("internal static IReadOnlyList<HostJournalRow> SnapshotActionsNewestFirst()"));
            Assert.That(controller, Does.Contain("internal static IReadOnlyList<HostJournalRow> SnapshotStatusNewestFirst()"));
            Assert.That(controller, Does.Contain("internal static HostJournalRow LatestStatus"));
            Assert.That(controller, Does.Contain("internal static int StatusChangeCount"));
            Assert.That(controller, Does.Not.Contain("SnapshotNewestFirst() =>"), "the shared ring is gone");
        });
    }

    [Test]
    public void Host_tools_box_is_actions_first_with_filters_hover_detail_and_copy()
    {
        string menu = ReadSource("src", "QolMenuWindow.cs");
        int start = menu.IndexOf("private void DrawHostJournalBox()", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        int end = menu.IndexOf("\n    private void ", start + 10, StringComparison.Ordinal);
        string body = menu.Substring(start, end - start);

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("GUILayout.Label(\"Host journal\", _labelStyle);"));
            Assert.That(body, Does.Contain("HostJournalBoxPolicy.PinnedStatusLine(HostJournalController.LatestStatus, HostJournalController.StatusChangeCount)"));
            Assert.That(body, Does.Contain("HostJournalBoxPolicy.Select("));
            Assert.That(body.Split(new[] { "GUILayout.Toggle(" }, StringSplitOptions.None).Length - 1, Is.GreaterThanOrEqualTo(3), "Actions / Status / Refused only");
            Assert.That(body, Does.Contain("new GUIContent(HostJournalBoxPolicy.ToShortLine(row), HostJournalFormat.ToDisplayLine(row))"), "one-glance row, full line on hover");
            Assert.That(body, Does.Contain("GUIUtility.systemCopyBuffer = HostJournalFormat.ToDisplayLine(row);"), "click copies the full row");
            Assert.That(body, Does.Contain("\"Copy last 20\""));
            Assert.That(body, Does.Contain("HostJournalBoxPolicy.CopyBlock("));
            Assert.That(body, Does.Contain("row.Kind == HostJournalRowKind.Action && !row.Success"), "refused rows stand out");
            Assert.That(body, Does.Not.Contain("last 50"));
        });
    }

    private static string ReadSource(params string[] pathParts)
    {
        string path = FindProjectRoot();
        foreach (string part in pathParts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
