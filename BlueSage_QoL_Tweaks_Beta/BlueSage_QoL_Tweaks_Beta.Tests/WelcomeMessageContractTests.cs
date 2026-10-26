using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WelcomeMessageContractTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Test]
    public void WelcomeMessage_RetriesUntilLocalNotificationActuallyPosts()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string policy = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Core", "VoiceWelcomePolicy.cs"));

        Assert.That(plugin, Does.Contain("internal static bool AddLocalNotification"));
        Assert.That(plugin, Does.Contain("_welcomeMessageShownForLobby = ShowWelcomeMessageForLobby(quietReconnectVisit);"));
        Assert.That(plugin, Does.Contain("private bool ShowWelcomeMessageForLobby(bool quietReconnectVisit)"));
        Assert.That(plugin, Does.Contain("_welcomePrimaryShownForLobby"));
        Assert.That(plugin, Does.Contain("_welcomeContextShownForLobby"));
        Assert.That(plugin, Does.Contain("return firstShown && secondShown;"));
        Assert.That(plugin, Does.Contain("BuildVoiceWelcomeGuidance"));
        Assert.That(plugin, Does.Contain("SettingsData.PushToTalk").Or.Contain("settings.PushToTalk"));
        Assert.That(policy, Does.Contain("(Voice Activation)"));
        Assert.That(plugin, Does.Contain("Pomodoro/focus timer"));
        Assert.That(plugin, Does.Contain("can earn tickets and XP for focus time"));
        Assert.That(plugin, Does.Not.Contain("completed sessions can earn tickets"));
        Assert.That(plugin, Does.Contain("No microphone device name was logged"));
        Assert.That(plugin, Does.Contain("_welcomeVoiceStateLogKey"));
        Assert.That(plugin, Does.Contain("if (!string.Equals(_welcomeVoiceStateLogKey, stateLogKey"));
        Assert.That(plugin, Does.Contain("_welcomeVoiceStateLogKey = string.Empty;"));
    }

    // Voice pass (2026-10-23) NOISE 1 + 2: a Reconnect Guard return to the same lobby is quiet (welcome, Tip and the
    // host's message), and the long Tip line shows on the first lobby visit of a game session only.
    [Test]
    public void Welcome_and_Tip_respect_quiet_reconnect_returns_and_the_once_per_session_Tip()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs")).Replace("\r\n", "\n");
        string controller = File.ReadAllText(Path.Combine(ProjectRoot, "src", "LobbyAnnouncementController.cs")).Replace("\r\n", "\n");
        string show = Slice(plugin, "private bool ShowWelcomeMessageForLobby(bool quietReconnectVisit)", "internal static bool IsQuietReconnectVisit(string visitKey)");
        string loop = Slice(plugin, "private IEnumerator WelcomeMessageLoop()", "private void ResetWelcomeMessageLobbyState()");
        string tracker = Slice(plugin, "internal static bool IsQuietReconnectVisit(string visitKey)", "private static void NoteReconnectReturnTarget(string targetLobbyId)");
        string joinLoop = Slice(plugin, "_cleanMenuReconnectPhase = \"joining-saved-lobby\";", "MonoSingleton<MultiplayerManager>.I.JoinByCode(targetLobbyCode, false);");
        string finish = Slice(plugin, "private void FinishCleanMenuReconnect(int generation, string reason)", "MarkReconnectGuardIdle();");
        string motd = Slice(controller, "private static void ShowHostMotdOncePerVisit(", "Plugin.Instance?.ShowHostAnnouncementToast(line);");

        Assert.Multiple(() =>
        {
            // The loop settles quiet-or-not before the welcome toggle check, so the host message agrees with it.
            Assert.That(loop, Does.Contain("bool quietReconnectVisit = TryGetCurrentLobbyKeyForWelcomeMessage(out string quietVisitKey) && IsQuietReconnectVisit(quietVisitKey);"));
            Assert.That(loop.IndexOf("IsQuietReconnectVisit(quietVisitKey)", StringComparison.Ordinal), Is.LessThan(loop.IndexOf("if (EnableWelcomeMessage == null || !EnableWelcomeMessage.Value)", StringComparison.Ordinal)));

            // NOISE 1: the welcome and the Tip are skipped, and the visit counts as settled (no retry loop).
            Assert.That(show, Does.Contain("if (!WelcomeRepeatPolicy.ShouldShowWelcome(quietReconnectVisit))"));
            Assert.That(show.IndexOf("WelcomeRepeatPolicy.ShouldShowWelcome(quietReconnectVisit)", StringComparison.Ordinal), Is.LessThan(show.IndexOf("NavigationCalloutPolicy.BuildPrimaryWelcomeMessage(", StringComparison.Ordinal)));
            // NOISE 2: once per session; a skipped Tip counts as shown so the welcome settles.
            Assert.That(show, Does.Contain("if (!WelcomeRepeatPolicy.ShouldShowTip(_welcomeTipShownThisSession, quietReconnectVisit))"));
            Assert.That(show, Does.Contain("_welcomeTipShownThisSession = secondShown;"));
            Assert.That(plugin, Does.Contain("private bool _welcomeTipShownThisSession;"));
            Assert.That(plugin.Split("_welcomeTipShownThisSession = false").Length, Is.EqualTo(1), "Nothing resets the Tip within a game session.");

            // The tracker decides once per visit, only once a lobby id is known, and consumes the reconnect target.
            Assert.That(tracker, Does.Contain("WelcomeLobbyKeyPolicy.IsSameVisit(_quietTrackedVisitKey, visitKey)"));
            Assert.That(tracker, Does.Contain("if (!_quietTrackedVisitDecided && lobbyId.Length > 0 && !stillTheVisitBeforeTheJoin)"));
            Assert.That(tracker, Does.Contain("WelcomeRepeatPolicy.IsQuietReconnectReturn(visitKey, _reconnectReturnTargetLobbyId, _reconnectReturnLobbyIdBeforeDrop)"));
            Assert.That(tracker, Does.Contain("_reconnectReturnTargetLobbyId = string.Empty;"));
            Assert.That(tracker, Does.Not.Contain("[BlueSageVisit]"), "The [BlueSageVisit] log prefix keeps its one meaning.");

            // The reconnect target is remembered when Reconnect Guard starts joining, and dropped when it never got in.
            Assert.That(joinLoop, Does.Contain("NoteReconnectReturnTarget(targetLobbyCode);"));
            Assert.That(finish, Does.Contain("if (!string.Equals(reason, \"playable scene confirmed\", StringComparison.Ordinal))"));
            Assert.That(finish, Does.Contain("ClearReconnectReturnTarget();"));
            Assert.That(plugin, Does.Contain("FinishCleanMenuReconnect(generation, \"playable scene confirmed\");"), "The success reason the clear skips must match the real one.");

            // The host's pinned message follows the same answer.
            Assert.That(motd, Does.Contain("if (!WelcomeRepeatPolicy.ShouldShowHostMessage(Plugin.IsQuietReconnectVisit(visitKey)))"));
            Assert.That(motd.IndexOf("Plugin.IsQuietReconnectVisit(visitKey)", StringComparison.Ordinal), Is.GreaterThan(motd.IndexOf("LobbyMotdPolicy.ShouldShow(visitKey, _motdShownForVisit)", StringComparison.Ordinal)));
        });
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Project root not found.");
    }
}
