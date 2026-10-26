using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
internal sealed class BlueSageReportProjectionTests
{
    [Test]
    public void All_mirrors_every_native_field_and_classified_totals_never_exceed_it()
    {
        NativeStatisticsSnapshot native = NativeFixture();
        WorkRelaxLedgerEvent[] events =
        {
            Event("w", "TotalFocusDuration", 20, "Work"),
            Event("r", "TotalFocusDuration", 30, "Relax"),
            Event("u", "TotalFocusDuration", 10, "Unclassified"),
            Event("too-much", "ToDoCompleted", 999, "Work")
        };

        BlueSageReportProjection report = BlueSageReportProjection.Build(native, events,
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<BlueSageMoodObservation>(),
            System.Array.Empty<BlueSageAchievementObservation>());

        Assert.That(report.All.Metrics.Count, Is.EqualTo(41));
        foreach (string name in NativeStatisticsSnapshot.FieldNames)
        {
            BlueSageReportMetric all = report.All.Get(name);
            if (name == "FocusSessionsPerDay")
            {
                Assert.That(all.WeekdayValues, Is.EqualTo(new[] { 1d, 2d, 3d, 4d, 5d, 6d, 7d }));
            }
            else
            {
                Assert.That(all.Value, Is.EqualTo(native.GetNumber(name)));
            }

            if (report.Work.Get(name).Qualifier != "whole game, not split")
            {
                Assert.That(report.Work.Get(name).Value + report.Relax.Get(name).Value +
                            report.Unclassified.Get(name).Value, Is.LessThanOrEqualTo(all.Value + 0.001d));
            }
        }
    }

    [Test]
    public void Non_additive_fields_are_native_overall_and_clock_units_stay_distinct()
    {
        BlueSageReportProjection report = BlueSageReportProjection.Build(NativeFixture(),
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<WorkRelaxLedgerEvent>(),
            System.Array.Empty<BlueSageMoodObservation>(), System.Array.Empty<BlueSageAchievementObservation>());

        Assert.That(report.Work.Get("CurrentStreak").Qualifier, Is.EqualTo("whole game, not split"));
        Assert.That(report.Work.Get("CurrentStreak").Value, Is.EqualTo(8));
        Assert.That(report.All.Get("TotalFocusDuration").UnitLabel, Is.EqualTo("seconds"));
        Assert.That(report.All.Get("ArtFocusDuration").UnitLabel, Is.EqualTo("minutes"));
    }

    [Test]
    public void Mood_uses_only_feel_and_time_and_achievement_wording_is_observational()
    {
        BlueSageMoodObservation[] moods =
        {
            new BlueSageMoodObservation(4, 638896000000000000L, WorkRelaxAttributionMode.Work)
        };
        BlueSageAchievementObservation[] achievements =
        {
            new BlueSageAchievementObservation("first-hour", true, WorkRelaxAttributionMode.Work, 638896000000000000L),
            new BlueSageAchievementObservation("tenth-hour", true, WorkRelaxAttributionMode.Relax, 638896000000000001L)
        };

        BlueSageReportProjection report = BlueSageReportProjection.Build(NativeFixture(),
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<WorkRelaxLedgerEvent>(), moods, achievements);

        Assert.That(report.Work.Moods[0].Feel, Is.EqualTo(4));
        Assert.That(report.Work.Moods[0].UtcTicks, Is.EqualTo(638896000000000000L));
        Assert.That(report.Work.Achievements[0].ObservationLabel, Is.EqualTo("During Work"));
        Assert.That(report.Relax.Achievements[0].ObservationLabel, Is.EqualTo("During Relax"));
    }

    [Test]
    public void Weekday_buckets_are_classified_without_relabeling_the_native_array_as_overall()
    {
        WorkRelaxLedgerEvent[] events =
        {
            Event("weekday-work", "FocusSessionsPerDay[0]", 1, "Work"),
            Event("weekday-relax", "FocusSessionsPerDay[1]", 2, "Relax")
        };
        BlueSageReportProjection report = BlueSageReportProjection.Build(NativeFixture(), events,
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<BlueSageMoodObservation>(),
            System.Array.Empty<BlueSageAchievementObservation>());

        Assert.That(report.Work.Get("FocusSessionsPerDay").Qualifier, Is.EqualTo("Attributed"));
        Assert.That(report.Work.Get("FocusSessionsPerDay").WeekdayValues, Is.EqualTo(new[] { 1d, 0d, 0d, 0d, 0d, 0d, 0d }));
        Assert.That(report.Relax.Get("FocusSessionsPerDay").WeekdayValues, Is.EqualTo(new[] { 0d, 2d, 0d, 0d, 0d, 0d, 0d }));
    }

