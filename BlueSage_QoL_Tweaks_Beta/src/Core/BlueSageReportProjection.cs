#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal enum BlueSageReportFilter
{
    All,
    Work,
    Relax,
    Unclassified
}

internal sealed class BlueSageReportMetric
{
    internal BlueSageReportMetric(string key, double value, string unitLabel, string qualifier,
        IReadOnlyList<double>? weekdayValues = null)
    {
        Key = key;
        Value = value;
        UnitLabel = unitLabel;
        Qualifier = qualifier;
        WeekdayValues = weekdayValues ?? Array.Empty<double>();
    }

    internal string Key { get; }
    internal double Value { get; }
    internal string UnitLabel { get; }
    internal string Qualifier { get; }
    internal IReadOnlyList<double> WeekdayValues { get; }
}

internal readonly struct BlueSageMoodObservation
{
    internal BlueSageMoodObservation(int feel, long utcTicks, WorkRelaxAttributionMode mode)
    {
        Feel = feel;
        UtcTicks = utcTicks;
        Mode = mode;
    }

    internal int Feel { get; }
    internal long UtcTicks { get; }
    internal WorkRelaxAttributionMode Mode { get; }
}

internal readonly struct NativeMoodObservation
{
    internal NativeMoodObservation(int feel, long utcTicks) { Feel = feel; UtcTicks = utcTicks; }
    internal int Feel { get; }
    internal long UtcTicks { get; }
}

internal readonly struct NativeAchievementObservation
{
    internal NativeAchievementObservation(string key, bool unlocked) { Key = key ?? string.Empty; Unlocked = unlocked; }
    internal string Key { get; }
    internal bool Unlocked { get; }
}

internal readonly struct NativeAchievementSummary
{
    internal NativeAchievementSummary(int unlocked, int total)
    {
        Unlocked = Math.Max(0, unlocked);
        Total = Math.Max(Unlocked, total);
    }
    internal int Unlocked { get; }
    internal int Total { get; }
}

internal sealed class BlueSageMoodStats
{
    internal BlueSageMoodStats(IEnumerable<BlueSageMoodObservation> observations, DateTime referenceUtc)
    {
        BlueSageMoodObservation[] items = observations.ToArray();
        Total = items.Length;
        OverviewCounts = CountFeels(items);
        if (items.Length == 0)
        {
            MonthCounts = CountFeels(items);
            YearDailyCounts = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
            return;
        }
        MonthCounts = CountFeels(items.Where(item =>
        {
            DateTime time = new(item.UtcTicks, DateTimeKind.Utc);
            return time.Year == referenceUtc.Year && time.Month == referenceUtc.Month;
        }));
        YearDailyCounts = new ReadOnlyDictionary<string, int>(items
            .Where(item => new DateTime(item.UtcTicks, DateTimeKind.Utc).Year == referenceUtc.Year)
            .GroupBy(item => new DateTime(item.UtcTicks, DateTimeKind.Utc).ToString("yyyy-MM-dd") + "|" + item.Feel)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
    }

    internal int Total { get; }
    internal IReadOnlyDictionary<int, int> OverviewCounts { get; }
    internal IReadOnlyDictionary<int, int> MonthCounts { get; }
    internal IReadOnlyDictionary<string, int> YearDailyCounts { get; }

    private static IReadOnlyDictionary<int, int> CountFeels(IEnumerable<BlueSageMoodObservation> items)
    {
        var counts = new Dictionary<int, int>();
        foreach (BlueSageMoodObservation item in items)
            counts[item.Feel] = counts.TryGetValue(item.Feel, out int count) ? count + 1 : 1;
        return new ReadOnlyDictionary<int, int>(counts);
    }
}

internal sealed class BlueSageMoodUiModel
{
    private BlueSageMoodUiModel(IReadOnlyList<string> overview, IReadOnlyList<string> month,
        IReadOnlyList<string> year)
    { OverviewLines = overview; MonthLines = month; YearLines = year; }
    internal IReadOnlyList<string> OverviewLines { get; }
    internal IReadOnlyList<string> MonthLines { get; }
    internal IReadOnlyList<string> YearLines { get; }
    internal static BlueSageMoodUiModel Build(BlueSageMoodStats stats)
    {
        string[] overview = stats.OverviewCounts.OrderBy(item => item.Key)
            .Select(item => BlueSageJournalMoodPresentation.Label(item.Key) + ": " + item.Value).ToArray();
        string[] month = stats.MonthCounts.Count == 0 ? new[] { "No check-ins this month." } :
            stats.MonthCounts.OrderBy(item => item.Key).Select(item => BlueSageJournalMoodPresentation.Label(item.Key) + ": " + item.Value).ToArray();
        string[] year = stats.YearDailyCounts.OrderBy(item => item.Key).Select(item =>
        {
            string[] parts = item.Key.Split('|');
            return parts[0] + " - " + BlueSageJournalMoodPresentation.Label(int.Parse(parts[1])) + ": " + item.Value;
        }).ToArray();
        return new BlueSageMoodUiModel(overview, month, year);
    }
}

internal static class BlueSageJournalMoodPresentation
{
    internal static string Label(int feel) => feel switch
    {
        0 => "Great (green face)",
        1 => "Good (orange face)",
        2 => "Okay (blue face)",
        3 => "Low (purple face)",
        4 => "Rough (red face)",
        _ => "Journal mood " + feel
    };
}

internal readonly struct BlueSageAchievementProgress
{
    internal BlueSageAchievementProgress(int unlocked, int total, bool isOverallNative)
    { Unlocked = unlocked; Total = total; IsOverallNative = isOverallNative; }
    internal int Unlocked { get; }
    internal int Total { get; }
    internal bool IsOverallNative { get; }
}

internal sealed class BlueSageAchievementObservation
{
    internal BlueSageAchievementObservation(string key, bool unlocked, WorkRelaxAttributionMode mode, long utcTicks)
    {
        Key = key ?? string.Empty;
        Unlocked = unlocked;
        Mode = mode;
        UtcTicks = utcTicks;
    }

