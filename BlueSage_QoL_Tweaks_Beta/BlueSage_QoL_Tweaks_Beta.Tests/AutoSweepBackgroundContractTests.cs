using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class AutoSweepBackgroundContractTests
{
    [Test]
    public void AutoSweepUsesRealtimeTimerWithAnOwnedBackgroundExecutionLease()
    {
        string source = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("yield return new WaitForSecondsRealtime(1f);"));
            Assert.That(source, Does.Contain("_sweepSchedule.ReconcileInterval(nowSeconds, SweepIntervalMinutes.Value);"));
            Assert.That(source, Does.Contain("_sweepSchedule.IsDue(nowSeconds)"));
            Assert.That(source, Does.Contain("EnsureSweepBackgroundExecution();"));
            Assert.That(source, Does.Contain("_sweepSchedule.Start(Time.realtimeSinceStartupAsDouble, SweepIntervalMinutes.Value);"));
            Assert.That(source, Does.Contain("_sweepCoroutine = StartCoroutine(SweepLoop());"));
            Assert.That(source, Does.Contain("Application.runInBackground = true;"));
            Assert.That(source, Does.Not.Contain("System.Threading.Timer"));
            Assert.That(source, Does.Not.Contain("Task.Run(() => RunSweep"));
        });
    }

    [Test]
    public void AutoSweepRestoresThePriorRuntimeStateWhenItsLoopStops()
    {
        string source = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("_runInBackgroundBeforeSweepOverride = Application.runInBackground;"));
            Assert.That(source, Does.Contain("Application.runInBackground = priorState;"));
            Assert.That(source, Does.Contain("ReleaseSweepBackgroundExecution();\n\n        if (_compatibilityRefreshCoroutine != null)"));
            Assert.That(source, Does.Contain("ReleaseSweepBackgroundExecution();\n    }\n\n    private void EnsureSweepBackgroundExecution()"));
        });
    }

    [Test]
    public void ExistingRunInBackgroundOwnershipIsNotClaimedOrRewritten()
    {
        string source = ReadPluginSource();

        Assert.That(source, Does.Contain("if (_ownsSweepRunInBackgroundOverride || Application.runInBackground)"));
        Assert.That(source, Does.Contain("if (!_ownsSweepRunInBackgroundOverride)"));
    }

    [Test]
    public void Saving_an_interval_reconciles_the_deadline_without_restarting_the_coroutine()
    {
        string plugin = ReadPluginSource();
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"))
            .Replace("\r\n", "\n");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("Plugin.Instance?.ReconcileSweepScheduleFromMenu();"));
            Assert.That(menu, Does.Not.Contain("Plugin.Instance?.RunSweepRestartFromMenu();\n        _sweepIntervalText = clamped.ToString();"));
            Assert.That(plugin, Does.Contain("internal void ReconcileSweepScheduleFromMenu()"));
        });
    }

    [Test]
    public void Manual_sweeps_do_not_move_the_automatic_deadline()
    {
        string source = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("RunSweep(\"QoL menu\", requireManualPermission: true);"));
            Assert.That(source, Does.Contain("RunSweep(\"manual hotkey\", requireManualPermission: true);"));
            Assert.That(source, Does.Not.Contain("MarkManual"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage QoL project root.");
    }

    private static string ReadPluginSource()
    {
        return File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"))
            .Replace("\r\n", "\n");
    }
}
