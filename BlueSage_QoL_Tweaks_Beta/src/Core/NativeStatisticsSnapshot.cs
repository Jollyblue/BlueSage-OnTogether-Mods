#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BlueSage.QoLTweaks.Core;

internal sealed class NativeStatisticsSnapshot
{
    private const string WeekdayFieldName = "FocusSessionsPerDay";

    private static readonly ReadOnlyCollection<string> NativeFieldNames = Array.AsReadOnly(new[]
    {
        "TotalSessionCount",
        "TodaySessionCount",
        "WeekFocusCount",
        "MonthFocusCount",
        "TodayFocusDuration",
        "WeekFocusDuration",
        "MonthFocusDuration",
        "AverageDailyFocus",
        "TotalDayOfFocus",
        "CurrentStreak",
        WeekdayFieldName,
        "SyncedFocus",
        "DailyMaxFocus",
        "DayOfYear",
        "CurrentWeek",
        "CurrentMonth",
        "ArtFocusCount",
        "ConnectFocusCount",
        "EatFocusCount",
        "ExerciseFocusCount",
        "ListenFocusCount",
        "MeditateFocusCount",
        "ReadFocusCount",
        "WriteFocusCount",
        "ChoreFocusCount",
        "PlayFocusCount",
        "ArtFocusDuration",
        "ConnectFocusDuration",
        "EatFocusDuration",
        "ExerciseFocusDuration",
        "ListenFocusDuration",
        "MeditateFocusDuration",
        "ReadFocusDuration",
        "WriteFocusDuration",
        "ChoreFocusDuration",
        "PlayFocusDuration",
        "TotalFocusDuration",
        "TotalCompleteSessions",
        "LongestStreak",
        "ToDoCompleted",
        "SecondsPlayed"
    });

    private static readonly HashSet<string> CountFieldNames = new(StringComparer.Ordinal)
    {
        "TotalSessionCount",
        "TodaySessionCount",
        "WeekFocusCount",
        "MonthFocusCount",
        "TotalDayOfFocus",
        "CurrentStreak",
        "SyncedFocus",
        "DailyMaxFocus",
        "DayOfYear",
        "CurrentWeek",
        "CurrentMonth",
        "ArtFocusCount",
        "ConnectFocusCount",
        "EatFocusCount",
        "ExerciseFocusCount",
        "ListenFocusCount",
        "MeditateFocusCount",
        "ReadFocusCount",
        "WriteFocusCount",
        "ChoreFocusCount",
        "PlayFocusCount",
        "TotalCompleteSessions",
        "LongestStreak",
        "ToDoCompleted"
    };

    private readonly IReadOnlyDictionary<string, double> _numbers;
    private readonly int[] _focusSessionsPerDay;

    private NativeStatisticsSnapshot(
        IReadOnlyDictionary<string, double> numbers,
        int[] focusSessionsPerDay)
    {
        _numbers = numbers;
        _focusSessionsPerDay = focusSessionsPerDay;
    }

    public static IReadOnlyList<string> FieldNames => NativeFieldNames;

    public int[] FocusSessionsPerDay => (int[])_focusSessionsPerDay.Clone();

    public double GetNumber(string fieldName)
    {
        if (!_numbers.TryGetValue(fieldName, out double value))
        {
            throw new KeyNotFoundException("Native statistics field is unavailable: " + fieldName);
        }

        return value;
    }

    public bool TryGetNumber(string fieldName, out double value)
    {
        if (fieldName == null)
        {
            value = 0d;
            return false;
        }

        return _numbers.TryGetValue(fieldName, out value);
    }

    public static bool TryCreate(
        IReadOnlyDictionary<string, double> numbers,
        IReadOnlyList<int> focusSessionsPerDay,
        out NativeStatisticsSnapshot? snapshot,
        out string reason)
    {
        snapshot = null;
        reason = string.Empty;

        if (numbers == null)
        {
            reason = "Native statistics numbers are unavailable.";
            return false;
        }

        if (focusSessionsPerDay == null || focusSessionsPerDay.Count != 7)
        {
            reason = "FocusSessionsPerDay must contain exactly seven weekday buckets.";
            return false;
        }

        Dictionary<string, double> captured = new(StringComparer.Ordinal);
        foreach (string fieldName in NativeFieldNames)
        {
            if (fieldName == WeekdayFieldName)
            {
                continue;
            }

            if (!numbers.TryGetValue(fieldName, out double value))
            {
                reason = "Native statistics field is unavailable: " + fieldName;
                return false;
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                reason = "Native statistics field is not finite: " + fieldName;
                return false;
            }

            if (CountFieldNames.Contains(fieldName))
            {
                value = Math.Max(0d, value);
            }
            else if (value < 0d)
            {
                reason = "Native statistics duration is negative: " + fieldName;
                return false;
            }

            captured.Add(fieldName, value);
        }

        int[] weekdays = new int[7];
        for (int index = 0; index < weekdays.Length; index++)
        {
            weekdays[index] = Math.Max(0, focusSessionsPerDay[index]);
        }

        snapshot = new NativeStatisticsSnapshot(
            new ReadOnlyDictionary<string, double>(captured),
            weekdays);
        return true;
    }
}
