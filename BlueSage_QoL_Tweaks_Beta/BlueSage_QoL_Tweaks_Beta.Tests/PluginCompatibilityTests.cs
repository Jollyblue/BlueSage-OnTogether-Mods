using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PluginCompatibilityTests
{
    [Test]
    public void Evaluate_DisablesAutoSweep_WhenAndrewSweepIsLoadedAndAutoDisableIsEnabled()
    {
        var result = PluginCompatibility.Evaluate(
            new[] { PluginCompatibility.AndrewSweepGuid },
            autoDisableSweepWhenAndrewSweepInstalled: true,
            allowManualSweepWhenAndrewSweepInstalled: false,
            autoDisableChatTweaksWhenSimpleQoLInstalled: true);

        Assert.That(result.AutoSweepAllowed, Is.False);
        Assert.That(result.ManualSweepAllowed, Is.False);
        Assert.That(result.Reasons, Has.Member("AndrewLin Sweep is loaded; BlueSage auto sweep is disabled to avoid double sweeping."));
        Assert.That(result.Reasons, Has.Member("AndrewLin Sweep is loaded; BlueSage manual sweep hotkey is disabled unless explicitly allowed in config."));
    }

    [Test]
    public void Evaluate_AllowsAllSweepActions_WhenAndrewSweepIsLoadedButOverrideIsDisabled()
    {
        var result = PluginCompatibility.Evaluate(
            new[] { PluginCompatibility.AndrewSweepGuid },
            autoDisableSweepWhenAndrewSweepInstalled: false,
            allowManualSweepWhenAndrewSweepInstalled: false,
            autoDisableChatTweaksWhenSimpleQoLInstalled: true);

        Assert.That(result.AutoSweepAllowed, Is.True);
        Assert.That(result.ManualSweepAllowed, Is.True);
    }

    [Test]
    public void Evaluate_AllowsManualSweep_WhenAndrewSweepIsLoadedAndManualOverrideIsEnabled()
    {
        var result = PluginCompatibility.Evaluate(
            new[] { PluginCompatibility.AndrewSweepGuid },
            autoDisableSweepWhenAndrewSweepInstalled: true,
            allowManualSweepWhenAndrewSweepInstalled: true,
            autoDisableChatTweaksWhenSimpleQoLInstalled: true);

        Assert.That(result.AutoSweepAllowed, Is.False);
        Assert.That(result.ManualSweepAllowed, Is.True);
    }

    [Test]
    public void Evaluate_DisablesChatTweaks_WhenSimpleQolIsLoadedAndAutoDisableIsEnabled()
    {
        var result = PluginCompatibility.Evaluate(
            new[] { PluginCompatibility.SimpleQolGuid },
            autoDisableSweepWhenAndrewSweepInstalled: true,
            allowManualSweepWhenAndrewSweepInstalled: false,
            autoDisableChatTweaksWhenSimpleQoLInstalled: true);

        Assert.That(result.ChatTweaksAllowed, Is.False);
        Assert.That(result.Reasons, Has.Member("Tiny_Plume Simple_QOL is loaded; BlueSage timestamps and leave notices are disabled to avoid duplicate chat formatting."));
    }

    [Test]
    public void Evaluate_AllowsEverything_WhenOnlyUnknownPluginsAreLoaded()
    {
        var result = PluginCompatibility.Evaluate(
            new[] { "example.unknown.plugin" },
            autoDisableSweepWhenAndrewSweepInstalled: true,
            allowManualSweepWhenAndrewSweepInstalled: false,
            autoDisableChatTweaksWhenSimpleQoLInstalled: true);

        Assert.That(result.AutoSweepAllowed, Is.True);
        Assert.That(result.ManualSweepAllowed, Is.True);
        Assert.That(result.ChatTweaksAllowed, Is.True);
        Assert.That(result.Reasons, Is.Empty);
    }

    [Test]
    public void Evaluate_IgnoresStaleOrDisabledModFileNames_WhenTheyAreNotLoadedPluginGuids()
    {
        var result = PluginCompatibility.Evaluate(
            new[]
            {
                @"C:\Games\OnTogether\BepInEx\plugins\AndrewLin-Sweep\Sweep.dll.disabled",
                @"C:\Games\OnTogether\BepInEx\plugins\Tiny_Plume-Simple_QOL\Simple_QOL.dll.old",
                "disabled-officerballs-ChatTweaks.dll.bak",
            },
            autoDisableSweepWhenAndrewSweepInstalled: true,
            allowManualSweepWhenAndrewSweepInstalled: false,
            autoDisableChatTweaksWhenSimpleQoLInstalled: true);

        Assert.That(result.AutoSweepAllowed, Is.True);
        Assert.That(result.ManualSweepAllowed, Is.True);
        Assert.That(result.ChatTweaksAllowed, Is.True);
        Assert.That(result.Reasons, Is.Empty);
    }

    [Test]
    public void Evaluate_MapsAndrewDefensiveOwnersAndPausesOnlyOverlappingHostHealthRepairs()
    {
        var result = PluginCompatibility.Evaluate(
            new[]
            {
                PluginCompatibility.AndrewHushGuid,
                PluginCompatibility.AndrewFomoGuid,
                PluginCompatibility.AndrewEchoGuid,
                PluginCompatibility.AndrewAlphaGuid,
                PluginCompatibility.AndrewChalkyGuid,
                PluginCompatibility.AndrewDesyncGuid,
            },
            autoDisableSweepWhenAndrewSweepInstalled: true,
            allowManualSweepWhenAndrewSweepInstalled: false,
            autoDisableChatTweaksWhenSimpleQoLInstalled: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.HushLoaded, Is.True);
            Assert.That(result.FomoLoaded, Is.True);
            Assert.That(result.EchoLoaded, Is.True);
            Assert.That(result.AlphaLoaded, Is.True);
            Assert.That(result.ChalkyLoaded, Is.True);
            Assert.That(result.DesyncLoaded, Is.True);
            Assert.That(result.HostHealthRepairsAllowed, Is.False);
            Assert.That(result.AutoSweepAllowed, Is.True);
            Assert.That(result.ChatTweaksAllowed, Is.True);
        });
    }

    [Test]
    public void DefensiveCompatibilityIsHostHelperVisibleWithAdditiveCommunityListSafety()
    {
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));

        Assert.Multiple(() =>
        {
            // Voice pass (2026-10-23): plain sentences, mod names only (never a modder's name).
            Assert.That(plugin, Does.Contain("Desync is installed: it handles lobby repairs, so BlueSage Host Health just watches."));
            Assert.That(plugin, Does.Contain("Echo is installed: it can copy names and outfits on purpose, so Clone Shield treats lookalikes as clues, not proof."));
            Assert.That(plugin, Does.Contain("Hush is installed: it shares the game's ban list; BlueSage still asks you to confirm each ban."));
            Assert.That(plugin, Does.Contain("\"No other safety mods found that overlap with BlueSage.\""));
            Assert.That(plugin, Does.Not.Contain("no overlapping Andrew defensive"));
            Assert.That(menu, Does.Contain("On for fresh installs"));
            Assert.That(menu, Does.Contain("It never removes anything"));
            Assert.That(File.ReadAllText(Path.Combine(root, "src", "Patches", "ModerationRelayPatch.cs")), Does.Contain("HarmonyPriority(Priority.First)"));
            Assert.That(File.ReadAllText(Path.Combine(root, "src", "Patches", "ChatHistoryLimitPatch.cs")), Does.Contain("HarmonyPriority(Priority.Last)"));
        });
    }
}
