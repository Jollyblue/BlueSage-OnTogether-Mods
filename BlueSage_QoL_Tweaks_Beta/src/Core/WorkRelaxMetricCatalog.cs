#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BlueSage.QoLTweaks.Core;

internal enum WorkRelaxMetricFamily
{
    PomodoroDuration,
    PomodoroSession,
    ActivityDuration,
    ActivityStart,
    TaskCheck,
    SecondsPlayed,
    Mood,
    AchievementObservation
}

internal enum WorkRelaxMetricUnit
{
    Count,
    Seconds,
    Minutes,
    WeekdayCounts,
    Marker
}

internal enum WorkRelaxEpochScope
{
    None,
    Lifetime,
    Day,
    Week,
    Month
}

internal sealed class WorkRelaxMetricDefinition
{
    internal WorkRelaxMetricDefinition(
        string key,
        WorkRelaxMetricFamily family,
        WorkRelaxMetricUnit unit,
        bool isAdditive,
        WorkRelaxEpochScope epochScope,
        string? derivedFrom = null)
    {
        Key = key;
        Family = family;
        Unit = unit;
        IsAdditive = isAdditive;
        EpochScope = epochScope;
        DerivedFrom = derivedFrom;
    }

    internal string Key { get; }
    internal WorkRelaxMetricFamily Family { get; }
    internal WorkRelaxMetricUnit Unit { get; }
    internal bool IsAdditive { get; }
    internal WorkRelaxEpochScope EpochScope { get; }

    // Round 5d (advisor-cleared): the game's Today/Week/Month counters move by exactly the same amounts as their
    // lifetime Total in the same call, so they are no longer written as their own rows. Their Work / Relax split is
    // derived from the Total rows (newest first, up to the game's own value). One flag, four readers: the recorder,
    // reconciliation, the allocation editor and the Report.
    internal string? DerivedFrom { get; }
    internal bool IsDerived => DerivedFrom != null;

    internal bool UsesFloatingTolerance =>
        Unit == WorkRelaxMetricUnit.Seconds || Unit == WorkRelaxMetricUnit.Minutes;
}

internal static class WorkRelaxMetricCatalog
{
    private static readonly ReadOnlyCollection<WorkRelaxMetricDefinition> Metrics =
        Array.AsReadOnly(new[]
        {
            Count("TotalSessionCount", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxEpochScope.Lifetime),
            Count("TodaySessionCount", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxEpochScope.Day, "TotalSessionCount"),
            Count("WeekFocusCount", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxEpochScope.Week, "TotalSessionCount"),
            Count("MonthFocusCount", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxEpochScope.Month, "TotalSessionCount"),
            Seconds("TodayFocusDuration", WorkRelaxEpochScope.Day, "TotalFocusDuration"),
            Seconds("WeekFocusDuration", WorkRelaxEpochScope.Week, "TotalFocusDuration"),
            Seconds("MonthFocusDuration", WorkRelaxEpochScope.Month, "TotalFocusDuration"),
            NonAdditive("AverageDailyFocus", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxMetricUnit.Count),
            NonAdditive("TotalDayOfFocus", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxMetricUnit.Count),
            NonAdditive("CurrentStreak", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxMetricUnit.Count),
            new WorkRelaxMetricDefinition("FocusSessionsPerDay", WorkRelaxMetricFamily.PomodoroSession,
                WorkRelaxMetricUnit.WeekdayCounts, false, WorkRelaxEpochScope.Week),
            NonAdditive("SyncedFocus", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxMetricUnit.Count),
            NonAdditive("DailyMaxFocus", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxMetricUnit.Count),
            Marker("DayOfYear"),
            Marker("CurrentWeek"),
            Marker("CurrentMonth"),
            ActivityStart("ArtFocusCount"),
            ActivityStart("ConnectFocusCount"),
            ActivityStart("EatFocusCount"),
            ActivityStart("ExerciseFocusCount"),
            ActivityStart("ListenFocusCount"),
            ActivityStart("MeditateFocusCount"),
            ActivityStart("ReadFocusCount"),
            ActivityStart("WriteFocusCount"),
            ActivityStart("ChoreFocusCount"),
            ActivityStart("PlayFocusCount"),
            ActivityDuration("ArtFocusDuration"),
            ActivityDuration("ConnectFocusDuration"),
            ActivityDuration("EatFocusDuration"),
            ActivityDuration("ExerciseFocusDuration"),
            ActivityDuration("ListenFocusDuration"),
            ActivityDuration("MeditateFocusDuration"),
            ActivityDuration("ReadFocusDuration"),
            ActivityDuration("WriteFocusDuration"),
            ActivityDuration("ChoreFocusDuration"),
            ActivityDuration("PlayFocusDuration"),
            Seconds("TotalFocusDuration", WorkRelaxEpochScope.Lifetime),
            Count("TotalCompleteSessions", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxEpochScope.Lifetime),
            NonAdditive("LongestStreak", WorkRelaxMetricFamily.PomodoroSession, WorkRelaxMetricUnit.Count),
            Count("ToDoCompleted", WorkRelaxMetricFamily.TaskCheck, WorkRelaxEpochScope.Lifetime),
            new WorkRelaxMetricDefinition("SecondsPlayed", WorkRelaxMetricFamily.SecondsPlayed,
                WorkRelaxMetricUnit.Seconds, true, WorkRelaxEpochScope.Lifetime)
        });

