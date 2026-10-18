#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BlueSage.QoLTweaks.Core;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class WorkRelaxObservationState
{
    internal WorkRelaxObservationState(
        NativeStatisticsSnapshot before,
        string source,
        WorkRelaxObservationKind kind)
    {
        Before = before;
        Source = source;
        Kind = kind;
    }

    internal NativeStatisticsSnapshot Before { get; }
    internal string Source { get; }
    internal WorkRelaxObservationKind Kind { get; }
}

internal static class WorkRelaxRuntimeController
{
    private static readonly object Gate = new();
    private static readonly StatisticsPulseReader Reader = new();
    private static readonly HashSet<string> LoggedFailures = new(StringComparer.Ordinal);
    private static readonly List<AttributedMetricDelta> PendingAttributedDeltas = new();
    private static WorkRelaxLedgerStore? _store;
    private static WorkRelaxLedger? _ledger;
    private static WorkRelaxModeController? _modeController;
    private static WorkRelaxDurationCursorController? _durationCursors;
    private static int _nativeObservationDepth;
    private static bool _reconciliationPending;
    private static bool _shutdownFlushClaimed;

    internal static WorkRelaxMode CurrentMode
    {
        get
        {
            lock (Gate)
            {
                return _modeController?.CurrentMode ?? WorkRelaxMode.Work;
            }
        }
    }

    internal static bool TryGetCurrentMode(out WorkRelaxMode mode)
    {
        lock (Gate)
        {
            if (_modeController == null)
            {
                mode = default;
                return false;
            }

            mode = _modeController.CurrentMode;
            return true;
        }
    }

    internal static bool TryInitialize()
    {
        lock (Gate)
        {
            if (_ledger != null)
            {
                return true;
            }

            try
            {
                if (!SteamManager.Initialized)
                {
                    return false;
                }

                CSteamID steamId = SteamUser.GetSteamID();
                if (steamId == CSteamID.Nil)
                {
                    return false;
                }

                if (!Reader.TryCaptureNative(out NativeStatisticsSnapshot snapshot, out string reason))
                {
                    LogOnce("initialize-native", "Work / Relax tracking is waiting for native statistics: " + reason);
                    return false;
                }

                string profileKey = WorkRelaxProfileKey.Create(((ulong)steamId).ToString());
                _store = WorkRelaxLedgerStore.FromConfigPath(Paths.ConfigPath, profileKey);
                WorkRelaxLedgerLoadResult loaded = _store.Load();
                WorkRelaxMode restoredMode = ParseSavedMode(loaded.Ledger?.LastMode);
                _ledger = loaded.Ledger ?? WorkRelaxLedger.Create(profileKey, snapshot, WorkRelaxMode.Work);
                _modeController = new WorkRelaxModeController(
                    restoredMode,
                    DateTime.UtcNow.Ticks,
                    MonotonicSeconds());
                _ledger.LastMode = ModeName(restoredMode);
                InitializeDurationCursors();
                _shutdownFlushClaimed = false;

                if (loaded.Ledger == null || loaded.RecoveredFromBackup)
                {
                    SaveFailOpen(loaded.Ledger == null ? "initial baseline" : "backup recovery");
                }

                return true;
            }
            catch (Exception exception)
            {
                LogOnce("initialize-" + exception.GetType().Name,
                    "Work / Relax tracking stayed unavailable: " + exception.GetType().Name + ": " + exception.Message);
                ResetUninitializedState();
                return false;
            }
        }
    }

    internal static bool SetMode(WorkRelaxMode mode)
    {
        lock (Gate)
        {
            if (!TryInitialize() || _modeController == null || _ledger == null)
            {
                return false;
            }

            double now = MonotonicSeconds();
            WorkRelaxModeChange change = _modeController.SetMode(mode, DateTime.UtcNow.Ticks, now);
            if (!change.Changed)
            {
                return true;
            }

            _durationCursors?.SwitchMode(mode, now);

            _ledger.LastMode = ModeName(mode);
            SaveFailOpen("mode change");
            return true;
        }
    }