    [Test]
    public void Empty_first_refresh_persists_baselines_and_the_next_observation_uses_active_mode()
    {
        var recovery = new WorkRelaxLedgerRecovery();

        WorkRelaxAttributionMode firstMood = WorkRelaxObservationBaselinePolicy.ClassifyMood(recovery, WorkRelaxMode.Relax);
        WorkRelaxAttributionMode firstAchievement = WorkRelaxObservationBaselinePolicy.ClassifyAchievement(recovery, WorkRelaxMode.Relax);
        WorkRelaxObservationBaselinePolicy.MarkMoodInitialized(recovery);
        WorkRelaxObservationBaselinePolicy.MarkAchievementInitialized(recovery);

        Assert.That(firstMood, Is.EqualTo(WorkRelaxAttributionMode.Unclassified));
        Assert.That(firstAchievement, Is.EqualTo(WorkRelaxAttributionMode.Unclassified));
        Assert.That(WorkRelaxObservationBaselinePolicy.ClassifyMood(recovery, WorkRelaxMode.Relax), Is.EqualTo(WorkRelaxAttributionMode.Relax));
        Assert.That(WorkRelaxObservationBaselinePolicy.ClassifyAchievement(recovery, WorkRelaxMode.Work), Is.EqualTo(WorkRelaxAttributionMode.Work));
    }

    [Test]
    public void Failed_native_source_does_not_poison_baseline_and_recovered_history_is_unclassified()
    {
        var recovery = new WorkRelaxLedgerRecovery();
        WorkRelaxAttributionMode? failedMood = WorkRelaxObservationRefreshPolicy.BeginMoodRefresh(
            recovery, sourceAvailable: false, WorkRelaxMode.Relax);
        WorkRelaxAttributionMode? failedAchievement = WorkRelaxObservationRefreshPolicy.BeginAchievementRefresh(
            recovery, sourceAvailable: false, WorkRelaxMode.Relax);

        Assert.That(failedMood, Is.Null);
        Assert.That(failedAchievement, Is.Null);
        Assert.That(recovery.MoodBaselineInitialized, Is.False);
        Assert.That(recovery.AchievementBaselineInitialized, Is.False);
        Assert.That(WorkRelaxObservationRefreshPolicy.BeginMoodRefresh(recovery, true, WorkRelaxMode.Relax),
            Is.EqualTo(WorkRelaxAttributionMode.Unclassified));
        Assert.That(WorkRelaxObservationRefreshPolicy.BeginAchievementRefresh(recovery, true, WorkRelaxMode.Work),
            Is.EqualTo(WorkRelaxAttributionMode.Unclassified));
    }

    [Test]
    public void Multi_metric_history_group_reclassifies_every_constituent_and_conserves_each_metric()
    {
        WorkRelaxLedgerEvent[] events =
        {
            Event("session-seconds", "TotalFocusDuration", 60, "Work"),
            Event("session-count", "TotalSessionCount", 1, "Work")
        };
        BlueSageReportProjection report = BlueSageReportProjection.Build(NativeFixture(), events,
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<BlueSageMoodObservation>(),
            System.Array.Empty<BlueSageAchievementObservation>());

        Assert.That(report.History[0].Members.Count, Is.EqualTo(2));
        BlueSageReportCorrectionBatch batch = BlueSageReportCorrectionBatchPolicy.Reclassify(
            report.History[0].Members, WorkRelaxAttributionMode.Relax, events,
            System.Array.Empty<WorkRelaxLedgerEvent>());

        Assert.That(batch.Corrections.Count, Is.EqualTo(2));
        Assert.That(batch.Balances["TotalFocusDuration"].Total, Is.EqualTo(60));
        Assert.That(batch.Balances["TotalSessionCount"].Total, Is.EqualTo(1));
        Assert.That(batch.Balances.Values, Has.All.Matches<ModeAllocation>(value => value.Relax == value.Total));
    }

