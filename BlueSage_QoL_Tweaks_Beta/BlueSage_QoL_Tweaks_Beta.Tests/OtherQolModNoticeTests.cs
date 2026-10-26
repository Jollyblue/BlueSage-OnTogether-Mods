using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 WS6 (Blue, 2026-10-19): a calm compatibility heads-up, not a data alarm, when another Helper-like QoL
// mod is loaded. Only once per session, only once the player is in a lobby, never on top of the welcome pop-up.
[TestFixture]
public sealed class OtherQolModNoticeTests
{
    private const string BlueSentence =
        "BlueSage QoL may act oddly next to other big QoL mods like TogetherPlus. If something breaks, try without them first and tell us on Discord.";

    [Test]
    public void Notice_uses_Blues_wording_and_names_what_was_detected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OtherQolModNoticePolicy.TryBuildNotice(new[] { "de.ghostystudios.togetherplus", "com.bluesage.ontogether.playerlimitlift" }, out string message), Is.True);
            Assert.That(message, Does.StartWith(BlueSentence));
            Assert.That(message, Does.EndWith("Detected: TogetherPlus."));
            Assert.That(OtherQolModNoticePolicy.TryBuildNotice(new[] { "com.andrewlin.ontogether.alpha", "DE.GHOSTYSTUDIOS.TOGETHERPLUS" }, out string both), Is.True);
            Assert.That(both, Does.EndWith("Detected: TogetherPlus, Alpha."));
        });
    }

    [Test]
    public void No_notice_without_another_Helper_like_mod()
    {
        Assert.That(OtherQolModNoticePolicy.TryBuildNotice(new[] { "com.bluesage.ontogether.playerlimitlift", "com.andrewlin.ontogether.sweep" }, out _), Is.False);
    }

    // Review R3 (2026-10-19): the full guide pop-up ("Open the full guide") is a second overlay; the notice must not
    // fire under it either. The plugin passes "any BlueSage overlay visible".
    [TestCase(true, false, false, true, TestName = "In_lobby_overlay_closed_first_time_fires")]
    [TestCase(false, false, false, false, TestName = "Not_in_a_lobby_waits")]
    [TestCase(true, true, false, false, TestName = "Welcome_or_guide_popup_open_waits")]
    [TestCase(true, false, true, false, TestName = "Already_shown_this_session_stays_quiet")]
    public void Notice_fires_once_in_a_lobby_after_the_overlays_close(bool inLobby, bool anyOverlayVisible, bool alreadyShown, bool expected)
    {
        Assert.That(OtherQolModNoticePolicy.ShouldFire(inLobby, anyOverlayVisible, alreadyShown), Is.EqualTo(expected));
    }

    // Review R11 (2026-10-19): one place knows other mods' GUIDs. The notice asks PluginCompatibility which
    // Helper-like mods are loaded instead of carrying its own GUID table.
    [Test]
    public void Notice_detection_lives_in_PluginCompatibility()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string policy = File.ReadAllText(Path.Combine(root, "src", "Core", "OtherQolModNoticePolicy.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Not.Contain("ghostystudios"), "The TogetherPlus GUID belongs to PluginCompatibility.");
            Assert.That(policy, Does.Contain("PluginCompatibility.DetectHelperLikeMods("));
            Assert.That(PluginCompatibility.TogetherPlusGuid, Is.EqualTo("de.ghostystudios.togetherplus"));
            Assert.That(PluginCompatibility.DetectHelperLikeMods(new[] { "x", PluginCompatibility.TogetherPlusGuid, PluginCompatibility.AndrewAlphaGuid }), Is.EqualTo(new[] { "TogetherPlus", "Alpha" }));
            Assert.That(PluginCompatibility.DetectHelperLikeMods(new[] { PluginCompatibility.AndrewSweepGuid }), Is.Empty, "Sweep is not Helper-like.");
            Assert.That(PluginCompatibility.DetectHelperLikeMods(null), Is.Empty);
        });
    }

    [Test]
    public void Plugin_fires_the_notice_from_the_welcome_loop_once_per_session()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("OtherQolModNoticePolicy.ShouldFire(TryGetCurrentLobbyKeyForWelcomeMessage(out _), _welcomeOverlayVisible || _guideOverlayVisible, _otherQolModNoticeShown)"), "R3: the guide pop-up counts as an overlay too.");
            Assert.That(plugin, Does.Contain("OtherQolModNoticePolicy.TryBuildNotice(Chainloader.PluginInfos.Keys, out string otherModNotice)"));
            Assert.That(plugin, Does.Contain("_otherQolModNoticeShown = true;"));
        });
    }
}
