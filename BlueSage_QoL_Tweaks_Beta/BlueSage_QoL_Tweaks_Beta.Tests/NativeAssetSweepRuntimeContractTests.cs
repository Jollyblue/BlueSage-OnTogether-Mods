using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class NativeAssetSweepRuntimeContractTests
{
    [Test]
    public void Plugin_stands_down_only_the_automatic_loop_while_preserving_manual_break_glass_cleanup()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string project = File.ReadAllText(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj"));
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        string typeahead = File.ReadAllText(Path.Combine(root, "src", "CommandTypeaheadController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(project, Does.Not.Contain("<Compile Remove=\"src\\Core\\NativeAssetSweepPolicy.cs\" />"));
            Assert.That(plugin, Does.Contain("DetectNativeAssetSweepOwnership()"));
            Assert.That(plugin, Does.Contain("AccessTools.TypeByName(\"MainSceneManager\")"));
            Assert.That(plugin, Does.Contain("AccessTools.TypeByName(\"SettingsData\")"));
            Assert.That(plugin, Does.Contain("\"_sweepTimer\""));
            Assert.That(plugin, Does.Contain("\"SweepDuration\""));
            Assert.That(plugin, Does.Contain("\"UnloadUnusedAssets\""));
            Assert.That(plugin, Does.Not.Contain("AccessTools.Field(typeof(MainSceneManager), \"_sweepTimer\")"));
            Assert.That(plugin, Does.Not.Contain("AccessTools.Field(typeof(SettingsData), \"SweepDuration\")"));
            Assert.That(plugin, Does.Contain("NativeAssetSweepOwnershipVerified"));
            Assert.That(plugin, Does.Contain("NativeAssetSweepPolicy.AllowBlueSageAutoSweep("));
            Assert.That(plugin, Does.Contain("NativeAssetSweepPolicy.AllowBlueSageManualSweep("));
            Assert.That(plugin, Does.Contain("EnableAutoSweep?.Value == true"));
            Assert.That(plugin, Does.Not.Contain("if (EnableManualSweepHotkey.Value && Input.GetKeyDown"));
            Assert.That(plugin, Does.Contain("if (Input.GetKeyDown(ManualSweepKey.Value))"));
            Assert.That(plugin, Does.Contain("new[] { \"sweep\", \"as\" }"));
            Assert.That(plugin, Does.Not.Contain("Asset Sweep: ran now."));
            Assert.That(plugin, Does.Contain("HandleAssetSweepCommand"));
            Assert.That(plugin, Does.Contain("if (context.Tokens.Count == 0)"));
            Assert.That(plugin, Does.Contain("RunSweep(\"manual command\", requireManualPermission: true)"));
            Assert.That(menu, Does.Contain("Automatic cleanup interval (minutes)"));
            Assert.That(menu, Does.Contain("/sweep now remain independent"));
            Assert.That(menu, Does.Contain("AutomaticCleanup={OnOff(Plugin.EnableAutoSweep)}"));
            Assert.That(menu, Does.Not.Contain("Sweep={OnOff(Plugin.EnableAutoSweep)}"));
            Assert.That(typeahead, Does.Contain("run one manual cleanup now"));
            Assert.That(typeahead, Does.Contain("enable BlueSage Automatic Cleanup"));
            Assert.That(plugin, Does.Contain(
                "QolStatusTextPolicy.BuildPlayerSummary(\n" +
                "            NativeAssetSweepOwnership,\n" +
                "            EnableAutoSweep.Value && AutoSweepAllowed,\n" +
                "            ManualSweepAllowed,\n" +
                "            QolMenuKey.Value.ToString())"));
        });
    }

    [Test]
    public void BlueSage_never_patches_or_replaces_the_games_native_sweep_duration()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string patchPath = Path.Combine(root, "src", "Patches", "NativeSweepIntervalOverridePatch.cs");
        string policyPath = Path.Combine(root, "src", "Core", "NativeSweepIntervalOverridePolicy.cs");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(patchPath), Is.False);
            Assert.That(File.Exists(policyPath), Is.False);
            Assert.That(plugin, Does.Not.Contain("ResolveNativeSweepDurationForRuntime"));
            Assert.That(plugin, Does.Not.Contain("ShouldPatchNativeSweepDuration"));
            Assert.That(plugin, Does.Not.Contain("SweepDuration = 900f"));
            Assert.That(plugin, Does.Not.Contain("SweepDuration = 1800f"));
        });
    }

    [Test]
    public void Cleanup_runtime_offers_optional_chat_and_managed_gc_relief_and_reads_vanilla_state_only()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"))
            .Replace("\r\n", "\n");
        string completion = Slice(
            plugin,
            "private IEnumerator CompleteSweepOperation(int sequence, string reason)",
            "private void FinishSweepOperation(int sequence, string reason)");
        string nativeCoordinationRead = Slice(
            plugin,
            "private static bool ReadNativeSweepCoordinationState(",
            "internal bool RunSweep(string reason, bool requireManualPermission)");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("EnableAutoSweep = Config.Bind(\"Asset Sweep\", \"EnableAutoSweep\", publicDefaults.AutomaticCleanup"));
            Assert.That(plugin, Does.Contain("SweepSettingsPolicy.DefaultIntervalMinutes"));
            Assert.That(plugin, Does.Contain("ReadNativeSweepCoordinationState("));
            Assert.That(plugin, Does.Contain("\"_sweepTimer\""));
            Assert.That(plugin, Does.Contain("\"_sweepUI\""));
            Assert.That(plugin, Does.Contain("NativeAssetSweepPolicy.DecideAutomaticSweep("));
            Assert.That(completion, Does.Contain("TryCreateNativeSweepRoutine(out IEnumerator nativeSweepRoutine)"));
            Assert.That(completion, Does.Contain("nativeSweepRoutine.MoveNext()"));
            Assert.That(plugin, Does.Contain("AccessTools.Method("));
            Assert.That(plugin, Does.Contain("\"SweepRoutine\""));
            Assert.That(plugin, Does.Contain("sweepMethod.Invoke(manager, null) as IEnumerator"));
            Assert.That(plugin, Does.Not.Contain("sweepUi.SetActive("));
            Assert.That(nativeCoordinationRead, Does.Not.Contain("SetValue("));
            Assert.That(plugin, Does.Contain("NotifyOnSweep = Config.Bind(\"Asset Sweep\", \"NotifyOnSweep\", false,"));
            Assert.That(plugin, Does.Contain("ForceManagedGcCollect = Config.Bind(\"Asset Sweep\", \"ForceManagedGcCollect\", false,"));
            Assert.That(completion, Does.Contain("if (ForceManagedGcCollect.Value)"));
            Assert.That(completion, Does.Contain("GC.Collect();"));
            Assert.That(completion, Does.Contain("if (SweepSettingsPolicy.ShouldNotifyOnSweep(NotifyOnSweep.Value, reason))"));
            Assert.That(completion, Does.Contain("AddLocalNotification(SweepCompletedNotice);"));
            // Voice pass NOISE 5: a manual cleanup that fails mid-run answers; the automatic timer stays quiet.
            string runFailure = Slice(completion, "operation = Resources.UnloadUnusedAssets();", "if (operation != null)");
            Assert.That(runFailure, Does.Contain("if (SweepMenuActionPolicy.ShouldAnnounceRunFailure(reason))"));
            Assert.That(runFailure, Does.Contain("AddLocalNotificationWithToast(ManualSweepRunFailedNotice);"));
            Assert.That(runFailure.IndexOf("FinishSweepOperation(sequence, reason);", StringComparison.Ordinal), Is.LessThan(runFailure.IndexOf("ManualSweepRunFailedNotice", StringComparison.Ordinal)), "The gate frees first, so a retry can start.");
            // The menu and /sweep answer with the specific refusal RunSweep gave.
            Assert.That(plugin, Does.Contain("return SweepMenuActionPolicy.BuildCompletionMessage(started, _lastManualSweepRefusalNotice);"));
            Assert.That(plugin, Does.Contain("_lastManualSweepRefusalNotice = ManualSweepPausedNotice;"));
            Assert.That(plugin, Does.Contain("_lastManualSweepRefusalNotice = ManualSweepBusyNotice;"));
            Assert.That(plugin, Does.Contain("_lastManualSweepRefusalNotice = ManualSweepFailedNotice;"));
            Assert.That(plugin, Does.Contain("if (Input.GetKeyDown(ManualSweepKey.Value))"));
            Assert.That(plugin, Does.Contain("RunSweep(\"QoL menu\", requireManualPermission: true)"));
            Assert.That(plugin, Does.Contain("RunSweep(\"manual command\", requireManualPermission: true)"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage QoL project root.");
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Start marker missing: " + startMarker);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "End marker missing: " + endMarker);
        return source.Substring(start, end - start);
    }
}