    [Test]
    public void Repeated_undo_walks_original_corrections_and_uses_correction_policy()
    {
        WorkRelaxLedgerEvent first = Correction("c1", "TotalFocusDuration", 10, "Move:Work->Relax", "e1");
        WorkRelaxLedgerEvent second = Correction("c2", "TotalFocusDuration", 5, "Move:Work->Relax", "e2");
        WorkRelaxLedgerEvent undoSecond = Correction("u2", "TotalFocusDuration", 5, "Undo:Relax->Work", "c2");

        Assert.That(WorkRelaxUndoHistoryPolicy.FindNextUndoable(new[] { first, second, undoSecond })!.EventId,
            Is.EqualTo("c1"));
        WorkRelaxCorrection undo = WorkRelaxUndoHistoryPolicy.CreateUndo(
            new ModeAllocation(0, 10, 0), first, "undo-c1");
        Assert.That(undo.IsUndo, Is.True);
        Assert.That(undo.FromMode, Is.EqualTo(WorkRelaxAttributionMode.Relax));
        Assert.That(undo.ToMode, Is.EqualTo(WorkRelaxAttributionMode.Work));
        ModeAllocation restored = BlueSageReportProjection.EffectiveBalance("TotalFocusDuration",
            new[] { Event("e", "TotalFocusDuration", 20, "Work") }, new[] { first, second, undoSecond });
        Assert.That((restored.Work, restored.Relax), Is.EqualTo((10d, 10d)));
    }

    [Test]
    public void Mood_has_overview_month_year_views_and_all_achievement_progress_is_live_native_truth()
    {
        BlueSageMoodObservation[] moods =
        {
            new(4, new System.DateTime(2026, 8, 1, 10, 0, 0, System.DateTimeKind.Utc).Ticks, WorkRelaxAttributionMode.Work),
            new(3, new System.DateTime(2026, 7, 31, 10, 0, 0, System.DateTimeKind.Utc).Ticks, WorkRelaxAttributionMode.Relax)
        };
        BlueSageReportProjection report = BlueSageReportProjection.Build(NativeFixture(),
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<WorkRelaxLedgerEvent>(), moods,
            System.Array.Empty<BlueSageAchievementObservation>(), new NativeAchievementSummary(73, 134),
            new System.DateTime(2026, 8, 1, 12, 0, 0, System.DateTimeKind.Utc));

        Assert.That(report.All.MoodStats.Total, Is.EqualTo(2));
        Assert.That(report.All.MoodStats.OverviewCounts[4], Is.EqualTo(1));
        Assert.That(report.All.MoodStats.MonthCounts[4], Is.EqualTo(1));
        Assert.That(report.All.MoodStats.YearDailyCounts.Count, Is.EqualTo(2));
        Assert.That(report.All.AchievementProgress.Unlocked, Is.EqualTo(73));
        Assert.That(report.All.AchievementProgress.Total, Is.EqualTo(134));
        Assert.That(report.Work.AchievementProgress.IsOverallNative, Is.False);
    }

    [Test]
    public void Mood_calendar_views_use_requested_now_and_ui_renders_actual_distributions()
    {
        BlueSageMoodObservation[] moods =
        {
            new(4, new System.DateTime(2026, 7, 31, 10, 0, 0, System.DateTimeKind.Utc).Ticks, WorkRelaxAttributionMode.Work),
            new(2, new System.DateTime(2025, 8, 1, 10, 0, 0, System.DateTimeKind.Utc).Ticks, WorkRelaxAttributionMode.Work)
        };
        BlueSageReportProjection report = BlueSageReportProjection.Build(NativeFixture(),
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<WorkRelaxLedgerEvent>(), moods,
            System.Array.Empty<BlueSageAchievementObservation>(), null,
            new System.DateTime(2026, 8, 15, 12, 0, 0, System.DateTimeKind.Utc));

        Assert.That(report.All.MoodStats.OverviewCounts[4], Is.EqualTo(1));
        Assert.That(report.All.MoodStats.MonthCounts, Is.Empty);
        Assert.That(report.All.MoodStats.YearDailyCounts.Count, Is.EqualTo(1));
        BlueSageMoodUiModel ui = BlueSageMoodUiModel.Build(report.All.MoodStats);
        Assert.That(ui.OverviewLines, Does.Contain("Rough (red face): 1"));
        Assert.That(ui.MonthLines, Does.Contain("No check-ins this month."));
        Assert.That(ui.YearLines.Single(), Does.Contain("2026-07-31 - Rough (red face): 1"));
    }

