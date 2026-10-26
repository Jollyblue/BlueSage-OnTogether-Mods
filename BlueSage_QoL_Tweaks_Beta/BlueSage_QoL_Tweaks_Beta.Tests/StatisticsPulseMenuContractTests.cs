using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatisticsPulseMenuContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Main_tab_draws_a_cached_read_only_focus_pulse_after_the_status_strip()
    {
        string source = ReadMenuSource();
        string main = Slice(source, "private void DrawMainTab()", "private void DrawMainStatusStrip()");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("private readonly StatisticsPulseReader _statisticsPulseReader"));
            Assert.That(source, Does.Contain("private StatisticsPulseSnapshot _statisticsPulse"));
            Assert.That(main.IndexOf("DrawFocusPulse();", StringComparison.Ordinal),
                Is.GreaterThan(main.IndexOf("DrawMainStatusStrip();", StringComparison.Ordinal)));
            Assert.That(source, Does.Contain("Focus Pulse"));
            Assert.That(source, Does.Contain("Total focus"));
            Assert.That(source, Does.Contain("Today's sessions"));
            Assert.That(source, Does.Contain("Current streak"));
            Assert.That(source, Does.Contain("StatisticsPulsePresentationPolicy.BuildStreakValue"));
            Assert.That(source, Does.Contain("Task checks recorded"));
            Assert.That(source, Does.Not.Contain("Tasks completed"));
            Assert.That(source, Does.Contain("Achievements unlocked"));
            Assert.That(source, Does.Contain("Full Report - Open with ESC"));
        });
    }

    [Test]
    public void Opening_the_menu_captures_base_statistics_without_enumerating_achievements()
    {
        string source = ReadMenuSource();
        string toggle = Slice(source, "public void ToggleVisible()", "private void OnGUI()");

        Assert.Multiple(() =>
        {
            Assert.That(toggle, Does.Contain("RefreshStatisticsPulse(false);"));
            Assert.That(toggle, Does.Not.Contain("TryCountAchievements"));
            Assert.That(source.Split("RefreshStatisticsPulse(false);", StringSplitOptions.None).Length - 1, Is.EqualTo(1));
        });
    }

    [Test]
    public void Explicit_refresh_is_the_only_achievement_enumeration_path()
    {
        string source = ReadMenuSource();
        string draw = Slice(source, "private void DrawFocusPulse()", "private void RefreshStatisticsPulse(bool includeAchievements)");
        string refresh = Slice(source, "private void RefreshStatisticsPulse(bool includeAchievements)", "private void DrawLobbySafetyAccessBanner()");

        Assert.Multiple(() =>
        {
            Assert.That(draw, Does.Contain("Refresh"));
            Assert.That(draw, Does.Contain("RefreshStatisticsPulse(true);"));
            Assert.That(source.Split("RefreshStatisticsPulse(true);", StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(source.Split("TryCountAchievements", StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(refresh, Does.Contain("if (includeAchievements &&"));
            Assert.That(refresh, Does.Contain("_statisticsPulseReader.TryCapturePulse"));
            Assert.That(refresh, Does.Contain("_statisticsPulseReader.TryCountAchievements"));
            Assert.That(refresh.IndexOf("TryCountAchievements", StringComparison.Ordinal),
                Is.GreaterThan(refresh.IndexOf("if (includeAchievements &&", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void Achievement_failure_copy_has_a_definitely_assigned_fail_open_reason()
    {
        string source = ReadMenuSource();
        string refresh = Slice(source, "private void RefreshStatisticsPulse(bool includeAchievements)", "private void DrawLobbySafetyAccessBanner()");

        Assert.Multiple(() =>
        {
            Assert.That(refresh, Does.Contain("string achievementUnavailableReason = string.Empty;"));
            Assert.That(refresh, Does.Contain("out achievementUnavailableReason"));
            Assert.That(refresh, Does.Not.Contain("out string achievementUnavailableReason"));
        });
    }

    [Test]
    public void Focus_pulse_never_opens_or_mutates_the_native_report_and_has_no_refresh_loop()
    {
        string source = ReadMenuSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("OpenStatisticsPanel"));
            Assert.That(source, Does.Not.Contain("SavePlayerStatistics"));
            Assert.That(source, Does.Not.Contain("SetAchievement"));
            Assert.That(source, Does.Not.Contain("SetStat"));
            Assert.That(source, Does.Not.Contain("StoreStats"));
            Assert.That(source, Does.Not.Contain("private void Update()"));
            Assert.That(source, Does.Not.Contain("IEnumerator"));
            Assert.That(source, Does.Not.Contain("StartCoroutine"));
            Assert.That(source, Does.Not.Contain("Locked achievement hints"));
            Assert.That(source, Does.Not.Contain("Unlock by"));
            Assert.That(source, Does.Not.Contain("current beta").IgnoreCase);
            Assert.That(source, Does.Contain("The full achievement gallery is in the game's Report."));
        });
    }

    private static string ReadMenuSource() => File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            Assert.Fail("Start marker missing: " + startMarker);
        }

        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "End marker missing: " + endMarker);
        return source.Substring(start, end - start);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
