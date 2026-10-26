using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5h (Blue, 2026-10-25): "a lot of folks never knew" the extra outfit slots 4 to 9 exist and start on. Every
// human surface (in-game guide, README, QoL menu tooltip, config description) says it in the same cozy words:
// where they are, that they start on, that styles 1 to 3 never change, and that turning the switch off keeps the outfits.
public sealed class ExtraOutfitSlotsWordingTests
{
    private const string SwitchName = "Extra Outfit Slots 4-9";

    [Test]
    public void The_in_game_guide_has_a_short_cozy_section_about_the_extra_outfits()
    {
        GuideSection section = GuideOverlayPolicy.BuildSections("0.2.7", "Home", "Insert", "End", "F8").Single(item => item.Heading == "Your extra outfits");
        string body = section.Body;
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("4 to 9"));
            Assert.That(body, Does.Contain("1 to 3"));
            Assert.That(body, Does.Contain("already switched on"));
            Assert.That(body, Does.Contain(SwitchName));
            Assert.That(body, Does.Contain("waiting"));
            Assert.That(body.Length, Is.LessThan(520), "one short paragraph, not a manual");
            foreach (string jargon in new[] { "preset", "dropdown", "payload", "slot data", "PurrNet", "Harmony" })
            {
                Assert.That(body, Does.Not.Contain(jargon).IgnoreCase, "cozy player words only: " + jargon);
            }
        });
    }

    [Test]
    public void The_readme_tells_players_where_the_extra_outfits_are_and_that_they_start_on()
    {
        string readme = File.ReadAllText(Path.Combine(FindProjectRoot(), "README.md")).Replace("\r\n", "\n");
        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Contain("**Extra outfit slots (" + SwitchName + ")**"));
            Assert.That(readme, Does.Contain("on from the start"));
            Assert.That(readme, Does.Contain("**Where are my extra outfit slots?**"));
            Assert.That(readme, Does.Not.Contain("Avatar presets 4-9"), "old wording is gone");
        });
    }

    [Test]
    public void The_menu_switch_and_config_text_use_the_same_words()
    {
        string root = FindProjectRoot();
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("DrawToggle(\"" + SwitchName + "\", Plugin.EnableExtendedAvatarStyles, \"Adds six extra outfit slots (4 to 9) below the game's three in the avatar screen's style list."));
            Assert.That(menu, Does.Contain("Styles 1 to 3 stay the game's own and never change"));
            Assert.That(menu, Does.Contain("your extra outfits wait for you"));
            Assert.That(plugin, Does.Contain("\"EnableExtendedAvatarStyles\", true, \"Adds six extra outfit slots (4 to 9) below the game's three in the avatar screen's style list."));
            Assert.That(menu, Does.Not.Contain("reversible avatar preset slots"));
            Assert.That(plugin, Does.Not.Contain("reversible avatar preset slots"));
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
