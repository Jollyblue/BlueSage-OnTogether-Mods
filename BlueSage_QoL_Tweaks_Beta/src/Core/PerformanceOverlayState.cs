using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal sealed class PerformanceOverlayState
{
    private const double SmoothingFactor = 0.20d;

    public double SmoothedFramesPerSecond { get; private set; }

    public void SampleFrame(double unscaledDeltaSeconds)
    {
        if (unscaledDeltaSeconds <= 0d ||
            double.IsNaN(unscaledDeltaSeconds) ||
            double.IsInfinity(unscaledDeltaSeconds))
        {
            return;
        }

        double currentFramesPerSecond = Math.Min(1000d, 1d / unscaledDeltaSeconds);
        SmoothedFramesPerSecond = SmoothedFramesPerSecond <= 0d
            ? currentFramesPerSecond
            : SmoothedFramesPerSecond +
              (currentFramesPerSecond - SmoothedFramesPerSecond) * SmoothingFactor;
    }

    public string BuildLabel(int? pingMilliseconds, long? workingSetBytes)
    {
        string framesPerSecond = SmoothedFramesPerSecond > 0d
            ? Math.Round(SmoothedFramesPerSecond, MidpointRounding.AwayFromZero)
                .ToString(CultureInfo.InvariantCulture)
            : "--";
        string ping = pingMilliseconds.HasValue && pingMilliseconds.Value >= 0
            ? pingMilliseconds.Value.ToString(CultureInfo.InvariantCulture) + " ms"
            : "--";
        return "FPS " + framesPerSecond + "  PING " + ping +
               "  RAM " + FormatWorkingSet(workingSetBytes);
    }

    private static string FormatWorkingSet(long? workingSetBytes)
    {
        if (!workingSetBytes.HasValue || workingSetBytes.Value <= 0L)
        {
            return "--";
        }

        const double bytesPerMegabyte = 1024d * 1024d;
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        return workingSetBytes.Value >= bytesPerGigabyte
            ? (workingSetBytes.Value / bytesPerGigabyte)
                .ToString("0.00", CultureInfo.InvariantCulture) + " GB"
            : Math.Round(
                    workingSetBytes.Value / bytesPerMegabyte,
                    MidpointRounding.AwayFromZero)
                .ToString(CultureInfo.InvariantCulture) + " MB";
    }
}
