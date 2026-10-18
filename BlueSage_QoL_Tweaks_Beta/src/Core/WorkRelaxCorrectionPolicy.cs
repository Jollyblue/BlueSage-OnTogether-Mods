#nullable enable

using System;

namespace BlueSage.QoLTweaks.Core;

internal sealed class WorkRelaxCorrection
{
    internal WorkRelaxCorrection(
        string metricKey,
        WorkRelaxAttributionMode fromMode,
        WorkRelaxAttributionMode toMode,
        double amount,
        string targetEventId,
        bool isUndo)
    {
        MetricKey = metricKey;
        FromMode = fromMode;
        ToMode = toMode;
        Amount = amount;
        TargetEventId = targetEventId;
        IsUndo = isUndo;
    }

    internal string MetricKey { get; }
    internal WorkRelaxAttributionMode FromMode { get; }
    internal WorkRelaxAttributionMode ToMode { get; }
    internal double Amount { get; }
    internal string TargetEventId { get; }
    internal bool IsUndo { get; }
}

internal readonly struct CorrectionResult
{
    internal CorrectionResult(ModeAllocation balance, WorkRelaxCorrection correction)
    {
        Balance = balance;
        Correction = correction;
    }

    internal ModeAllocation Balance { get; }
    internal WorkRelaxCorrection Correction { get; }
}

internal static class WorkRelaxCorrectionPolicy
{
    private const double Tolerance = 0.001d;

    internal static CorrectionResult Reclassify(
        string metricKey,
        ModeAllocation balance,
        WorkRelaxAttributionMode fromMode,
        WorkRelaxAttributionMode toMode,
        double amount,
        string targetEventId)
    {
        return Move(metricKey, balance, fromMode, toMode, amount, targetEventId, isUndo: false);
    }

    internal static CorrectionResult Undo(
        string metricKey,
        ModeAllocation balance,
        WorkRelaxCorrection correction,
        string correctionId)
    {
        if (correction == null)
        {
            throw new ArgumentNullException(nameof(correction));
        }

        if (!string.Equals(metricKey, correction.MetricKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Undo must target the same native metric.", nameof(metricKey));
        }

        return Move(metricKey, balance, correction.ToMode, correction.FromMode, correction.Amount,
            correctionId, isUndo: true);
    }

    private static CorrectionResult Move(
        string metricKey,
        ModeAllocation balance,
        WorkRelaxAttributionMode fromMode,
        WorkRelaxAttributionMode toMode,
        double amount,
        string targetEventId,
        bool isUndo)
    {
        WorkRelaxMetricDefinition metric = WorkRelaxMetricCatalog.Get(metricKey);
        if (!metric.IsAdditive)
        {
            throw new ArgumentException("Only additive native metrics can be reclassified.", nameof(metricKey));
        }

        if (fromMode == toMode)
        {
            throw new ArgumentException("A correction must change the effective mode.", nameof(toMode));
        }

        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if ((metric.Unit == WorkRelaxMetricUnit.Count || metric.Unit == WorkRelaxMetricUnit.WeekdayCounts) &&
            amount != Math.Truncate(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Count corrections must use whole values.");
        }

        if (string.IsNullOrWhiteSpace(targetEventId))
        {
            throw new ArgumentException("A correction must target an existing event or correction.", nameof(targetEventId));
        }

        double available = balance.Get(fromMode);
        double tolerance = metric.UsesFloatingTolerance ? Tolerance : 0d;
        if (amount - available > tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A correction cannot exceed the source balance.");
        }

        amount = Math.Min(amount, available);

        double work = balance.Work;
        double relax = balance.Relax;
        double unclassified = balance.Unclassified;
        Subtract(ref work, ref relax, ref unclassified, fromMode, amount);
        Add(ref work, ref relax, ref unclassified, toMode, amount);

        var updated = new ModeAllocation(work, relax, unclassified);
        double conservationTolerance = metric.UsesFloatingTolerance ? Tolerance : 0d;
        if (Math.Abs(updated.Total - balance.Total) > conservationTolerance)
        {
            throw new InvalidOperationException("A correction must conserve the native total.");
        }

        return new CorrectionResult(
            updated,
            new WorkRelaxCorrection(metricKey, fromMode, toMode, amount, targetEventId, isUndo));
    }

    private static void Subtract(
        ref double work,
        ref double relax,
        ref double unclassified,
        WorkRelaxAttributionMode mode,
        double amount)
    {
        switch (mode)
        {
            case WorkRelaxAttributionMode.Work:
                work -= amount;
                break;
            case WorkRelaxAttributionMode.Relax:
                relax -= amount;
                break;
            default:
                unclassified -= amount;
                break;
        }
    }

    private static void Add(
        ref double work,
        ref double relax,
        ref double unclassified,
        WorkRelaxAttributionMode mode,
        double amount)
    {
        switch (mode)
        {
            case WorkRelaxAttributionMode.Work:
                work += amount;
                break;
            case WorkRelaxAttributionMode.Relax:
                relax += amount;
                break;
            default:
                unclassified += amount;
                break;
        }
    }
}
