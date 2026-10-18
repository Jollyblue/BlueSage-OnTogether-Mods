using System;

namespace BlueSage.QoLTweaks;

internal static class StatisticsPulsePresentationPolicy
{
    public static string BuildStreakValue(int currentStreak, int longestStreak)
    {
        return "🔥 " + Math.Max(0, currentStreak) + "  •  Best " + Math.Max(0, longestStreak);
    }
}