    internal string Key { get; }
    internal bool Unlocked { get; }
    internal WorkRelaxAttributionMode Mode { get; }
    internal long UtcTicks { get; }
    internal string ObservationLabel => Mode switch
    {
        WorkRelaxAttributionMode.Work => "Observed during Work",
        WorkRelaxAttributionMode.Relax => "Observed during Relax",
        _ => "Observed before Work / Relax tracking"
    };
}

internal sealed class BlueSageReportView
{
    private readonly IReadOnlyDictionary<string, BlueSageReportMetric> _byKey;

    internal BlueSageReportView(BlueSageReportFilter filter, IEnumerable<BlueSageReportMetric> metrics,
        IEnumerable<BlueSageMoodObservation> moods, IEnumerable<BlueSageAchievementObservation> achievements,
        BlueSageAchievementProgress achievementProgress, DateTime referenceUtc)
    {
        Filter = filter;
        Metrics = new ReadOnlyCollection<BlueSageReportMetric>(metrics.ToList());
        _byKey = Metrics.ToDictionary(metric => metric.Key, StringComparer.Ordinal);
        Moods = new ReadOnlyCollection<BlueSageMoodObservation>(moods.ToList());
        Achievements = new ReadOnlyCollection<BlueSageAchievementObservation>(achievements.ToList());
        MoodStats = new BlueSageMoodStats(Moods, referenceUtc);
        AchievementProgress = achievementProgress;
    }

    internal BlueSageReportFilter Filter { get; }
    internal IReadOnlyList<BlueSageReportMetric> Metrics { get; }
    internal IReadOnlyList<BlueSageMoodObservation> Moods { get; }
    internal IReadOnlyList<BlueSageAchievementObservation> Achievements { get; }
    internal BlueSageMoodStats MoodStats { get; }
    internal BlueSageAchievementProgress AchievementProgress { get; }
    internal BlueSageReportMetric Get(string key) => _byKey[key];
}

internal sealed class BlueSageReportProjection
{
    private BlueSageReportProjection(BlueSageReportView all, BlueSageReportView work,
        BlueSageReportView relax, BlueSageReportView unclassified,
        IReadOnlyList<BlueSageReportHistoryItem> history)
    {
        All = all;
        Work = work;
        Relax = relax;
        Unclassified = unclassified;
        History = history;
    }

    internal BlueSageReportView All { get; }
    internal BlueSageReportView Work { get; }
    internal BlueSageReportView Relax { get; }
    internal BlueSageReportView Unclassified { get; }
    internal IReadOnlyList<BlueSageReportHistoryItem> History { get; }

    internal BlueSageReportView Select(BlueSageReportFilter filter) => filter switch
    {
        BlueSageReportFilter.Work => Work,
        BlueSageReportFilter.Relax => Relax,
        BlueSageReportFilter.Unclassified => Unclassified,
        _ => All
    };

