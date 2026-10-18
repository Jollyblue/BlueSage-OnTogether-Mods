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
            Assert.That(plugin, Does.Contain("Desync owns periodic lobby/persona repair; BlueSage Host Health is read-only"));
            Assert.That(plugin, Does.Contain("Echo can intentionally copy names/outfits, so Clone Shield remains clues-not-proof"));
            Assert.That(menu, Does.Contain("On for fresh installs"));
            Assert.That(menu, Does.Contain("preserves every existing row"));
            Assert.That(File.ReadAllText(Path.Combine(root, "src", "Patches", "ModerationRelayPatch.cs")), Does.Contain("HarmonyPriority(Priority.First)"));
            Assert.That(File.ReadAllText(Path.Combine(root, "src", "Patches", "ChatHistoryLimitPatch.cs")), Does.Contain("HarmonyPriority(Priority.Last)"));
        });
    }
}
