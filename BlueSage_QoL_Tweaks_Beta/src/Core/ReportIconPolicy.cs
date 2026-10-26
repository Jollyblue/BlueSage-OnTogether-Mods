#nullable enable
using System;

namespace BlueSage.QoLTweaks.Core;

// Round 5 R (Blue, 2026-10-22: "game icons where able"). Which Report rows carry which game icon, and how an
// icon cell is sized: in layout units, so the window's display scale (the same Auto/75-200 the QoL menu uses)
// scales it for 1080p, 1440p and 4K alike. Pure.
internal static class ReportIconPolicy
{
    internal const float IconCellSize = 22f;
    internal const float LockedIconAlpha = 0.35f;

    // Enum overload for the per-frame draw path: no Enum.ToString allocation per row per OnGUI pass.
    internal static bool IsPomodoroMetric(WorkRelaxMetricFamily family)
    {
        return family == WorkRelaxMetricFamily.PomodoroDuration || family == WorkRelaxMetricFamily.PomodoroSession;
    }

    internal static bool IsPomodoroMetric(string? metricFamily)
    {
        return string.Equals(metricFamily, "PomodoroDuration", StringComparison.Ordinal) ||
               string.Equals(metricFamily, "PomodoroSession", StringComparison.Ordinal);
    }
}