    private static readonly ReadOnlyCollection<WorkRelaxMetricDefinition> WeekdayMetrics =
        Array.AsReadOnly(CreateWeekdayMetrics());

    private static readonly IReadOnlyDictionary<string, WorkRelaxMetricDefinition> ByKey = BuildIndex();

    internal static IReadOnlyList<WorkRelaxMetricDefinition> NativeMetrics => Metrics;
    internal static IReadOnlyList<WorkRelaxMetricDefinition> WeekdayBucketMetrics => WeekdayMetrics;

    internal static WorkRelaxMetricDefinition GetWeekdayBucket(int weekdayIndex)
    {
        if (weekdayIndex < 0 || weekdayIndex >= WeekdayMetrics.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(weekdayIndex));
        }

        return WeekdayMetrics[weekdayIndex];
    }

    internal static WorkRelaxMetricDefinition Get(string key)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (!ByKey.TryGetValue(key, out WorkRelaxMetricDefinition? metric))
        {
            if (key.StartsWith("Mood:", StringComparison.Ordinal))
            {
                return new WorkRelaxMetricDefinition(key, WorkRelaxMetricFamily.Mood,
                    WorkRelaxMetricUnit.Count, true, WorkRelaxEpochScope.Lifetime);
            }
            if (key.StartsWith("Achievement:", StringComparison.Ordinal))
            {
                return new WorkRelaxMetricDefinition(key, WorkRelaxMetricFamily.AchievementObservation,
                    WorkRelaxMetricUnit.Count, true, WorkRelaxEpochScope.Lifetime);
            }
            throw new KeyNotFoundException("Unknown native statistics metric: " + key);
        }

        return metric;
    }

    private static IReadOnlyDictionary<string, WorkRelaxMetricDefinition> BuildIndex()
    {
        var result = new Dictionary<string, WorkRelaxMetricDefinition>(StringComparer.Ordinal);
        foreach (WorkRelaxMetricDefinition metric in Metrics)
        {
            result.Add(metric.Key, metric);
        }

        foreach (WorkRelaxMetricDefinition metric in WeekdayMetrics)
        {
            result.Add(metric.Key, metric);
        }

        return new ReadOnlyDictionary<string, WorkRelaxMetricDefinition>(result);
    }

    private static WorkRelaxMetricDefinition Count(
        string key,
        WorkRelaxMetricFamily family,
        WorkRelaxEpochScope scope,
        string? derivedFrom = null) =>
        new(key, family, WorkRelaxMetricUnit.Count, true, scope, derivedFrom);

    private static WorkRelaxMetricDefinition Seconds(string key, WorkRelaxEpochScope scope, string? derivedFrom = null) =>
        new(key, WorkRelaxMetricFamily.PomodoroDuration, WorkRelaxMetricUnit.Seconds, true, scope, derivedFrom);

    internal static bool IsDerived(string? key) =>
        key != null && ByKey.TryGetValue(key, out WorkRelaxMetricDefinition? metric) && metric.IsDerived;

    private static WorkRelaxMetricDefinition ActivityStart(string key) =>
        new(key, WorkRelaxMetricFamily.ActivityStart, WorkRelaxMetricUnit.Count, true, WorkRelaxEpochScope.Lifetime);

    private static WorkRelaxMetricDefinition ActivityDuration(string key) =>
        new(key, WorkRelaxMetricFamily.ActivityDuration, WorkRelaxMetricUnit.Minutes, true, WorkRelaxEpochScope.Lifetime);

    private static WorkRelaxMetricDefinition NonAdditive(
        string key,
        WorkRelaxMetricFamily family,
        WorkRelaxMetricUnit unit) =>
        new(key, family, unit, false, WorkRelaxEpochScope.Lifetime);

    private static WorkRelaxMetricDefinition Marker(string key) =>
        new(key, WorkRelaxMetricFamily.PomodoroSession, WorkRelaxMetricUnit.Marker, false, WorkRelaxEpochScope.None);

    private static WorkRelaxMetricDefinition[] CreateWeekdayMetrics()
    {
        var result = new WorkRelaxMetricDefinition[7];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = new WorkRelaxMetricDefinition(
                $"FocusSessionsPerDay[{index}]",
                WorkRelaxMetricFamily.PomodoroSession,
                WorkRelaxMetricUnit.Count,
                true,
                WorkRelaxEpochScope.Week);
        }

        return result;
    }
}
