#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BlueSage.QoLTweaks.Core;

internal enum WorkRelaxObservationKind
{
    NativeStatistics,
    Todo
}

internal sealed class ObservedNativeMetricDelta
{
    internal ObservedNativeMetricDelta(WorkRelaxMetricDefinition metric, double amount)
    {
        Metric = metric ?? throw new ArgumentNullException(nameof(metric));
        Amount = amount;
    }

    internal WorkRelaxMetricDefinition Metric { get; }
    internal double Amount { get; }
}

internal static class WorkRelaxObservationDeltaPolicy
{
    internal static IReadOnlyList<ObservedNativeMetricDelta> SelectPositive(
        NativeStatisticsSnapshot before,
        NativeStatisticsSnapshot after,
        WorkRelaxObservationKind kind)
    {
        if (before == null)
        {
            throw new ArgumentNullException(nameof(before));
        }

        if (after == null)
        {
            throw new ArgumentNullException(nameof(after));
        }

        var positive = new List<ObservedNativeMetricDelta>();
        if (kind == WorkRelaxObservationKind.Todo)
        {
            AddPositive(
                positive,
                WorkRelaxMetricCatalog.Get("ToDoCompleted"),
                after.GetNumber("ToDoCompleted") - before.GetNumber("ToDoCompleted"));
            return new ReadOnlyCollection<ObservedNativeMetricDelta>(positive);
        }

        foreach (WorkRelaxMetricDefinition metric in WorkRelaxMetricCatalog.NativeMetrics)
        {
            if (!metric.IsAdditive || string.Equals(metric.Key, "FocusSessionsPerDay", StringComparison.Ordinal))
            {
                continue;
            }

            AddPositive(positive, metric, after.GetNumber(metric.Key) - before.GetNumber(metric.Key));
        }

        int[] beforeWeekdays = before.FocusSessionsPerDay;
        int[] afterWeekdays = after.FocusSessionsPerDay;
        for (int index = 0; index < 7; index++)
        {
            AddPositive(
                positive,
                WorkRelaxMetricCatalog.GetWeekdayBucket(index),
                afterWeekdays[index] - beforeWeekdays[index]);
        }

        return new ReadOnlyCollection<ObservedNativeMetricDelta>(positive);
    }

    private static void AddPositive(
        ICollection<ObservedNativeMetricDelta> destination,
        WorkRelaxMetricDefinition metric,
        double amount)
    {
        if (amount > 0d)
        {
            destination.Add(new ObservedNativeMetricDelta(metric, amount));
        }
    }
}
