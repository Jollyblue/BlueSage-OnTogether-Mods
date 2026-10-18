using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct WorkRelaxModeChange
{
    internal WorkRelaxModeChange(
        bool changed,
        WorkRelaxMode previous,
        WorkRelaxMode current,
        long utcTicks,
        double monotonicSeconds)
    {
        Changed = changed;
        Previous = previous;
        Current = current;
        UtcTicks = utcTicks;
        MonotonicSeconds = monotonicSeconds;
    }

    public bool Changed { get; }

    public WorkRelaxMode Previous { get; }

    public WorkRelaxMode Current { get; }

    public long UtcTicks { get; }

    public double MonotonicSeconds { get; }
}

internal sealed class WorkRelaxModeController
{
    public WorkRelaxModeController(
        WorkRelaxMode restoredMode,
        long restoredUtcTicks,
        double restoredMonotonicSeconds)
    {
        EnsureValid(restoredMode, nameof(restoredMode));
        CurrentMode = restoredMode;
        CurrentSinceUtcTicks = restoredUtcTicks;
        CurrentSinceMonotonicSeconds = restoredMonotonicSeconds;
    }

    public WorkRelaxMode CurrentMode { get; private set; }

    public long CurrentSinceUtcTicks { get; private set; }

    public double CurrentSinceMonotonicSeconds { get; private set; }

    public WorkRelaxModeChange SetMode(
        WorkRelaxMode mode,
        long utcTicks,
        double monotonicSeconds)
    {
        EnsureValid(mode, nameof(mode));

        WorkRelaxMode previous = CurrentMode;
        if (mode == previous)
        {
            return new WorkRelaxModeChange(
                false,
                previous,
                previous,
                CurrentSinceUtcTicks,
                CurrentSinceMonotonicSeconds);
        }

        CurrentMode = mode;
        CurrentSinceUtcTicks = utcTicks;
        CurrentSinceMonotonicSeconds = monotonicSeconds;
        return new WorkRelaxModeChange(
            true,
            previous,
            mode,
            utcTicks,
            monotonicSeconds);
    }

    private static void EnsureValid(WorkRelaxMode mode, string parameterName)
    {
        if (mode != WorkRelaxMode.Work && mode != WorkRelaxMode.Relax)
        {
            throw new ArgumentOutOfRangeException(parameterName, mode, "Mode must be Work or Relax.");
        }
    }
}
