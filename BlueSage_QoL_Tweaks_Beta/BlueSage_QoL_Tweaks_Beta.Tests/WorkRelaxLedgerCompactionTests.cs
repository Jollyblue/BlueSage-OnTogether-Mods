using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage G (Marbles, 2026-10-20): an 11 MB ledger (~46,000 rows) was rewritten at every autosave. History
// older than 7 days folds into one row per UTC day, metric and mode; sums never change; corrections keep their targets.
public sealed class WorkRelaxLedgerCompactionTests
{
    private const string ProfileKey = "p_dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Retention_keeps_today_and_yesterday_correctable()
    {
        Assert.That(WorkRelaxLedgerCompactionPolicy.RetentionDays, Is.EqualTo(2));
    }

    [Test]
    public void Reconciliation_rows_fold_per_day_even_inside_the_retention_window()
    {
        WorkRelaxLedger ledger = NewLedger();
        DateTime today = Now.AddHours(-3);
        for (int i = 0; i < 17; i++)
        {
            ledger.AppendEvent(Row("SecondsPlayed", 30, "Unclassified", today.AddMinutes(3 * i), WorkRelaxLedger.ReconciliationSource, null));
        }
        ledger.AppendEvent(Row("TotalFocusDuration", 60, "Work", today, "StatisticManager.UpdatePomodoroTime", "session-today"));

        int removed = ledger.CompactHistory(Now.Ticks);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(16));
            Assert.That(ledger.Events.Single(item => item.MetricKey == "SecondsPlayed").Amount, Is.EqualTo(510));
            Assert.That(ledger.Events.Single(item => item.CorrelationId == "session-today").Source, Is.EqualTo("StatisticManager.UpdatePomodoroTime"));
        });
    }

    [Test]
    public void Old_rows_fold_per_day_metric_and_mode_and_recent_rows_stay()
    {
        WorkRelaxLedger ledger = NewLedger();
        DateTime oldDay = Now.AddDays(-20);
        for (int minute = 0; minute < 60; minute++)
        {
            ledger.AppendEvent(Row("TotalFocusDuration", 60, "Work", oldDay.AddMinutes(minute), "StatisticManager.UpdatePomodoroTime", "session-a" + minute));
            ledger.AppendEvent(Row("SecondsPlayed", 30, "Unclassified", oldDay.AddMinutes(minute), WorkRelaxLedger.ReconciliationSource, null));
        }
        ledger.AppendEvent(Row("TotalFocusDuration", 60, "Relax", oldDay.AddDays(1), "StatisticManager.UpdatePomodoroTime", "session-b"));
        ledger.AppendEvent(Row("TotalFocusDuration", 60, "Work", Now.AddDays(-2), "StatisticManager.UpdatePomodoroTime", "session-recent"));

        int removed = ledger.CompactHistory(Now.Ticks);
        IReadOnlyList<WorkRelaxLedgerEvent> events = ledger.Events;

        Assert.Multiple(() =>
        {
            // Round 5 Stage A: the 60 focus minutes already joined one stretch at append time, so the ledger held
            // 1 + 60 + 1 + 1 rows before compaction; the fold still leaves the same four.
            Assert.That(removed, Is.EqualTo(63 - 4));
            Assert.That(events, Has.Count.EqualTo(4));
            WorkRelaxLedgerEvent focusWork = events.Single(item => item.MetricKey == "TotalFocusDuration" && item.Mode == "Work" && item.Source == WorkRelaxLedgerCompactionPolicy.CompactedSource);
            Assert.That(focusWork.Amount, Is.EqualTo(3600));
            Assert.That(focusWork.CorrelationId, Is.EqualTo("compacted:" + oldDay.ToString("yyyy-MM-dd") + ":Work"));
            Assert.That(focusWork.UtcTicks, Is.EqualTo(oldDay.AddMinutes(59).Ticks));
            Assert.That(events.Single(item => item.MetricKey == "SecondsPlayed").Amount, Is.EqualTo(1800));
            Assert.That(events.Single(item => item.Mode == "Relax").Amount, Is.EqualTo(60));
            Assert.That(events.Single(item => item.CorrelationId == "session-recent").Source, Is.EqualTo("StatisticManager.UpdatePomodoroTime"), "recent rows keep their session identity");
            Assert.That(events.Select(item => item.UtcTicks), Is.Ordered);
        });
    }

    [Test]
    public void Rows_targeted_by_a_correction_survive_untouched()
    {
        WorkRelaxLedger ledger = NewLedger();
        DateTime oldDay = Now.AddDays(-30);
        WorkRelaxLedgerEvent corrected = Row("TotalFocusDuration", 60, "Work", oldDay, "StatisticManager.UpdatePomodoroTime", "session-x");
        ledger.AppendEvent(corrected);
        // Round 5 Stage A: a corrected row is frozen, so the next minute starts its own row instead of joining it.
        ledger.AppendCorrection(new WorkRelaxLedgerEvent
        {
            EventId = Guid.NewGuid().ToString("N"), UtcTicks = oldDay.AddHours(1).Ticks, MetricFamily = corrected.MetricFamily,
            MetricKey = corrected.MetricKey, Amount = 60, Mode = "Relax", Source = "BlueSage Report", TargetEventId = corrected.EventId, BatchId = "b1"
        });
        ledger.AppendEvent(Row("TotalFocusDuration", 60, "Work", oldDay.AddMinutes(1), "StatisticManager.UpdatePomodoroTime", "session-x"));

        ledger.CompactHistory(Now.Ticks);

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Events, Has.Count.EqualTo(2));
            Assert.That(ledger.Events.Any(item => item.EventId == corrected.EventId && item.Amount == 60 && item.Source == "StatisticManager.UpdatePomodoroTime"), Is.True);
            Assert.That(ledger.Corrections.Single().TargetEventId, Is.EqualTo(corrected.EventId));
        });
    }

    [Test]
    public void Sums_per_metric_and_mode_are_identical_before_and_after()
    {
        WorkRelaxLedger ledger = NewLedger();
        var random = new Random(20261020);
        string[] metrics = { "TotalFocusDuration", "SecondsPlayed", "ArtFocusDuration" };
        string[] modes = { "Work", "Relax", "Unclassified" };
        for (int i = 0; i < 2000; i++)
        {
            ledger.AppendEvent(Row(metrics[i % 3], random.Next(1, 120), modes[i % 3], Now.AddDays(-random.Next(0, 40)).AddMinutes(-i), "source", "c" + i));
        }

        Dictionary<string, double> before = Totals(ledger.Events);
        int removed = ledger.CompactHistory(Now.Ticks);
        Dictionary<string, double> after = Totals(ledger.Events);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.GreaterThan(1000));
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (KeyValuePair<string, double> pair in before)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value).Within(0.000001), pair.Key);
            }
        });
    }

    [Test]
    public void Compaction_is_idempotent_and_round_trips()
    {
        WorkRelaxLedger ledger = NewLedger();
        for (int i = 0; i < 100; i++)
        {
            ledger.AppendEvent(Row("SecondsPlayed", 1, "Unclassified", Now.AddDays(-10).AddMinutes(i), WorkRelaxLedger.ReconciliationSource, null));
        }

        ledger.CompactHistory(Now.Ticks);
        WorkRelaxLedger back = WorkRelaxLedgerCodec.Deserialize(WorkRelaxLedgerCodec.Serialize(ledger));
        int second = back.CompactHistory(Now.Ticks);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.EqualTo(0));
            Assert.That(back.Events.Single().Amount, Is.EqualTo(100));
        });
    }

    [Test]
    public void Runtime_compacts_once_per_session_on_initialize()
    {
        string runtime = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "WorkRelaxRuntimeController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(runtime, Does.Contain("_ledger.CompactHistory(initUtcTicks)"));
            Assert.That(runtime, Does.Contain("MarkDirty(prompt: true, \"history compaction\")"));
        });
    }

    private static Dictionary<string, double> Totals(IEnumerable<WorkRelaxLedgerEvent> events) =>
        events.GroupBy(item => item.MetricKey + "|" + item.Mode).ToDictionary(group => group.Key, group => group.Sum(item => item.Amount), StringComparer.Ordinal);

    private static WorkRelaxLedger NewLedger() => new() { SchemaVersion = 1, ProfileKey = ProfileKey };

    private static WorkRelaxLedgerEvent Row(string metric, double amount, string mode, DateTime utc, string source, string? correlation) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        UtcTicks = utc.Ticks,
        MetricFamily = metric == "SecondsPlayed" ? "SecondsPlayed" : metric == "ArtFocusDuration" ? "ActivityDuration" : "PomodoroDuration",
        MetricKey = metric,
        Amount = amount,
        Mode = mode,
        Source = source,
        CorrelationId = correlation
    };

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
