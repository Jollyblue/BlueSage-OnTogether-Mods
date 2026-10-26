using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class StatisticsPulseReader
{
    private const BindingFlags ReadFlags =
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Instance |
        BindingFlags.Static;

    public bool TryCapturePulse(
        out StatisticsPulseSnapshot snapshot,
        out string unavailableReason)
    {
        snapshot = null;
        unavailableReason = string.Empty;

        try
        {
            if (!TryCaptureNative(out NativeStatisticsSnapshot nativeSnapshot, out unavailableReason) ||
                !StatisticsPulseSnapshot.TryCreate(nativeSnapshot, null, null, out snapshot))
            {
                if (string.IsNullOrEmpty(unavailableReason))
                {
                    unavailableReason = "Native statistics contain an invalid focus duration.";
                }

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            snapshot = null;
            unavailableReason = "Native statistics read failed: " + ex.GetType().Name;
            return false;
        }
    }

    public bool TryCaptureNative(
        out NativeStatisticsSnapshot snapshot,
        out string unavailableReason)
    {
        snapshot = null;
        unavailableReason = string.Empty;

        try
        {
            if (!TryResolveSurface(out StatisticsSurface surface, out unavailableReason))
            {
                return false;
            }

            UnityEngine.Object dataManager = NativeManagerCache.FindLiveObject(surface.DataManagerType);
            if (dataManager == null)
            {
                unavailableReason = "Native DataManager is not live.";
                return false;
            }

            object statisticsData = ReadRequiredMember(
                dataManager,
                surface.DataManagerType,
                "StatisticsData");
            if (statisticsData == null || !surface.StatisticsDataType.IsInstanceOfType(statisticsData))
            {
                unavailableReason = "Native StatisticsData is unavailable.";
                return false;
            }

            Dictionary<string, double> numbers = new Dictionary<string, double>(StringComparer.Ordinal);
            int[] focusSessionsPerDay = null;
            foreach (string fieldName in NativeStatisticsSnapshot.FieldNames)
            {
                object value = ReadRequiredMember(statisticsData, surface.StatisticsDataType, fieldName);
                if (string.Equals(fieldName, "FocusSessionsPerDay", StringComparison.Ordinal))
                {
                    focusSessionsPerDay = value as int[];
                    if (focusSessionsPerDay == null)
                    {
                        unavailableReason = "Native FocusSessionsPerDay is unavailable.";
                        return false;
                    }

                    continue;
                }

                numbers.Add(fieldName, Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }

            return NativeStatisticsSnapshot.TryCreate(
                numbers,
                focusSessionsPerDay,
                out snapshot,
                out unavailableReason);
        }
        catch (Exception ex)
        {
            snapshot = null;
            unavailableReason = "Native statistics read failed: " + ex.GetType().Name;
            return false;
        }
    }

    public bool TryCountAchievements(
        out int unlocked,
        out int total,
        out string unavailableReason)
    {
        unlocked = 0;
        total = 0;
        unavailableReason = string.Empty;

        try
        {
            if (!TryResolveSurface(out StatisticsSurface surface, out unavailableReason))
            {
                return false;
            }

            UnityEngine.Object achievementManager = NativeManagerCache.FindLiveObject(surface.AchievementManagerType);
            if (achievementManager == null)
            {
                unavailableReason = "Native AchievementManager is not live.";
                return false;
            }

            MethodInfo getAchievement = surface.AchievementManagerType.GetMethod(
                "GetAchievement",
                ReadFlags,
                null,
                new[] { surface.AchievementType },
                null);
            if (getAchievement == null)
            {
                unavailableReason = "Native achievement reader is unavailable.";
                return false;
            }

            Array achievements = Enum.GetValues(surface.AchievementType);
            total = achievements.Length;
            foreach (object achievement in achievements)
            {
                object result = getAchievement.Invoke(
                    getAchievement.IsStatic ? null : achievementManager,
                    new[] { achievement });
                if (result is bool isUnlocked && isUnlocked)
                {
                    unlocked++;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            unlocked = 0;
            total = 0;
            unavailableReason = "Native achievement read failed: " + ex.GetType().Name;
            return false;
        }
    }

    private static bool TryResolveSurface(
        out StatisticsSurface surface,
        out string unavailableReason)
    {
        surface = null;
        unavailableReason = string.Empty;

        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
        if (assembly == null ||
            !StatisticsTypeResolutionPolicy.IsExactGameSurface(
                assembly.GetName().Name,
                name => assembly.GetType(name, false) != null))
        {
            unavailableReason = "Native statistics are not available on this game build.";
            return false;
        }

        Type statisticsDataType = assembly.GetType("StatisticsData", false);
        Type dataManagerType = assembly.GetType("DataManager", false);
        Type achievementManagerType = assembly.GetType("AchievementManager", false);
        Type achievementType = assembly.GetType("Achievement", false);
        if (statisticsDataType == null ||
            dataManagerType == null ||
            achievementManagerType == null ||
            achievementType == null ||
            !achievementType.IsEnum)
        {
            unavailableReason = "Native statistics surface is incomplete.";
            return false;
        }

        surface = new StatisticsSurface(
            statisticsDataType,
            dataManagerType,
            achievementManagerType,
            achievementType);
        return true;
    }

    private static object ReadRequiredMember(object target, Type ownerType, string name)
    {
        FieldInfo field = ownerType.GetField(name, ReadFlags);
        if (field != null)
        {
            return field.GetValue(field.IsStatic ? null : target);
        }

        PropertyInfo property = ownerType.GetProperty(name, ReadFlags);
        MethodInfo getter = property?.GetGetMethod(true);
        if (getter != null)
        {
            return getter.Invoke(getter.IsStatic ? null : target, null);
        }

        throw new MissingMemberException(ownerType.FullName, name);
    }

    private sealed class StatisticsSurface
    {
        public StatisticsSurface(
            Type statisticsDataType,
            Type dataManagerType,
            Type achievementManagerType,
            Type achievementType)
        {
            StatisticsDataType = statisticsDataType;
            DataManagerType = dataManagerType;
            AchievementManagerType = achievementManagerType;
            AchievementType = achievementType;
        }

        public Type StatisticsDataType { get; }

        public Type DataManagerType { get; }

        public Type AchievementManagerType { get; }

        public Type AchievementType { get; }
    }
}

internal static class NativeManagerCache
{
    private static readonly Dictionary<Type, UnityEngine.Object> LiveObjects =
        new Dictionary<Type, UnityEngine.Object>();
    private static readonly Dictionary<Type, float> LastMissAt =
        new Dictionary<Type, float>();

    internal static UnityEngine.Object FindLiveObject(Type type)
    {
        if (type == null) return null;
        if (LiveObjects.TryGetValue(type, out UnityEngine.Object cached) &&
            !ReferenceEquals(cached, null) && cached != null) return cached;

        // Round 4 WS5: a recent miss (menu, loading) waits instead of scanning every loaded object again.
        float now = Time.unscaledTime;
        float lastMissAt = LastMissAt.TryGetValue(type, out float missedAt) ? missedAt : -1f;
        if (!NativeLookupCooldownPolicy.ShouldScan(lastMissAt, now, NativeLookupCooldownPolicy.MissCooldownSeconds)) return null;

        UnityEngine.Object[] candidates = Resources.FindObjectsOfTypeAll(type);
        if (candidates != null)
        {
            foreach (UnityEngine.Object candidate in candidates)
            {
                if (ReferenceEquals(candidate, null) || candidate == null) continue;
                LiveObjects[type] = candidate;
                LastMissAt.Remove(type);
                return candidate;
            }
        }

        LiveObjects.Remove(type);
        LastMissAt[type] = now;
        return null;
    }

    internal static void Clear()
    {
        LiveObjects.Clear();
        LastMissAt.Clear();
    }
}
