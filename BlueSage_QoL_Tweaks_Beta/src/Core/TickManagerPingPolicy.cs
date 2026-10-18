using System;

namespace BlueSage.QoLTweaks.Core;

internal static class TickManagerPingPolicy
{
    public static int? FromRttSeconds(double rttSeconds)
    {
        if (rttSeconds <= 0d || rttSeconds > 60d || double.IsNaN(rttSeconds) || double.IsInfinity(rttSeconds))
        {
            return null;
        }

        return (int)Math.Round(rttSeconds * 1000d, MidpointRounding.AwayFromZero);
    }
}