    internal static BlueSageReportProjection Build(NativeStatisticsSnapshot native,
        IEnumerable<WorkRelaxLedgerEvent> events, IEnumerable<WorkRelaxLedgerEvent> corrections,
        IEnumerable<BlueSageMoodObservation> moods,
        IEnumerable<BlueSageAchievementObservation> achievements,
        NativeAchievementSummary? achievementSummary = null, DateTime? referenceUtc = null)
    {
        if (native == null) throw new ArgumentNullException(nameof(native));
        WorkRelaxLedgerEvent[] eventList = (events ?? throw new ArgumentNullException(nameof(events))).ToArray();
        WorkRelaxLedgerEvent[] correctionList = (corrections ?? throw new ArgumentNullException(nameof(corrections))).ToArray();
        BlueSageMoodObservation[] moodList = (moods ?? throw new ArgumentNullException(nameof(moods))).ToArray();
        BlueSageAchievementObservation[] achievementList =
            (achievements ?? throw new ArgumentNullException(nameof(achievements))).ToArray();
        DateTime reportReferenceUtc = (referenceUtc ?? DateTime.UtcNow).ToUniversalTime();

        Dictionary<string, ModeAllocation> effective = BuildEffectiveBalances(eventList, correctionList);
        var all = new List<BlueSageReportMetric>();
        var work = new List<BlueSageReportMetric>();
        var relax = new List<BlueSageReportMetric>();
        var unclassified = new List<BlueSageReportMetric>();

        foreach (string key in NativeStatisticsSnapshot.FieldNames)
        {
            WorkRelaxMetricDefinition definition = WorkRelaxMetricCatalog.Get(key);
            double nativeValue = key == "FocusSessionsPerDay"
                ? native.FocusSessionsPerDay.Sum()
                : native.GetNumber(key);
            string unit = UnitLabel(definition.Unit);
            IReadOnlyList<double> allWeekdays = key == "FocusSessionsPerDay"
                ? native.FocusSessionsPerDay.Select(value => (double)value).ToArray()
                : Array.Empty<double>();
            all.Add(new BlueSageReportMetric(key, nativeValue, unit, string.Empty, allWeekdays));

            if (key == "FocusSessionsPerDay")
            {
                var workDays = new double[7];
                var relaxDays = new double[7];
                var unclassifiedDays = new double[7];
                int[] nativeDays = native.FocusSessionsPerDay;
                for (int day = 0; day < 7; day++)
                {
                    string bucket = WorkRelaxMetricCatalog.GetWeekdayBucket(day).Key;
                    ModeAllocation allocation = effective.TryGetValue(bucket, out ModeAllocation foundBucket) ? foundBucket : default;
                    workDays[day] = Math.Min(nativeDays[day], Math.Max(0d, allocation.Work));
                    relaxDays[day] = Math.Min(nativeDays[day] - workDays[day], Math.Max(0d, allocation.Relax));
                    unclassifiedDays[day] = Math.Max(0d, nativeDays[day] - workDays[day] - relaxDays[day]);
                }
                work.Add(new BlueSageReportMetric(key, workDays.Sum(), unit, "Attributed", workDays));
                relax.Add(new BlueSageReportMetric(key, relaxDays.Sum(), unit, "Attributed", relaxDays));
                unclassified.Add(new BlueSageReportMetric(key, unclassifiedDays.Sum(), unit, "Unclassified balance", unclassifiedDays));
                continue;
            }

            if (!definition.IsAdditive)
            {
                work.Add(new BlueSageReportMetric(key, nativeValue, unit, "Overall native", allWeekdays));
                relax.Add(new BlueSageReportMetric(key, nativeValue, unit, "Overall native", allWeekdays));
                unclassified.Add(new BlueSageReportMetric(key, nativeValue, unit, "Overall native", allWeekdays));
                continue;
            }

            ModeAllocation attributed = effective.TryGetValue(key, out ModeAllocation found) ? found : default;
            double workValue = Math.Min(nativeValue, Math.Max(0d, attributed.Work));
            double relaxValue = Math.Min(Math.Max(0d, nativeValue - workValue), Math.Max(0d, attributed.Relax));
            double unclassifiedValue = Math.Max(0d, nativeValue - workValue - relaxValue);
            work.Add(new BlueSageReportMetric(key, workValue, unit, "Attributed"));
            relax.Add(new BlueSageReportMetric(key, relaxValue, unit, "Attributed"));
            unclassified.Add(new BlueSageReportMetric(key, unclassifiedValue, unit, "Unclassified balance"));
        }

        return new BlueSageReportProjection(
            View(BlueSageReportFilter.All, all, moodList, achievementList, achievementSummary, reportReferenceUtc),
            View(BlueSageReportFilter.Work, work, moodList, achievementList, achievementSummary, reportReferenceUtc),
            View(BlueSageReportFilter.Relax, relax, moodList, achievementList, achievementSummary, reportReferenceUtc),
            View(BlueSageReportFilter.Unclassified, unclassified, moodList, achievementList, achievementSummary, reportReferenceUtc),
            BuildHistory(eventList, correctionList));
    }

    internal static ModeAllocation EffectiveBalance(string metricKey,
        IEnumerable<WorkRelaxLedgerEvent> events, IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        Dictionary<string, ModeAllocation> balances = BuildEffectiveBalances(events, corrections);
        return balances.TryGetValue(metricKey, out ModeAllocation value) ? value : default;
    }

    internal static BlueSageEffectiveEvent ResolveEffectiveEvent(WorkRelaxLedgerEvent primary,
        IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        WorkRelaxAttributionMode mode = ParseMode(primary.Mode);
        string targetEventId = primary.EventId;
        var targets = new HashSet<string>(StringComparer.Ordinal) { primary.EventId };
        var audit = new List<string>();
        foreach (WorkRelaxLedgerEvent correction in corrections.OrderBy(item => item.UtcTicks))
        {
            if (correction.TargetEventId == null || !targets.Contains(correction.TargetEventId) ||
                !TryParseCorrection(correction, out WorkRelaxAttributionMode from, out WorkRelaxAttributionMode to) ||
                from != mode) continue;
            mode = to;
            targetEventId = correction.EventId;
            targets.Add(correction.EventId);
            audit.Add((correction.Source ?? "Correction") + " [" + correction.EventId + "]");
        }
        return new BlueSageEffectiveEvent(primary, mode, targetEventId,
            new ReadOnlyCollection<string>(audit));
    }

    private static BlueSageReportView View(BlueSageReportFilter filter, IEnumerable<BlueSageReportMetric> metrics,
        IEnumerable<BlueSageMoodObservation> moods, IEnumerable<BlueSageAchievementObservation> achievements,
        NativeAchievementSummary? achievementSummary, DateTime referenceUtc) =>
        new(filter, metrics,
            filter == BlueSageReportFilter.All ? moods : moods.Where(mood => Matches(filter, mood.Mode)),
            filter == BlueSageReportFilter.All ? achievements : achievements.Where(item => Matches(filter, item.Mode)),
            filter == BlueSageReportFilter.All && achievementSummary.HasValue
                ? new BlueSageAchievementProgress(achievementSummary.Value.Unlocked, achievementSummary.Value.Total, true)
                : new BlueSageAchievementProgress(
                    achievements.Count(item => filter != BlueSageReportFilter.All && Matches(filter, item.Mode)),
                    0, false), referenceUtc);

    private static bool Matches(BlueSageReportFilter filter, WorkRelaxAttributionMode mode) =>
        filter == BlueSageReportFilter.Work && mode == WorkRelaxAttributionMode.Work ||
        filter == BlueSageReportFilter.Relax && mode == WorkRelaxAttributionMode.Relax ||
        filter == BlueSageReportFilter.Unclassified && mode == WorkRelaxAttributionMode.Unclassified;

