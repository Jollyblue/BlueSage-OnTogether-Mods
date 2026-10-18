#nullable enable

using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct AttributedMetricDelta
{
    internal AttributedMetricDelta(string metricKey, WorkRelaxMetricFamily family, double amount)
    {
        MetricKey = metricKey ?? throw new ArgumentNullException(nameof(metricKey));
        Family = family;
        Amount = amount;
    }

    internal string MetricKey { get; }
    internal WorkRelaxMetricFamily Family { get; }
    internal double Amount { get; }
}

internal readonly struct NativeEpochMarkers
{
    internal NativeEpochMarkers(int dayOfYear, int currentWeek, int currentMonth)
    {
        DayOfYear = dayOfYear;
        CurrentWeek = currentWeek;
        CurrentMonth = currentMonth;
    }

    internal int DayOfYear { get; }
    internal int CurrentWeek { get; }
    internal int CurrentMonth { get; }
}

internal enum ReconciliationKind
{
    None,
    UnclassifiedAdjustment,
    EpochReset,
    InvalidNativeDecrease,
    InvalidAttribution,
    ConservationViolation
}

internal readonly struct ReconciliationDecision
{
    internal ReconciliationDecision(
        ReconciliationKind kind,
        string metricKey,
        double amount,
        WorkRelaxAttributionMode mode)
    {
        Kind = kind;
        MetricKey = metricKey;
        Amount = amount;
        Mode = mode;
    }

    internal ReconciliationKind Kind { get; }
    internal string MetricKey { get; }
    internal double Amount { get; }
    internal WorkRelaxAttributionMode Mode { get; }
}

internal static class WorkRelaxReconciliationPolicy
{
    private const double FloatingTolerance = 0.001d;

    internal static ReconciliationDecision Reconcile(
        string metricKey,
        double previousNative,
        double currentNative,
        IEnumerable<AttributedMetricDelta> attributedDeltas,
        NativeEpochMarkers previousEpoch,
        NativeEpochMarkers currentEpoch)
    {
        WorkRelaxMetricDefinition metric = WorkRelaxMetricCatalog.Get(metricKey);
        EnsureFiniteNonnegative(previousNative, nameof(previousNative));
        EnsureFiniteNonnegative(currentNative, nameof(currentNative));
        if (attributedDeltas == null)
        {
            throw new ArgumentNullException(nameof(attributedDeltas));
        }

        if (!metric.IsAdditive)
        {
            return new ReconciliationDecision(ReconciliationKind.None, metricKey, 0d,
                WorkRelaxAttributionMode.Unclassified);
        }

        double attributed = 0d;
        foreach (AttributedMetricDelta delta in attributedDeltas)
        {
            if (!string.Equals(delta.MetricKey, metricKey, StringComparison.Ordinal) ||
                delta.Family != metric.Family)
            {
                continue;
            }

            if (double.IsNaN(delta.Amount) || double.IsInfinity(delta.Amount) || delta.Amount < 0d)
            {
                return new ReconciliationDecision(ReconciliationKind.InvalidAttribution, metricKey, 0d,
                    WorkRelaxAttributionMode.Unclassified);
            }

            if ((metric.Unit == WorkRelaxMetricUnit.Count || metric.Unit == WorkRelaxMetricUnit.WeekdayCounts) &&
                delta.Amount != Math.Truncate(delta.Amount))
            {
                return new ReconciliationDecision(ReconciliationKind.InvalidAttribution, metricKey, 0d,
                    WorkRelaxAttributionMode.Unclassified);
            }

            attributed += delta.Amount;
        }

        bool isEpochReset = HasEpochTransition(metric.EpochScope, previousEpoch, currentEpoch);
        double nativeDelta;
        if (isEpochReset)
        {
            nativeDelta = currentNative;
        }
        else if (currentNative < previousNative)
        {
            return new ReconciliationDecision(ReconciliationKind.InvalidNativeDecrease, metricKey, 0d,
                WorkRelaxAttributionMode.Unclassified);
        }
        else
        {
            nativeDelta = currentNative - previousNative;
        }

        double unexplained = nativeDelta - attributed;
        double tolerance = metric.UsesFloatingTolerance ? FloatingTolerance : 0d;
        if (unexplained < -tolerance)
        {
            return new ReconciliationDecision(ReconciliationKind.ConservationViolation, metricKey, 0d,
                WorkRelaxAttributionMode.Unclassified);
        }

        if (unexplained <= tolerance)
        {
            return new ReconciliationDecision(
                isEpochReset ? ReconciliationKind.EpochReset : ReconciliationKind.None,
                metricKey,
                0d,
                WorkRelaxAttributionMode.Unclassified);
        }

        return new ReconciliationDecision(
            isEpochReset ? ReconciliationKind.EpochReset : ReconciliationKind.UnclassifiedAdjustment,
            metricKey,
            unexplained,
            WorkRelaxAttributionMode.Unclassified);
    }

    private static bool HasEpochTransition(
        WorkRelaxEpochScope scope,
        NativeEpochMarkers previous,
        NativeEpochMarkers current)
    {
        return scope switch
        {
            WorkRelaxEpochScope.Day => previous.DayOfYear != current.DayOfYear,
            WorkRelaxEpochScope.Week => previous.CurrentWeek != current.CurrentWeek,
            WorkRelaxEpochScope.Month => previous.CurrentMonth != current.CurrentMonth,
            _ => false
        };
    }

    private static void EnsureFiniteNonnegative(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
