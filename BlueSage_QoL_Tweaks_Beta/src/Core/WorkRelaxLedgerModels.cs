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

        _events.Add(ledgerEvent.Clone());
    }

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
