#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal enum WorkRelaxAttributionMode
{
    Unclassified = 0,
    Work = 1,
    Relax = 2
}

internal readonly struct ModeInterval
{
    internal ModeInterval(WorkRelaxMode mode, double start, double end)
    {
        if (!IsFinite(start) || !IsFinite(end) || start < 0d || end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Mode interval bounds must be finite and ordered.");
        }

        Mode = mode;
        Start = start;
        End = end;
    }

    internal WorkRelaxMode Mode { get; }
    internal double Start { get; }
    internal double End { get; }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

internal readonly struct ModeAllocation
{
    internal ModeAllocation(double work, double relax, double unclassified)
    {
        Work = Normalize(work);
        Relax = Normalize(relax);
        Unclassified = Normalize(unclassified);
    }

    internal double Work { get; }
    internal double Relax { get; }
    internal double Unclassified { get; }
    internal double Total => Work + Relax + Unclassified;
    internal double Minimum => Math.Min(Work, Math.Min(Relax, Unclassified));

    internal double Get(WorkRelaxAttributionMode mode) => mode switch
    {
        WorkRelaxAttributionMode.Work => Work,
        WorkRelaxAttributionMode.Relax => Relax,
        _ => Unclassified
    };

    private static double Normalize(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Allocations must be finite and non-negative.");
        }

        return value;
    }
}

internal static class WorkRelaxIntervalAllocator
{
    private const double Tolerance = 0.001d;

    internal static ModeAllocation Allocate(
        double nativeAmount,
        double observationWindowDuration,
        IEnumerable<ModeInterval> intervals)
    {
        if (double.IsNaN(nativeAmount) || double.IsInfinity(nativeAmount) || nativeAmount < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeAmount));
        }

        if (intervals == null)
        {
            throw new ArgumentNullException(nameof(intervals));
        }

        if (double.IsNaN(observationWindowDuration) || double.IsInfinity(observationWindowDuration) ||
            observationWindowDuration < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(observationWindowDuration));
        }

        ModeInterval[] ordered = intervals.OrderBy(interval => interval.Start).ToArray();
        if (nativeAmount == 0d)
        {
            return new ModeAllocation(0d, 0d, 0d);
        }

        if (observationWindowDuration <= 0d)
        {
            return new ModeAllocation(0d, 0d, nativeAmount);
        }

        double workDuration = 0d;
        double relaxDuration = 0d;
        double coveredThrough = 0d;
        foreach (ModeInterval interval in ordered)
        {
            double start = Math.Max(Math.Max(interval.Start, 0d), coveredThrough);
            double end = Math.Min(interval.End, observationWindowDuration);
            if (end <= start)
            {
                continue;
            }

            double duration = end - start;
            if (interval.Mode == WorkRelaxMode.Work)
            {
                workDuration += duration;
            }
            else
            {
                relaxDuration += duration;
            }

            coveredThrough = end;
        }

        double scale = nativeAmount / observationWindowDuration;
        double work = workDuration * scale;
        double relax = relaxDuration * scale;
        double unclassified = nativeAmount - work - relax;
        if (unclassified < 0d && unclassified > -Tolerance)
        {
            unclassified = 0d;
        }

        return new ModeAllocation(work, relax, unclassified);
    }

    internal static ModeAllocation Unclassified(double nativeAmount) => new(0d, 0d, nativeAmount);
}

internal sealed class WorkRelaxDurationCursorController
{
    private readonly DurationCursor _pomodoro = new();
    private readonly DurationCursor _activity = new();
    private readonly HashSet<string> _activityMetricKeys;

    internal WorkRelaxDurationCursorController(IEnumerable<string> activityMetricKeys)
    {
        if (activityMetricKeys == null)
        {
            throw new ArgumentNullException(nameof(activityMetricKeys));
        }

        _activityMetricKeys = new HashSet<string>(activityMetricKeys, StringComparer.Ordinal);
    }

    internal void BeginPomodoro(WorkRelaxMode mode, double now) => _pomodoro.Begin(mode, now);

    internal void PausePomodoro(double now) => _pomodoro.Pause(now);

