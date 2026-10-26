#nullable enable
using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

// Round 5 Stage A (Blue, 2026-10-22: the ledger "needs revising along with better logics around tracking
// activity pomo and keeping write size minimal"). The game ticks UpdatePomodoroTime once per focus minute
// (study only, never during a break) and fires UpdateFocusStatistics at the study-to-break transition. So a
// focus minute joins the current stretch instead of becoming its own row, a completed pomodoro closes the
// stretch (one row is one pomodoro where the game tells us), and a 10-minute gap closes it when the game does
// not (abandoned session, quit). A row any correction targets is frozen, exactly as the reconciliation merge
// does. Measured on Blue's real 21,581-row ledger: totals identical; the two-day window 12,448 -> 932 rows.
internal static class WorkRelaxLedgerCoalescingPolicy
{
    internal const long GapTicks = TimeSpan.TicksPerMinute * 10;
    internal const string PomodoroDurationFamily = "PomodoroDuration";
    internal const string ActivityDurationFamily = "ActivityDuration";
    internal const string PomodoroSessionFamily = "PomodoroSession";

    internal static bool IsCoalescable(string? metricFamily)
    {
        return string.Equals(metricFamily, PomodoroDurationFamily, StringComparison.Ordinal) ||
               string.Equals(metricFamily, ActivityDurationFamily, StringComparison.Ordinal);
    }

    // Only observation ticks coalesce. Reconciliation rows have their own per-day merge, and compacted rows are
    // already the finished shape of a past day.
    internal static bool IsCoalescableSource(string? source)
    {
        return !string.Equals(source, WorkRelaxLedger.ReconciliationSource, StringComparison.Ordinal) &&
               !string.Equals(source, WorkRelaxLedgerCompactionPolicy.CompactedSource, StringComparison.Ordinal);
    }

    // Index of the row the incoming tick joins, or -1 for a new row. Rows arrive in time order, so the backward
    // scan stops at the first row older than the gap: a handful of comparisons once recent rows are stretches.
    internal static int FindMergeIndex(
        IReadOnlyList<WorkRelaxLedgerEvent> events,
        WorkRelaxLedgerEvent incoming,
        Func<string, bool> isTargeted,
        long gapTicks = GapTicks)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        if (incoming == null) throw new ArgumentNullException(nameof(incoming));
        if (isTargeted == null) throw new ArgumentNullException(nameof(isTargeted));
        if (!IsCoalescable(incoming.MetricFamily) || !IsCoalescableSource(incoming.Source)) return -1;
        // A row a correction points at keeps its own identity, whether it would be the target or the joiner.
        if (isTargeted(incoming.EventId)) return -1;
        bool pomodoro = string.Equals(incoming.MetricFamily, PomodoroDurationFamily, StringComparison.Ordinal);

        long incomingDay = incoming.UtcTicks / TimeSpan.TicksPerDay;
        for (int index = events.Count - 1; index >= 0; index--)
        {
            WorkRelaxLedgerEvent existing = events[index];
            bool session = string.Equals(existing.MetricFamily, PomodoroSessionFamily, StringComparison.Ordinal);
            // Only rows appended in time order may stop the scan: stretches (moved to the end when joined) and
            // session rows (stamped when they happen). Mood and achievement rows carry the game's own, possibly
            // hours-old time and are appended on a Report refresh, so they are skipped, never a stop.
            if (!session && !IsCoalescable(existing.MetricFamily)) continue;
            // A clock step backwards never merges; a row older than the gap ends the search.
            if (existing.UtcTicks > incoming.UtcTicks) return -1;
            if (incoming.UtcTicks - existing.UtcTicks > gapTicks) return -1;
            // A stretch never spans a UTC day: a calendar day is folded or loose, never half of each (compaction rule).
            if (existing.UtcTicks / TimeSpan.TicksPerDay != incomingDay) return -1;
            // The game said a pomodoro completed since this stretch started: the next focus minute is a new one.
            if (pomodoro && session) return -1;
            if (string.Equals(existing.MetricFamily, incoming.MetricFamily, StringComparison.Ordinal) &&
                string.Equals(existing.MetricKey, incoming.MetricKey, StringComparison.Ordinal) &&
                string.Equals(existing.Mode, incoming.Mode, StringComparison.Ordinal) &&
                string.Equals(existing.Source, incoming.Source, StringComparison.Ordinal))
            {
                return isTargeted(existing.EventId) ? -1 : index;
            }
        }

        return -1;
    }
}