    [TestCase(0, "Great (green face)")]
    [TestCase(1, "Good (orange face)")]
    [TestCase(2, "Okay (blue face)")]
    [TestCase(3, "Low (purple face)")]
    [TestCase(4, "Rough (red face)")]
    public void Mood_ui_names_the_five_journal_faces_in_plain_english(int feel, string expectedLabel)
    {
        BlueSageMoodStats stats = new(
            new[]
            {
                new BlueSageMoodObservation(
                    feel,
                    new System.DateTime(2026, 8, 6, 12, 0, 0, System.DateTimeKind.Utc).Ticks,
                    WorkRelaxAttributionMode.Relax)
            },
            new System.DateTime(2026, 8, 6, 12, 0, 0, System.DateTimeKind.Utc));

        BlueSageMoodUiModel ui = BlueSageMoodUiModel.Build(stats);

        Assert.That(ui.OverviewLines, Does.Contain(expectedLabel + ": 1"));
        Assert.That(ui.MonthLines, Does.Contain(expectedLabel + ": 1"));
        Assert.That(ui.YearLines.Single(), Does.Contain(expectedLabel + ": 1"));
    }

    [Test]
    public void Friendly_allocation_editor_covers_every_supported_additive_metric_without_raw_keys()
    {
        IReadOnlyList<BlueSageAllocationOption> options = BlueSageAllocationCatalog.Options;
        string[] keys = options.Select(item => item.MetricKey).ToArray();
        Assert.That(keys, Does.Contain("TotalFocusDuration"));
        // Round 5d: Today/Week/Month are worked out from the totals, so there is nothing of their own to move.
        Assert.That(keys, Does.Not.Contain("TodayFocusDuration"));
        Assert.That(keys, Does.Not.Contain("WeekFocusCount"));
        Assert.That(keys, Does.Not.Contain("MonthFocusDuration"));
        Assert.That(keys, Does.Contain("ArtFocusCount"));
        Assert.That(keys, Does.Contain("PlayFocusDuration"));
        Assert.That(keys, Does.Contain("ToDoCompleted"));
        Assert.That(keys, Does.Contain("SecondsPlayed"));
        Assert.That(keys, Does.Contain("Mood:4"));
        Assert.That(options, Has.All.Matches<BlueSageAllocationOption>(item =>
            !string.IsNullOrWhiteSpace(item.Label) && item.Label != item.MetricKey));
        Assert.That(WorkRelaxMetricCatalog.NativeMetrics.Where(item => item.IsAdditive && !item.IsDerived)
            .Select(item => item.Key).Except(keys), Is.Empty);
        Assert.That(WorkRelaxMetricCatalog.WeekdayBucketMetrics.Select(item => item.Key).Except(keys), Is.Empty);
    }

    [Test]
    public void Heterogeneous_session_uses_constituent_lines_and_undo_reverses_whole_logical_batch()
    {
        WorkRelaxLedgerEvent[] events =
        {
            Event("seconds", "TotalFocusDuration", 60, "Work"),
            Event("count", "TotalSessionCount", 1, "Work")
        };
        BlueSageReportProjection report = BlueSageReportProjection.Build(NativeFixture(), events,
            System.Array.Empty<WorkRelaxLedgerEvent>(), System.Array.Empty<BlueSageMoodObservation>(),
            System.Array.Empty<BlueSageAchievementObservation>());
        Assert.That(report.History[0].DisplayLines, Does.Contain("Pomodoro time: 60 seconds"));
        Assert.That(report.History[0].DisplayLines, Does.Contain("Pomodoro sessions: 1 count"));
        Assert.That(report.History[0].Summary, Does.Not.Contain("61"));

        BlueSageReportCorrectionBatch moved = BlueSageReportCorrectionBatchPolicy.Reclassify(
            report.History[0].Members, WorkRelaxAttributionMode.Relax, events,
            System.Array.Empty<WorkRelaxLedgerEvent>());
        Assert.That(moved.BatchId, Is.Not.Empty);
        WorkRelaxLedgerEvent[] stored = moved.Corrections.Select((item, index) => Correction(
            "c" + index, item.MetricKey, item.Amount, "Move:" + item.FromMode + "->" + item.ToMode,
            item.TargetEventId, moved.BatchId)).ToArray();
        WorkRelaxCorrectionBatch undo = WorkRelaxUndoHistoryPolicy.CreateUndoBatch(events, stored,
            WorkRelaxUndoHistoryPolicy.FindNextUndoableBatch(stored)!);
        Assert.That(undo.Corrections.Count, Is.EqualTo(2));
        Assert.That(undo.Corrections, Has.All.Matches<WorkRelaxCorrection>(item => item.IsUndo));
        Assert.That(undo.TargetBatchId, Is.EqualTo(moved.BatchId));
    }