    private static Dictionary<string, ModeAllocation> BuildEffectiveBalances(
        IEnumerable<WorkRelaxLedgerEvent> events, IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        var result = new Dictionary<string, ModeAllocation>(StringComparer.Ordinal);
        foreach (WorkRelaxLedgerEvent item in events.OrderBy(item => item.UtcTicks))
        {
            if (item.Amount <= 0d || string.IsNullOrWhiteSpace(item.MetricKey)) continue;
            result[item.MetricKey] = Add(result.TryGetValue(item.MetricKey, out ModeAllocation prior) ? prior : default,
                ParseMode(item.Mode), item.Amount);
        }

        foreach (WorkRelaxLedgerEvent item in corrections.OrderBy(item => item.UtcTicks))
        {
            if (!TryParseCorrection(item, out WorkRelaxAttributionMode from, out WorkRelaxAttributionMode to)) continue;
            ModeAllocation prior = result.TryGetValue(item.MetricKey, out ModeAllocation found) ? found : default;
            try
            {
                result[item.MetricKey] = WorkRelaxCorrectionPolicy.Reclassify(item.MetricKey, prior, from, to,
                    item.Amount, item.TargetEventId ?? string.Empty).Balance;
            }
            catch (ArgumentException)
            {
                // Corrupt or stale correction records are ignored; native Report remains authoritative.
            }
        }
        return result;
    }

    private static ModeAllocation Add(ModeAllocation value, WorkRelaxAttributionMode mode, double amount) => mode switch
    {
        WorkRelaxAttributionMode.Work => new ModeAllocation(value.Work + amount, value.Relax, value.Unclassified),
        WorkRelaxAttributionMode.Relax => new ModeAllocation(value.Work, value.Relax + amount, value.Unclassified),
        _ => new ModeAllocation(value.Work, value.Relax, value.Unclassified + amount)
    };

    private static WorkRelaxAttributionMode ParseMode(string? mode) =>
        Enum.TryParse(mode, true, out WorkRelaxAttributionMode parsed) ? parsed : WorkRelaxAttributionMode.Unclassified;

    private static bool TryParseCorrection(WorkRelaxLedgerEvent item, out WorkRelaxAttributionMode from,
        out WorkRelaxAttributionMode to)
    {
        from = default;
        to = default;
        string source = item.Source ?? string.Empty;
        if (!source.StartsWith("Move:", StringComparison.Ordinal) &&
            !source.StartsWith("Undo:", StringComparison.Ordinal)) return false;
        string[] pair = source.Substring(source.IndexOf(':') + 1).Split(new[] { "->" }, StringSplitOptions.None);
        return pair.Length == 2 && Enum.TryParse(pair[0], true, out from) && Enum.TryParse(pair[1], true, out to);
    }

    private static IReadOnlyList<BlueSageReportHistoryItem> BuildHistory(
        IEnumerable<WorkRelaxLedgerEvent> events, IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        WorkRelaxLedgerEvent[] correctionList = corrections.ToArray();
        List<BlueSageReportHistoryItem> result = events
            .Select(item => ResolveEffectiveEvent(item, correctionList))
            .OrderByDescending(item => item.Primary.UtcTicks)
            .GroupBy(item => new
            {
                Correlation = string.IsNullOrWhiteSpace(item.Primary.CorrelationId)
                    ? "legacy:" + item.Primary.UtcTicks / TimeSpan.TicksPerMinute + ":" + item.Primary.Source
                    : item.Primary.CorrelationId,
                item.Primary.Source,
                item.Mode
            })
            .Select(group => new BlueSageReportHistoryItem(
                string.Join(",", group.Select(item => item.Primary.EventId)),
                group.Key.Mode +
                " session (" + group.Count() + " tracked value" + (group.Count() == 1 ? string.Empty : "s") + ")",
                group.Max(item => item.Primary.UtcTicks),
                group.Select(item => item.Primary.MetricKey).Distinct(StringComparer.Ordinal).Count() == 1
                    ? group.First().Primary.MetricKey : string.Empty,
                group.Select(item => WorkRelaxMetricCatalog.Get(item.Primary.MetricKey).Unit).Distinct().Count() == 1 &&
                group.Select(item => WorkRelaxMetricCatalog.Get(item.Primary.MetricKey).Family).Distinct().Count() == 1
                    ? group.Sum(item => item.Primary.Amount) : 0d,
                group.Key.Mode,
                group.Select(item => new BlueSageReportHistoryMember(item.Primary.EventId,
                    item.Primary.MetricKey, item.Primary.Amount, item.Mode, item.TargetEventId)).ToArray(),
                group.Select(item => BlueSageMetricPresentation.FriendlyLabel(item.Primary.MetricKey) + ": " +
                    item.Primary.Amount.ToString("0.##") + " " +
                    BlueSageMetricPresentation.UnitLabel(item.Primary.MetricKey)).ToArray(),
                group.SelectMany(item => item.AuditLines).ToArray()))
            .ToList();
        return new ReadOnlyCollection<BlueSageReportHistoryItem>(result);
    }

    private static string UnitLabel(WorkRelaxMetricUnit unit) => unit switch
    {
        WorkRelaxMetricUnit.Seconds => "seconds",
        WorkRelaxMetricUnit.Minutes => "minutes",
        WorkRelaxMetricUnit.WeekdayCounts => "sessions by weekday",
        WorkRelaxMetricUnit.Marker => "native marker",
        _ => "count"
    };
}

internal sealed class BlueSageReportHistoryItem
{
    internal BlueSageReportHistoryItem(string eventId, string summary, long utcTicks)
        : this(eventId, summary, utcTicks, string.Empty, 0d, WorkRelaxAttributionMode.Unclassified,
            Array.Empty<BlueSageReportHistoryMember>())
    {
    }

