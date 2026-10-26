using System;
using System.Collections.Generic;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage E #3 (Blue, 2026-10-19 audit): Helper/host kick and a lobby-wide mute.
// Kick = ban through the game's own flow, wait until the player's row is gone (max ~20 s), unban. Fable amendment:
// a kick that does not finish fails toward the ban, with a journal row and a host message.
// Mute = the host stops relaying that player's chat to everyone, keyed by the authoritative network sender.
[TestFixture]
public sealed class KickAndMuteTests
{
    [Test]
    public void Kick_follow_up_unbans_when_the_row_is_gone_and_fails_toward_ban_at_twenty_seconds()
    {
        long timeout = KickFollowUpPolicy.TimeoutTicks;
        Assert.Multiple(() =>
        {
            Assert.That(timeout, Is.EqualTo(TimeSpan.FromSeconds(20).Ticks));
            Assert.That(KickFollowUpPolicy.IncompleteMessage, Is.EqualTo("Kick incomplete: the player is still banned. Unban manually if you want."));
            Assert.That(KickFollowUpPolicy.Decide(1, 0, false), Is.EqualTo(KickFollowUp.Wait));
            Assert.That(KickFollowUpPolicy.Decide(1, timeout - 1, false), Is.EqualTo(KickFollowUp.Wait));
            Assert.That(KickFollowUpPolicy.Decide(0, 1, false), Is.EqualTo(KickFollowUp.Unban), "The row is gone: lift the ban.");
            Assert.That(KickFollowUpPolicy.Decide(1, timeout, false), Is.EqualTo(KickFollowUp.FailTowardBan), "Still live at the timeout: the ban stays.");
            Assert.That(KickFollowUpPolicy.Decide(2, timeout + 1, false), Is.EqualTo(KickFollowUp.FailTowardBan));
            Assert.That(KickFollowUpPolicy.Decide(0, 1, true), Is.EqualTo(KickFollowUp.Cancelled), "An explicit ban during the window keeps the ban; the unban never runs.");
        });
    }

    [Test]
    public void Lobby_mute_keys_on_an_exactly_resolved_sender_and_fails_open_otherwise()
    {
        var muted = new HashSet<string>(StringComparer.Ordinal) { "76561198000000002" };
        Assert.Multiple(() =>
        {
            Assert.That(LobbyMutePolicy.TryResolveSender(1), Is.True);
            Assert.That(LobbyMutePolicy.TryResolveSender(0), Is.False, "No row claims the network id: let the chat through.");
            Assert.That(LobbyMutePolicy.TryResolveSender(2), Is.False, "Two rows claim it: the mapping is unsafe, let it through.");
            Assert.That(LobbyMutePolicy.ShouldDrop("76561198000000002", muted), Is.True);
            Assert.That(LobbyMutePolicy.ShouldDrop("76561198000000003", muted), Is.False);
            Assert.That(LobbyMutePolicy.ShouldDrop("", muted), Is.False);
            Assert.That(LobbyMutePolicy.ShouldDrop("76561198000000002", null), Is.False);
            Assert.That(LobbyMutePolicy.IsMuteAction("mute") && LobbyMutePolicy.IsMuteAction("unmute") && !LobbyMutePolicy.IsMuteAction("ban"), Is.True);
        });
    }