    [Test]
    public void Aggregate_mood_allocation_resolves_concrete_unclassified_events_and_undo_is_atomic()
    {
        WorkRelaxLedgerEvent[] moods =
        {
            MoodEvent("m1", 4, WorkRelaxAttributionMode.Unclassified, new System.DateTime(2026, 7, 31, 10, 0, 0, System.DateTimeKind.Utc)),
            MoodEvent("m2", 4, WorkRelaxAttributionMode.Unclassified, new System.DateTime(2026, 8, 1, 10, 0, 0, System.DateTimeKind.Utc))
        };
        BlueSageReportCorrectionBatch moved = BlueSageAggregateCorrectionPolicy.Reclassify(
            "Mood:4", 2, WorkRelaxAttributionMode.Work, moods, System.Array.Empty<WorkRelaxLedgerEvent>());

        Assert.That(moved.Corrections.Count, Is.EqualTo(2));
        Assert.That(moved.Corrections.Select(item => item.TargetEventId), Is.EquivalentTo(new[] { "m1", "m2" }));
        WorkRelaxLedgerEvent[] stored = moved.Corrections.Select((item, index) => StoredCorrection(
            "mc" + index, item, moved.BatchId)).ToArray();
        Assert.That(moods.All(item => BlueSageReportProjection.ResolveEffectiveEvent(item, stored).Mode ==
                                      WorkRelaxAttributionMode.Work), Is.True);
        Assert.That(() => BlueSageAggregateCorrectionPolicy.Reclassify("Mood:4", 1,
            WorkRelaxAttributionMode.Relax, moods, stored), Throws.InvalidOperationException);

        IReadOnlyList<WorkRelaxLedgerEvent> latest = WorkRelaxUndoHistoryPolicy.FindNextUndoableBatch(stored)!;
        WorkRelaxCorrectionBatch undo = WorkRelaxUndoHistoryPolicy.CreateUndoBatch(moods, stored, latest);
        WorkRelaxLedgerEvent[] undoStored = undo.Corrections.Select((item, index) => StoredCorrection(
            "mu" + index, item, undo.BatchId, undo.TargetBatchId)).ToArray();
        Assert.That(moods.All(item => BlueSageReportProjection.ResolveEffectiveEvent(item,
            stored.Concat(undoStored)).Mode == WorkRelaxAttributionMode.Unclassified), Is.True);
    }