    internal static bool TryRefreshReport(
        IEnumerable<NativeMoodObservation> nativeMoods,
        bool moodsAvailable,
        IEnumerable<NativeAchievementObservation> nativeAchievements,
        bool achievementsAvailable,
        out BlueSageReportProjection? projection,
        out string reason)
    {
        lock (Gate)
        {
            projection = null;
            reason = string.Empty;
            if (!TryInitialize() || _ledger == null || _modeController == null)
            {
                reason = "Work / Relax tracking is not ready.";
                return false;
            }

            if (!Reader.TryCaptureNative(out NativeStatisticsSnapshot snapshot, out reason))
            {
                return false;
            }

            NativeAchievementObservation[] achievementSnapshot = (nativeAchievements ?? Array.Empty<NativeAchievementObservation>()).ToArray();
            WorkRelaxAttributionMode? moodMode = WorkRelaxObservationRefreshPolicy.BeginMoodRefresh(
                _ledger.Recovery, moodsAvailable, _modeController.CurrentMode);
            WorkRelaxAttributionMode? achievementMode = WorkRelaxObservationRefreshPolicy.BeginAchievementRefresh(
                _ledger.Recovery, achievementsAvailable, _modeController.CurrentMode);
            if (moodMode.HasValue) RecordMoodObservations(nativeMoods ?? Array.Empty<NativeMoodObservation>(), moodMode.Value);
            if (achievementMode.HasValue) RecordAchievementObservations(achievementSnapshot, achievementMode.Value);
            NativeAchievementSummary? achievementSummary = achievementsAvailable
                ? new NativeAchievementSummary(achievementSnapshot.Count(item => item.Unlocked), achievementSnapshot.Length)
                : null;
            projection = BuildProjection(snapshot, achievementSummary, DateTime.UtcNow);
            if (moodsAvailable || achievementsAvailable) SaveFailOpen("BlueSage Report refresh");
            return true;
        }
    }

