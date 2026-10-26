using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 R: which Report rows carry the pomodoro icon, and the icon cell contract.
[TestFixture]
internal sealed class ReportIconPolicyTests
{
    [Test]
    public void Pomodoro_duration_and_session_families_carry_the_icon()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReportIconPolicy.IsPomodoroMetric(WorkRelaxMetricFamily.PomodoroDuration), Is.True);
            Assert.That(ReportIconPolicy.IsPomodoroMetric(WorkRelaxMetricFamily.PomodoroSession), Is.True);
            Assert.That(ReportIconPolicy.IsPomodoroMetric(WorkRelaxMetricFamily.ActivityDuration), Is.False);
            Assert.That(ReportIconPolicy.IsPomodoroMetric("PomodoroDuration"), Is.True);
            Assert.That(ReportIconPolicy.IsPomodoroMetric("SecondsPlayed"), Is.False);
            Assert.That(ReportIconPolicy.IsPomodoroMetric((string?)null), Is.False);
        });
    }

    [Test]
    public void Icon_cell_is_a_small_layout_unit_and_locked_icons_stay_visible_but_dimmed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReportIconPolicy.IconCellSize, Is.InRange(16f, 32f), "small enough for 134 rows, large enough to read at 1080p");
            Assert.That(ReportIconPolicy.LockedIconAlpha, Is.InRange(0.2f, 0.6f));
        });
    }
}
