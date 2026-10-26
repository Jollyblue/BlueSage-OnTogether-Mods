using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 WS3/WS4, audit Must 2 target side (Blue: "allow moderation always", 2026-10-19). A player who rejoined
// through a reconnect mod is connected over PurrNet but missing from Steam's lobby list. TryBan used to call that
// "absent" and saved an offline ban without removing them, so they stayed in the lobby. Liveness now comes from
// the host's own PurrNet connection table, and the reconnect leaves stale duplicate rows behind, so only rows
// that are connected right now count.
[TestFixture]
public sealed class BanTargetPresenceTests
{
    [TestCase(1, 1, 1, true, BanTargetPresence.InSteamLobby, TestName = "Normal_member_is_in_the_Steam_lobby")]
    [TestCase(1, 2, 1, false, BanTargetPresence.InSteamLobby, TestName = "Rejoined_member_with_a_stale_row_uses_the_live_row")]
    [TestCase(0, 1, 1, true, BanTargetPresence.Reconnected, TestName = "Live_but_missing_from_Steam_is_reconnected")]
    [TestCase(0, 2, 1, false, BanTargetPresence.Reconnected, TestName = "Reconnected_with_a_stale_duplicate_row")]
    [TestCase(0, 1, 0, true, BanTargetPresence.Absent, TestName = "Left_player_is_absent")]
    [TestCase(0, 0, 0, false, BanTargetPresence.Absent, TestName = "Never_seen_player_is_absent")]
    [TestCase(0, 2, 2, false, BanTargetPresence.Ambiguous, TestName = "Two_live_rows_claiming_one_id_are_ambiguous")]
    [TestCase(2, 1, 1, true, BanTargetPresence.Ambiguous, TestName = "Two_Steam_members_with_one_id_are_ambiguous")]
    // Stage C, review P2 (2026-10-19): a Steam lobby member with no live row yet (still loading, or just left) used to be
    // Ambiguous, so a host ban did nothing. It is now Loading: saved as an offline-style ban labeled LOADING; the game
    // refuses the ID when the connection completes (UpdateOnNewPeopleJoin) and on any later join. Deliberate pin change.
    [TestCase(1, 1, 0, true, BanTargetPresence.Loading, TestName = "Steam_member_without_a_live_row_is_loading")]
    [TestCase(1, 0, 0, false, BanTargetPresence.Loading, TestName = "Steam_member_with_no_row_at_all_is_loading")]
    public void Ban_targets_are_classified_by_Steam_membership_and_live_connections(int steamLobbyMatches, int panelMatches, int liveRowMatches, bool uniquePlayerId, object expected)
    {
        Assert.That(BanTargetPresencePolicy.Classify(steamLobbyMatches, panelMatches, liveRowMatches, uniquePlayerId).ToString(), Is.EqualTo(expected.ToString()));
    }

    // Stage B rename (review smell): the pre-round-4 Steam-membership rule used when the host's connection table is
    // unavailable lived inline in SteamIdModerationController. It belongs next to Classify, unchanged in meaning.
    [TestCase(true, 1, true, 1, BanTargetPresence.InSteamLobby, TestName = "Legacy_rule_unique_member_is_in_the_Steam_lobby")]
    [TestCase(true, 1, true, 0, BanTargetPresence.Absent, TestName = "Legacy_rule_no_Steam_member_is_absent")]
    [TestCase(false, 0, false, 0, BanTargetPresence.Absent, TestName = "Legacy_rule_never_seen_is_absent")]
    [TestCase(true, 2, true, 1, BanTargetPresence.Ambiguous, TestName = "Legacy_rule_duplicate_panel_rows_are_ambiguous")]
    [TestCase(true, 1, false, 1, BanTargetPresence.Ambiguous, TestName = "Legacy_rule_shared_player_id_is_ambiguous")]
    [TestCase(true, 1, true, 2, BanTargetPresence.Ambiguous, TestName = "Legacy_rule_two_Steam_members_are_ambiguous")]
    [TestCase(false, 0, false, 1, BanTargetPresence.Ambiguous, TestName = "Legacy_rule_Steam_member_without_a_panel_row_is_ambiguous")]
    public void Without_a_connection_table_the_legacy_Steam_membership_rule_applies(bool targetRowFound, int panelMatches, bool uniquePlayerId, int steamLobbyMatches, object expected)
    {
        Assert.That(BanTargetPresencePolicy.ClassifyWithoutLiveness(targetRowFound, panelMatches, uniquePlayerId, steamLobbyMatches).ToString(), Is.EqualTo(expected.ToString()));
    }

