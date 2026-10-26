#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

namespace BlueSage.QoLTweaks.Core;

[DataContract]
internal sealed class WorkRelaxLedger
{
    private List<WorkRelaxLedgerEvent> _events = new();
    private List<WorkRelaxLedgerEvent> _corrections = new();

    [DataMember(Name = "schema", Order = 1, IsRequired = true)]
    public int SchemaVersion { get; set; } = 1;

    [DataMember(Name = "profile", Order = 2, IsRequired = true)]
    public string ProfileKey { get; set; } = string.Empty;

    [DataMember(Name = "mode", Order = 3)]
    public string LastMode { get; set; } = "Work";

    [DataMember(Name = "baseline", Order = 4)]
    public WorkRelaxLedgerStatistics Baseline { get; set; } = new();

    [DataMember(Name = "reconciled", Order = 5)]
    public WorkRelaxLedgerStatistics LastReconciled { get; set; } = new();

    [DataMember(Name = "events", Order = 6)]
    private List<WorkRelaxLedgerEvent> SerializedEvents
    {
        get => _events;
        set => _events = value ?? new List<WorkRelaxLedgerEvent>();
    }

    [DataMember(Name = "corrections", Order = 7)]
    private List<WorkRelaxLedgerEvent> SerializedCorrections
    {
        get => _corrections;
        set => _corrections = value ?? new List<WorkRelaxLedgerEvent>();
    }

    [DataMember(Name = "recovery", Order = 8)]
    public WorkRelaxLedgerRecovery Recovery { get; set; } = new();

    // Copies on purpose: callers can never mutate the ledger through a returned record (pinned by
    // WorkRelaxLedgerTests). Round 5 Stage A keeps this contract; with minutes joined into stretches the list is
    // about ten times shorter, so the per-refresh clone cost that worried the audit went away with the rows.
    public IReadOnlyList<WorkRelaxLedgerEvent> Events =>
        new ReadOnlyCollection<WorkRelaxLedgerEvent>(_events.Select(ledgerEvent => ledgerEvent.Clone()).ToList());

    public IReadOnlyList<WorkRelaxLedgerEvent> Corrections =>
        new ReadOnlyCollection<WorkRelaxLedgerEvent>(_corrections.Select(correction => correction.Clone()).ToList());

    public static WorkRelaxLedger Create(
        string profileKey,
        NativeStatisticsSnapshot baseline,
        WorkRelaxMode mode)
    {
        if (baseline == null)
        {
            throw new ArgumentNullException(nameof(baseline));
        }

        WorkRelaxLedgerStatistics statistics = WorkRelaxLedgerStatistics.FromNativeSnapshot(baseline);
        return new WorkRelaxLedger
        {
            SchemaVersion = 1,
            ProfileKey = profileKey ?? throw new ArgumentNullException(nameof(profileKey)),
            LastMode = mode == WorkRelaxMode.Relax ? "Relax" : "Work",
            Baseline = statistics,
            LastReconciled = statistics.Clone()
        };
    }

    // Round 4 Stage G: the one source whose rows merge per UTC day (see AppendReconciliationEvent).
    public const string ReconciliationSource = "Native reconciliation";

    public void AppendEvent(WorkRelaxLedgerEvent ledgerEvent)
    {
        if (ledgerEvent == null)
        {
            throw new ArgumentNullException(nameof(ledgerEvent));
        }

        if (!string.IsNullOrEmpty(ledgerEvent.TargetEventId))
        {
            throw new ArgumentException("A primary event cannot target another event.", nameof(ledgerEvent));
        }

        // Round 5 Stage A: a focus or activity minute joins the current stretch (see WorkRelaxLedgerCoalescingPolicy).
        int mergeIndex = WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(_events, ledgerEvent, IsTargetedByCorrection);
        if (mergeIndex >= 0)
        {
            JoinStretch(_events, mergeIndex, ledgerEvent);
            return;
        }

        _events.Add(ledgerEvent.Clone());
    }

    // The stretch takes the minute's amount and time and moves to the end, so the list stays ordered by last
    // tick and the policy's backward scan (which stops at the first row older than the gap) stays correct.
    private static void JoinStretch(List<WorkRelaxLedgerEvent> events, int index, WorkRelaxLedgerEvent incoming)
    {
        WorkRelaxLedgerEvent stretch = events[index];
        stretch.Amount += incoming.Amount;
        stretch.UtcTicks = incoming.UtcTicks;
        if (index != events.Count - 1)
        {
            events.RemoveAt(index);
            events.Add(stretch);
        }
    }

    // Round 5 Stage A: one-time fold of a ledger written before coalescing (one row per native tick). Replays
    // every row through the same merge rule in time order; totals never change. Returns how many rows vanished.
    public int CoalesceHistory(long gapTicks = WorkRelaxLedgerCoalescingPolicy.GapTicks)
    {
        var targeted = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkRelaxLedgerEvent correction in _corrections)
        {
            if (!string.IsNullOrEmpty(correction.TargetEventId))
            {
                targeted.Add(correction.TargetEventId!);
            }
        }

