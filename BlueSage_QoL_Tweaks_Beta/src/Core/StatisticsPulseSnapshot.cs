#nullable enable

using System;

namespace BlueSage.QoLTweaks.Core;

internal sealed class StatisticsPulseSnapshot
{
    private StatisticsPulseSnapshot(
        string totalFocusLabel,
        int todaySessions,
        int currentStreak,
        int longestStreak,
        int taskChecks,
        string achievementsLabel)
    {
        TotalFocusLabel = totalFocusLabel;
        TodaySessions = todaySessions;
        CurrentStreak = currentStreak;
        LongestStreak = longestStreak;
        TaskChecks = taskChecks;
        AchievementsLabel = achievementsLabel;
    }

    public string TotalFocusLabel { get; }

    public int TodaySessions { get; }

    public int CurrentStreak { get; }

    public int LongestStreak { get; }

    public int TaskChecks { get; }

    public string AchievementsLabel { get; }

    public StatisticsPulseSnapshot WithAchievementProgress(int unlocked, int total)
    {
        return new StatisticsPulseSnapshot(
            TotalFocusLabel,
            TodaySessions,
            CurrentStreak,
            LongestStreak,
            TaskChecks,
            $"{Math.Max(0, unlocked)} / {Math.Max(0, total)}");
    }

    public static bool TryCreate(
        NativeStatisticsSnapshot nativeSnapshot,
        int? achievementsUnlocked,
        int? achievementsTotal,
        out StatisticsPulseSnapshot? snapshot)
    {
        snapshot = null;
        if (nativeSnapshot == null)
        {
            return false;
        }

        return TryCreate(
            (float)nativeSnapshot.GetNumber("TotalFocusDuration"),
            (int)nativeSnapshot.GetNumber("TodaySessionCount"),
            (int)nativeSnapshot.GetNumber("CurrentStreak"),
            (int)nativeSnapshot.GetNumber("LongestStreak"),
            (int)nativeSnapshot.GetNumber("ToDoCompleted"),
            achievementsUnlocked,
            achievementsTotal,
            out snapshot);
    }

    public static bool TryCreate(
        float totalFocusSeconds,
        int todaySessions,
        int currentStreak,
        int longestStreak,
        int taskChecks,
        int? achievementsUnlocked,
        int? achievementsTotal,
        out StatisticsPulseSnapshot? snapshot)
    {
        snapshot = null;
        if (float.IsNaN(totalFocusSeconds) ||
            float.IsInfinity(totalFocusSeconds) ||
            totalFocusSeconds < 0f)
        {
            return false;
        }

        int wholeMinutes = (int)Math.Floor(totalFocusSeconds / 60f);
        string achievementsLabel = achievementsUnlocked.HasValue && achievementsTotal.HasValue
            ? $"{Math.Max(0, achievementsUnlocked.Value)} / {Math.Max(0, achievementsTotal.Value)}"
            : "Press Refresh to count";

        snapshot = new StatisticsPulseSnapshot(
            $"{wholeMinutes / 60}h {wholeMinutes % 60:00}m",
            Math.Max(0, todaySessions),
            Math.Max(0, currentStreak),
            Math.Max(0, longestStreak),
            Math.Max(0, taskChecks),
            achievementsLabel);
        return true;
    }
}