    internal static bool TryApplyReportCorrection(
        string metricKey,
        WorkRelaxAttributionMode fromMode,
        WorkRelaxAttributionMode toMode,
        double amount,
        string targetEventId,
        out string reason)
    {
        lock (Gate)
        {
            reason = string.Empty;
            if (!TryInitialize() || _ledger == null ||
                !Reader.TryCaptureNative(out NativeStatisticsSnapshot snapshot, out reason))
            {
                return false;
            }

            BlueSageReportProjection projection = BuildProjection(snapshot);
            if (metricKey.StartsWith("Mood:", StringComparison.Ordinal))
            {
                if (fromMode != WorkRelaxAttributionMode.Unclassified)
                {
                    reason = "Aggregate mood allocation can move only Unclassified observations.";
                    return false;
                }
                try
                {
                    BlueSageReportCorrectionBatch moodBatch = BlueSageAggregateCorrectionPolicy.Reclassify(
                        metricKey, amount, toMode, _ledger.Events, _ledger.Corrections);
                    foreach (WorkRelaxCorrection correction in moodBatch.Corrections)
                        AppendCorrection(correction, moodBatch.BatchId, null);
                    SaveFailOpen("BlueSage Report mood allocation");
                    return true;
                }
                catch (Exception exception)
                {
                    reason = exception.Message;
                    return false;
                }
            }
            ModeAllocation balance;
            if (NativeStatisticsSnapshot.FieldNames.Contains(metricKey))
            {
                BlueSageReportMetric work = projection.Work.Get(metricKey);
                BlueSageReportMetric relax = projection.Relax.Get(metricKey);
                BlueSageReportMetric unclassified = projection.Unclassified.Get(metricKey);
                balance = new ModeAllocation(work.Value, relax.Value, unclassified.Value);
            }
            else
            {
                balance = BlueSageReportProjection.EffectiveBalance(metricKey, _ledger.Events, _ledger.Corrections);
            }
            try
            {
                CorrectionResult result = WorkRelaxCorrectionPolicy.Reclassify(metricKey, balance, fromMode, toMode,
                    amount, targetEventId);
                AppendCorrection(result.Correction, "batch-" + Guid.NewGuid().ToString("N"), null);
                SaveFailOpen("BlueSage Report correction");
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }
    }

    internal static bool TryApplyReportCorrections(IReadOnlyList<BlueSageReportHistoryMember> members,
        WorkRelaxAttributionMode toMode, out string reason)
    {
        lock (Gate)
        {
            reason = string.Empty;
            if (!TryInitialize() || _ledger == null)
            {
                reason = "Work / Relax tracking is not ready.";
                return false;
            }
            try
            {
                BlueSageReportCorrectionBatch batch = BlueSageReportCorrectionBatchPolicy.Reclassify(
                    members, toMode, _ledger.Events, _ledger.Corrections);
                if (batch.Corrections.Count == 0)
                {
                    reason = "This session is already classified that way.";
                    return false;
                }
                foreach (WorkRelaxCorrection correction in batch.Corrections)
                    AppendCorrection(correction, batch.BatchId, null);
                SaveFailOpen("BlueSage Report session correction");
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }
    }

    internal static bool TryUndoLatestReportCorrection(out string reason)
    {
        lock (Gate)
        {
            reason = string.Empty;
            if (!TryInitialize() || _ledger == null || _ledger.Corrections.Count == 0)
            {
                reason = "There is no correction to undo.";
                return false;
            }

            IReadOnlyList<WorkRelaxLedgerEvent>? latest = WorkRelaxUndoHistoryPolicy.FindNextUndoableBatch(_ledger.Corrections);
            if (latest == null)
            {
                reason = "There is no correction left to undo.";
                return false;
            }
            try
            {
                WorkRelaxCorrectionBatch undo = WorkRelaxUndoHistoryPolicy.CreateUndoBatch(
                    _ledger.Events, _ledger.Corrections, latest);
                foreach (WorkRelaxCorrection correction in undo.Corrections)
                    AppendCorrection(correction, undo.BatchId, undo.TargetBatchId);
                SaveFailOpen("BlueSage Report undo");
                return true;
            }
            catch (Exception exception)
            {
                reason = exception.Message;
                return false;
            }
        }
    }

    internal static WorkRelaxObservationState? BeginObservation(
        string source,
        WorkRelaxObservationKind kind)
    {
        lock (Gate)
        {
            if (!TryInitialize())
            {
                return null;
            }

            _nativeObservationDepth++;
            if (Reader.TryCaptureNative(out NativeStatisticsSnapshot snapshot, out string reason))
            {
                return new WorkRelaxObservationState(snapshot, source ?? string.Empty, kind);
            }

            _nativeObservationDepth--;
            LogOnce("prefix-capture", "Work / Relax observation skipped safely: " + reason);
            return null;
        }
    }

    internal static void CompleteObservation(WorkRelaxObservationState? state)
    {
        lock (Gate)
        {
            if (state == null)
            {
                return;
            }

            try
            {
                if (Reader.TryCaptureNative(out NativeStatisticsSnapshot after, out string reason))
                {
                    RecordPositiveDeltas(state.Before, after, state.Source, state.Kind);
                }
                else
                {
                    LogOnce("postfix-capture", "Work / Relax observation skipped safely: " + reason);
                }
            }
            catch (Exception exception)
            {
                LogOnce("postfix-" + exception.GetType().Name,
                    "Work / Relax observation failed open: " + exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                _nativeObservationDepth = Math.Max(0, _nativeObservationDepth - 1);
                if (_nativeObservationDepth == 0 && _reconciliationPending)
                {
                    _reconciliationPending = false;
                    ReconcileAfterNativeSave();
                }
            }
        }
    }

    internal static void AbortObservation(WorkRelaxObservationState? state)
    {
        lock (Gate)
        {
            if (state == null)
            {
                return;
            }

            _nativeObservationDepth = Math.Max(0, _nativeObservationDepth - 1);
            if (_nativeObservationDepth == 0 && _reconciliationPending)
            {
                _reconciliationPending = false;
                ReconcileAfterNativeSave();
            }
        }
    }

    internal static void ObservePomodoroState(bool isStudy, bool isPaused)
    {
        lock (Gate)
        {
            if (!TryInitialize() || _modeController == null || _durationCursors == null)
            {
                return;
            }

            double now = MonotonicSeconds();
            if (!isStudy)
            {
                _durationCursors.DiscardPomodoro();
            }
            else if (isPaused)
            {
                _durationCursors.PausePomodoro(now);
            }
            else
            {
                _durationCursors.ResumePomodoro(_modeController.CurrentMode, now);
            }
        }
    }

    internal static void BeginActivityInterval(string focusTypeName)
    {
        lock (Gate)
        {
            if (!TryInitialize() || _modeController == null || _durationCursors == null)
            {
                return;
            }

            string metricKey = (focusTypeName ?? string.Empty) + "FocusDuration";
            if (!_durationCursors.BeginActivity(metricKey, _modeController.CurrentMode, MonotonicSeconds()))
            {
                LogOnce("activity-cursor-" + metricKey,
                    "One optional Work / Relax activity duration boundary is unavailable: " + metricKey + ".");
            }
        }
    }

    internal static void DiscardActivityIntervals()
    {
        lock (Gate)
        {
            _durationCursors?.DiscardActivities();
        }
    }

    internal static void ReconcileAfterNativeSave()
    {
        lock (Gate)
        {
            if (!TryInitialize() || _ledger == null)
            {
                return;
            }

            if (_nativeObservationDepth > 0)
            {
                _reconciliationPending = true;
                return;
            }

            if (!Reader.TryCaptureNative(out NativeStatisticsSnapshot current, out string reason))
            {
                LogOnce("reconcile-capture", "Work / Relax reconciliation skipped safely: " + reason);
                return;
            }

            try
            {
                WorkRelaxLedgerStatistics previous = _ledger.LastReconciled;
                NativeEpochMarkers previousEpoch = ReadEpoch(previous);
                NativeEpochMarkers currentEpoch = ReadEpoch(current);
                foreach (WorkRelaxMetricDefinition metric in WorkRelaxMetricCatalog.NativeMetrics)
                {
                    if (!metric.IsAdditive || string.Equals(metric.Key, "FocusSessionsPerDay", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    double previousValue = ReadLedgerNumber(previous, metric.Key);
                    ReconciliationDecision decision = WorkRelaxReconciliationPolicy.Reconcile(
                        metric.Key,
                        previousValue,
                        current.GetNumber(metric.Key),
                        PendingAttributedDeltas,
                        previousEpoch,
                        currentEpoch);
                    AppendReconciliationAdjustment(decision);
                }

                ReconcileWeekdayBuckets(previous, current, previousEpoch, currentEpoch);
                _ledger.LastReconciled = WorkRelaxLedgerStatistics.FromNativeSnapshot(current);
                PendingAttributedDeltas.Clear();
                SaveFailOpen("native reconciliation");
            }
            catch (Exception exception)
            {
                LogOnce("reconcile-" + exception.GetType().Name,
                    "Work / Relax reconciliation failed open: " + exception.GetType().Name + ": " + exception.Message);
            }
        }
    }

    internal static void Flush()
    {
        lock (Gate)
        {
            if (_shutdownFlushClaimed)
            {
                return;
            }

            _shutdownFlushClaimed = true;
            if (_ledger == null)
            {
                return;
            }

            SaveFailOpen("shutdown");
        }
    }

    internal static void LogOnce(string key, string message)
    {
        lock (Gate)
        {
            if (!LoggedFailures.Add(key ?? string.Empty))
            {
                return;
            }

            Plugin.Log?.LogWarning(message);
        }
    }

    private static void RecordPositiveDeltas(
        NativeStatisticsSnapshot before,
        NativeStatisticsSnapshot after,
        string source,
        WorkRelaxObservationKind kind)
    {
        if (_ledger == null || _modeController == null)
        {
            return;
        }

        double now = MonotonicSeconds();
        string correlationId = "session-" + Guid.NewGuid().ToString("N");
        IReadOnlyList<ObservedNativeMetricDelta> positive =
            WorkRelaxObservationDeltaPolicy.SelectPositive(before, after, kind);

        ModeAllocation? pomodoroAllocation = null;
        var activityAllocations = new Dictionary<string, ModeAllocation>(StringComparer.Ordinal);
        foreach (ObservedNativeMetricDelta candidate in positive)
        {
            WorkRelaxMetricDefinition metric = candidate.Metric;
            double amount = candidate.Amount;
            if (metric.Family == WorkRelaxMetricFamily.PomodoroDuration)
            {
                pomodoroAllocation ??= _durationCursors?.CommitPomodoro(
                                            amount, _modeController.CurrentMode, now) ??
                                        WorkRelaxIntervalAllocator.Unclassified(amount);
                AppendAllocatedEvents(metric, pomodoroAllocation.Value, source, correlationId);
            }
            else if (metric.Family == WorkRelaxMetricFamily.ActivityDuration)
            {
                if (!activityAllocations.TryGetValue(metric.Key, out ModeAllocation allocation))
                {
                    allocation = _durationCursors?.CommitActivity(
                                     metric.Key, amount, _modeController.CurrentMode, now) ??
                                 WorkRelaxIntervalAllocator.Unclassified(amount);
                    activityAllocations.Add(metric.Key, allocation);
                }

                AppendAllocatedEvents(metric, allocation, source, correlationId);
            }
            else
            {
                AppendEvent(metric, amount, ToAttributionMode(_modeController.CurrentMode), source, correlationId);
            }
        }

    }

    private static void AppendAllocatedEvents(
        WorkRelaxMetricDefinition metric,
        ModeAllocation allocation,
        string source, string correlationId)
    {
        AppendEvent(metric, allocation.Work, WorkRelaxAttributionMode.Work, source, correlationId);
        AppendEvent(metric, allocation.Relax, WorkRelaxAttributionMode.Relax, source, correlationId);
        AppendEvent(metric, allocation.Unclassified, WorkRelaxAttributionMode.Unclassified, source, correlationId);
    }

    private static void AppendEvent(
        WorkRelaxMetricDefinition metric,
        double amount,
        WorkRelaxAttributionMode mode,
        string source, string correlationId)
    {
        if (_ledger == null || amount <= 0d)
        {
            return;
        }

        _ledger.AppendEvent(new WorkRelaxLedgerEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            UtcTicks = DateTime.UtcNow.Ticks,
            MetricFamily = metric.Family.ToString(),
            MetricKey = metric.Key,
            Amount = amount,
            Mode = mode.ToString(),
            Source = source ?? string.Empty,
            CorrelationId = correlationId
        });
        PendingAttributedDeltas.Add(new AttributedMetricDelta(metric.Key, metric.Family, amount));
    }

    private static BlueSageReportProjection BuildProjection(NativeStatisticsSnapshot snapshot,
        NativeAchievementSummary? achievementSummary = null, DateTime? referenceUtc = null)
    {
        if (_ledger == null) throw new InvalidOperationException("Work / Relax ledger is unavailable.");
        BlueSageMoodObservation[] moods = _ledger.Events
            .Where(item => string.Equals(item.MetricFamily, WorkRelaxMetricFamily.Mood.ToString(), StringComparison.Ordinal))
            .Select(item => new BlueSageMoodObservation(ParseMoodFeel(item.MetricKey), item.UtcTicks,
                ResolveObservationMode(item, _ledger.Corrections)))
            .ToArray();
        BlueSageAchievementObservation[] achievements = _ledger.Events
            .Where(item => string.Equals(item.MetricFamily, WorkRelaxMetricFamily.AchievementObservation.ToString(), StringComparison.Ordinal))
            .Select(item => new BlueSageAchievementObservation(
                item.MetricKey.StartsWith("Achievement:", StringComparison.Ordinal) ? item.MetricKey.Substring(12) : item.MetricKey,
                true, ResolveObservationMode(item, _ledger.Corrections), item.UtcTicks))
            .ToArray();
        return BlueSageReportProjection.Build(snapshot, _ledger.Events, _ledger.Corrections, moods, achievements,
            achievementSummary, referenceUtc);
    }

    private static void RecordMoodObservations(IEnumerable<NativeMoodObservation> observations,
        WorkRelaxAttributionMode observationMode)
    {
        if (_ledger == null || _modeController == null) return;
        var known = new HashSet<string>(_ledger.Events
            .Where(item => string.Equals(item.MetricFamily, WorkRelaxMetricFamily.Mood.ToString(), StringComparison.Ordinal))
            .Select(item => item.EventId), StringComparer.Ordinal);
        foreach (NativeMoodObservation mood in observations)
        {
            string id = "mood-" + mood.UtcTicks + "-" + mood.Feel;
            if (!known.Add(id)) continue;
            _ledger.AppendEvent(new WorkRelaxLedgerEvent
            {
                EventId = id,
                UtcTicks = mood.UtcTicks,
                MetricFamily = WorkRelaxMetricFamily.Mood.ToString(),
                MetricKey = "Mood:" + mood.Feel,
                Amount = 1d,
                Mode = observationMode.ToString(),
                Source = "Mood observation"
            });
        }
    }

    private static void RecordAchievementObservations(IEnumerable<NativeAchievementObservation> observations,
        WorkRelaxAttributionMode observationMode)
    {
        if (_ledger == null || _modeController == null) return;
        var known = new HashSet<string>(_ledger.Events
            .Where(item => string.Equals(item.MetricFamily, WorkRelaxMetricFamily.AchievementObservation.ToString(), StringComparison.Ordinal))
            .Select(item => item.MetricKey), StringComparer.Ordinal);
        foreach (NativeAchievementObservation achievement in observations.Where(item => item.Unlocked))
        {
            string metricKey = "Achievement:" + achievement.Key;
            if (!known.Add(metricKey)) continue;
            _ledger.AppendEvent(new WorkRelaxLedgerEvent
            {
                EventId = "achievement-" + Guid.NewGuid().ToString("N"),
                UtcTicks = DateTime.UtcNow.Ticks,
                MetricFamily = WorkRelaxMetricFamily.AchievementObservation.ToString(),
                MetricKey = metricKey,
                Amount = 1d,
                Mode = observationMode.ToString(),
                Source = "Achievement observation"
            });
        }
    }

    private static void AppendCorrection(WorkRelaxCorrection correction, string batchId, string? targetBatchId)
    {
        if (_ledger == null) return;
        WorkRelaxMetricDefinition metric = WorkRelaxMetricCatalog.Get(correction.MetricKey);
        _ledger.AppendCorrection(new WorkRelaxLedgerEvent
        {
            EventId = "correction-" + Guid.NewGuid().ToString("N"),
            UtcTicks = DateTime.UtcNow.Ticks,
            MetricFamily = metric.Family.ToString(),
            MetricKey = correction.MetricKey,
            Amount = correction.Amount,
            Mode = correction.ToMode.ToString(),
            Source = (correction.IsUndo ? "Undo:" : "Move:") + correction.FromMode + "->" + correction.ToMode,
            TargetEventId = correction.TargetEventId,
            BatchId = batchId,
            TargetBatchId = targetBatchId
        });
    }

    private static bool TryReadCorrection(WorkRelaxLedgerEvent item, out WorkRelaxAttributionMode from,
        out WorkRelaxAttributionMode to)
    {
        from = default;
        to = default;
        if (item.Source == null || (!item.Source.StartsWith("Move:", StringComparison.Ordinal) &&
            !item.Source.StartsWith("Undo:", StringComparison.Ordinal))) return false;
        string[] pair = item.Source.Substring(item.Source.IndexOf(':') + 1)
            .Split(new[] { "->" }, StringSplitOptions.None);
        return pair.Length == 2 && Enum.TryParse(pair[0], true, out from) && Enum.TryParse(pair[1], true, out to);
    }

    private static WorkRelaxAttributionMode ParseAttributionMode(string? mode) =>
        Enum.TryParse(mode, true, out WorkRelaxAttributionMode parsed) ? parsed : WorkRelaxAttributionMode.Unclassified;

    private static int ParseMoodFeel(string key)
    {
        return key.StartsWith("Mood:", StringComparison.Ordinal) &&
               int.TryParse(key.Substring(5), out int feel) ? feel : 0;
    }

    private static WorkRelaxAttributionMode ResolveObservationMode(
        WorkRelaxLedgerEvent observation,
        IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        WorkRelaxAttributionMode mode = ParseAttributionMode(observation.Mode);
        var targets = new HashSet<string>(StringComparer.Ordinal) { observation.EventId };
        foreach (WorkRelaxLedgerEvent correction in corrections.OrderBy(item => item.UtcTicks))
        {
            if (correction.TargetEventId == null || !targets.Contains(correction.TargetEventId) ||
                !TryReadCorrection(correction, out WorkRelaxAttributionMode from, out WorkRelaxAttributionMode to) ||
                from != mode)
                continue;
            mode = to;
            targets.Add(correction.EventId);
        }
        return mode;
    }

    private static void AppendReconciliationAdjustment(ReconciliationDecision decision)
    {
        if (_ledger == null || decision.Amount <= 0d ||
            (decision.Kind != ReconciliationKind.UnclassifiedAdjustment && decision.Kind != ReconciliationKind.EpochReset))
        {
            return;
        }

        WorkRelaxMetricDefinition metric = decision.MetricKey.StartsWith("FocusSessionsPerDay[", StringComparison.Ordinal)
            ? WorkRelaxMetricCatalog.GetWeekdayBucket(ParseWeekdayIndex(decision.MetricKey))
            : WorkRelaxMetricCatalog.Get(decision.MetricKey);
        _ledger.AppendEvent(new WorkRelaxLedgerEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            UtcTicks = DateTime.UtcNow.Ticks,
            MetricFamily = metric.Family.ToString(),
            MetricKey = metric.Key,
            Amount = decision.Amount,
            Mode = WorkRelaxAttributionMode.Unclassified.ToString(),
            Source = "Native reconciliation"
        });
    }

    private static void ReconcileWeekdayBuckets(
        WorkRelaxLedgerStatistics previous,
        NativeStatisticsSnapshot current,
        NativeEpochMarkers previousEpoch,
        NativeEpochMarkers currentEpoch)
    {
        int[] previousWeekdays = previous.FocusSessionsPerDay ?? new int[7];
        int[] currentWeekdays = current.FocusSessionsPerDay;
        for (int index = 0; index < 7; index++)
        {
            WorkRelaxMetricDefinition metric = WorkRelaxMetricCatalog.GetWeekdayBucket(index);
            ReconciliationDecision decision = WorkRelaxReconciliationPolicy.Reconcile(
                metric.Key,
                index < previousWeekdays.Length ? previousWeekdays[index] : 0d,
                currentWeekdays[index],
                PendingAttributedDeltas,
                previousEpoch,
                currentEpoch);
            AppendReconciliationAdjustment(decision);
        }
    }

    private static NativeEpochMarkers ReadEpoch(WorkRelaxLedgerStatistics statistics) =>
        new(
            (int)ReadLedgerNumber(statistics, "DayOfYear"),
            (int)ReadLedgerNumber(statistics, "CurrentWeek"),
            (int)ReadLedgerNumber(statistics, "CurrentMonth"));

    private static NativeEpochMarkers ReadEpoch(NativeStatisticsSnapshot snapshot) =>
        new(
            (int)snapshot.GetNumber("DayOfYear"),
            (int)snapshot.GetNumber("CurrentWeek"),
            (int)snapshot.GetNumber("CurrentMonth"));

    private static double ReadLedgerNumber(WorkRelaxLedgerStatistics statistics, string key) =>
        statistics.Numbers.FirstOrDefault(metric => string.Equals(metric.Key, key, StringComparison.Ordinal))?.Value ?? 0d;

    private static void InitializeDurationCursors() =>
        _durationCursors = new WorkRelaxDurationCursorController(
            WorkRelaxMetricCatalog.NativeMetrics
                .Where(metric => metric.Family == WorkRelaxMetricFamily.ActivityDuration)
                .Select(metric => metric.Key));

    private static WorkRelaxMode ParseSavedMode(string? value) =>
        string.Equals(value, "Relax", StringComparison.OrdinalIgnoreCase)
            ? WorkRelaxMode.Relax
            : WorkRelaxMode.Work;

    private static string ModeName(WorkRelaxMode mode) => mode == WorkRelaxMode.Relax ? "Relax" : "Work";

    private static WorkRelaxAttributionMode ToAttributionMode(WorkRelaxMode mode) =>
        mode == WorkRelaxMode.Relax ? WorkRelaxAttributionMode.Relax : WorkRelaxAttributionMode.Work;

    private static double MonotonicSeconds() => Time.realtimeSinceStartup;

    private static int ParseWeekdayIndex(string key)
    {
        int start = key.IndexOf("[", StringComparison.Ordinal) + 1;
        int end = key.IndexOf(']', start);
        return int.Parse(key.Substring(start, end - start));
    }

    private static void SaveFailOpen(string reason)
    {
        try
        {
            _store?.Save(_ledger!);
        }
        catch (Exception exception)
        {
            LogOnce("save-" + exception.GetType().Name,
                "Work / Relax ledger save failed safely during " + reason + ": " +
                exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static void ResetUninitializedState()
    {
        _store = null;
        _ledger = null;
        _modeController = null;
        _durationCursors = null;
        PendingAttributedDeltas.Clear();
        _nativeObservationDepth = 0;
        _reconciliationPending = false;
    }

}
