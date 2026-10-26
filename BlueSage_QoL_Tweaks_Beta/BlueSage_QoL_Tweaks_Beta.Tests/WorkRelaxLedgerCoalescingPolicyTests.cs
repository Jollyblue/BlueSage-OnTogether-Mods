using System;
using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 Stage A: a focus minute joins the current stretch; a completed pomodoro or a 10-minute gap closes it.
[TestFixture]
internal sealed class WorkRelaxLedgerCoalescingPolicyTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 14, 0, 0, DateTimeKind.Utc);
    private const string Pomodoro = "StatisticManager.UpdatePomodoroTime";

    private static WorkRelaxLedgerEvent Row(string family, string key, string mode, DateTime utc, string source = Pomodoro, double amount = 60) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        UtcTicks = utc.Ticks,
        MetricFamily = family,
        MetricKey = key,
        Amount = amount,
        Mode = mode,
        Source = source,
    };

    private static bool NeverTargeted(string id) => false;

    [Test]
    public void Only_duration_families_coalesce()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerCoalescingPolicy.IsCoalescable("PomodoroDuration"), Is.True);
            Assert.That(WorkRelaxLedgerCoalescingPolicy.IsCoalescable("ActivityDuration"), Is.True);
            Assert.That(WorkRelaxLedgerCoalescingPolicy.IsCoalescable("PomodoroSession"), Is.False);
            Assert.That(WorkRelaxLedgerCoalescingPolicy.IsCoalescable("Mood"), Is.False);
            Assert.That(WorkRelaxLedgerCoalescingPolicy.IsCoalescable("AchievementObservation"), Is.False);
            Assert.That(WorkRelaxLedgerCoalescingPolicy.IsCoalescable(null), Is.False);
        });
    }

    [Test]
    public void A_minute_joins_the_newest_row_with_the_same_key_mode_and_source()
    {
        var rows = new List<WorkRelaxLedgerEvent> { Row("PomodoroDuration", "TotalFocusDuration", "Work", T0) };
        int index = WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(1)), NeverTargeted);
        Assert.That(index, Is.EqualTo(0));
    }

    [Test]
    public void Different_mode_key_or_source_never_merge()
    {
        var rows = new List<WorkRelaxLedgerEvent> { Row("PomodoroDuration", "TotalFocusDuration", "Work", T0) };
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Relax", T0.AddMinutes(1)), NeverTargeted), Is.EqualTo(-1));
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TodayFocusDuration", "Work", T0.AddMinutes(1)), NeverTargeted), Is.EqualTo(-1));
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(1), source: "other"), NeverTargeted), Is.EqualTo(-1));
        });
    }

    [Test]
    public void A_gap_over_ten_minutes_starts_a_new_stretch()
    {
        var rows = new List<WorkRelaxLedgerEvent> { Row("PomodoroDuration", "TotalFocusDuration", "Work", T0) };
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(10)), NeverTargeted), Is.EqualTo(0), "exactly the gap still joins");
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(11)), NeverTargeted), Is.EqualTo(-1));
        });
    }

    [Test]
    public void A_completed_pomodoro_closes_the_focus_stretch_but_not_an_activity_stretch()
    {
        var rows = new List<WorkRelaxLedgerEvent>
        {
            Row("PomodoroDuration", "TotalFocusDuration", "Work", T0),
            Row("ActivityDuration", "ActivityDuration:Reading", "Work", T0, source: "StatisticManager.UpdateStatisticsDurations"),
            Row("PomodoroSession", "TotalSessionCount", "Work", T0.AddSeconds(30), source: "StatisticManager.UpdateFocusStatistics", amount: 1),
        };
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(1)), NeverTargeted), Is.EqualTo(-1), "the game said a pomodoro completed");
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("ActivityDuration", "ActivityDuration:Reading", "Work", T0.AddMinutes(1), source: "StatisticManager.UpdateStatisticsDurations"), NeverTargeted), Is.EqualTo(1));
        });
    }

    [Test]
    public void A_correction_targeted_row_is_frozen_and_a_clock_step_back_never_merges()
    {
        var rows = new List<WorkRelaxLedgerEvent> { Row("PomodoroDuration", "TotalFocusDuration", "Work", T0) };
        string frozen = rows[0].EventId;
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(1)), id => id == frozen), Is.EqualTo(-1));
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(-1)), NeverTargeted), Is.EqualTo(-1));
        });
    }

    [Test]
    public void The_backward_scan_stops_at_the_first_row_older_than_the_gap()
    {
        // 1,000 old rows, then one fresh one: the match must be found without walking the old rows.
        var rows = new List<WorkRelaxLedgerEvent>();
        for (int i = 0; i < 1000; i++) rows.Add(Row("ActivityDuration", "ActivityDuration:Old", "Work", T0.AddDays(-1).AddMinutes(i)));
        rows.Add(Row("PomodoroDuration", "TotalFocusDuration", "Work", T0));
        int calls = 0;
        bool Targeted(string id) { calls++; return false; }
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(1)), Targeted), Is.EqualTo(1000));
            Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("ActivityDuration", "ActivityDuration:Old", "Work", T0.AddMinutes(1)), Targeted), Is.EqualTo(-1), "the old activity rows are out of reach");
            // One targeted check per incoming row plus one for the single match; never one per old row.
            Assert.That(calls, Is.EqualTo(3));
        });
    }

    [Test]
    public void A_stretch_never_spans_a_utc_day()
    {
        DateTime lateNight = new(2026, 10, 22, 23, 59, 0, DateTimeKind.Utc);
        var rows = new List<WorkRelaxLedgerEvent> { Row("PomodoroDuration", "TotalFocusDuration", "Work", lateNight) };
        Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", lateNight.AddMinutes(1)), NeverTargeted), Is.EqualTo(-1),
            "00:00 the next day starts a new row so the day-boundary fold never sees half a stretch");
    }

    [Test]
    public void An_old_stamped_mood_row_appended_by_a_report_refresh_does_not_split_a_stretch()
    {
        var rows = new List<WorkRelaxLedgerEvent>
        {
            Row("PomodoroDuration", "TotalFocusDuration", "Work", T0),
            Row("Mood", "Mood:2", "Work", T0.AddHours(-5), source: "Mood observation", amount: 1),
        };
        Assert.That(WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rows, Row("PomodoroDuration", "TotalFocusDuration", "Work", T0.AddMinutes(1)), NeverTargeted), Is.EqualTo(0));
    }

    [Test]
    public void Compaction_schedule_fires_once_per_utc_day_roll()
    {
        long day = new DateTime(2026, 10, 22, 23, 59, 0, DateTimeKind.Utc).Ticks;
        long initDay = WorkRelaxLedgerCompactionSchedulePolicy.UtcDay(day);
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerCompactionSchedulePolicy.IsDue(0, day), Is.False, "never before the ledger initialized");
            Assert.That(WorkRelaxLedgerCompactionSchedulePolicy.IsDue(initDay, day), Is.False);
            Assert.That(WorkRelaxLedgerCompactionSchedulePolicy.IsDue(initDay, day + TimeSpan.TicksPerMinute * 2), Is.True, "00:01 the next UTC day");
        });
    }
}
