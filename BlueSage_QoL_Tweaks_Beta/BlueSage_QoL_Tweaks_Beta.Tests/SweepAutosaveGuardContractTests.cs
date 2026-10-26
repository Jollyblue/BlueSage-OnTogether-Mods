using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage G (Marbles, 2026-10-20): the sweep loop consults the game's own save timer before starting, and the
// one-time 10/15-minute values that 0.2.5's migrations wrote move to the 40-minute overlap-avoiding default once.
public sealed class SweepAutosaveGuardContractTests
{
    [Test]
    public void Sweep_loop_reads_the_games_save_timer_and_defers_near_an_autosave()
    {
        string plugin = ReadSource("src", "Plugin.cs");
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("AccessTools.Field(dataManagerType, \"saveTimer\")"));
            Assert.That(plugin, Does.Contain("AutosaveBoundaryPolicy.ShouldDefer(autosaveTimerSeconds)"));
            Assert.That(plugin, Does.Contain("deferred: the game autosaves in"));
        });
    }

    [Test]
    public void Forty_minute_overlap_migration_is_recorded_once()
    {
        string plugin = ReadSource("src", "Plugin.cs");
        Assert.Multiple(() =>
        {
            Assert.That(ConfigMigrationPolicy.SweepFortyMinuteOverlapV4Migration, Is.EqualTo("sweep-40-overlap-v4"));
            Assert.That(ConfigMigrationPolicy.CurrentMigrationLedger, Does.Contain("sweep-40-overlap-v4"));
            Assert.That(plugin, Does.Contain("ConfigMigrationPolicy.SweepFortyMinuteOverlapV4Migration"));
        });
    }

    private static string ReadSource(params string[] pathParts)
    {
        string path = FindProjectRoot();
        foreach (string part in pathParts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
