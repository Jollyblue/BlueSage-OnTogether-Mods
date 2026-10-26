using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5h (Blue, 2026-10-25: "first time toast sounds good"): the one-time note about the extra outfit slots.
public sealed class OutfitSlotsHintTests
{
    [Test]
    public void The_note_shows_once_only_when_it_can_be_seen_and_the_extra_rows_exist()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OutfitSlotsHintPolicy.ShouldShow(alreadyShown: false, toastsEnabled: true, extraRowsReady: 6), Is.True);
            Assert.That(OutfitSlotsHintPolicy.ShouldShow(alreadyShown: true, toastsEnabled: true, extraRowsReady: 6), Is.False, "already shown");
            Assert.That(OutfitSlotsHintPolicy.ShouldShow(alreadyShown: false, toastsEnabled: false, extraRowsReady: 6), Is.False, "Toast Pop-ups off: nothing shown, nothing marked");
            Assert.That(OutfitSlotsHintPolicy.ShouldShow(alreadyShown: false, toastsEnabled: true, extraRowsReady: 0), Is.False, "no extra rows were added, so there is nothing to point at");
        });
    }

    [Test]
    public void The_note_is_short_and_cozy()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OutfitSlotsHintPolicy.Notice, Does.Contain("4 to 9").And.Contain("below the game's three"));
            Assert.That(OutfitSlotsHintPolicy.Notice.Length, Is.LessThan(140));
            foreach (string jargon in new[] { "preset", "dropdown", "payload", "Harmony", "PurrNet" })
            {
                Assert.That(OutfitSlotsHintPolicy.Notice, Does.Not.Contain(jargon).IgnoreCase, jargon);
            }
        });
    }

    [Test]
    public void The_plugin_keeps_a_saved_flag_and_the_note_fires_when_the_avatar_panel_opens_not_at_scene_load()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        string patch = File.ReadAllText(Path.Combine(root, "src", "Patches", "AvatarStyleSlotsPatch.cs")).Replace("\r\n", "\n");
        int extendStart = patch.IndexOf("internal static void ExtendDropdown(CustomizationUIController controller)", System.StringComparison.Ordinal);
        int extendEnd = patch.IndexOf("internal static void NotifyAvatarScreenOpened()", extendStart, System.StringComparison.Ordinal);
        string extend = patch.Substring(extendStart, extendEnd - extendStart);
        int openStart = patch.IndexOf("[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.ButtonOpenCustomizationUI))]", System.StringComparison.Ordinal);
        string open = openStart < 0 ? string.Empty : patch.Substring(openStart, System.Math.Min(900, patch.Length - openStart));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("Config.Bind(\"Avatar Styles\", \"OutfitSlotsHintShown\", false,"));
            Assert.That(plugin, Does.Contain("internal static void ShowOutfitSlotsHintOnce(int extraRowsReady)"));
            Assert.That(plugin, Does.Contain("OutfitSlotsHintPolicy.ShouldShow("));
            Assert.That(plugin, Does.Contain("ShowToast(OutfitSlotsHintPolicy.Notice)"));
            // The game builds the style rows in CustomizationUIController.Start (scene load); a toast there would fade unseen
            // and burn the saved flag. The note waits for ButtonOpenCustomizationUI on the Avatar page.
            Assert.That(extend, Does.Not.Contain("ShowOutfitSlotsHintOnce"), "never at scene load");
            Assert.That(open, Does.Contain("[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.ButtonOpenCustomizationUI))]"));
            Assert.That(open, Does.Contain("CustomizationPageType.Avatar"));
            Assert.That(open, Does.Contain("AvatarStyleSlotsBridge.NotifyAvatarScreenOpened()"));
            Assert.That(patch, Does.Contain("Plugin.ShowOutfitSlotsHintOnce("));
        });
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
