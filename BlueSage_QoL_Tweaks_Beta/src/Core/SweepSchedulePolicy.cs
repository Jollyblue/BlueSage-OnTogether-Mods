#nullable disable

using System;

namespace BlueSage.QoLTweaks.Core;

internal sealed class SweepSchedulePolicy
{
    internal bool IsInitialized { get; private set; }
    internal double AnchorSeconds { get; private set; }
    internal double DueSeconds { get; private set; }
    internal int ConfiguredIntervalMinutes { get; private set; }

    internal void Start(double nowSeconds, int configuredMinutes)
    {
        int intervalMinutes = SweepSettingsPolicy.ClampIntervalMinutes(configuredMinutes);
        IsInitialized = true;
        AnchorSeconds = NormalizeNow(nowSeconds);
        DueSeconds = AnchorSeconds + (intervalMinutes * 60d);
        ConfiguredIntervalMinutes = intervalMinutes;
    }

    internal void ReconcileInterval(double nowSeconds, int configuredMinutes)
    {
        int intervalMinutes = SweepSettingsPolicy.ClampIntervalMinutes(configuredMinutes);
        if (!IsInitialized)
        {
            Start(nowSeconds, intervalMinutes);
            return;
        }

        if (intervalMinutes < ConfiguredIntervalMinutes)
        {
            double pulledForwardDue = AnchorSeconds + (intervalMinutes * 60d);
            DueSeconds = Math.Min(DueSeconds, pulledForwardDue);
        }

        ConfiguredIntervalMinutes = intervalMinutes;
    }

    internal bool IsDue(double nowSeconds)
    {
        return IsInitialized && NormalizeNow(nowSeconds) >= DueSeconds;
    }

    internal void MarkAutomaticAttemptFinished(double nowSeconds, int configuredMinutes)
    {
        Start(nowSeconds, configuredMinutes);
    }

    internal void Reset()
    {
        IsInitialized = false;
        AnchorSeconds = 0d;
        DueSeconds = 0d;
        ConfiguredIntervalMinutes = 0;
    }

    private static double NormalizeNow(double nowSeconds)
    {
        return double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds) || nowSeconds < 0d
            ? 0d
            : nowSeconds;
    }
}