    [Test]
    public void Kick_and_mute_are_wired_through_the_host_check_the_relay_and_the_journal()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string controller = File.ReadAllText(Path.Combine(root, "src", "SteamIdModerationController.cs")).Replace("\r\n", "\n");
        string relay = File.ReadAllText(Path.Combine(root, "src", "Patches", "ModerationRelayPatch.cs")).Replace("\r\n", "\n");
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs")).Replace("\r\n", "\n");
        string matrix = File.ReadAllText(Path.Combine(root, "docs", "COMMAND_FEATURE_REGRESSION_MATRIX.md"));
        int kickStart = controller.IndexOf("private static bool TryKick(", StringComparison.Ordinal);
        string kick = kickStart < 0 ? string.Empty : controller.Substring(kickStart, controller.IndexOf("\n    }\n", kickStart, StringComparison.Ordinal) - kickStart);
        int tickStart = controller.IndexOf("internal static void TickPendingKicks()", StringComparison.Ordinal);
        string tick = tickStart < 0 ? string.Empty : controller.Substring(tickStart, controller.IndexOf("\n    }\n", tickStart, StringComparison.Ordinal) - tickStart);
        int executeStart = controller.IndexOf("internal static bool TryExecuteHostAction(", StringComparison.Ordinal);
        string execute = controller.Substring(executeStart, controller.IndexOf("\n    }\n", executeStart, StringComparison.Ordinal) - executeStart);
        Assert.Multiple(() =>
        {
            // Kick: live targets only, never an already-banned player (a kick would lift that ban), ban first, then the follow-up.
            Assert.That(kickStart, Is.GreaterThan(0));
            Assert.That(kick, Does.Contain("is already banned; a kick would lift that ban"), "An already-banned live player is refused.");
            Assert.That(kick, Does.Contain("presence == BanTargetPresence.InSteamLobby || presence == BanTargetPresence.Reconnected"), "Only a live row can be kicked.");
            Assert.That(kick, Does.Contain("TryBan(lobby, ownerSteamId, issuerSteamId, targetSteamId, incidentId, out message)"));
            Assert.That(kick, Does.Contain("PendingKicks.Add("));
            // Follow-up: its own tick on the network host; unban runs directly as the owner (a Helper removed mid-window must not block it).
            Assert.That(tickStart, Is.GreaterThan(0));
            Assert.That(tick, Does.Contain("NetworkManager.main != null && NetworkManager.main.isHost"), "Not the Steam-owner gate: a Steam read failure must not freeze a kick with the ban in place.");
            Assert.That(tick, Does.Contain("KickFollowUpPolicy.Decide("));
            Assert.That(tick, Does.Contain("TryUnban(pending.TargetSteamId, ownerSteamId, pending.IncidentId, out string unbanMessage)"));
            Assert.That(tick, Does.Not.Contain("TryExecuteHostAction("), "The follow-up never re-checks the Helper's authorization.");
            Assert.That(tick, Does.Contain("HostJournalController.BeginSurface(\"kick follow-up\");"));
            Assert.That(tick, Does.Contain("KickFollowUpPolicy.IncompleteMessage"));
            Assert.That(execute, Does.Contain("CancelPendingKick(targetSteamId);"), "An explicit ban promotes a pending kick to a ban.");
            Assert.That(execute, Does.Contain("action == \"kick\"").And.Contain("action == \"mute\"").And.Contain("action == \"unmute\""));
            Assert.That(plugin, Does.Contain("SteamIdModerationController.TickPendingKicks();"));
            // Mute: host-side drop in the chat relay, keyed by the authoritative sender, after the BlueSage-packet test, fail open.
            Assert.That(relay, Does.Contain("[HarmonyPrefix, HarmonyPriority(Priority.First)]"));
            Assert.That(relay, Does.Contain("return !recognizedBlueSagePacket;"));
            Assert.That(relay, Does.Contain("SteamIdModerationController.ShouldDropChatFromSender(info.sender)"));
            Assert.That(relay.IndexOf("ModerationProtocolPolicy.Prefix, StringComparison.Ordinal))", StringComparison.Ordinal), Is.LessThan(relay.IndexOf("ShouldDropChatFromSender", StringComparison.Ordinal)), "BlueSage packets are judged first.");
            Assert.That(controller, Does.Contain("LobbyMutePolicy.TryResolveSender(matches)"));
            Assert.That(controller, Does.Contain("LobbyMutePolicy.ShouldDrop(senderSteamId, LobbyMutedSteamIds)"));
            Assert.That(controller, Does.Contain("LobbyMutedSteamIds.Clear();"), "Hosting-session only; a new lobby starts clean.");
            // Advisor at the E gate: the sender's own line is drawn only when the server echoes it, so a muted player's
            // own messages stop too; the text must say so. Hosting ending with a kick pending finalizes the kick as a
            // timeout row (fail toward ban) instead of leaving a silent permanent ban; an exact-id unmute needs no live row.
            Assert.That(controller, Does.Contain("Their own messages stop appearing for them as well, so they will notice."));
            Assert.That(controller, Does.Not.Contain("still see their own text"));
            Assert.That(tick, Does.Contain("hosting ended while the kick was pending"));
            Assert.That(tick, Does.Contain("PendingKicks.Clear();"));
            Assert.That(plugin, Does.Contain("action == \"unmute\" && ModerationProtocolPolicy.IsSteamId64(query) && !currentSteamIds.Contains(query, StringComparer.Ordinal)"));
            Assert.That(plugin, Does.Contain("an older host ignores this request silently"), "Helper honesty on an older host.");
            // Commands through the existing preview + confirm flow; menu buttons on the selected row; regression matrix.
            Assert.That(plugin, Does.Contain("\"sidkick\",").And.Contain("\"sidmute\",").And.Contain("\"sidunmute\","));
            Assert.That(plugin, Does.Contain("normalized == \"sidkick\" || normalized == \"sidmute\" || normalized == \"sidunmute\""));
            Assert.That(plugin, Does.Contain("action == \"kick\" || action == \"mute\" || action == \"unmute\""), "Kick, mute and unmute re-resolve the live target at confirm time.");
            Assert.That(menu, Does.Contain("DrawSelectedRowLiveAction(\"kick\""));
            Assert.That(menu, Does.Contain("DrawSelectedRowLiveAction(\"mute\""));
            Assert.That(menu, Does.Contain("Plugin.Instance.PreviewSidLiveActionFromQolMenu(action, steamId)"));
            Assert.That(menu, Does.Contain("Plugin.Instance.ConfirmSidLiveActionFromQolMenu(action, steamId)"));
            Assert.That(matrix, Does.Contain("`/sidkick`").And.Contain("`/sidmute`").And.Contain("`/sidunmute`"));
        });
    }
}
