using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
internal sealed class WorkRelaxDurationCursorTests
{
    private static readonly string[] ActivityKeys = { "ArtFocusDuration", "ListenFocusDuration" };

    [Test]
    public void Idle_gap_before_native_start_is_not_included_in_first_committed_minute()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.SwitchMode(WorkRelaxMode.Relax, 900d);
        cursors.BeginPomodoro(WorkRelaxMode.Relax, 1_000d);
        ModeAllocation allocation = cursors.CommitPomodoro(60d, WorkRelaxMode.Relax, 1_060d);

        Assert.That(allocation.Work, Is.Zero);
        Assert.That(allocation.Relax, Is.EqualTo(60d).Within(0.001d));
        Assert.That(allocation.Unclassified, Is.Zero);
    }

    [Test]
    public void Mode_switch_inside_active_native_minute_splits_only_that_minute()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.BeginPomodoro(WorkRelaxMode.Work, 100d);
        cursors.SwitchMode(WorkRelaxMode.Relax, 120d);
        ModeAllocation allocation = cursors.CommitPomodoro(60d, WorkRelaxMode.Relax, 160d);

        Assert.That(allocation.Work, Is.EqualTo(20d).Within(0.001d));
        Assert.That(allocation.Relax, Is.EqualTo(40d).Within(0.001d));
        Assert.That(allocation.Total, Is.EqualTo(60d).Within(0.001d));
    }

    [Test]
    public void Cancel_then_restart_discards_uncommitted_time()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.BeginPomodoro(WorkRelaxMode.Work, 0d);
        cursors.SwitchMode(WorkRelaxMode.Relax, 20d);
        cursors.DiscardPomodoro();
        cursors.BeginPomodoro(WorkRelaxMode.Work, 100d);
        ModeAllocation allocation = cursors.CommitPomodoro(60d, WorkRelaxMode.Work, 160d);

        Assert.That(allocation.Work, Is.EqualTo(60d).Within(0.001d));
        Assert.That(allocation.Relax, Is.Zero);
        Assert.That(allocation.Unclassified, Is.Zero);
    }

    [Test]
    public void Consecutive_activity_genres_use_only_their_committing_minute_allocation()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.BeginActivity("ArtFocusDuration", WorkRelaxMode.Work, 0d);
        cursors.SwitchMode(WorkRelaxMode.Relax, 20d);
        ModeAllocation art = cursors.CommitActivity("ArtFocusDuration", 1d, WorkRelaxMode.Relax, 60d);

        cursors.BeginActivity("ListenFocusDuration", WorkRelaxMode.Relax, 80d);
        ModeAllocation listen = cursors.CommitActivity("ListenFocusDuration", 1d, WorkRelaxMode.Relax, 120d);

        Assert.That(art.Work, Is.EqualTo(1d / 3d).Within(0.001d));
        Assert.That(art.Relax, Is.EqualTo(2d / 3d).Within(0.001d));
        Assert.That(listen.Work, Is.Zero);
        Assert.That(listen.Relax, Is.EqualTo(1d).Within(0.001d));
    }

    [Test]
    public void Genre_change_preserves_the_shared_native_activity_minute()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.BeginActivity("ArtFocusDuration", WorkRelaxMode.Work, 0d);
        cursors.BeginActivity("ListenFocusDuration", WorkRelaxMode.Work, 40d);
        cursors.SwitchMode(WorkRelaxMode.Relax, 40d);
        ModeAllocation listen = cursors.CommitActivity(
            "ListenFocusDuration", 1d, WorkRelaxMode.Relax, 60d);

        Assert.That(listen.Work, Is.EqualTo(2d / 3d).Within(0.001d));
        Assert.That(listen.Relax, Is.EqualTo(1d / 3d).Within(0.001d));
        Assert.That(listen.Unclassified, Is.EqualTo(0d).Within(0.001d));
        Assert.That(listen.Total, Is.EqualTo(1d).Within(0.001d));
    }

    [Test]
    public void Activity_end_then_restart_discards_the_previous_partial_minute()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.BeginActivity("ArtFocusDuration", WorkRelaxMode.Work, 0d);
        cursors.SwitchMode(WorkRelaxMode.Relax, 20d);
        cursors.DiscardActivities();
        cursors.BeginActivity("ArtFocusDuration", WorkRelaxMode.Work, 100d);
        ModeAllocation allocation = cursors.CommitActivity(
            "ArtFocusDuration", 1d, WorkRelaxMode.Work, 160d);

        Assert.That(allocation.Work, Is.EqualTo(1d).Within(0.001d));
        Assert.That(allocation.Relax, Is.Zero);
        Assert.That(allocation.Unclassified, Is.Zero);
    }

    [Test]
    public void Missing_optional_start_boundary_falls_back_without_idle_history()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.SwitchMode(WorkRelaxMode.Relax, 1_000d);
        ModeAllocation first = cursors.CommitPomodoro(60d, WorkRelaxMode.Relax, 1_060d);
        cursors.SwitchMode(WorkRelaxMode.Work, 1_080d);
        ModeAllocation second = cursors.CommitPomodoro(60d, WorkRelaxMode.Work, 1_120d);

        Assert.That(first.Unclassified, Is.EqualTo(60d).Within(0.001d));
        Assert.That(first.Relax, Is.Zero);
        Assert.That(first.Work, Is.Zero);
        Assert.That(second.Relax, Is.EqualTo(20d).Within(0.001d));
        Assert.That(second.Work, Is.EqualTo(40d).Within(0.001d));
    }

    [Test]
    public void Stale_boundary_window_falls_back_to_unclassified()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.BeginPomodoro(WorkRelaxMode.Work, 0d);
        cursors.SwitchMode(WorkRelaxMode.Relax, 20d);
        ModeAllocation allocation = cursors.CommitPomodoro(60d, WorkRelaxMode.Relax, 160d);

        Assert.That(allocation.Work, Is.Zero);
        Assert.That(allocation.Relax, Is.Zero);
        Assert.That(allocation.Unclassified, Is.EqualTo(60d).Within(0.001d));
    }

    [Test]
    public void Pause_excludes_inactive_wall_clock_without_discarding_active_progress()
    {
        var cursors = new WorkRelaxDurationCursorController(ActivityKeys);

        cursors.BeginPomodoro(WorkRelaxMode.Work, 0d);
        cursors.PausePomodoro(20d);
        cursors.SwitchMode(WorkRelaxMode.Relax, 80d);
        cursors.ResumePomodoro(WorkRelaxMode.Relax, 100d);
        ModeAllocation allocation = cursors.CommitPomodoro(60d, WorkRelaxMode.Relax, 140d);

        Assert.That(allocation.Work, Is.EqualTo(20d).Within(0.001d));
        Assert.That(allocation.Relax, Is.EqualTo(40d).Within(0.001d));
        Assert.That(allocation.Total, Is.EqualTo(60d).Within(0.001d));
    }
}
