using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
internal sealed class WorkRelaxCorrectionTests
{
    [Test]
    public void Allocation_cannot_exceed_the_unclassified_balance()
    {
        var balance = new ModeAllocation(0d, 0d, 10d);

        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            WorkRelaxCorrectionPolicy.Reclassify("ToDoCompleted", balance, WorkRelaxAttributionMode.Unclassified,
                WorkRelaxAttributionMode.Work, 10.001d, "event-1"));
    }

    [Test]
    public void Reclassification_conserves_total_and_keeps_every_bucket_nonnegative()
    {
        var before = new ModeAllocation(12d, 3d, 5d);

        CorrectionResult result = WorkRelaxCorrectionPolicy.Reclassify(
            "TotalFocusDuration", before, WorkRelaxAttributionMode.Work,
            WorkRelaxAttributionMode.Relax, 7d, "event-2");

        Assert.That(result.Balance.Work, Is.EqualTo(5d));
        Assert.That(result.Balance.Relax, Is.EqualTo(10d));
        Assert.That(result.Balance.Unclassified, Is.EqualTo(5d));
        Assert.That(result.Balance.Total, Is.EqualTo(before.Total).Within(0.001d));
        Assert.That(result.Balance.Minimum, Is.GreaterThanOrEqualTo(0d));
        Assert.That(result.Correction.TargetEventId, Is.EqualTo("event-2"));
    }

    [Test]
    public void Undo_restores_the_prior_effective_mode_and_is_append_only()
    {
        var original = new ModeAllocation(4d, 0d, 0d);
        CorrectionResult changed = WorkRelaxCorrectionPolicy.Reclassify(
            "TotalFocusDuration", original, WorkRelaxAttributionMode.Work,
            WorkRelaxAttributionMode.Relax, 4d, "event-3");

        CorrectionResult undone = WorkRelaxCorrectionPolicy.Undo(
            "TotalFocusDuration", changed.Balance, changed.Correction, "correction-1");

        Assert.That(undone.Balance.Work, Is.EqualTo(4d));
        Assert.That(undone.Balance.Relax, Is.Zero);
        Assert.That(undone.Balance.Total, Is.EqualTo(original.Total).Within(0.001d));
        Assert.That(undone.Correction.FromMode, Is.EqualTo(WorkRelaxAttributionMode.Relax));
        Assert.That(undone.Correction.ToMode, Is.EqualTo(WorkRelaxAttributionMode.Work));
        Assert.That(undone.Correction.TargetEventId, Is.EqualTo("correction-1"));
        Assert.That(undone.Correction.IsUndo, Is.True);
    }

    [Test]
    public void Floating_duration_roundoff_is_clamped_without_exceeding_balance()
    {
        var before = new ModeAllocation(10d, 0d, 0d);

        CorrectionResult result = WorkRelaxCorrectionPolicy.Reclassify(
            "TotalFocusDuration", before, WorkRelaxAttributionMode.Work,
            WorkRelaxAttributionMode.Relax, 10.0005d, "event-4");

        Assert.That(result.Balance.Work, Is.Zero);
        Assert.That(result.Balance.Relax, Is.EqualTo(10d));
        Assert.That(result.Balance.Total, Is.EqualTo(10d).Within(0.001d));
    }

    [Test]
    public void Count_corrections_require_exact_whole_values()
    {
        var before = new ModeAllocation(10d, 0d, 0d);

        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            WorkRelaxCorrectionPolicy.Reclassify(
                "ToDoCompleted", before, WorkRelaxAttributionMode.Work,
                WorkRelaxAttributionMode.Relax, 1.5d, "event-5"));
    }

    [Test]
    public void Weekday_bucket_correction_is_indexed_and_value_conserving()
    {
        string bucket = WorkRelaxMetricCatalog.GetWeekdayBucket(5).Key;
        var before = new ModeAllocation(0d, 0d, 3d);

        CorrectionResult result = WorkRelaxCorrectionPolicy.Reclassify(
            bucket, before, WorkRelaxAttributionMode.Unclassified,
            WorkRelaxAttributionMode.Work, 2d, "weekday-event");

        Assert.That(result.Balance.Work, Is.EqualTo(2d));
        Assert.That(result.Balance.Unclassified, Is.EqualTo(1d));
        Assert.That(result.Balance.Total, Is.EqualTo(3d));
        Assert.That(result.Correction.MetricKey, Is.EqualTo("FocusSessionsPerDay[5]"));
    }
}