    internal void ResumePomodoro(WorkRelaxMode mode, double now) => _pomodoro.Resume(mode, now);

    internal void DiscardPomodoro() => _pomodoro.Discard();

    internal bool BeginActivity(string metricKey, WorkRelaxMode mode, double now)
    {
        if (!_activityMetricKeys.Contains(metricKey))
        {
            return false;
        }

        if (!_activity.IsTracking)
        {
            _activity.Begin(mode, now);
        }

        return true;
    }

    internal void DiscardActivities() => _activity.Discard();

    internal void SwitchMode(WorkRelaxMode mode, double now)
    {
        _pomodoro.Switch(mode, now);
        _activity.Switch(mode, now);
    }

    internal ModeAllocation CommitPomodoro(double nativeAmount, WorkRelaxMode mode, double now) =>
        _pomodoro.Commit(nativeAmount, mode, now);

    internal ModeAllocation CommitActivity(string metricKey, double nativeAmount, WorkRelaxMode mode, double now)
    {
        if (!_activityMetricKeys.Contains(metricKey))
        {
            return WorkRelaxIntervalAllocator.Unclassified(nativeAmount);
        }

        return _activity.Commit(nativeAmount, mode, now);
    }

    private sealed class DurationCursor
    {
        // Native duration commits are one active minute apart. A longer observed window means
        // an optional lifecycle boundary was missed, so the unexplained delta stays Unclassified.
        private const double MaximumTrustedActiveSeconds = 75d;

        private readonly List<ModeInterval> _intervals = new();
        private WorkRelaxMode _mode;
        private double _activeDuration;
        private double _segmentStartedAt;
        private bool _running;

        internal bool IsTracking { get; private set; }

        internal void Begin(WorkRelaxMode mode, double now)
        {
            ValidateTimestamp(now);
            _intervals.Clear();
            _mode = mode;
            _activeDuration = 0d;
            _segmentStartedAt = now;
            _running = true;
            IsTracking = true;
        }

        internal void Switch(WorkRelaxMode mode, double now)
        {
            if (!IsTracking || mode == _mode)
            {
                return;
            }

            ValidateTimestamp(now);
            if (_running)
            {
                CloseRunningSegment(now);
            }

            _mode = mode;
            _segmentStartedAt = now;
        }

        internal void Pause(double now)
        {
            if (!IsTracking || !_running)
            {
                return;
            }

            ValidateTimestamp(now);
            CloseRunningSegment(now);
            _running = false;
        }

        internal void Resume(WorkRelaxMode mode, double now)
        {
            ValidateTimestamp(now);
            if (!IsTracking)
            {
                Begin(mode, now);
                return;
            }

            if (_running)
            {
                Switch(mode, now);
                return;
            }

            _mode = mode;
            _segmentStartedAt = now;
            _running = true;
        }

        internal void Discard()
        {
            _intervals.Clear();
            _activeDuration = 0d;
            _running = false;
            IsTracking = false;
        }

        internal ModeAllocation Commit(double nativeAmount, WorkRelaxMode mode, double now)
        {
            ValidateTimestamp(now);
            ModeAllocation allocation;
            if (!IsTracking)
            {
                allocation = WorkRelaxIntervalAllocator.Unclassified(nativeAmount);
            }
            else
            {
                if (_running)
                {
                    CloseRunningSegment(now);
                }

                allocation = _activeDuration <= 0d || _activeDuration > MaximumTrustedActiveSeconds
                    ? WorkRelaxIntervalAllocator.Unclassified(nativeAmount)
                    : WorkRelaxIntervalAllocator.Allocate(nativeAmount, _activeDuration, _intervals);
            }

            Begin(mode, now);
            return allocation;
        }

        private void CloseRunningSegment(double now)
        {
            double duration = Math.Max(0d, now - _segmentStartedAt);
            if (duration > 0d)
            {
                _intervals.Add(new ModeInterval(_mode, _activeDuration, _activeDuration + duration));
                _activeDuration += duration;
            }

            _segmentStartedAt = now;
        }

        private static void ValidateTimestamp(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cursor timestamps must be finite and non-negative.");
            }
        }
    }
}
