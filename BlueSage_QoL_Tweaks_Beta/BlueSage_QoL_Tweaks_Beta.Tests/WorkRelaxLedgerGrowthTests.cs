using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage G (Marbles, 2026-10-20): "Native reconciliation" rows arrived about 17 per autosave and were 30% of
// Blue's 2.6 MB ledger. They are catch-all Unclassified adjustments with no correlation, so same-day rows for the
// same metric and mode merge into one row unless a correction already targets it. Totals never change.
public sealed class WorkRelaxLedgerGrowthTests
{
    private const string ProfileKey = "p_cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static readonly DateTime Day1 = new(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Same_day_reconciliation_rows_merge_into_one_row_per_metric_and_mode()
    {
        WorkRelaxLedger ledger = NewLedger();

        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 30.75, Day1));
        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 29.5, Day1.AddMinutes(3)));
        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 31.0, Day1.AddHours(5)));
        ledger.AppendReconciliationEvent(Reconciliation("TotalFocusDuration", 60, Day1.AddMinutes(3)));

        IReadOnlyList<WorkRelaxLedgerEvent> events = ledger.Events;
        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Count.EqualTo(2));
            WorkRelaxLedgerEvent seconds = events.Single(item => item.MetricKey == "SecondsPlayed");
            Assert.That(seconds.Amount, Is.EqualTo(91.25).Within(0.0001));
            Assert.That(seconds.UtcTicks, Is.EqualTo(Day1.AddHours(5).Ticks), "the merged row keeps the latest time");
            Assert.That(seconds.Source, Is.EqualTo(WorkRelaxLedger.ReconciliationSource));
            Assert.That(seconds.Mode, Is.EqualTo("Unclassified"));
        });
    }

    [Test]
    public void Rows_on_different_utc_days_stay_separate()
    {
        WorkRelaxLedger ledger = NewLedger();
        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 10, Day1));
        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 10, Day1.AddDays(1)));

        Assert.That(ledger.Events, Has.Count.EqualTo(2));
    }

    [Test]
    public void A_row_targeted_by_a_correction_is_never_merged_into()
    {
        WorkRelaxLedger ledger = NewLedger();
        WorkRelaxLedgerEvent first = Reconciliation("SecondsPlayed", 10, Day1);
        ledger.AppendReconciliationEvent(first);
        ledger.AppendCorrection(new WorkRelaxLedgerEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            UtcTicks = Day1.AddMinutes(1).Ticks,
            MetricFamily = first.MetricFamily,
            MetricKey = first.MetricKey,
            Amount = 10,
            Mode = "Work",
            Source = "BlueSage Report",
            TargetEventId = first.EventId,
            BatchId = "batch-1"
        });

        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 5, Day1.AddMinutes(3)));

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Events, Has.Count.EqualTo(2));
            Assert.That(ledger.Events.Single(item => item.EventId == first.EventId).Amount, Is.EqualTo(10));
        });
    }

    [Test]
    public void Merging_keeps_the_reports_effective_balances_identical()
    {
        WorkRelaxLedger merged = NewLedger();
        WorkRelaxLedger unmerged = NewLedger();
        for (int i = 0; i < 50; i++)
        {
            WorkRelaxLedgerEvent row = Reconciliation(i % 2 == 0 ? "SecondsPlayed" : "TotalFocusDuration", 1.5 + i, Day1.AddMinutes(3 * i));
            merged.AppendReconciliationEvent(row);
            unmerged.AppendEvent(row);
        }

        Assert.Multiple(() =>
        {
            Assert.That(merged.Events, Has.Count.EqualTo(2));
            Assert.That(unmerged.Events, Has.Count.EqualTo(50));
            foreach (string metric in new[] { "SecondsPlayed", "TotalFocusDuration" })
            {
                double mergedTotal = merged.Events.Where(item => item.MetricKey == metric).Sum(item => item.Amount);
                double unmergedTotal = unmerged.Events.Where(item => item.MetricKey == metric).Sum(item => item.Amount);
                Assert.That(mergedTotal, Is.EqualTo(unmergedTotal).Within(0.000001), metric);
            }
        });
    }

    [Test]
    public void Merged_ledger_round_trips_through_the_codec()
    {
        WorkRelaxLedger ledger = NewLedger();
        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 10, Day1));
        ledger.AppendReconciliationEvent(Reconciliation("SecondsPlayed", 10, Day1.AddMinutes(3)));

        WorkRelaxLedger back = WorkRelaxLedgerCodec.Deserialize(WorkRelaxLedgerCodec.Serialize(ledger));
        Assert.That(back.Events.Single().Amount, Is.EqualTo(20));
    }

    [Test]
    public void Runtime_appends_reconciliation_rows_through_the_merging_path()
    {
        string runtime = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "WorkRelaxRuntimeController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(runtime, Does.Contain("_ledger.AppendReconciliationEvent("));
            Assert.That(runtime, Does.Contain("Source = WorkRelaxLedger.ReconciliationSource"));
            Assert.That(runtime, Does.Not.Contain("Source = \"Native reconciliation\""));
        });
    }

    // Round 5 Stage A (Blue, 2026-10-22): one row per focus minute made a 5.3 MB file rewritten every minute. Focus
    // and activity minutes now join the current stretch; a completed pomodoro or a 10-minute gap closes it.
    [Test]
    public void Focus_minutes_of_one_pomodoro_become_one_row_per_metric_and_a_completion_closes_it()
    {
        WorkRelaxLedger ledger = NewLedger();
        DateTime start = Day1;
        for (int minute = 0; minute < 25; minute++)
        {
            ledger.AppendEvent(Tick("PomodoroDuration", "TotalFocusDuration", 60, start.AddMinutes(minute)));
            ledger.AppendEvent(Tick("PomodoroDuration", "TodayFocusDuration", 60, start.AddMinutes(minute)));
        }
        ledger.AppendEvent(Tick("PomodoroSession", "TotalSessionCount", 1, start.AddMinutes(25), "StatisticManager.UpdateFocusStatistics"));
        // 5-minute break, then the next pomodoro: under the gap, but the completion row closed the stretch.
        for (int minute = 30; minute < 55; minute++)
        {
            ledger.AppendEvent(Tick("PomodoroDuration", "TotalFocusDuration", 60, start.AddMinutes(minute)));
        }

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Events, Has.Count.EqualTo(4), "two focus-total stretches, one today stretch, one session row");
            Assert.That(ledger.Events.Where(item => item.MetricKey == "TotalFocusDuration").Select(item => item.Amount), Is.EqualTo(new[] { 1500d, 1500d }));
            Assert.That(ledger.Events.Single(item => item.MetricKey == "TodayFocusDuration").Amount, Is.EqualTo(1500d));
            Assert.That(ledger.Events.First().UtcTicks, Is.EqualTo(start.AddMinutes(24).Ticks), "a stretch carries its last minute");
            Assert.That(ledger.Events.Sum(item => item.Amount), Is.EqualTo(1500d * 3 + 1));
        });
    }

    [Test]
    public void Forty_eight_hours_at_the_observed_rate_stay_small_and_keep_totals()
    {
        WorkRelaxLedger ledger = NewLedger();
        double appended = 0;
        for (int hour = 0; hour < 48; hour++)
        {
            DateTime start = Day1.AddHours(hour);
            for (int minute = 0; minute < 25; minute++)
            {
                DateTime at = start.AddMinutes(minute);
                foreach (string key in new[] { "TotalFocusDuration", "TodayFocusDuration", "WeekFocusDuration" })
                {
                    ledger.AppendEvent(Tick("PomodoroDuration", key, 60, at));
                    appended += 60;
                }
                ledger.AppendEvent(Tick("ActivityDuration", "ActivityDuration:Reading", 60, at, "StatisticManager.UpdateStatisticsDurations"));
                appended += 60;
            }
            ledger.AppendEvent(Tick("PomodoroSession", "TotalSessionCount", 1, start.AddMinutes(25), "StatisticManager.UpdateFocusStatistics"));
            appended += 1;
        }

        string json = WorkRelaxLedgerCodec.Serialize(ledger);
        Assert.Multiple(() =>
        {
            Assert.That(ledger.Events, Has.Count.EqualTo(48 * 5), "per pomodoro: three focus stretches, one activity stretch, one session row");
            Assert.That(ledger.Events.Sum(item => item.Amount), Is.EqualTo(appended).Within(0.0001));
            Assert.That(json.Length, Is.LessThan(150_000), "two days under 150 KB (was ~5.3 MB)");
        });
    }

    [Test]
    public void Coalesce_history_folds_a_ledger_written_before_this_release_with_identical_totals()
    {
        // A pre-0.2.7 file holds one row per focus minute. Build it through the codec (AppendEvent would merge).
        string skeleton = WorkRelaxLedgerCodec.Serialize(NewLedger());
        var rows = new System.Text.StringBuilder();
        for (int minute = 0; minute < 30; minute++)
        {
            if (minute > 0) rows.Append(',');
            rows.Append("{\"id\":\"raw").Append(minute).Append("\",\"utc\":").Append(Day1.AddMinutes(minute).Ticks)
                .Append(",\"family\":\"PomodoroDuration\",\"metric\":\"TotalFocusDuration\",\"amount\":60,\"mode\":\"Work\",\"source\":\"StatisticManager.UpdatePomodoroTime\",\"correlation\":\"session-")
                .Append(minute).Append("\"}");
        }
        WorkRelaxLedger raw = WorkRelaxLedgerCodec.Deserialize(skeleton.Replace("\"events\":[]", "\"events\":[" + rows + "]"));
        Assert.That(raw.Events, Has.Count.EqualTo(30), "the fixture must deserialize as raw rows");

        int removed = raw.CoalesceHistory();
        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(29));
            Assert.That(raw.Events.Single().Amount, Is.EqualTo(1800d));
            Assert.That(raw.Events.Single().EventId, Is.EqualTo("raw0"), "the stretch keeps the first minute's identity and correlation");
            Assert.That(raw.CoalesceHistory(), Is.EqualTo(0), "idempotent");
        });
    }

    [Test]
    public void The_writer_clone_is_independent_of_later_joins()
    {
        WorkRelaxLedger ledger = NewLedger();
        ledger.AppendEvent(Tick("PomodoroDuration", "TotalFocusDuration", 60, Day1));
        WorkRelaxLedger copy = ledger.Clone();
        ledger.AppendEvent(Tick("PomodoroDuration", "TotalFocusDuration", 60, Day1.AddMinutes(1)));
        Assert.Multiple(() =>
        {
            Assert.That(ledger.Events.Single().Amount, Is.EqualTo(120d));
            Assert.That(copy.Events.Single().Amount, Is.EqualTo(60d), "the background writer's copy never sees later merges");
        });
    }

    private static WorkRelaxLedgerEvent Tick(string family, string key, double amount, DateTime utc, string source = "StatisticManager.UpdatePomodoroTime") => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        UtcTicks = utc.Ticks,
        MetricFamily = family,
        MetricKey = key,
        Amount = amount,
        Mode = "Work",
        Source = source,
        CorrelationId = "session-" + Guid.NewGuid().ToString("N")
    };

    private static WorkRelaxLedger NewLedger() => new() { SchemaVersion = 1, ProfileKey = ProfileKey };

    private static WorkRelaxLedgerEvent Reconciliation(string metric, double amount, DateTime utc) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        UtcTicks = utc.Ticks,
        MetricFamily = metric == "SecondsPlayed" ? "SecondsPlayed" : "PomodoroDuration",
        MetricKey = metric,
        Amount = amount,
        Mode = "Unclassified",
        Source = WorkRelaxLedger.ReconciliationSource
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