    [Test]
    public void Controller_delegates_the_legacy_rule_to_the_policy()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string controller = File.ReadAllText(Path.Combine(root, "src", "SteamIdModerationController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("BanTargetPresencePolicy.ClassifyWithoutLiveness(targetIndex >= 0, panelMatches, uniquePlayerId, lobbyMatches)"));
            Assert.That(controller, Does.Not.Contain("OfflineBanPolicy.IsConfirmedAbsentFromCurrentLobby(lobbyMatches) ? BanTargetPresence.Absent"), "The inline rule is gone.");
        });
    }

    [Test]
    public void TryBan_saves_a_loading_target_as_an_offline_ban_without_a_live_kick()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string source = File.ReadAllText(Path.Combine(root, "src", "SteamIdModerationController.cs")).Replace("\r\n", "\n");
        int start = source.IndexOf("private static bool TryBan(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("bool targetLoading = presence == BanTargetPresence.Loading;"));
            Assert.That(body, Does.Contain("OfflineBanPolicy.CanSaveWithoutLiveRow("), "Loading is not 'absent': it has its own host-only gate.");
            Assert.That(body, Does.Contain("\"LOADING\""));
            Assert.That(body, Does.Contain("\"bluesage-sidban-loading\""));
            Assert.That(body, Does.Contain("in the Steam lobby but haven't connected to the game yet"), "Says what is known, not that they are loading.");
            Assert.That(body.IndexOf("if (!targetPresent)", System.StringComparison.Ordinal), Is.GreaterThan(0)
                .And.LessThan(body.IndexOf("hostController.BanRPC(", System.StringComparison.Ordinal)),
                "Absent and Loading targets return before any live kick; BanRPC runs only for a present target.");
        });
    }

    [Test]
    public void TryBan_removes_reconnected_players_and_labels_the_action()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string source = File.ReadAllText(Path.Combine(root, "src", "SteamIdModerationController.cs")).Replace("\r\n", "\n");
        int start = source.IndexOf("private static bool TryBan(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("BanTargetPresencePolicy.Classify("));
            Assert.That(body, Does.Contain("TryCountLiveTargetRows(panel, targetSteamId, out int liveRowMatches, out int liveIndex)"));
            Assert.That(source, Does.Contain("manager.playerModule.IsPlayerConnected(panel.PlayerIDs[index])"));
            Assert.That(body, Does.Contain("presence == BanTargetPresence.Reconnected"));
            Assert.That(body, Does.Contain("\"RECONNECTED\""));
            Assert.That(body, Does.Contain("hostController.BanRPC(panel.PlayerIDs[targetIndex], true);"));
        });
    }

    // The host reads Helper requests only from Steam lobby members, so a Helper who dropped out of Steam's list
    // (reconnect) is told plainly instead of the request silently vanishing.
    [Test]
    public void Helper_outside_the_Steam_lobby_list_gets_an_honest_message()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string source = File.ReadAllText(Path.Combine(root, "src", "SteamIdModerationController.cs")).Replace("\r\n", "\n");
        int start = source.IndexOf("internal static bool TrySendHelperRequest(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(body.IndexOf("CountSteamLobbyMembers(lobby, localSteamId) == 0", System.StringComparison.Ordinal), Is.GreaterThan(0)
                .And.LessThan(body.IndexOf("SetLobbyMemberData(", System.StringComparison.Ordinal)));
            Assert.That(body, Does.Contain("message = HelperNotInSteamLobbyMessage;"));
            Assert.That(source, Does.Contain("Leave and rejoin the lobby to moderate again."));
        });
    }
}
