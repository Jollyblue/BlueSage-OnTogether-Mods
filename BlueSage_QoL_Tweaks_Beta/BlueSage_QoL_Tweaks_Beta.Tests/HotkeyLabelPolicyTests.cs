using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Ultrareview #8/#9 (2026-10-17): help text hardcoded Home/Insert after rebinding, and a hotkey
// set to None produced "NONE opens QoL Menu". Every player-facing key label goes through one
// helper so an unbound hotkey points at its slash command instead.
public sealed class HotkeyLabelPolicyTests
{
    [TestCase("None")]
    [TestCase("none")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("  ")]
    public void None_or_blank_counts_as_unbound(string keyName)
    {
        Assert.That(HotkeyLabelPolicy.IsUnbound(keyName), Is.True);
        Assert.That(HotkeyLabelPolicy.KeyOrCommand(keyName, "/qolmenu"), Is.EqualTo("/qolmenu"));
    }

    [Test]
    public void A_bound_key_is_offered_alongside_its_command()
    {
        Assert.That(HotkeyLabelPolicy.KeyOrCommand("F7", "/qolmenu"), Is.EqualTo("F7 or /qolmenu"));
    }

    [Test]
    public void Welcome_message_points_unbound_keys_at_their_commands()
    {
        string message = NavigationCalloutPolicy.BuildPrimaryWelcomeMessage("0.2.5", "None", "None");

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Not.Contain("NONE"));
            Assert.That(message, Does.Contain("/qolmenu").And.Contain("opens QoL Menu"));
            Assert.That(message, Does.Contain("/styleui").And.Contain("opens Style Helper"));
        });
    }

    [Test]
    public void Menu_navigation_hint_points_unbound_keys_at_their_commands()
    {
        string hint = NavigationCalloutPolicy.BuildMenuNavigationHint("70D6FF", "None", "F9");

        Assert.Multiple(() =>
        {
            Assert.That(hint, Does.Not.Contain("NONE"));
            Assert.That(hint, Does.Contain("/qolmenu").And.Contain("reopens this menu"));
            Assert.That(hint, Does.Contain("F9"));
        });
    }

    [Test]
    public void Welcome_overlay_body_never_says_None_for_an_unbound_hotkey()
    {
        string body = WelcomeOverlayPolicy.BuildBody("0.2.5", "None", "None", "None", "None");

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Not.Contain("None"));
            Assert.That(body, Does.Contain("/qolmenu"));
            Assert.That(body, Does.Contain("/styleui"));
            Assert.That(body, Does.Contain("/rebind fly"));
            Assert.That(body, Does.Contain("/sweep now"));
        });
    }

    [Test]
    public void Help_basics_line_uses_the_real_keys_and_mentions_rebind_and_welcome()
    {
        string bound = NavigationCalloutPolicy.BuildHelpBasicsLine("F7", "F9");
        string unbound = NavigationCalloutPolicy.BuildHelpBasicsLine("None", "Insert");

        Assert.Multiple(() =>
        {
            Assert.That(bound, Does.Contain("F7 or /qolmenu").And.Contain("F9 or /styleui"));
            Assert.That(bound, Does.Not.Contain("Home"));
            Assert.That(bound, Does.Contain("/rebind").And.Contain("/welcome"));
            Assert.That(unbound, Does.Contain("/qolmenu").And.Not.Contain("None"));
            Assert.That(unbound, Does.Contain("Insert or /styleui"));
        });
    }

    [Test]
    public void Status_summary_says_only_the_command_when_the_menu_key_is_unbound()
    {
        string result = QolStatusTextPolicy.BuildPlayerSummary(
            NativeAssetSweepOwnershipState.NativeGame,
            blueSageAutomaticLoopReady: true,
            blueSageManualControlsReady: true,
            "None");

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("/qolmenu"));
            Assert.That(result, Does.Not.Contain("None or"));
        });
    }

    [Test]
    public void Qol_help_command_uses_the_shared_help_line_builder()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("NavigationCalloutPolicy.BuildHelpBasicsLine(QolMenuKey.Value.ToString(), StyleMenuKey.Value.ToString())"));
            Assert.That(plugin, Does.Not.Contain("\"BlueSage basics: Home or /qolmenu, Insert or /styleui"));
        });
    }
}
