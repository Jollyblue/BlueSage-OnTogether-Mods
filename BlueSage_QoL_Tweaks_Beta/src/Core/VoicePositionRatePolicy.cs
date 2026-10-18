#nullable disable

using System;

namespace BlueSage.QoLTweaks.Core;

internal static class VoicePositionRatePolicy
{
    internal const float DefaultMinimumIntervalSeconds = 0.25f;

    internal static bool ShouldForward(
        float now,
        float lastForwardedAt,
        string channel,
        string lastChannel,
        float minimumIntervalSeconds = DefaultMinimumIntervalSeconds)
    {
        if (lastForwardedAt < 0f || now < lastForwardedAt)
        {
            return true;
        }

        if (!string.Equals(channel ?? string.Empty, lastChannel ?? string.Empty, StringComparison.Ordinal))
        {
            return true;
        }

        return now - lastForwardedAt >= Math.Max(0.05f, minimumIntervalSeconds);
    }
}