    internal BlueSageReportHistoryItem(string eventId, string summary, long utcTicks, string metricKey,
        double amount, WorkRelaxAttributionMode mode, IReadOnlyList<BlueSageReportHistoryMember>? members = null,
        IReadOnlyList<string>? displayLines = null, IReadOnlyList<string>? auditLines = null)
    {
        EventId = eventId ?? string.Empty;
        Summary = summary ?? string.Empty;
        UtcTicks = utcTicks;
        MetricKey = metricKey ?? string.Empty;
        Amount = amount;
        Mode = mode;
        Members = members ?? Array.Empty<BlueSageReportHistoryMember>();
        DisplayLines = displayLines ?? Array.Empty<string>();
        AuditLines = auditLines ?? Array.Empty<string>();
    }

    internal string EventId { get; }
    internal string Summary { get; }
    internal long UtcTicks { get; }
    internal string MetricKey { get; }
    internal double Amount { get; }
    internal WorkRelaxAttributionMode Mode { get; }
    internal IReadOnlyList<BlueSageReportHistoryMember> Members { get; }
    internal IReadOnlyList<string> DisplayLines { get; }
    internal IReadOnlyList<string> AuditLines { get; }
    internal string Render(bool detailsExpanded) => detailsExpanded
        ? Summary + " | " + EventId + (AuditLines.Count == 0 ? string.Empty : " | " + string.Join("; ", AuditLines))
        : Summary;
}

internal sealed class BlueSageEffectiveEvent
{
    internal BlueSageEffectiveEvent(WorkRelaxLedgerEvent primary, WorkRelaxAttributionMode mode,
        string targetEventId, IReadOnlyList<string> auditLines)
    { Primary = primary; Mode = mode; TargetEventId = targetEventId; AuditLines = auditLines; }
    internal WorkRelaxLedgerEvent Primary { get; }
    internal WorkRelaxAttributionMode Mode { get; }
    internal string TargetEventId { get; }
    internal IReadOnlyList<string> AuditLines { get; }
}

internal readonly struct BlueSageReportHistoryMember
{
    internal BlueSageReportHistoryMember(string eventId, string metricKey, double amount,
        WorkRelaxAttributionMode mode, string? targetEventId = null)
    { EventId = eventId; MetricKey = metricKey; Amount = amount; Mode = mode; TargetEventId = targetEventId ?? eventId; }
    internal string EventId { get; }
    internal string MetricKey { get; }
    internal double Amount { get; }
    internal WorkRelaxAttributionMode Mode { get; }
    internal string TargetEventId { get; }
}

internal sealed class BlueSageReportCorrectionBatch
{
    internal BlueSageReportCorrectionBatch(IReadOnlyList<WorkRelaxCorrection> corrections,
        IReadOnlyDictionary<string, ModeAllocation> balances, string batchId)
    { Corrections = corrections; Balances = balances; BatchId = batchId; }
    internal IReadOnlyList<WorkRelaxCorrection> Corrections { get; }
    internal IReadOnlyDictionary<string, ModeAllocation> Balances { get; }
    internal string BatchId { get; }
}

internal static class BlueSageReportCorrectionBatchPolicy
{
    internal static BlueSageReportCorrectionBatch Reclassify(IEnumerable<BlueSageReportHistoryMember> members,
        WorkRelaxAttributionMode target, IEnumerable<WorkRelaxLedgerEvent> events,
        IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        var planned = new List<WorkRelaxCorrection>();
        var balances = new Dictionary<string, ModeAllocation>(StringComparer.Ordinal);
        foreach (BlueSageReportHistoryMember member in members)
        {
            if (member.Mode == target || member.Amount <= 0d) continue;
            ModeAllocation balance = balances.TryGetValue(member.MetricKey, out ModeAllocation current)
                ? current
                : BlueSageReportProjection.EffectiveBalance(member.MetricKey, events, corrections);
            CorrectionResult result = WorkRelaxCorrectionPolicy.Reclassify(member.MetricKey, balance,
                member.Mode, target, member.Amount, member.TargetEventId);
            planned.Add(result.Correction);
            balances[member.MetricKey] = result.Balance;
        }
        return new BlueSageReportCorrectionBatch(new ReadOnlyCollection<WorkRelaxCorrection>(planned),
            new ReadOnlyDictionary<string, ModeAllocation>(balances), "batch-" + Guid.NewGuid().ToString("N"));
    }
}

internal static class BlueSageAggregateCorrectionPolicy
{
    internal static BlueSageReportCorrectionBatch Reclassify(string metricKey, double amount,
        WorkRelaxAttributionMode target, IEnumerable<WorkRelaxLedgerEvent> events,
        IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        if (!metricKey.StartsWith("Mood:", StringComparison.Ordinal))
            throw new ArgumentException("Aggregate event allocation is supported only for mood observations.", nameof(metricKey));
        if (amount <= 0d || amount != Math.Truncate(amount))
            throw new ArgumentOutOfRangeException(nameof(amount), "Mood allocation must use a positive whole count.");
        WorkRelaxLedgerEvent[] correctionList = corrections.ToArray();
        BlueSageEffectiveEvent[] eligible = events
            .Where(item => string.Equals(item.MetricKey, metricKey, StringComparison.Ordinal))
            .Select(item => BlueSageReportProjection.ResolveEffectiveEvent(item, correctionList))
            .Where(item => item.Mode == WorkRelaxAttributionMode.Unclassified)
            .OrderBy(item => item.Primary.UtcTicks).ToArray();
        if (eligible.Sum(item => item.Primary.Amount) < amount)
            throw new InvalidOperationException("No eligible Unclassified mood observations are available for that amount.");
        double remaining = amount;
        var members = new List<BlueSageReportHistoryMember>();
        foreach (BlueSageEffectiveEvent item in eligible)
        {
            if (remaining <= 0d) break;
            double selected = Math.Min(item.Primary.Amount, remaining);
            members.Add(new BlueSageReportHistoryMember(item.Primary.EventId, item.Primary.MetricKey,
                selected, item.Mode, item.TargetEventId));
            remaining -= selected;
        }
        BlueSageReportCorrectionBatch batch = BlueSageReportCorrectionBatchPolicy.Reclassify(
            members, target, events, correctionList);
        if (batch.Corrections.Count == 0)
            throw new InvalidOperationException("No eligible Unclassified mood observations were changed.");
        return batch;
    }
}

internal sealed class WorkRelaxCorrectionBatch
{
    internal WorkRelaxCorrectionBatch(string batchId, string targetBatchId,
        IReadOnlyList<WorkRelaxCorrection> corrections)
    { BatchId = batchId; TargetBatchId = targetBatchId; Corrections = corrections; }
    internal string BatchId { get; }
    internal string TargetBatchId { get; }
    internal IReadOnlyList<WorkRelaxCorrection> Corrections { get; }
}

internal static class WorkRelaxUndoHistoryPolicy
{
    internal static WorkRelaxLedgerEvent? FindNextUndoable(IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        WorkRelaxLedgerEvent[] items = corrections.OrderByDescending(item => item.UtcTicks).ToArray();
        var undone = new HashSet<string>(items.Where(item => (item.Source ?? string.Empty).StartsWith("Undo:", StringComparison.Ordinal))
            .Select(item => item.TargetEventId ?? string.Empty), StringComparer.Ordinal);
        return items.FirstOrDefault(item => (item.Source ?? string.Empty).StartsWith("Move:", StringComparison.Ordinal) &&
            !undone.Contains(item.EventId));
    }

