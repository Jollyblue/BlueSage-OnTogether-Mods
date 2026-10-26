using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5d (Blue: "rework the Work-Relax tracker logic and file"; advisor-cleared design): the game's Today/Week/Month
// counters move with their lifetime Total in the same call, so they are no longer stored as their own rows. The
// Report works out their Work / Relax split from the newest Total rows, up to the game's own value.
public sealed class WorkRelaxDerivedMetricTests
{
    private static readonly long T0 = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc).Ticks;

    [Test]
    public void Exactly_the_six_today_week_month_counters_are_derived_from_their_totals()
    {
        Dictionary<string, string> derived = WorkRelaxMetricCatalog.NativeMetrics.Where(item => item.IsDerived).ToDictionary(item => item.Key, item => item.DerivedFrom!);
        Assert.That(derived, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["TodayFocusDuration"] = "TotalFocusDuration",
            ["WeekFocusDuration"] = "TotalFocusDuration",
            ["MonthFocusDuration"] = "TotalFocusDuration",
            ["TodaySessionCount"] = "TotalSessionCount",
            ["WeekFocusCount"] = "TotalSessionCount",
            ["MonthFocusCount"] = "TotalSessionCount",
        }));
        Assert.That(WorkRelaxMetricCatalog.IsDerived("TotalCompleteSessions"), Is.False, "Kept: not proven equal to the session count.");
        Assert.That(WorkRelaxMetricCatalog.IsDerived("FocusSessionsPerDay[3]"), Is.False);
    }

    [Test]
    public void The_split_is_the_newest_total_rows_in_time_order_not_list_order()
    {
        List<WorkRelaxLedgerEvent> events = new List<WorkRelaxLedgerEvent>
        {
            Row("newest", "TotalFocusDuration", 15, "Work", T0 + 3),
            Row("oldest", "TotalFocusDuration", 50, "Work", T0 + 1),
            Row("middle", "TotalFocusDuration", 20, "Relax", T0 + 2),
            Row("other", "ArtFocusDuration", 99, "Relax", T0 + 4),
        };

        ModeAllocation split = BlueSageReportProjection.DeriveRecentSplit(30, events, Array.Empty<WorkRelaxLedgerEvent>(), "TotalFocusDuration");
        Assert.Multiple(() =>
        {
            Assert.That(split.Work, Is.EqualTo(15), "The newest row first.");
            Assert.That(split.Relax, Is.EqualTo(15), "The edge row counts only the part still needed.");
            Assert.That(split.Unclassified, Is.EqualTo(0));
        });
    }

    [Test]
    public void Anything_the_rows_do_not_cover_is_unclassified_and_the_sum_is_the_games_value()
    {
        List<WorkRelaxLedgerEvent> events = new List<WorkRelaxLedgerEvent> { Row("a", "TotalSessionCount", 2, "Work", T0) };
        ModeAllocation split = BlueSageReportProjection.DeriveRecentSplit(5, events, Array.Empty<WorkRelaxLedgerEvent>(), "TotalSessionCount");
        Assert.Multiple(() =>
        {
            Assert.That(split.Work, Is.EqualTo(2));
            Assert.That(split.Unclassified, Is.EqualTo(3));
            Assert.That(BlueSageReportProjection.DeriveRecentSplit(0, events, Array.Empty<WorkRelaxLedgerEvent>(), "TotalSessionCount").Work, Is.EqualTo(0), "A new day starts at zero.");
        });
    }

    [Test]
    public void A_row_moved_by_a_correction_counts_in_its_new_mode_and_aggregate_allocations_do_not_apply()
    {
        List<WorkRelaxLedgerEvent> events = new List<WorkRelaxLedgerEvent>
        {
            Row("r1", "TotalFocusDuration", 30, "Work", T0 + 2),
            Row("r0", "TotalFocusDuration", 30, "Work", T0 + 1),
        };
        List<WorkRelaxLedgerEvent> corrections = new List<WorkRelaxLedgerEvent>
        {
            Correction("c1", "TotalFocusDuration", 30, "Move:Work->Relax", "r1", T0 + 5),
            Correction("c2", "TotalFocusDuration", 30, "Move:Work->Relax", "aggregate-unclassified", T0 + 6),
        };

        ModeAllocation split = BlueSageReportProjection.DeriveRecentSplit(45, events, corrections, "TotalFocusDuration");
        Assert.Multiple(() =>
        {
            Assert.That(split.Relax, Is.EqualTo(30), "The corrected newest row counts as Relax.");
            Assert.That(split.Work, Is.EqualTo(15), "The aggregate allocation shapes the lifetime total only.");
        });
    }

    [Test]
    public void The_one_time_strip_removes_repeated_rows_keeps_correction_targets_and_is_idempotent()
    {
        WorkRelaxLedger ledger = WorkRelaxLedgerCodec.Deserialize(WorkRelaxLedgerCodec.Serialize(new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = "p_" + new string('a', 64) }));
        ledger.AppendEvent(Row("total", "TotalFocusDuration", 60, "Work", T0));
        ledger.AppendEvent(Row("today", "TodayFocusDuration", 60, "Work", T0 + TimeSpan.TicksPerHour));
        ledger.AppendEvent(Row("week-targeted", "WeekFocusDuration", 60, "Work", T0 + 2 * TimeSpan.TicksPerHour));
        ledger.AppendEvent(Row("month", "MonthFocusCount", 1, "Work", T0 + 3 * TimeSpan.TicksPerHour));
        ledger.AppendCorrection(Correction("fix", "WeekFocusDuration", 60, "Move:Work->Relax", "week-targeted", T0 + 4 * TimeSpan.TicksPerHour));

        int stripped = ledger.StripDerivedRows();
        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.EqualTo(2));
            Assert.That(ledger.Events.Select(item => item.EventId), Is.EquivalentTo(new[] { "total", "week-targeted" }));
            Assert.That(ledger.Corrections.Single().TargetEventId, Is.EqualTo("week-targeted"), "Corrections are never touched.");
            Assert.That(ledger.StripDerivedRows(), Is.EqualTo(0));
            Assert.That(ledger.SchemaVersion, Is.EqualTo(1), "0.2.6 throws away any other schema number.");
        });
    }

    [Test]
    public void Undo_skips_an_old_tweak_that_only_moved_a_today_week_month_number()
    {
        WorkRelaxLedgerEvent older = Correction("c-total", "TotalFocusDuration", 30, "Move:Work->Relax", "r1", T0 + 1);
        older.BatchId = "batch-total";
        WorkRelaxLedgerEvent newestDerived = Correction("c-today", "TodayFocusDuration", 30, "Move:Unclassified->Work", "aggregate-unclassified", T0 + 2);
        newestDerived.BatchId = "batch-today";

        IReadOnlyList<WorkRelaxLedgerEvent>? next = WorkRelaxUndoHistoryPolicy.FindNextUndoableBatch(new[] { older, newestDerived });
        Assert.That(next?.Single().EventId, Is.EqualTo("c-total"), "Undo goes to the newest tweak that still means something, instead of getting stuck.");
    }

    [Test]
    public void The_report_shows_only_the_plain_sentence_of_a_refused_change()
    {
        string net46Style = "Use a whole number." + Environment.NewLine + "Parameter name: amount";
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxCorrectionPolicy.PlayerMessage(new ArgumentException(net46Style)), Is.EqualTo("Use a whole number."));
            Assert.That(WorkRelaxCorrectionPolicy.PlayerMessage(new InvalidOperationException("That's more than is there to move.")), Is.EqualTo("That's more than is there to move."));
        });
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        Assert.That(File.ReadAllText(Path.Combine(root, "src", "WorkRelaxRuntimeController.cs")), Does.Not.Contain("reason = exception.Message;"));
    }

    [Test]
    public void Recorder_reconciliation_and_allocation_editor_all_skip_derived_keys()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string runtime = File.ReadAllText(Path.Combine(root, "src", "WorkRelaxRuntimeController.cs"));
        string projection = File.ReadAllText(Path.Combine(root, "src", "Core", "BlueSageReportProjection.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(runtime, Does.Contain("if (metric.IsDerived)"), "Not recorded.");
            Assert.That(runtime, Does.Contain("!metric.IsAdditive || metric.IsDerived ||"), "Not reconciled (no Unclassified row per save).");
            Assert.That(runtime.IndexOf("_ledger.StripDerivedRows()", StringComparison.Ordinal), Is.LessThan(runtime.IndexOf("_ledger.CompactHistory(initUtcTicks)", StringComparison.Ordinal)), "Strip before the fold.");
            Assert.That(projection, Does.Contain(".Where(item => item.IsAdditive && !item.IsDerived)"), "Not offered in the allocation editor.");
            Assert.That(projection, Does.Contain("DeriveRecentSplit(nativeValue, eventList, correctionList, definition.DerivedFrom!)"));
        });
    }

    private static WorkRelaxLedgerEvent Row(string id, string key, double amount, string mode, long ticks) => new WorkRelaxLedgerEvent
    {
        EventId = id,
        UtcTicks = ticks,
        MetricFamily = WorkRelaxMetricCatalog.Get(key).Family.ToString(),
        MetricKey = key,
        Amount = amount,
        Mode = mode,
        Source = "test",
        CorrelationId = "session-" + id,
    };

    private static WorkRelaxLedgerEvent Correction(string id, string key, double amount, string source, string target, long ticks) => new WorkRelaxLedgerEvent
    {
        EventId = id,
        UtcTicks = ticks,
        MetricFamily = WorkRelaxMetricCatalog.Get(key).Family.ToString(),
        MetricKey = key,
        Amount = amount,
        Mode = "Relax",
        Source = source,
        TargetEventId = target,
    };
}