    [Test]
    public void History_projects_effective_primary_sessions_only_and_successive_corrections_target_the_chain()
    {
        WorkRelaxLedgerEvent primary = Event("e1", "TotalFocusDuration", 10, "Work");
        WorkRelaxLedgerEvent first = Correction("c1", "TotalFocusDuration", 10,
            "Move:Work->Relax", "e1", "batch-1");
        BlueSageReportProjection relaxed = BlueSageReportProjection.Build(NativeFixture(), new[] { primary },
            new[] { first }, System.Array.Empty<BlueSageMoodObservation>(),
            System.Array.Empty<BlueSageAchievementObservation>());
        Assert.That(relaxed.History.Count, Is.EqualTo(1), "correction records must not render as sessions");
        Assert.That(relaxed.History[0].Mode, Is.EqualTo(WorkRelaxAttributionMode.Relax));
        Assert.That(relaxed.History[0].Members[0].TargetEventId, Is.EqualTo("c1"));

        BlueSageReportCorrectionBatch successive = BlueSageReportCorrectionBatchPolicy.Reclassify(
            relaxed.History[0].Members, WorkRelaxAttributionMode.Unclassified, new[] { primary }, new[] { first });
        WorkRelaxLedgerEvent second = StoredCorrection("c2", successive.Corrections.Single(), successive.BatchId);
        BlueSageReportProjection unclassified = BlueSageReportProjection.Build(NativeFixture(), new[] { primary },
            new[] { first, second }, System.Array.Empty<BlueSageMoodObservation>(),
            System.Array.Empty<BlueSageAchievementObservation>());
        Assert.That(unclassified.History.Count, Is.EqualTo(1));
        Assert.That(unclassified.History[0].Mode, Is.EqualTo(WorkRelaxAttributionMode.Unclassified));

        WorkRelaxCorrectionBatch undo = WorkRelaxUndoHistoryPolicy.CreateUndoBatch(new[] { primary },
            new[] { first, second }, new[] { second });
        WorkRelaxLedgerEvent undoStored = StoredCorrection("u2", undo.Corrections.Single(), undo.BatchId,
            undo.TargetBatchId);
        BlueSageReportProjection restored = BlueSageReportProjection.Build(NativeFixture(), new[] { primary },
            new[] { first, second, undoStored }, System.Array.Empty<BlueSageMoodObservation>(),
            System.Array.Empty<BlueSageAchievementObservation>());
        Assert.That(restored.History.Count, Is.EqualTo(1));
        Assert.That(restored.History[0].Mode, Is.EqualTo(WorkRelaxAttributionMode.Relax));
    }

    private static WorkRelaxLedgerEvent Event(string id, string metric, double amount, string mode) => new()
    {
        EventId = id, UtcTicks = 638896000000000000L, MetricKey = metric,
        MetricFamily = "PomodoroDuration", Amount = amount, Mode = mode, Source = "Native observation",
        CorrelationId = "session-1"
    };

    private static WorkRelaxLedgerEvent Correction(string id, string metric, double amount, string source, string target,
        string? batchId = null) => new()
    {
        EventId = id, UtcTicks = 638896000000000100L, MetricKey = metric,
        MetricFamily = "PomodoroDuration", Amount = amount, Mode = "Relax", Source = source, TargetEventId = target,
        BatchId = batchId
    };

    private static WorkRelaxLedgerEvent MoodEvent(string id, int feel, WorkRelaxAttributionMode mode,
        System.DateTime time) => new()
    {
        EventId = id, UtcTicks = time.Ticks, MetricKey = "Mood:" + feel, MetricFamily = "Mood",
        Amount = 1, Mode = mode.ToString(), Source = "Mood observation", CorrelationId = id
    };

    private static WorkRelaxLedgerEvent StoredCorrection(string id, WorkRelaxCorrection correction, string batchId,
        string? targetBatchId = null) => new()
    {
        EventId = id, UtcTicks = 638896000000000200L, MetricKey = correction.MetricKey,
        MetricFamily = WorkRelaxMetricCatalog.Get(correction.MetricKey).Family.ToString(), Amount = correction.Amount,
        Mode = correction.ToMode.ToString(), Source = (correction.IsUndo ? "Undo:" : "Move:") +
            correction.FromMode + "->" + correction.ToMode, TargetEventId = correction.TargetEventId,
        BatchId = batchId, TargetBatchId = targetBatchId
    };

    private static NativeStatisticsSnapshot NativeFixture()
    {
        var values = new System.Collections.Generic.Dictionary<string, double>(System.StringComparer.Ordinal);
        foreach (string name in NativeStatisticsSnapshot.FieldNames)
        {
            if (name != "FocusSessionsPerDay") values[name] = name switch
            {
                "TotalFocusDuration" => 100,
                "ArtFocusDuration" => 12,
                "ToDoCompleted" => 4,
                "CurrentStreak" => 8,
                _ => 10
            };
        }
        Assert.That(NativeStatisticsSnapshot.TryCreate(values, new[] { 1, 2, 3, 4, 5, 6, 7 }, out var snapshot, out var reason), Is.True, reason);
        return snapshot!;
    }
}
