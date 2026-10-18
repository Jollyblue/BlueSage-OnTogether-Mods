using System.IO;
using System.Text;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Ultra review of PR #6 (2026-10-17): the visit log leaked the lobby join code, the welcome key read
// visible lobby counts on every poll, the announcement sanitizer could end on a space, the chat
// default lived in two disagreeing constants, three retired menu methods stayed behind, and the
// runtime-load probes still defaulted to the Default profile the installers now refuse.
public sealed partial class UltraReviewRound2FindingsTests
{
    [Test]
    public void Visit_key_redaction_keeps_only_the_last_four_digits_of_a_lobby_id_or_code()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WelcomeLobbyKeyPolicy.Redact("steam:109775241234567890|scene:1001"), Is.EqualTo("steam:[redacted:7890]|scene:1001"));
            Assert.That(WelcomeLobbyKeyPolicy.Redact("code:ABCDEFGH|scene:7"), Is.EqualTo("code:[redacted:EFGH]|scene:7"));
            Assert.That(WelcomeLobbyKeyPolicy.Redact("code:ABCD|scene:7"), Is.EqualTo("code:[redacted]|scene:7"));
            Assert.That(WelcomeLobbyKeyPolicy.Redact("visible|scene:7"), Is.EqualTo("visible|scene:7"));
            Assert.That(WelcomeLobbyKeyPolicy.Redact(""), Is.EqualTo("[none]"));
        });
    }

    [Test]
    public void Redacted_visit_keys_still_tell_rejoins_apart()
    {
        string first = WelcomeLobbyKeyPolicy.Redact(WelcomeLobbyKeyPolicy.Build(109775241234567890UL, 1001, "", false));
        string rejoin = WelcomeLobbyKeyPolicy.Redact(WelcomeLobbyKeyPolicy.Build(109775241234567890UL, 2002, "", false));

        Assert.Multiple(() =>
        {
            Assert.That(rejoin, Is.Not.EqualTo(first));
            Assert.That(first, Does.Not.Contain("109775241234567890"));
        });
    }

    [Test]
    public void The_visit_log_line_writes_only_the_redacted_key()
    {
        string plugin = ReadSource("Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("Log?.LogInfo(\"[BlueSageVisit] new lobby visit; key=\" + WelcomeLobbyKeyPolicy.Redact(lobbyKey));"));
            Assert.That(plugin, Does.Not.Contain("new lobby visit; key=\" + lobbyKey)"));
        });
    }

    [Test]
    public void Visible_lobby_counts_are_read_only_when_there_is_no_Steam_id_or_code()
    {
        int reads = 0;
        bool Visible() { reads++; return true; }

        WelcomeLobbyKeyPolicy.Build(109775241234567890UL, 1001, "", Visible);
        WelcomeLobbyKeyPolicy.Build(0UL, 1001, "ABCD", Visible);
        Assert.That(reads, Is.Zero, "A Steam id or join code already identifies the lobby.");

        Assert.That(WelcomeLobbyKeyPolicy.Build(0UL, 1001, "", Visible), Is.EqualTo("visible|scene:1001"));
        Assert.That(reads, Is.EqualTo(1));
    }

    [Test]
    public void Welcome_detection_passes_the_visible_lobby_check_lazily()
    {
        string plugin = ReadSource("Plugin.cs");
        string detect = Slice(plugin, "private static bool TryGetCurrentLobbyKeyForWelcomeMessage(out string lobbyKey)", "private bool ShowWelcomeMessageForLobby()");

        Assert.Multiple(() =>
        {
            Assert.That(detect, Does.Contain("WelcomeLobbyKeyPolicy.Build(steamLobbyId, scene.GetInstanceID(), lobbyCode, HasVisibleLobbyForWelcome)"));
            Assert.That(detect, Does.Not.Contain("bool hasVisibleLobby ="));
        });
    }

    [Test]
    public void Sanitize_is_idempotent_and_never_ends_on_a_space_or_half_an_emoji_at_the_length_cap()
    {
        const string emoji = "\U0001F600";
        for (int prefix = LobbyAnnouncementPolicy.MaxTextLength - 6; prefix <= LobbyAnnouncementPolicy.MaxTextLength + 2; prefix++)
        {
            string raw = new string('a', prefix) + " " + emoji + " tail text";
            string once = LobbyAnnouncementPolicy.Sanitize(raw);
            string twice = LobbyAnnouncementPolicy.Sanitize(once);

            Assert.Multiple(() =>
            {
                Assert.That(twice, Is.EqualTo(once), "prefix=" + prefix);
                Assert.That(once.Length, Is.LessThanOrEqualTo(LobbyAnnouncementPolicy.MaxTextLength), "prefix=" + prefix);
                Assert.That(once, Does.Not.EndWith(" "), "prefix=" + prefix);
                Assert.That(char.IsHighSurrogate(once[once.Length - 1]), Is.False, "prefix=" + prefix);
            });
        }
    }

    [Test]
    public void The_reviewers_space_then_emoji_at_159_case_round_trips_through_the_MOTD_wire()
    {
        string raw = new string('a', 158) + " \U0001F600 more";
        string sent = LobbyMotdPolicy.Effective(raw);
        string wire = LobbyMotdPolicy.Build("76561198000000001", raw);

        Assert.That(LobbyMotdPolicy.TryParse(wire, "76561198000000001", out string text), Is.True);
        Assert.That(text, Is.EqualTo(sent));
    }

    [Test]
    public void Chat_default_has_one_source_of_truth_and_a_separate_wrap_warning_threshold()
    {
        string menu = ReadSource("QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(InputFieldLimitPolicy.DefaultMaxChatCharacters, Is.EqualTo(4000));
            Assert.That(PublicFirstRunDefaults.Current.ChatTextLimit, Is.EqualTo(InputFieldLimitPolicy.DefaultMaxChatCharacters));
            Assert.That(InputFieldLimitPolicy.ChatWrapWarningCharacters, Is.EqualTo(3000));
            Assert.That(ReadSource(Path.Combine("Core", "PublicFirstRunDefaults.cs")), Does.Contain("internal int ChatTextLimit => InputFieldLimitPolicy.DefaultMaxChatCharacters;"));
            Assert.That(menu, Does.Not.Contain("Default is 3000"));
            Assert.That(menu, Does.Contain("clamped > InputFieldLimitPolicy.ChatWrapWarningCharacters"));
        });
    }

    [Test]
    public void Retired_render_saver_menu_methods_are_gone()
    {
        string menu = ReadSource("QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Not.Contain("private void DrawExperimentalToggles()"));
            Assert.That(menu, Does.Not.Contain("private void DrawPlayerRenderSaverTuning()"));
            Assert.That(menu, Does.Not.Contain("private void DrawExperimentalToggle("));
        });
    }

    [Test]
    public void Fresh_installs_keep_Community_Safety_on_while_older_configs_still_migrate()
    {
        string plugin = ReadSource("Plugin.cs");
        string migration = Slice(plugin, "private void ApplyOneTimeConfigMigration()", "\n    }\n");

        Assert.Multiple(() =>
        {
            // Blue, 2026-10-17: the README promise wins; the July opt-in flip is for old configs only.
            Assert.That(migration, Does.Contain("alreadyMigrated: freshInstall || communityBanAnyClientOptInMigrated"));
            Assert.That(PublicFirstRunDefaults.Current.CommunitySafetyList, Is.True);
            Assert.That(ConfigMigrationPolicy.MigrateOldDefaultBool(true, oldDefault: true, newDefault: false, alreadyMigrated: true), Is.True);
            Assert.That(ConfigMigrationPolicy.MigrateOldDefaultBool(true, oldDefault: true, newDefault: false, alreadyMigrated: false), Is.False, "Older configs unchanged.");
        });
    }

    [Test]
    public void Render_saver_retirement_is_not_a_compile_time_constant_so_the_seal_build_has_no_dead_code()
    {
        // Seal 2026-10-17: 'const bool Retired = true' made the code after each early return
        // unreachable (CS0162), and the provenance build's -warnaserror refused it.
        var field = typeof(PlayerRenderSaverPolicy).GetField(nameof(PlayerRenderSaverPolicy.Retired));

        Assert.Multiple(() =>
        {
            Assert.That(field, Is.Not.Null);
            Assert.That(field!.IsLiteral, Is.False, "Must be static readonly, not const.");
            Assert.That(field.IsInitOnly && field.IsStatic, Is.True);
            Assert.That(PlayerRenderSaverPolicy.Retired, Is.True, "Still retired.");
        });
    }

    private static string ProjectRoot()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return root;
    }

    private static string SuiteRoot() => Directory.GetParent(ProjectRoot())!.FullName;

    private static string ReadSource(string relative) =>
        File.ReadAllText(Path.Combine(ProjectRoot(), "src", relative), Encoding.UTF8).Replace("\r\n", "\n");

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
