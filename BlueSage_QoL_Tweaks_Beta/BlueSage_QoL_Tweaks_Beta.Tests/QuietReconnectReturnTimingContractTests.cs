using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5d full review: the "quiet return after a same-lobby reconnect" decision must not be spent on the visit that
// was current when Reconnect Guard started joining (the menu scene can already report the target lobby id).
public sealed class QuietReconnectReturnTimingContractTests
{
    [Test]
    public void The_quiet_return_is_decided_on_the_visit_after_the_join_not_the_one_before_it()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        int quiet = plugin.IndexOf("internal static bool IsQuietReconnectVisit(string visitKey)", StringComparison.Ordinal);
        string body = plugin.Substring(quiet, plugin.IndexOf("\n    }\n", quiet, StringComparison.Ordinal) - quiet);
        int note = plugin.IndexOf("private static void NoteReconnectReturnTarget(string targetLobbyId)", StringComparison.Ordinal);
        string noteBody = plugin.Substring(note, plugin.IndexOf("\n    }\n", note, StringComparison.Ordinal) - note);
        Assert.Multiple(() =>
        {
            Assert.That(noteBody, Does.Contain("_reconnectReturnNotedVisitKey = _quietTrackedVisitKey"), "The visit current at the join is remembered.");
            Assert.That(body, Does.Contain("WelcomeLobbyKeyPolicy.IsSameVisit(_reconnectReturnNotedVisitKey, visitKey)"));
            Assert.That(body, Does.Contain("&& !stillTheVisitBeforeTheJoin"), "That visit never decides (and so never spends the target).");
            Assert.That(plugin, Does.Contain("_reconnectReturnNotedVisitKey = string.Empty;"), "Cleared with the target.");
        });
    }

    [Test]
    public void The_welcome_pop_up_waits_for_the_lobby_id_and_stays_closed_on_a_quiet_return()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("WelcomeRepeatPolicy.LobbyIdOf(overlayLobbyKey).Length > 0 &&"), "Round 5e: decided only once the lobby id is known.");
            Assert.That(plugin, Does.Contain("_welcomeOverlayVisible = !quietReconnectVisit;"), "One decision for the welcome lines, the Tip, the host message and the pop-up.");
        });
    }
}