        var rebuilt = new List<WorkRelaxLedgerEvent>(_events.Count);
        foreach (WorkRelaxLedgerEvent item in _events.OrderBy(ledgerEvent => ledgerEvent.UtcTicks))
        {
            int mergeIndex = WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(rebuilt, item, targeted.Contains, gapTicks);
            if (mergeIndex >= 0)
            {
                JoinStretch(rebuilt, mergeIndex, item);
                continue;
            }

            rebuilt.Add(item);
        }

        int removed = _events.Count - rebuilt.Count;
        _events = rebuilt;
        return removed;
    }

    // Round 5d: one-time removal of Today/Week/Month rows written before they became derived (each one repeated its
    // Total row). A row a correction points at stays so every correction keeps its target; corrections themselves
    // are never touched. Idempotent. Returns how many rows went.
    public int StripDerivedRows()
    {
        var targeted = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkRelaxLedgerEvent correction in _corrections)
        {
            if (!string.IsNullOrEmpty(correction.TargetEventId))
            {
                targeted.Add(correction.TargetEventId!);
            }
        }

        return _events.RemoveAll(item => WorkRelaxMetricCatalog.IsDerived(item.MetricKey) && !targeted.Contains(item.EventId));
    }

    // Round 4 Stage G (Marbles, 2026-10-20): reconciliation rows are catch-all Unclassified adjustments with no
    // session correlation, written at every autosave (about 17 per autosave in Blue's ledger, 30% of its 2.6 MB).
    // A row for the same UTC day, metric and mode absorbs the new amount unless a correction already targets it,
    // so the report's balances are unchanged while the file stops growing on idle players.
    public void AppendReconciliationEvent(WorkRelaxLedgerEvent ledgerEvent)
    {
        if (ledgerEvent == null)
        {
            throw new ArgumentNullException(nameof(ledgerEvent));
        }

        if (!string.Equals(ledgerEvent.Source, ReconciliationSource, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only reconciliation rows merge.", nameof(ledgerEvent));
        }

        long newDay = UtcDay(ledgerEvent.UtcTicks);
        for (int index = _events.Count - 1; index >= 0; index--)
        {
            WorkRelaxLedgerEvent existing = _events[index];
            if (!string.Equals(existing.Source, ReconciliationSource, StringComparison.Ordinal) ||
                !string.Equals(existing.MetricKey, ledgerEvent.MetricKey, StringComparison.Ordinal) ||
                !string.Equals(existing.Mode, ledgerEvent.Mode, StringComparison.Ordinal) ||
                UtcDay(existing.UtcTicks) != newDay)
            {
                continue;
            }

            if (IsTargetedByCorrection(existing.EventId))
            {
                break;
            }

            existing.Amount += ledgerEvent.Amount;
            existing.UtcTicks = Math.Max(existing.UtcTicks, ledgerEvent.UtcTicks);
            return;
        }

        AppendEvent(ledgerEvent);
    }

    // Round 4 Stage G: fold rows older than the retention window (see WorkRelaxLedgerCompactionPolicy). Returns how
    // many rows disappeared; 0 means the ledger was already compact.
    public int CompactHistory(long nowUtcTicks, int retentionDays = WorkRelaxLedgerCompactionPolicy.RetentionDays)
    {
        var targeted = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkRelaxLedgerEvent correction in _corrections)
        {
            if (!string.IsNullOrEmpty(correction.TargetEventId))
            {
                targeted.Add(correction.TargetEventId!);
            }
        }

        int before = _events.Count;
        _events = WorkRelaxLedgerCompactionPolicy.Compact(_events, targeted, nowUtcTicks, retentionDays);
        return before - _events.Count;
    }

    // Round 4 Stage G: a deep copy for the background writer, so serialization never reads the live lists.
    public WorkRelaxLedger Clone()
    {
        var copy = new WorkRelaxLedger
        {
            SchemaVersion = SchemaVersion,
            ProfileKey = ProfileKey,
            LastMode = LastMode,
            Baseline = Baseline.Clone(),
            LastReconciled = LastReconciled.Clone(),
            Recovery = new WorkRelaxLedgerRecovery
            {
                LastSuccessfulSaveUtcTicks = Recovery.LastSuccessfulSaveUtcTicks,
                RecoveryCount = Recovery.RecoveryCount,
                MoodBaselineInitialized = Recovery.MoodBaselineInitialized,
                AchievementBaselineInitialized = Recovery.AchievementBaselineInitialized
            }
        };
        copy._events = _events.Select(item => item.Clone()).ToList();
        copy._corrections = _corrections.Select(item => item.Clone()).ToList();
        return copy;
    }

    private bool IsTargetedByCorrection(string eventId)
    {
        foreach (WorkRelaxLedgerEvent correction in _corrections)
        {
            if (string.Equals(correction.TargetEventId, eventId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static long UtcDay(long utcTicks) => utcTicks / TimeSpan.TicksPerDay;

    public void AppendCorrection(WorkRelaxLedgerEvent correction)
    {
        if (correction == null)
        {
            throw new ArgumentNullException(nameof(correction));
        }

        if (string.IsNullOrWhiteSpace(correction.TargetEventId))
        {
            throw new ArgumentException("A correction must target an existing event or correction.", nameof(correction));
        }

        _corrections.Add(correction.Clone());
    }

    internal void NormalizeAfterDeserialize()
    {
        _events ??= new List<WorkRelaxLedgerEvent>();
        _corrections ??= new List<WorkRelaxLedgerEvent>();
        Baseline ??= new WorkRelaxLedgerStatistics();
        LastReconciled ??= new WorkRelaxLedgerStatistics();
        Recovery ??= new WorkRelaxLedgerRecovery();
    }
}

[DataContract]
internal sealed class WorkRelaxLedgerStatistics
{
    [DataMember(Name = "numbers", Order = 1)]
    public List<WorkRelaxLedgerMetric> Numbers { get; set; } = new();

    [DataMember(Name = "weekdays", Order = 2)]
    public int[] FocusSessionsPerDay { get; set; } = new int[7];

    public static WorkRelaxLedgerStatistics FromNativeSnapshot(NativeStatisticsSnapshot snapshot)
    {
        var result = new WorkRelaxLedgerStatistics
        {
            FocusSessionsPerDay = snapshot.FocusSessionsPerDay
        };

        foreach (string fieldName in NativeStatisticsSnapshot.FieldNames.Where(name => name != "FocusSessionsPerDay"))
        {
            result.Numbers.Add(new WorkRelaxLedgerMetric
            {
                Key = fieldName,
                Value = snapshot.GetNumber(fieldName)
            });
        }

        return result;
    }

    public WorkRelaxLedgerStatistics Clone()
    {
        return new WorkRelaxLedgerStatistics
        {
            Numbers = Numbers.Select(metric => new WorkRelaxLedgerMetric
            {
                Key = metric.Key,
                Value = metric.Value
            }).ToList(),
            FocusSessionsPerDay = (int[])FocusSessionsPerDay.Clone()
        };
    }

}

[DataContract]
internal sealed class WorkRelaxLedgerMetric
{
    [DataMember(Name = "key", Order = 1)]
    public string Key { get; set; } = string.Empty;

    [DataMember(Name = "value", Order = 2)]
    public double Value { get; set; }
}

[DataContract]
internal sealed class WorkRelaxLedgerEvent
{
    [DataMember(Name = "id", Order = 1)]
    public string EventId { get; set; } = string.Empty;

    [DataMember(Name = "utc", Order = 2)]
    public long UtcTicks { get; set; }

    [DataMember(Name = "family", Order = 3)]
    public string MetricFamily { get; set; } = string.Empty;

    [DataMember(Name = "metric", Order = 4)]
    public string MetricKey { get; set; } = string.Empty;

    [DataMember(Name = "amount", Order = 5)]
    public double Amount { get; set; }

    [DataMember(Name = "mode", Order = 6)]
    public string Mode { get; set; } = string.Empty;

    [DataMember(Name = "source", Order = 7)]
    public string Source { get; set; } = string.Empty;

    [DataMember(Name = "target", Order = 8, EmitDefaultValue = false)]
    public string? TargetEventId { get; set; }

    [DataMember(Name = "correlation", Order = 9, EmitDefaultValue = false)]
    public string? CorrelationId { get; set; }

    [DataMember(Name = "batch", Order = 10, EmitDefaultValue = false)]
    public string? BatchId { get; set; }

    [DataMember(Name = "targetBatch", Order = 11, EmitDefaultValue = false)]
    public string? TargetBatchId { get; set; }

    internal WorkRelaxLedgerEvent Clone()
    {
        return new WorkRelaxLedgerEvent
        {
            EventId = EventId,
            UtcTicks = UtcTicks,
            MetricFamily = MetricFamily,
            MetricKey = MetricKey,
            Amount = Amount,
            Mode = Mode,
            Source = Source,
            TargetEventId = TargetEventId,
            CorrelationId = CorrelationId,
            BatchId = BatchId,
            TargetBatchId = TargetBatchId
        };
    }
}

[DataContract]
internal sealed class WorkRelaxLedgerRecovery
{
    [DataMember(Name = "savedUtc", Order = 1)]
    public long LastSuccessfulSaveUtcTicks { get; set; }

    [DataMember(Name = "recoveries", Order = 2)]
    public int RecoveryCount { get; set; }

    [DataMember(Name = "moodBaseline", Order = 3)]
    public bool MoodBaselineInitialized { get; set; }

    [DataMember(Name = "achievementBaseline", Order = 4)]
    public bool AchievementBaselineInitialized { get; set; }
}