    internal static WorkRelaxCorrection CreateUndo(ModeAllocation balance, WorkRelaxLedgerEvent original, string targetId)
    {
        string[] pair = (original.Source ?? string.Empty).Substring(5).Split(new[] { "->" }, StringSplitOptions.None);
        var correction = new WorkRelaxCorrection(original.MetricKey,
            (WorkRelaxAttributionMode)Enum.Parse(typeof(WorkRelaxAttributionMode), pair[0], true),
            (WorkRelaxAttributionMode)Enum.Parse(typeof(WorkRelaxAttributionMode), pair[1], true),
            original.Amount, original.TargetEventId ?? string.Empty,
            false);
        return WorkRelaxCorrectionPolicy.Undo(original.MetricKey, balance, correction, targetId).Correction;
    }

    internal static IReadOnlyList<WorkRelaxLedgerEvent>? FindNextUndoableBatch(
        IEnumerable<WorkRelaxLedgerEvent> corrections)
    {
        WorkRelaxLedgerEvent[] items = corrections.OrderBy(item => item.UtcTicks).ToArray();
        var undone = new HashSet<string>(items.Where(item => (item.Source ?? string.Empty).StartsWith("Undo:", StringComparison.Ordinal))
            .Select(item => item.TargetBatchId ?? string.Empty), StringComparer.Ordinal);
        return items.Where(item => (item.Source ?? string.Empty).StartsWith("Move:", StringComparison.Ordinal))
            .GroupBy(item => string.IsNullOrWhiteSpace(item.BatchId) ? item.EventId : item.BatchId!, StringComparer.Ordinal)
            .Where(group => !undone.Contains(group.Key))
            .OrderByDescending(group => group.Max(item => item.UtcTicks))
            .Select(group => (IReadOnlyList<WorkRelaxLedgerEvent>)group.ToArray())
            .FirstOrDefault();
    }

