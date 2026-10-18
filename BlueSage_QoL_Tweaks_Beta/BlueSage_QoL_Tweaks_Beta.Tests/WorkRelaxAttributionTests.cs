using System;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
internal sealed class WorkRelaxAttributionTests
{
    [Test]
    public void Mid_minute_mode_change_conserves_the_native_increment()
    {
        var intervals = new[]
        {
            new ModeInterval(WorkRelaxMode.Work, 0d, 20d),
            new ModeInterval(WorkRelaxMode.Relax, 20d, 60d)
        };

        ModeAllocation allocation = WorkRelaxIntervalAllocator.Allocate(60d, 60d, intervals);

        Assert.That(allocation.Work, Is.EqualTo(20d).Within(0.001d));
        Assert.That(allocation.Relax, Is.EqualTo(40d).Within(0.001d));
        Assert.That(allocation.Work + allocation.Relax + allocation.Unclassified,
            Is.EqualTo(60d).Within(0.001d));
    }

    [Test]
    public void Gaps_are_unclassified_without_inventing_value()
    {
        var intervals = new[]
        {
            new ModeInterval(WorkRelaxMode.Work, 0d, 15d),
            new ModeInterval(WorkRelaxMode.Relax, 45d, 60d)
        };

        ModeAllocation allocation = WorkRelaxIntervalAllocator.Allocate(60d, 60d, intervals);

        Assert.That(allocation.Work, Is.EqualTo(15d).Within(0.001d));
        Assert.That(allocation.Relax, Is.EqualTo(15d).Within(0.001d));
        Assert.That(allocation.Unclassified, Is.EqualTo(30d).Within(0.001d));
    }

    [Test]
    public void Metric_catalog_covers_every_native_field_once()
    {
        Assert.That(WorkRelaxMetricCatalog.NativeMetrics.Select(metric => metric.Key),
            Is.EquivalentTo(NativeStatisticsSnapshot.FieldNames));
        Assert.That(WorkRelaxMetricCatalog.NativeMetrics.Select(metric => metric.Key).Distinct().Count(),
            Is.EqualTo(41));
    }

    [Test]
    public void Pomodoro_and_activity_clocks_have_distinct_families_and_units()
    {
        WorkRelaxMetricDefinition pomodoro = WorkRelaxMetricCatalog.Get("TotalFocusDuration");
        WorkRelaxMetricDefinition activity = WorkRelaxMetricCatalog.Get("ArtFocusDuration");

        Assert.That(pomodoro.Family, Is.EqualTo(WorkRelaxMetricFamily.PomodoroDuration));
        Assert.That(pomodoro.Unit, Is.EqualTo(WorkRelaxMetricUnit.Seconds));
        Assert.That(activity.Family, Is.EqualTo(WorkRelaxMetricFamily.ActivityDuration));
        Assert.That(activity.Unit, Is.EqualTo(WorkRelaxMetricUnit.Minutes));
    }

    [Test]
    public void Native_daily_session_summaries_are_counts_not_duration_clocks()
    {
        WorkRelaxMetricDefinition average = WorkRelaxMetricCatalog.Get("AverageDailyFocus");
        WorkRelaxMetricDefinition maximum = WorkRelaxMetricCatalog.Get("DailyMaxFocus");

        Assert.That(average.Family, Is.EqualTo(WorkRelaxMetricFamily.PomodoroSession));
        Assert.That(average.Unit, Is.EqualTo(WorkRelaxMetricUnit.Count));
        Assert.That(average.IsAdditive, Is.False);
        Assert.That(maximum.Family, Is.EqualTo(WorkRelaxMetricFamily.PomodoroSession));
        Assert.That(maximum.Unit, Is.EqualTo(WorkRelaxMetricUnit.Count));
        Assert.That(maximum.IsAdditive, Is.False);
    }

    [Test]
    public void Trailing_untracked_time_remains_unclassified_in_the_observation_window()
    {
        var intervals = new[] { new ModeInterval(WorkRelaxMode.Work, 0d, 20d) };

        ModeAllocation allocation = WorkRelaxIntervalAllocator.Allocate(60d, 60d, intervals);

        Assert.That(allocation.Work, Is.EqualTo(20d).Within(0.001d));
        Assert.That(allocation.Relax, Is.Zero);
        Assert.That(allocation.Unclassified, Is.EqualTo(40d).Within(0.001d));
        Assert.That(allocation.Total, Is.EqualTo(60d).Within(0.001d));
    }

    [Test]
    public void Activity_minute_uses_the_same_window_without_cross_clock_scaling()
    {
        var intervals = new[] { new ModeInterval(WorkRelaxMode.Work, 0d, 20d) };

        ModeAllocation allocation = WorkRelaxIntervalAllocator.Allocate(1d, 60d, intervals);

        Assert.That(allocation.Work, Is.EqualTo(1d / 3d).Within(0.001d));
        Assert.That(allocation.Unclassified, Is.EqualTo(2d / 3d).Within(0.001d));
        Assert.That(allocation.Total, Is.EqualTo(1d).Within(0.001d));
    }

    [Test]
    public void Weekday_buckets_have_seven_distinct_reconcilable_metric_keys()
    {
        Assert.That(WorkRelaxMetricCatalog.WeekdayBucketMetrics.Count, Is.EqualTo(7));
        Assert.That(WorkRelaxMetricCatalog.WeekdayBucketMetrics.Select(metric => metric.Key).Distinct().Count(),
            Is.EqualTo(7));
        Assert.That(WorkRelaxMetricCatalog.GetWeekdayBucket(3).Key, Is.EqualTo("FocusSessionsPerDay[3]"));
        Assert.That(WorkRelaxMetricCatalog.GetWeekdayBucket(3).IsAdditive, Is.True);
        Assert.That(WorkRelaxMetricCatalog.GetWeekdayBucket(3).EpochScope, Is.EqualTo(WorkRelaxEpochScope.Week));
    }
}
