using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class NavigationCalloutPolicyTests
{
    [Test]
    public void Primary_welcome_highlights_only_the_two_navigation_keys()
    {
        string message = NavigationCalloutPolicy.BuildPrimaryWelcomeMessage("0.2.0");

        Assert.That(message, Is.EqualTo(
            "BlueSage QoL v0.2.0: <b><color=#0B6FA4>HOME</color></b> opens QoL Menu. " +
            "<b><color=#0B6FA4>INSERT</color></b> opens Style Helper for names, status, Spoons, colors, and templates. " +
            "Change any key in the menu's Hotkeys section or with /rebind. /bsqol opens the friendly guide; the full README is here: " +
            "https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/ . /qol status shows what is on."));
    }

    [Test]
    public void Primary_welcome_reports_the_actual_configured_hotkeys_when_rebound()
    {
        string message = NavigationCalloutPolicy.BuildPrimaryWelcomeMessage("0.2.5", "F7", "F9");

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("<b><color=#0B6FA4>F7</color></b> opens QoL Menu"));
            Assert.That(message, Does.Contain("<b><color=#0B6FA4>F9</color></b> opens Style Helper"));
            Assert.That(message, Does.Not.Contain("HOME"));
            Assert.That(message, Does.Not.Contain("INSERT"));
        });
    }

    [Test]
    public void Command_browser_hint_names_the_discoverable_surface()
    {
        Assert.That(NavigationCalloutPolicy.BuildCommandBrowserHint(), Is.EqualTo(
            "open <b><color=#0B6FA4>SLASH COMMANDS</color></b> by typing /, then press Tab or click a suggestion."));
    }

    [Test]
    public void Menu_hint_uses_the_current_theme_keyword_color()
    {
        Assert.That(NavigationCalloutPolicy.BuildMenuNavigationHint("70D6FF"), Is.EqualTo(
            "<b><color=#70D6FF>HOME</color></b> reopens this menu. " +
            "<b><color=#70D6FF>INSERT</color></b> opens Style Helper. Type / to browse " +
            "<b><color=#70D6FF>SLASH COMMANDS</color></b>."));
    }

    [Test]
    public void Untrusted_version_and_color_text_cannot_inject_markup()
    {
        string welcome = NavigationCalloutPolicy.BuildPrimaryWelcomeMessage("0.2.0</color><size=99>");
        string menu = NavigationCalloutPolicy.BuildMenuNavigationHint("NOPE!!");

        Assert.Multiple(() =>
        {
            Assert.That(welcome, Does.Contain("v0.2.0colorsize99:"));
            Assert.That(welcome, Does.Not.Contain("<size=99>"));
            Assert.That(menu, Does.Contain("<color=#0B6FA4>HOME</color>"));
            Assert.That(menu, Does.Not.Contain("NOPE!!"));
        });
    }
}
