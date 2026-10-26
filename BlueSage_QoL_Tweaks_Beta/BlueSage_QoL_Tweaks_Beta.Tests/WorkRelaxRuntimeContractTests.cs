using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxRuntimeContractTests
{
    [Test]
    public void Optional_observer_targets_only_the_exact_native_statistics_surface()
    {
        string source = ReadSource("src", "Patches", "WorkRelaxStatisticsPatch.cs") +
                        ReadSource("src", "WorkRelaxRuntimeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("assembly.GetType(\"StatisticManager\", false)"));
            Assert.That(source, Does.Contain("UpdatePomodoroTime"));
            Assert.That(source, Does.Contain("UpdateFocusStatistics"));
            Assert.That(source, Does.Contain("UpdateFocusTypeCountStatistics"));
            Assert.That(source, Does.Contain("UpdateStatisticsDurations"));
            Assert.That(source, Does.Contain("assembly.GetType(\"TodoController\", false)"));
            Assert.That(source, Does.Contain("ToggleTodo"));
            Assert.That(source, Does.Contain("assembly.GetType(\"DataManager\", false)"));
            Assert.That(source, Does.Contain("SavePlayerStatistics"));
            Assert.That(source, Does.Contain("TargetMethods()"));
        });
    }

    [Test]
    public void Runtime_uses_private_config_ledger_and_idempotent_lifecycle_flush()
    {
        string runtime = ReadSource("src", "WorkRelaxRuntimeController.cs");
        string plugin = ReadSource("src", "Plugin.cs");
        string shutdown = ReadSource("src", "PluginShutdownController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(runtime, Does.Contain("WorkRelaxLedgerStore.ForLedgerDirectory(ModSuitePaths.Current.WorkRelaxDirectory, profileKey, ModSuitePaths.Current.WorkRelaxFilePrefix)"), "Round 5d: BlueSageQoLModSuite/trackers/work-relax-p_<id>.json, or the old place while the move is unfinished.");
            Assert.That(runtime, Does.Contain("WorkRelaxProfileKey.Create"));
            Assert.That(runtime, Does.Contain("SteamUser.GetSteamID()"));
            Assert.That(runtime, Does.Contain("WorkRelaxMode.Work"));
            Assert.That(runtime, Does.Contain("WorkRelaxLedger.Create"));
            Assert.That(runtime, Does.Contain("Flush"));
            Assert.That(runtime, Does.Contain("LogOnce"));
            Assert.That(plugin, Does.Contain("WorkRelaxRuntimeController.TryInitialize"));
            Assert.That(shutdown, Does.Contain("WorkRelaxRuntimeController.Flush"));
        });
    }

    [Test]
    public void Observer_captures_before_and_records_only_positive_after_deltas()
    {
        string source = ReadSource("src", "Patches", "WorkRelaxStatisticsPatch.cs") +
                        ReadSource("src", "WorkRelaxRuntimeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("Prefix"));
            Assert.That(source, Does.Contain("Postfix"));
            Assert.That(source, Does.Contain("NativeStatisticsSnapshot"));
            Assert.That(source, Does.Contain("RecordPositiveDeltas"));
            Assert.That(source, Does.Contain("ReconcileAfterNativeSave"));
        });
    }

    [Test]
    public void Runtime_keeps_one_shared_activity_duration_cursor_separate_from_pomodoro()
    {
        string source = ReadSource("src", "WorkRelaxRuntimeController.cs") +
                        ReadSource("src", "Core", "WorkRelaxIntervalAllocator.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("WorkRelaxDurationCursorController"));
            Assert.That(source, Does.Contain("_pomodoro"));
            Assert.That(source, Does.Contain("_activity"));
            Assert.That(source, Does.Contain("WorkRelaxIntervalAllocator.Allocate"));
        });
    }

    [Test]
    public void Optional_duration_boundaries_use_exact_native_setters_and_fail_open()
    {
        string source = ReadSource("src", "Patches", "WorkRelaxStatisticsPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("assembly.GetType(\"PomodoroController\", false)"));
            Assert.That(source, Does.Contain("set_PomodoroType"));
            Assert.That(source, Does.Contain("set_IsPaused"));
            Assert.That(source, Does.Contain("assembly.GetType(\"PlayerFocusController\", false)"));
            Assert.That(source, Does.Contain("set_IsFocus"));
            Assert.That(source, Does.Contain("LogMissingLifecycleTarget"));
            Assert.That(source, Does.Not.Contain("HarmonyPatch(\"Update\")"));
            Assert.That(
                source.Split(new[] { "WorkRelaxRuntimeController.DiscardActivityIntervals();" }, StringSplitOptions.None).Length - 1,
                Is.EqualTo(1),
                "The shared native activity cursor must be discarded only by exact local IsFocus=false.");
        });
    }

    [Test]
    public void Work_relax_runtime_has_no_native_write_or_polling_surface()
    {
        string source = ReadSource("src", "WorkRelaxRuntimeController.cs") +
                        ReadSource("src", "Patches", "WorkRelaxStatisticsPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("Statistics.txt"));
            Assert.That(source, Does.Not.Contain("BackupStatistics.txt"));
            Assert.That(source, Does.Not.Contain("OldStatistics.txt"));
            Assert.That(source, Does.Not.Contain("OpenStatisticsPanel("));
            Assert.That(source, Does.Not.Contain("void Update()"));
            Assert.That(source, Does.Not.Contain("PurrNet.StatisticsManager"));
            Assert.That(source, Does.Not.Match(@"StatisticsData\s*\.\s*\w+\s*="));
        });
    }

    private static string ReadSource(params string[] pathParts)
    {
        string root = FindProjectRoot();
        string path = root;
        foreach (string part in pathParts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
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
