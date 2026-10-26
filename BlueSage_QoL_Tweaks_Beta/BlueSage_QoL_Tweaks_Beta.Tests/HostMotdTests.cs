using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Blue (2026-10-17): hosts can set a lobby MOTD that QoL players see on join as a toast and chat line;
// a host who never sets one shows "Welcome to my lobby!" (voice pass 2026-10-23: lobby, not server). Only the lobby owner can write lobby data,
// and the payload names the owner so a previous host's message is never shown.
public sealed class HostMotdTests
{
    private const string Owner = "76561198000000100";

    [Test]
    public void Unset_motd_falls_back_to_the_friendly_default()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LobbyMotdPolicy.Effective(""), Is.EqualTo("Welcome to my lobby!"));
            Assert.That(LobbyMotdPolicy.Effective("   "), Is.EqualTo("Welcome to my lobby!"));
            Assert.That(LobbyMotdPolicy.DefaultMessage, Is.EqualTo("Welcome to my lobby!"));
        });
    }

    [Test]
    public void Host_motd_round_trips_for_the_current_owner()
    {
        string value = LobbyMotdPolicy.Build(Owner, "Cozy study lobby - restarts at 9pm");

        Assert.Multiple(() =>
        {
            Assert.That(LobbyMotdPolicy.TryParse(value, Owner, out string text), Is.True);
            Assert.That(text, Is.EqualTo("Cozy study lobby - restarts at 9pm"));
        });
    }

    [Test]
    public void A_previous_hosts_motd_is_never_shown()
    {
        string value = LobbyMotdPolicy.Build("76561198000000999", "Old host message");

        Assert.That(LobbyMotdPolicy.TryParse(value, Owner, out _), Is.False);
    }

    [Test]
    public void Motd_is_bounded_and_markup_free()
    {
        string value = LobbyMotdPolicy.Build(Owner, "<b>Hi</b> | there " + new string('x', 400));

        Assert.Multiple(() =>
        {
            Assert.That(LobbyMotdPolicy.TryParse(value, Owner, out string text), Is.True);
            Assert.That(text, Does.StartWith("bHi/b there"));
            Assert.That(text.Length, Is.LessThanOrEqualTo(LobbyAnnouncementPolicy.MaxTextLength));
        });
    }

    [TestCase("")]
    [TestCase("motd0|76561198000000100|hi")]
    [TestCase("motd1|76561198000000100|")]
    [TestCase("motd1|76561198000000100|<b>hi</b>")]
    public void Malformed_motd_is_ignored(string value)
    {
        Assert.That(LobbyMotdPolicy.TryParse(value, Owner, out _), Is.False);
    }

    // Review 2026-10-17: right-to-left override characters could visually reorder a host's text, and
    // cutting at 160 could split an emoji so the receiver silently rejected the whole MOTD.
    [Test]
    public void Sanitize_drops_text_direction_overrides()
    {
        Assert.That(LobbyAnnouncementPolicy.Sanitize("Hi‮there‏!"), Is.EqualTo("Hithere!"));
    }

    [Test]
    public void Sanitize_never_splits_an_emoji_at_the_length_limit()
    {
        string text = new string('a', LobbyAnnouncementPolicy.MaxTextLength - 1) + "🐟" + "tail";

        string clean = LobbyAnnouncementPolicy.Sanitize(text);

        Assert.Multiple(() =>
        {
            Assert.That(char.IsHighSurrogate(clean[clean.Length - 1]), Is.False);
            Assert.That(LobbyAnnouncementPolicy.Sanitize(clean), Is.EqualTo(clean), "Stable, so receivers accept it.");
        });
    }

    [Test]
    public void The_host_does_not_get_their_own_motd()
    {
        Assert.That(ReadSource("LobbyAnnouncementController.cs"), Does.Contain("if (string.Equals(owner, localSteamId, StringComparison.Ordinal))"));
    }

    [Test]
    public void Motd_toast_says_who_it_is_from()
    {
        Assert.That(LobbyMotdPolicy.FormatToast("Welcome to my lobby!"), Is.EqualTo("Message from the host: Welcome to my lobby!"));
    }

    [Test]
    public void Motd_shows_once_per_lobby_visit()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LobbyMotdPolicy.ShouldShow(currentVisitKey: "steam:1|scene:5", lastShownVisitKey: ""), Is.True);
            Assert.That(LobbyMotdPolicy.ShouldShow("steam:1|scene:5", "steam:1|scene:5"), Is.False);
            Assert.That(LobbyMotdPolicy.ShouldShow("steam:1|scene:6", "steam:1|scene:5"), Is.True, "Rejoin is a new visit.");
            Assert.That(LobbyMotdPolicy.ShouldShow("", ""), Is.False, "Not in a lobby.");
        });
    }

    [Test]
    public void Host_publishes_motd_with_its_capability_and_clients_show_it_per_visit()
    {
        string moderation = ReadSource("SteamIdModerationController.cs");
        string controller = ReadSource("LobbyAnnouncementController.cs");
        string plugin = ReadSource("Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(moderation, Does.Contain("internal const string MotdKey = \"bluesage_motd\";"));
            Assert.That(moderation, Does.Contain("LobbyMotdPolicy.Build(ownerSteamId, Plugin.HostMotd?.Value)"));
            Assert.That(moderation, Does.Contain("SetLobbyData(lobby, MotdKey, string.Empty)"));
            Assert.That(controller, Does.Contain("LobbyMotdPolicy.ShouldShow("));
            Assert.That(controller, Does.Contain("LobbyMotdPolicy.TryParse("));
            Assert.That(controller, Does.Contain("Plugin.TryGetCurrentLobbyVisitKey(out string visitKey)"));
            Assert.That(plugin, Does.Contain("\"motd\","));
            Assert.That(plugin, Does.Contain("HostMotd = Config.Bind(\"Host\", \"HostMotd\", string.Empty"));
        });
    }

    // Round 3 (Blue, UAT 2026-10-18): the MOTD is a host function, so it sits in Host Tools (host only),
    // not in the Customize tab every player sees.
    [Test]
    public void Motd_card_lives_in_host_tools_only()
    {
        string menu = ReadSource("QolMenuWindow.cs");
        int customize = menu.IndexOf("private void DrawCustomizeTab()", System.StringComparison.Ordinal);
        string customizeBody = customize < 0 ? string.Empty : menu.Substring(customize, menu.IndexOf("\n    }\n", customize, System.StringComparison.Ordinal) - customize);

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("            DrawHostReadinessCard();\n            GUILayout.Space(10f);\n            DrawHostMotdCard();"));
            Assert.That(menu, Does.Contain("private void DrawHostMotdCard()"));
            Assert.That(menu, Does.Not.Contain("DrawSettingsSection(\"Lobby welcome message (MOTD)\""));
            Assert.That(customizeBody, Does.Not.Contain("Motd"));
        });
    }

    [Test]
    public void Long_MOTDs_are_fully_visible_in_a_wrapping_box_with_a_live_count()
    {
        string menu = ReadSource("QolMenuWindow.cs").Replace("\r\n", "\n");
        int start = menu.IndexOf("private void DrawMotdSettings()", System.StringComparison.Ordinal);
        string motd = start < 0 ? string.Empty : menu.Substring(start, menu.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        int general = menu.IndexOf("private void DrawGeneralSettings()", System.StringComparison.Ordinal);
        string generalBody = menu.Substring(general, menu.IndexOf("\n    }\n", general, System.StringComparison.Ordinal) - general);

        Assert.Multiple(() =>
        {
            Assert.That(motd, Does.Contain("GUILayout.TextArea(previous, LobbyAnnouncementPolicy.MaxTextLength, _textFieldStyle"));
            Assert.That(motd, Does.Contain("GUILayout.MinHeight(64f)"));
            Assert.That(motd, Does.Contain("+ \"/\" + LobbyAnnouncementPolicy.MaxTextLength"));
            Assert.That(motd, Does.Contain("SaveHostMotd();"));
            Assert.That(generalBody, Does.Not.Contain("MOTD"));
        });
    }

    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName)).Replace("\r\n", "\n");
    }
}
