using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
internal sealed class WorkRelaxReconciliationTests
{
    private static readonly NativeEpochMarkers StableEpoch = new(213, 31, 8);

    [Test]
    public void Activity_delta_never_cross_reconciles_a_pomodoro_clock()
    {
        var attributed = new[]
        {
            new AttributedMetricDelta("ArtFocusDuration", WorkRelaxMetricFamily.ActivityDuration, 60d)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "TotalFocusDuration", 120d, 180d, attributed, StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.UnclassifiedAdjustment));
        Assert.That(result.Amount, Is.EqualTo(60d).Within(0.001d));
        Assert.That(result.Mode, Is.EqualTo(WorkRelaxAttributionMode.Unclassified));
    }

    [Test]
    public void Positive_unexplained_delta_becomes_unclassified()
    {
        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "ToDoCompleted", 4d, 7d, System.Array.Empty<AttributedMetricDelta>(), StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.UnclassifiedAdjustment));
        Assert.That(result.Amount, Is.EqualTo(3d));
        Assert.That(result.Amount, Is.GreaterThanOrEqualTo(0d));
    }

    [Test]
    public void Epoch_reset_records_a_marker_and_never_negative_activity()
    {
        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "TodayFocusDuration", 3600d, 0d, System.Array.Empty<AttributedMetricDelta>(),
            StableEpoch, new NativeEpochMarkers(214, 31, 8));

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.EpochReset));
        Assert.That(result.Amount, Is.Zero);
    }

    [Test]
    public void Matching_attributed_delta_leaves_no_adjustment()
    {
        var attributed = new[]
        {
            new AttributedMetricDelta("TotalFocusDuration", WorkRelaxMetricFamily.PomodoroDuration, 60d)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "TotalFocusDuration", 120d, 180d, attributed, StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.None));
        Assert.That(result.Amount, Is.Zero);
    }

    [Test]
    public void Lifetime_counter_decrease_is_rejected_as_native_corruption()
    {
        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "ToDoCompleted", 9d, 2d, System.Array.Empty<AttributedMetricDelta>(), StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.InvalidNativeDecrease));
        Assert.That(result.Amount, Is.Zero);
    }

    [Test]
    public void Daily_decrease_without_day_marker_transition_is_rejected()
    {
        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "TodaySessionCount", 9d, 2d, System.Array.Empty<AttributedMetricDelta>(), StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.InvalidNativeDecrease));
        Assert.That(result.Amount, Is.Zero);
    }

    [Test]
    public void Weekday_bucket_reconciles_by_exact_index_and_conserves_residual()
    {
        string bucket = WorkRelaxMetricCatalog.GetWeekdayBucket(2).Key;
        var attributed = new[]
        {
            new AttributedMetricDelta(bucket, WorkRelaxMetricFamily.PomodoroSession, 1d),
            new AttributedMetricDelta(WorkRelaxMetricCatalog.GetWeekdayBucket(3).Key,
                WorkRelaxMetricFamily.PomodoroSession, 99d)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            bucket, 2d, 5d, attributed, StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.UnclassifiedAdjustment));
        Assert.That(result.Amount, Is.EqualTo(2d));
    }

    [Test]
    public void Weekday_bucket_reset_requires_the_week_marker_to_change()
    {
        string bucket = WorkRelaxMetricCatalog.GetWeekdayBucket(0).Key;

        ReconciliationDecision valid = WorkRelaxReconciliationPolicy.Reconcile(
            bucket, 4d, 0d, System.Array.Empty<AttributedMetricDelta>(),
            StableEpoch, new NativeEpochMarkers(214, 32, 8));
        ReconciliationDecision corrupt = WorkRelaxReconciliationPolicy.Reconcile(
            bucket, 4d, 0d, System.Array.Empty<AttributedMetricDelta>(), StableEpoch, StableEpoch);

        Assert.That(valid.Kind, Is.EqualTo(ReconciliationKind.EpochReset));
        Assert.That(corrupt.Kind, Is.EqualTo(ReconciliationKind.InvalidNativeDecrease));
    }

    [Test]
    public void Rollover_reconciles_new_epoch_value_instead_of_discarding_it()
    {
        string bucket = WorkRelaxMetricCatalog.GetWeekdayBucket(1).Key;
        var attributed = new[]
        {
            new AttributedMetricDelta(bucket, WorkRelaxMetricFamily.PomodoroSession, 1d)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            bucket, 9d, 3d, attributed,
            StableEpoch, new NativeEpochMarkers(214, 32, 8));

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.EpochReset));
        Assert.That(result.Amount, Is.EqualTo(2d));
        Assert.That(result.Mode, Is.EqualTo(WorkRelaxAttributionMode.Unclassified));
    }

    [Test]
    public void Material_over_attribution_is_a_conservation_violation()
    {
        var attributed = new[]
        {
            new AttributedMetricDelta("ToDoCompleted", WorkRelaxMetricFamily.TaskCheck, 4d)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "ToDoCompleted", 4d, 7d, attributed, StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.ConservationViolation));
        Assert.That(result.Amount, Is.Zero);
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    [TestCase(-1d)]
    public void Invalid_attributed_amounts_are_rejected(double amount)
    {
        var attributed = new[]
        {
            new AttributedMetricDelta("TotalFocusDuration", WorkRelaxMetricFamily.PomodoroDuration, amount)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "TotalFocusDuration", 120d, 180d, attributed, StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.InvalidAttribution));
        Assert.That(result.Amount, Is.Zero);
    }

    [TestCase("TodaySessionCount", 2d, 5d, 214, 31, 8, 5d)]
    [TestCase("WeekFocusCount", 2d, 5d, 213, 32, 8, 5d)]
    [TestCase("MonthFocusCount", 2d, 5d, 213, 31, 9, 5d)]
    public void Nondecreasing_scoped_value_reconciles_from_zero_when_its_epoch_marker_changes(
        string metricKey,
        double previous,
        double current,
        int dayOfYear,
        int week,
        int month,
        double expectedUnclassified)
    {
        var currentEpoch = new NativeEpochMarkers(dayOfYear, week, month);

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            metricKey, previous, current, System.Array.Empty<AttributedMetricDelta>(), StableEpoch, currentEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.EpochReset));
        Assert.That(result.Amount, Is.EqualTo(expectedUnclassified));
    }

    [Test]
    public void Lifetime_metric_does_not_reset_when_calendar_markers_change()
    {
        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "ToDoCompleted", 2d, 5d, System.Array.Empty<AttributedMetricDelta>(),
            StableEpoch, new NativeEpochMarkers(214, 32, 9));

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.UnclassifiedAdjustment));
        Assert.That(result.Amount, Is.EqualTo(3d));
    }

    [Test]
    public void Malformed_activity_delta_does_not_poison_pomodoro_reconciliation()
    {
        var attributed = new[]
        {
            new AttributedMetricDelta("ArtFocusDuration", WorkRelaxMetricFamily.ActivityDuration, double.NaN)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "TotalFocusDuration", 120d, 180d, attributed, StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.UnclassifiedAdjustment));
        Assert.That(result.Amount, Is.EqualTo(60d).Within(0.001d));
    }

    [Test]
    public void Malformed_pomodoro_delta_does_not_poison_task_reconciliation()
    {
        var attributed = new[]
        {
            new AttributedMetricDelta("TotalFocusDuration", WorkRelaxMetricFamily.PomodoroDuration,
                double.PositiveInfinity)
        };

        ReconciliationDecision result = WorkRelaxReconciliationPolicy.Reconcile(
            "ToDoCompleted", 4d, 7d, attributed, StableEpoch, StableEpoch);

        Assert.That(result.Kind, Is.EqualTo(ReconciliationKind.UnclassifiedAdjustment));
        Assert.That(result.Amount, Is.EqualTo(3d));
    }
}
