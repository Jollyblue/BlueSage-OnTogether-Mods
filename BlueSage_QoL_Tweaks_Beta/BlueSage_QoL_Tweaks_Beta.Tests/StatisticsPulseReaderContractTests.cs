using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatisticsPulseReaderContractTests
{
    [Test]
    public void Reader_uses_exact_global_types_and_has_no_mutation_calls()
    {
        string source = ReadReaderSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("assembly.GetName().Name, \"Assembly-CSharp\""));
            Assert.That(source, Does.Contain("assembly.GetType(\"StatisticsData\", false)"));
            Assert.That(source, Does.Contain("assembly.GetType(\"DataManager\", false)"));
            Assert.That(source, Does.Contain("assembly.GetType(\"AchievementManager\", false)"));
            Assert.That(source, Does.Contain("assembly.GetType(\"Achievement\", false)"));
            Assert.That(source, Does.Not.Contain("assembly.GetType(\"StatisticsManager\", false)"));
            Assert.That(source, Does.Not.Contain("PurrNet.StatisticsManager"));
            Assert.That(source, Does.Not.Contain("SavePlayerStatistics"));
            Assert.That(source, Does.Not.Contain("SetAchievement"));
            Assert.That(source, Does.Not.Contain("SetStat"));
            Assert.That(source, Does.Not.Contain("StoreStats"));
        });
    }

    [Test]
    public void Reader_has_no_automatic_refresh_or_report_open_surface()
    {
        string source = ReadReaderSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("IEnumerator"));
            Assert.That(source, Does.Not.Contain("Update()"));
            Assert.That(source, Does.Not.Contain("StartCoroutine"));
            Assert.That(source, Does.Not.Contain("OpenStatisticsPanel"));
        });
    }

    [Test]
    public void Pulse_capture_does_not_depend_on_achievement_counting()
    {
        string source = ReadReaderSource();
        int captureStart = source.IndexOf("TryCapturePulse", System.StringComparison.Ordinal);
        int achievementStart = source.IndexOf("TryCountAchievements", System.StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(captureStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(achievementStart, Is.GreaterThan(captureStart));
            Assert.That(
                source.Substring(captureStart, achievementStart - captureStart),
                Does.Not.Contain("GetAchievement"));
        });
    }

    [Test]
    public void Reader_performs_explicit_unity_fake_null_checks()
    {
        string source = ReadReaderSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("ReferenceEquals(candidate, null)"));
            Assert.That(source, Does.Contain("candidate == null"));
        });
    }

    [Test]
    public void Native_capture_reads_all_fields_by_exact_name_without_mutation_surfaces()
    {
        string source = ReadReaderSource();
        string[] exactNames =
        {
            "TotalSessionCount", "TodaySessionCount", "WeekFocusCount", "MonthFocusCount",
            "TodayFocusDuration", "WeekFocusDuration", "MonthFocusDuration", "AverageDailyFocus",
            "TotalDayOfFocus", "CurrentStreak", "FocusSessionsPerDay", "SyncedFocus",
            "DailyMaxFocus", "DayOfYear", "CurrentWeek", "CurrentMonth",
            "ArtFocusCount", "ConnectFocusCount", "EatFocusCount", "ExerciseFocusCount",
            "ListenFocusCount", "MeditateFocusCount", "ReadFocusCount", "WriteFocusCount",
            "ChoreFocusCount", "PlayFocusCount", "ArtFocusDuration", "ConnectFocusDuration",
            "EatFocusDuration", "ExerciseFocusDuration", "ListenFocusDuration", "MeditateFocusDuration",
            "ReadFocusDuration", "WriteFocusDuration", "ChoreFocusDuration", "PlayFocusDuration",
            "TotalFocusDuration", "TotalCompleteSessions", "LongestStreak", "ToDoCompleted", "SecondsPlayed"
        };

        Assert.That(source, Does.Contain("TryCaptureNative"));
        Assert.That(NativeStatisticsSnapshot.FieldNames, Is.EqualTo(exactNames));
        foreach (string exactName in NativeStatisticsSnapshot.FieldNames)
        {
            Assert.That(exactNames, Does.Contain(exactName), exactName);
        }

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("foreach (string fieldName in NativeStatisticsSnapshot.FieldNames)"));
            Assert.That(source, Does.Contain("ReadRequiredMember(statisticsData, surface.StatisticsDataType, fieldName)"));
            Assert.That(source, Does.Not.Contain("OpenStatisticsPanel"));
            Assert.That(source, Does.Not.Contain("SavePlayerStatistics"));
            Assert.That(source, Does.Not.Contain("UpdateStatistics"));
            Assert.That(source, Does.Not.Contain("UpdatePomodoro"));
            Assert.That(source, Does.Not.Contain("ResetStatistics"));
        });
    }

    private static string ReadReaderSource()
    {
        string root = FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "src", "StatisticsPulseReader.cs"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
               throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
