using System;
using System.Collections.Generic;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxObservationDeltaPolicyTests
{
    [Test]
    public void Todo_observation_records_only_the_completed_task_increase()
    {
        NativeStatisticsSnapshot before = Snapshot(("ToDoCompleted", 4d), ("TotalSessionCount", 10d));
        NativeStatisticsSnapshot after = Snapshot(("ToDoCompleted", 5d), ("TotalSessionCount", 11d));

        IReadOnlyList<ObservedNativeMetricDelta> deltas =
            WorkRelaxObservationDeltaPolicy.SelectPositive(before, after, WorkRelaxObservationKind.Todo);

        Assert.Multiple(() =>
        {
            Assert.That(deltas.Count, Is.EqualTo(1));
            Assert.That(deltas[0].Metric.Key, Is.EqualTo("ToDoCompleted"));
            Assert.That(deltas[0].Amount, Is.EqualTo(1d));
            Assert.That(deltas.Any(delta => delta.Metric.Key == "TotalSessionCount"), Is.False);
        });
    }

    [Test]
    public void Todo_observation_records_nothing_when_completed_task_total_does_not_increase()
    {
        NativeStatisticsSnapshot before = Snapshot(("ToDoCompleted", 4d));
        NativeStatisticsSnapshot after = Snapshot(("ToDoCompleted", 4d), ("ArtFocusDuration", 1d));

        IReadOnlyList<ObservedNativeMetricDelta> deltas =
            WorkRelaxObservationDeltaPolicy.SelectPositive(before, after, WorkRelaxObservationKind.Todo);

        Assert.That(deltas, Is.Empty);
    }

    private static NativeStatisticsSnapshot Snapshot(params (string Key, double Value)[] overrides)
    {
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string fieldName in NativeStatisticsSnapshot.FieldNames)
        {
            if (!string.Equals(fieldName, "FocusSessionsPerDay", StringComparison.Ordinal))
            {
                numbers[fieldName] = 0d;
            }
        }

        foreach ((string key, double value) in overrides)
        {
            numbers[key] = value;
        }

        bool created = NativeStatisticsSnapshot.TryCreate(numbers, new int[7], out NativeStatisticsSnapshot? snapshot, out string reason);
        Assert.That(created, Is.True, reason);
        return snapshot!;
    }
}
