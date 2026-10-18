using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class NativeStatisticsSnapshotTests
{
    [Test]
    public void TryCreate_captures_all_native_values_and_defensively_copies_weekdays()
    {
        Dictionary<string, double> numbers = CompleteNumbers();
        numbers["TotalFocusDuration"] = 320880d;
        numbers["ArtFocusDuration"] = 94d;
        numbers["DayOfYear"] = 213d;
        int[] weekdays = { 78, 43, 77, 2, 11, 83, 28 };

        bool created = NativeStatisticsSnapshot.TryCreate(
            numbers,
            weekdays,
            out NativeStatisticsSnapshot? snapshot,
            out string reason);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True, reason);
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot!.GetNumber("TotalFocusDuration"), Is.EqualTo(320880d));
            Assert.That(snapshot.GetNumber("ArtFocusDuration"), Is.EqualTo(94d));
            Assert.That(snapshot.GetNumber("DayOfYear"), Is.EqualTo(213d));
            Assert.That(snapshot.FocusSessionsPerDay, Is.EqualTo(new[] { 78, 43, 77, 2, 11, 83, 28 }));
            Assert.That(NativeStatisticsSnapshot.FieldNames.Count, Is.EqualTo(41));
        });

        weekdays[0] = 999;
        int[] returned = snapshot!.FocusSessionsPerDay;
        returned[1] = 999;

        Assert.That(snapshot.FocusSessionsPerDay, Is.EqualTo(new[] { 78, 43, 77, 2, 11, 83, 28 }));
    }

    [Test]
    public void TryCreate_rejects_missing_fields()
    {
        Dictionary<string, double> numbers = CompleteNumbers();
        numbers.Remove("SecondsPlayed");

        bool created = NativeStatisticsSnapshot.TryCreate(
            numbers,
            new[] { 1, 2, 3, 4, 5, 6, 7 },
            out NativeStatisticsSnapshot? snapshot,
            out string reason);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(reason, Does.Contain("SecondsPlayed"));
        });
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void TryCreate_rejects_non_finite_numbers(double invalidValue)
    {
        Dictionary<string, double> numbers = CompleteNumbers();
        numbers["TodayFocusDuration"] = invalidValue;

        bool created = NativeStatisticsSnapshot.TryCreate(
            numbers,
            new[] { 1, 2, 3, 4, 5, 6, 7 },
            out NativeStatisticsSnapshot? snapshot,
            out string reason);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(reason, Does.Contain("TodayFocusDuration"));
        });
    }

    [Test]
    public void TryCreate_normalizes_negative_counts_and_weekday_values()
    {
        Dictionary<string, double> numbers = CompleteNumbers();
        numbers["TodaySessionCount"] = -7d;

        bool created = NativeStatisticsSnapshot.TryCreate(
            numbers,
            new[] { 1, -2, 3, 4, 5, 6, 7 },
            out NativeStatisticsSnapshot? snapshot,
            out string reason);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True, reason);
            Assert.That(snapshot!.GetNumber("TodaySessionCount"), Is.Zero);
            Assert.That(snapshot.FocusSessionsPerDay, Is.EqualTo(new[] { 1, 0, 3, 4, 5, 6, 7 }));
        });
    }

    [Test]
    public void TryCreate_rejects_negative_durations()
    {
        Dictionary<string, double> numbers = CompleteNumbers();
        numbers["ArtFocusDuration"] = -1d;

        bool created = NativeStatisticsSnapshot.TryCreate(
            numbers,
            new[] { 1, 2, 3, 4, 5, 6, 7 },
            out NativeStatisticsSnapshot? snapshot,
            out string reason);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(reason, Does.Contain("ArtFocusDuration"));
        });
    }

    [Test]
    public void TryCreate_requires_seven_weekday_buckets_and_try_get_is_non_throwing()
    {
        bool created = NativeStatisticsSnapshot.TryCreate(
            CompleteNumbers(),
            new[] { 1, 2, 3 },
            out NativeStatisticsSnapshot? snapshot,
            out string reason);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(reason, Does.Contain("seven"));
        });

        Assert.That(
            NativeStatisticsSnapshot.TryCreate(
                CompleteNumbers(),
                new[] { 1, 2, 3, 4, 5, 6, 7 },
                out snapshot,
                out reason),
            Is.True,
            reason);
        Assert.That(snapshot!.TryGetNumber("Unknown", out double value), Is.False);
        Assert.That(value, Is.Zero);
        Assert.Throws<KeyNotFoundException>(() => snapshot.GetNumber("Unknown"));
    }

    private static Dictionary<string, double> CompleteNumbers()
    {
        Dictionary<string, double> numbers = new();
        foreach (string fieldName in NativeStatisticsSnapshot.FieldNames)
        {
            if (fieldName != "FocusSessionsPerDay")
            {
                numbers[fieldName] = 1d;
            }
        }

        return numbers;
    }
}
