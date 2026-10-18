using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatisticsPulseSnapshotTests
{
    [Test]
    public void Total_focus_uses_total_hours_beyond_one_day()
    {
        Assert.That(
            StatisticsPulseSnapshot.TryCreate(90060f, 2, 3, 5, 7, 8, 134, out StatisticsPulseSnapshot? pulse),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(pulse, Is.Not.Null);
            Assert.That(pulse!.TotalFocusLabel, Is.EqualTo("25h 01m"));
            Assert.That(pulse.TodaySessions, Is.EqualTo(2));
            Assert.That(pulse.CurrentStreak, Is.EqualTo(3));
            Assert.That(pulse.LongestStreak, Is.EqualTo(5));
            Assert.That(pulse.TaskChecks, Is.EqualTo(7));
            Assert.That(pulse.AchievementsLabel, Is.EqualTo("8 / 134"));
        });
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    public void Invalid_duration_is_unavailable(float seconds)
    {
        Assert.That(
            StatisticsPulseSnapshot.TryCreate(seconds, 0, 0, 0, 0, null, null, out _),
            Is.False);
    }

    [Test]
    public void Counts_are_non_negative_and_achievements_are_optional()
    {
        Assert.That(
            StatisticsPulseSnapshot.TryCreate(0f, -1, -2, -3, -4, null, null, out StatisticsPulseSnapshot? pulse),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(pulse, Is.Not.Null);
            Assert.That(pulse!.TodaySessions, Is.Zero);
            Assert.That(pulse.CurrentStreak, Is.Zero);
            Assert.That(pulse.LongestStreak, Is.Zero);
            Assert.That(pulse.TaskChecks, Is.Zero);
            Assert.That(pulse.AchievementsLabel, Is.EqualTo("Press Refresh to count"));
        });
    }

    [Test]
    public void Achievement_progress_returns_an_immutable_copy_without_changing_base_values()
    {
        Assert.That(
            StatisticsPulseSnapshot.TryCreate(90060f, 2, 3, 5, 7, null, null, out StatisticsPulseSnapshot? basePulse),
            Is.True);

        StatisticsPulseSnapshot pulseWithAchievements = basePulse!.WithAchievementProgress(8, 134);

        Assert.Multiple(() =>
        {
            Assert.That(pulseWithAchievements, Is.Not.SameAs(basePulse));
            Assert.That(pulseWithAchievements.TotalFocusLabel, Is.EqualTo("25h 01m"));
            Assert.That(pulseWithAchievements.TodaySessions, Is.EqualTo(2));
            Assert.That(pulseWithAchievements.CurrentStreak, Is.EqualTo(3));
            Assert.That(pulseWithAchievements.LongestStreak, Is.EqualTo(5));
            Assert.That(pulseWithAchievements.TaskChecks, Is.EqualTo(7));
            Assert.That(pulseWithAchievements.AchievementsLabel, Is.EqualTo("8 / 134"));
            Assert.That(basePulse.AchievementsLabel, Is.EqualTo("Press Refresh to count"));
        });
    }
}
