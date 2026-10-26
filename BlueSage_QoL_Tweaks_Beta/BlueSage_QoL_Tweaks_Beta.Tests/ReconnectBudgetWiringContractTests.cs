using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5b (2026-10-23): wiring proofs for the per-lobby reconnect budget and the host's lobby-lost notice.
// Decisions are behavior-tested in ReconnectGuardPolicyTests and HostHealthPolicyTests; this file pins that the
// plugin feeds the policy real counts, stops with a toast when the budget is spent, and resets on a real success.
public sealed class ReconnectBudgetWiringContractTests
{
    [Test]
    public void Budget_is_five_generations_per_lobby_and_feeds_the_policy()
    {
        string plugin = ReadSource("Plugin.cs");
        string handler = Slice(plugin, "private bool HandleConnectionLostForReconnectGuardInstance(", "private void StartCleanMenuReconnect(");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal const int LockedReconnectMaxGenerationsPerLobby = 5;"), "Blue's number: five guarded rejoins per lobby.");
            Assert.That(handler, Does.Contain("lobbyGenerationsUsed: GetReconnectGenerationsUsedForLobby(_lastReconnectLobbyCode)"), "The policy must see how many generations this lobby already used.");
            Assert.That(handler, Does.Contain("maxGenerationsPerLobby: LockedReconnectMaxGenerationsPerLobby"));
        });
    }

    [Test]
    public void Spent_budget_stops_the_guard_tells_the_player_and_lets_the_native_menu_flow_run()
    {
        string plugin = ReadSource("Plugin.cs");
        string handler = Slice(plugin, "private bool HandleConnectionLostForReconnectGuardInstance(", "private void StartCleanMenuReconnect(");
        string stopCase = Slice(handler, "case ReconnectGuardAction.StopManagedRetry:", "case ReconnectGuardAction.WaitForManagedRetryCadence:");

        Assert.Multiple(() =>
        {
            Assert.That(stopCase, Does.Contain("if (decision.BudgetSpent)"));
            Assert.That(stopCase, Does.Contain("AddLocalNotificationWithToast(ReconnectBudgetSpentNotice)"), "The player must be told why no rejoin starts.");
            Assert.That(stopCase, Does.Contain("StopManagedReconnect(decision.Reason);"));
            Assert.That(stopCase, Does.Contain("return true;"), "The game's own ReturnMenu must run so the player lands on the menu.");
            Assert.That(plugin, Does.Contain("internal const string ReconnectBudgetSpentNotice = \"BlueSage tried to reconnect 5 times, so it's resting now. You can rejoin from the menu when the host is back.\";"));
        });
    }

    [Test]
    public void Every_generation_counts_against_its_lobby_and_only_an_idle_guard_clears_the_count()
    {
        string plugin = ReadSource("Plugin.cs");
        string start = Slice(plugin, "private void StartCleanMenuReconnect(", "private IEnumerator CleanMenuReconnectLoop(");
        string announce = Slice(plugin, "private void AnnounceManagedReconnectSuccess()", "private IEnumerator DelayedReconnectAnnouncement(");
        string counter = Slice(plugin, "private void NoteReconnectGenerationStarted()", "private void ResetReconnectBudget(");
        string finish = Slice(plugin, "private void FinishCleanMenuReconnect(int generation, string reason)", "private static bool IsPlayerJoinInProgress()");
        string stop = Slice(plugin, "private void StopManagedReconnect(string reason)", "private void AnnounceManagedReconnectSuccess()");
        string handler = Slice(plugin, "private bool HandleConnectionLostForReconnectGuardInstance(", "private void StartCleanMenuReconnect(");

        Assert.Multiple(() =>
        {
            Assert.That(start, Does.Contain("NoteReconnectGenerationStarted();"), "Each generation must count.");
            Assert.That(start.IndexOf("NoteReconnectGenerationStarted();", System.StringComparison.Ordinal), Is.GreaterThan(start.IndexOf("_cleanMenuReconnectActive = true;", System.StringComparison.Ordinal)), "Only a generation that really starts counts (the overlapping-start return comes first).");
            Assert.That(counter, Does.Contain("_reconnectBudgetLobbyCode = _lastReconnectLobbyCode;"), "The count is keyed by the saved lobby id.");
            Assert.That(counter, Does.Contain("_reconnectBudgetGenerationsUsed = 0;"), "A different lobby starts a fresh budget.");
            Assert.That(counter, Does.Contain("ReconnectGuardPolicy.BudgetClearsAfterIdle(idleSeconds, LockedReconnectBudgetIdleResetSeconds)"), "The lazy clear uses the policy's rule and the locked window.");
            Assert.That(plugin, Does.Contain("internal const int LockedReconnectBudgetIdleResetSeconds = 120;"));
            Assert.That(announce, Does.Not.Contain("ResetReconnectBudget("), "Reviewer case 1: the success confirmation never clears the budget (a rejoin that gets in for two seconds and drops is the loop too).");
            Assert.That(finish, Does.Contain("MarkReconnectGuardIdle();"), "Idle time is measured from the end of a generation.");
            Assert.That(stop, Does.Contain("MarkReconnectGuardIdle();"), "Idle time is measured from a budget stop too.");
            Assert.That(handler, Does.Contain("secondsSinceLastGuardActivity: GetSecondsSinceReconnectGuardIdle()"), "The policy sees the same idle time the plugin uses.");
        });
    }

    [Test]
    public void Host_sees_one_lobby_lost_notice_per_streak()
    {
        string plugin = ReadSource("Plugin.cs");
        string check = Slice(plugin, "private string RunHostHealthCheck(string reason)", "private void StartCommunityBanListMaintenance()");

        Assert.Multiple(() =>
        {
            Assert.That(check, Does.Contain("decision.Actions.Contains(HostHealthAction.NotifyHostLobbyLost)"));
            Assert.That(check, Does.Contain("healthChanged"), "Once per streak, not once per 5-minute tick.");
            Assert.That(check, Does.Contain("string.Equals(previousHealthKey, \"healthy\", StringComparison.Ordinal)"), "Only on a healthy-to-lost transition: offline or solo hosting without a Steam lobby never hears it.");
            Assert.That(check, Does.Contain("AddLocalNotificationWithToast(HostLobbyLostNotice)"));
            Assert.That(plugin, Does.Contain("internal const string HostLobbyLostNotice = \"Steam dropped your lobby, so new players can't join and host tools are locked. Host again when you're ready.\";"));
            Assert.That(Plugin_HostLobbyLostNotice(plugin), Does.Contain("Steam dropped your lobby"), "tools/Get-BlueSageLiveLogReadout.ps1 counts this phrase.");
            Assert.That(plugin.Split("Steam dropped your lobby").Length - 1, Is.EqualTo(2), "Only the notice (and the comment above it) may carry the counted phrase; the Host Health reply says it differently.");
        });
    }

    private static string Plugin_HostLobbyLostNotice(string plugin) =>
        Slice(plugin, "internal const string HostLobbyLostNotice = ", ";\n");

    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