    internal static WorkRelaxCorrectionBatch CreateUndoBatch(IEnumerable<WorkRelaxLedgerEvent> events,
        IEnumerable<WorkRelaxLedgerEvent> corrections, IReadOnlyList<WorkRelaxLedgerEvent> originalBatch)
    {
        WorkRelaxLedgerEvent[] correctionList = corrections.ToArray();
        string targetBatchId = originalBatch.Select(item => item.BatchId).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
            ?? originalBatch[0].EventId;
        string undoBatchId = "batch-" + Guid.NewGuid().ToString("N");
        var balances = new Dictionary<string, ModeAllocation>(StringComparer.Ordinal);
        var undos = new List<WorkRelaxCorrection>();
        foreach (WorkRelaxLedgerEvent original in originalBatch)
        {
            ModeAllocation balance = balances.TryGetValue(original.MetricKey, out ModeAllocation prior) ? prior :
                BlueSageReportProjection.EffectiveBalance(original.MetricKey, events, correctionList);
            WorkRelaxCorrection undo = CreateUndo(balance, original, original.EventId);
            CorrectionResult applied = WorkRelaxCorrectionPolicy.Reclassify(undo.MetricKey, balance,
                undo.FromMode, undo.ToMode, undo.Amount, undo.TargetEventId);
            balances[original.MetricKey] = applied.Balance;
            undos.Add(undo);
        }
        return new WorkRelaxCorrectionBatch(undoBatchId, targetBatchId,
            new ReadOnlyCollection<WorkRelaxCorrection>(undos));
    }
}

internal static class WorkRelaxObservationBaselinePolicy
{
    internal static WorkRelaxAttributionMode ClassifyMood(WorkRelaxLedgerRecovery recovery, WorkRelaxAttributionMode mode) =>
        recovery.MoodBaselineInitialized ? mode : WorkRelaxAttributionMode.Unclassified;
    internal static WorkRelaxAttributionMode ClassifyAchievement(WorkRelaxLedgerRecovery recovery, WorkRelaxAttributionMode mode) =>
        recovery.AchievementBaselineInitialized ? mode : WorkRelaxAttributionMode.Unclassified;
    internal static WorkRelaxAttributionMode ClassifyMood(WorkRelaxLedgerRecovery recovery, WorkRelaxMode mode) =>
        ClassifyMood(recovery, mode == WorkRelaxMode.Relax ? WorkRelaxAttributionMode.Relax : WorkRelaxAttributionMode.Work);
    internal static WorkRelaxAttributionMode ClassifyAchievement(WorkRelaxLedgerRecovery recovery, WorkRelaxMode mode) =>
        ClassifyAchievement(recovery, mode == WorkRelaxMode.Relax ? WorkRelaxAttributionMode.Relax : WorkRelaxAttributionMode.Work);
    internal static void MarkMoodInitialized(WorkRelaxLedgerRecovery recovery) => recovery.MoodBaselineInitialized = true;
    internal static void MarkAchievementInitialized(WorkRelaxLedgerRecovery recovery) => recovery.AchievementBaselineInitialized = true;
}

internal static class WorkRelaxObservationRefreshPolicy
{
    internal static WorkRelaxAttributionMode? BeginMoodRefresh(WorkRelaxLedgerRecovery recovery,
        bool sourceAvailable, WorkRelaxMode mode)
    {
        if (!sourceAvailable) return null;
        WorkRelaxAttributionMode result = WorkRelaxObservationBaselinePolicy.ClassifyMood(recovery, mode);
        WorkRelaxObservationBaselinePolicy.MarkMoodInitialized(recovery);
        return result;
    }
    internal static WorkRelaxAttributionMode? BeginAchievementRefresh(WorkRelaxLedgerRecovery recovery,
        bool sourceAvailable, WorkRelaxMode mode)
    {
        if (!sourceAvailable) return null;
        WorkRelaxAttributionMode result = WorkRelaxObservationBaselinePolicy.ClassifyAchievement(recovery, mode);
        WorkRelaxObservationBaselinePolicy.MarkAchievementInitialized(recovery);
        return result;
    }
}

internal sealed class BlueSageAllocationOption
{
    internal BlueSageAllocationOption(string metricKey, string label)
    { MetricKey = metricKey; Label = label; }
    internal string MetricKey { get; }
    internal string Label { get; }
}

internal static class BlueSageMetricPresentation
{
    internal static string FriendlyLabel(string key)
    {
        if (key.StartsWith("Mood:", StringComparison.Ordinal) && int.TryParse(key.Substring(5), out int feel))
            return BlueSageJournalMoodPresentation.Label(feel);
        if (key.StartsWith("FocusSessionsPerDay[", StringComparison.Ordinal))
            return "Pomodoro sessions - weekday " + key.Substring(20).TrimEnd(']');
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TotalFocusDuration"] = "Pomodoro time", ["TodayFocusDuration"] = "Today's Pomodoro time",
            ["WeekFocusDuration"] = "This week's Pomodoro time", ["MonthFocusDuration"] = "This month's Pomodoro time",
            ["TotalSessionCount"] = "Pomodoro sessions", ["TodaySessionCount"] = "Today's Pomodoro sessions",
            ["WeekFocusCount"] = "This week's Pomodoro sessions", ["MonthFocusCount"] = "This month's Pomodoro sessions",
            ["TotalCompleteSessions"] = "Completed Pomodoro sessions", ["ToDoCompleted"] = "Task checks",
            ["SecondsPlayed"] = "Time in app"
        };
        if (labels.TryGetValue(key, out string? label)) return label;
        string spaced = string.Concat(key.Select((character, index) => index > 0 && char.IsUpper(character)
            ? " " + character : character.ToString()));
        return spaced.Replace(" Focus Count", " activity starts").Replace(" Focus Duration", " activity time");
    }
    internal static string UnitLabel(string key) => WorkRelaxMetricCatalog.Get(key).Unit switch
    {
        WorkRelaxMetricUnit.Seconds => "seconds",
        WorkRelaxMetricUnit.Minutes => "minutes",
        _ => "count"
    };
}

internal static class BlueSageAllocationCatalog
{
    private static readonly IReadOnlyList<BlueSageAllocationOption> _options = Build();
    internal static IReadOnlyList<BlueSageAllocationOption> Options => _options;
    private static IReadOnlyList<BlueSageAllocationOption> Build()
    {
        IEnumerable<WorkRelaxMetricDefinition> definitions = WorkRelaxMetricCatalog.NativeMetrics
            .Where(item => item.IsAdditive).Concat(WorkRelaxMetricCatalog.WeekdayBucketMetrics);
        var result = definitions.Select(item => new BlueSageAllocationOption(item.Key,
                BlueSageMetricPresentation.FriendlyLabel(item.Key) + " (" + BlueSageMetricPresentation.UnitLabel(item.Key) + ")"))
            .ToList();
        for (int feel = 0; feel <= 4; feel++) result.Add(new BlueSageAllocationOption("Mood:" + feel,
            BlueSageJournalMoodPresentation.Label(feel) + " (Journal check-ins)"));
        return new ReadOnlyCollection<BlueSageAllocationOption>(result);
    }
}

