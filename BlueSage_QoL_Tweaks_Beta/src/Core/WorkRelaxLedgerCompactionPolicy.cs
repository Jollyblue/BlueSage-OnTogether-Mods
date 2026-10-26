#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

// Round 4 Stage G (Marbles, 2026-10-20: an 11 MB ledger, ~46,000 rows, rewritten every autosave). Rows older than
// the retention window fold into one row per UTC day, metric and mode. The report's balances are sums, so they do
// not change; the history view shows one "compacted" session per day instead of one per minute. A row any
// correction targets is kept as is, so corrections and undo keep resolving.
internal static class WorkRelaxLedgerCompactionPolicy
{
    // Every observation row already carries its own one-minute "session" correlation, so a per-day fold loses
    // nothing a person could act on once the day is over. Two days keeps today and yesterday correctable.
    // Measured on Blue's ledger (2026-10-20): a 7-day window left 10,895 of 10,899 rows; the pain is this week.
    internal const int RetentionDays = 2;
    internal const string CompactedSource = "Compacted history";
    internal const string CorrelationPrefix = "compacted:";

    internal static List<WorkRelaxLedgerEvent> Compact(
        IReadOnlyList<WorkRelaxLedgerEvent> events,
        ISet<string> targetedEventIds,
        long nowUtcTicks,
        int retentionDays = RetentionDays)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        if (targetedEventIds == null) throw new ArgumentNullException(nameof(targetedEventIds));

        // Aligned to a UTC day boundary, so a calendar day is either folded or loose, never half of each.
        long cutoff = (nowUtcTicks / TimeSpan.TicksPerDay - Math.Max(0, retentionDays)) * TimeSpan.TicksPerDay;
        var kept = new List<WorkRelaxLedgerEvent>(events.Count);
        var groups = new Dictionary<string, WorkRelaxLedgerEvent>(StringComparer.Ordinal);
        foreach (WorkRelaxLedgerEvent item in events)
        {
            // Reconciliation rows have no session of their own, so they fold per day at any age (ledgers written
            // before Stage G hold thousands of them from this week). Everything else waits out the retention window.
            bool reconciliation = string.Equals(item.Source, WorkRelaxLedger.ReconciliationSource, StringComparison.Ordinal);
            if ((item.UtcTicks >= cutoff && !reconciliation) || targetedEventIds.Contains(item.EventId))
            {
                kept.Add(item.Clone());
                continue;
            }

            long day = item.UtcTicks / TimeSpan.TicksPerDay;
            string key = day.ToString(CultureInfo.InvariantCulture) + "|" + item.MetricFamily + "|" + item.MetricKey + "|" + item.Mode;
            if (groups.TryGetValue(key, out WorkRelaxLedgerEvent? merged))
            {
                merged.Amount += item.Amount;
                merged.UtcTicks = Math.Max(merged.UtcTicks, item.UtcTicks);
                continue;
            }

            string dayLabel = new DateTime(day * TimeSpan.TicksPerDay, DateTimeKind.Utc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            groups[key] = new WorkRelaxLedgerEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                UtcTicks = item.UtcTicks,
                MetricFamily = item.MetricFamily,
                MetricKey = item.MetricKey,
                Amount = item.Amount,
                Mode = item.Mode,
                Source = CompactedSource,
                CorrelationId = CorrelationPrefix + dayLabel + ":" + item.Mode
            };
        }

        kept.AddRange(groups.Values);
        return kept.OrderBy(item => item.UtcTicks).ToList();
    }
}