internal static class BlueSageReportSurfacePolicy
{
    internal static IReadOnlyList<string> FilterOrder { get; } =
        Array.AsReadOnly(new[] { "ALL", "WORK", "RELAX", "UNCLASSIFIED" });
    internal static IReadOnlyList<string> SectionOrder { get; } =
        Array.AsReadOnly(new[] { "Overview", "Activity Snapshot", "Detailed Analysis", "Journal Mood Check-ins", "Achievements", "Classification History" });
    internal const string QolFallbackLabel = "Open BlueSage Report";
    internal const string FailOpenDiagnostic = "BlueSage Report disabled: exact native Report anchors are incomplete; native Report remains unchanged.";
    internal const string LifecycleFailOpenDiagnostic = "BlueSage Report disabled: OpenStatisticsPanel lifecycle target is unavailable; native Report remains unchanged.";
}

internal sealed class BlueSageReportStatusPresenter
{
    private const string InitialStatus = "Refresh to read the current native statistics.";
    private const string RefreshSuccess = "Refreshed from native statistics. Vanilla data was not changed.";
    private bool _actionResultPending;

    internal string Text { get; private set; } = InitialStatus;

    internal void BeginProjectionRefresh()
    {
        if (!_actionResultPending) Text = "Refreshing Journal, achievements, and native statistics without blocking the report...";
    }

    internal void ShowActionResult(string? result)
    {
        Text = (result ?? string.Empty).Trim();
        _actionResultPending = Text.Length > 0;
    }

    internal void CompleteProjectionRefresh(bool succeeded, string? warning, string? failureReason)
    {
        string refreshResult = succeeded ? RefreshSuccess : (failureReason ?? string.Empty).Trim();
        if (_actionResultPending)
        {
            Text = Join(Text, warning, succeeded ? string.Empty : refreshResult);
            _actionResultPending = false;
            return;
        }

        Text = Join(warning, refreshResult);
    }

    private static string Join(params string?[] parts) => string.Join(" ", parts
        .Where(part => !string.IsNullOrWhiteSpace(part))
        .Select(part => part!.Trim())
        .Distinct(StringComparer.Ordinal));
}

internal static class BlueSageReportPrivacyPolicy
{
    internal static bool IsAllowedJournalMember(string? memberName) =>
        string.Equals(memberName, "Feel", StringComparison.Ordinal) ||
        string.Equals(memberName, "Time", StringComparison.Ordinal);
}

internal readonly struct BlueSageReportAnchors
{
    internal BlueSageReportAnchors(bool statisticManager, bool reportRoot, bool reportTab,
        bool lifecycle, bool button, bool writableLabel)
    {
        StatisticManager = statisticManager;
        ReportRoot = reportRoot;
        ReportTab = reportTab;
        Lifecycle = lifecycle;
        Button = button;
        WritableLabel = writableLabel;
    }

    internal bool StatisticManager { get; }
    internal bool ReportRoot { get; }
    internal bool ReportTab { get; }
    internal bool Lifecycle { get; }
    internal bool Button { get; }
    internal bool WritableLabel { get; }
    internal bool Complete => StatisticManager && ReportRoot && ReportTab && Lifecycle && Button && WritableLabel;
}

internal sealed class BlueSageReportAnchorGate
{
    private bool _logged;
    internal bool NativeReportEnabled => true;
    internal bool BlueSagePanelOwnedSeparately { get; private set; }

    internal bool TryEnable(BlueSageReportAnchors anchors, Action logOnce)
    {
        BlueSagePanelOwnedSeparately = anchors.Complete;
        if (!anchors.Complete && !_logged)
        {
            _logged = true;
            logOnce?.Invoke();
        }
        return anchors.Complete;
    }
}

internal sealed class BlueSageReportTabController
{
    private readonly Action<bool> _native;
    private readonly Action<bool> _qol;
    private readonly Action _open;
    private readonly Action _close;
    private readonly HashSet<int> _attachments = new();
    private readonly Func<bool> _alive;
    internal BlueSageReportTabController(Action<bool> native, Action<bool> qol, Action open, Action close)
        : this(native, qol, open, close, () => true) { }
    internal BlueSageReportTabController(Action<bool> native, Action<bool> qol, Action open, Action close,
        Func<bool> alive)
    { _native = native; _qol = qol; _open = open; _close = close; _alive = alive; }
    internal bool IsAlive => _alive();
    internal bool TryClaimAttachment(int id) => _attachments.Add(id);
    internal void SelectBlueSage() { _native(false); _qol(true); _open(); }
    internal void SelectNative() { _qol(false); _native(true); _close(); }
    internal void Close() => SelectNative();
    internal void Reopen() => SelectNative();
    internal bool TryRestoreNative()
    {
        try { if (!IsAlive) return false; SelectNative(); return true; }
        catch { return false; }
    }
}

internal sealed class BlueSageReportControllerRegistry
{
    private readonly Dictionary<int, BlueSageReportTabController> _items = new();
    internal int Count => _items.Count;
    internal void Register(int key, BlueSageReportTabController controller) { Prune(); _items[key] = controller; }
    internal bool TryGet(int key, out BlueSageReportTabController? controller)
    { Prune(); return _items.TryGetValue(key, out controller); }
    internal void Prune()
    {
        foreach (int key in _items.Where(item => !SafeAlive(item.Value)).Select(item => item.Key).ToArray())
            _items.Remove(key);
    }
    internal int RestoreAll()
    {
        Prune();
        int restored = 0;
        foreach (BlueSageReportTabController controller in _items.Values.ToArray())
            if (controller.TryRestoreNative()) restored++;
        Prune();
        return restored;
    }
    private static bool SafeAlive(BlueSageReportTabController controller)
    { try { return controller.IsAlive; } catch { return false; } }
}
