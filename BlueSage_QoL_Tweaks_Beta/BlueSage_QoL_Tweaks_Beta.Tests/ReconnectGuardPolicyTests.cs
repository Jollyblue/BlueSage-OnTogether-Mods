using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReconnectGuardPolicyTests
{
    [Test]
    public void Evaluate_AllowsNormalFlow_WhenGuardDisabled()
    {
        var input = new ReconnectGuardInput(
            guardEnabled: false,
            reconnectLoaded: true,
            suppressHostKickReconnect: true,
            blockDuringInitialLobbyGrace: true,
            requireVisibleLobby: true,
            reconnectAttemptIntervalSeconds: 15,
            initialLobbyGraceSeconds: 20,
            maxGuardedAttempts: 20,
            isHost: false,
            isIntentionalLeave: false,
            isAlreadyReconnecting: false,
            notificationStatusName: "Kicked",
            secondsSinceLobbyCreated: 5,
            secondsSinceLastManagedAttempt: null,
            reconnectAttempt: 1,
            visibleLobbyMemberCount: 1,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.AllowReconnectMod));
        Assert.That(decision.Reason, Is.EqualTo("Reconnect guard disabled."));
    }

    [Test]
    public void Evaluate_RunsManagedReconnect_WhenAndrewReconnectIsNotLoaded()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            reconnectLoaded: false,
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            reconnectAttempt: 1,
            visibleLobbyMemberCount: 1,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.RunManagedRetryNow));
        Assert.That(decision.Reason, Is.EqualTo("Visible lobby confirmed at 1/128 players; managed reconnect retry may run now."));
    }

    [Test]
    public void Evaluate_TakesOverFromAndrewReconnect_WhenItIsLoaded()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            reconnectLoaded: true,
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            reconnectAttempt: 1,
            visibleLobbyMemberCount: 2,
            visibleLobbyMaxPlayers: 90);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.RunManagedRetryNow));
        Assert.That(decision.Reason, Does.Contain("suppress its transport-only retry"));
    }

    [TestCase("Kicked")]
    [TestCase("HostKick")]
    [TestCase("RemovedByHost")]
    public void Evaluate_DoesNotReconnectByDefault_ForHostKickStatuses(string notificationStatus)
    {
        var input = ReconnectGuardInput.DefaultForTests(
            suppressHostKickReconnect: true,
            notificationStatusName: notificationStatus,
            secondsSinceLobbyCreated: 45);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.Noop));
        Assert.That(decision.Reason, Does.Contain("not auto reconnecting"));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
        Assert.That(decision.RecommendedMaxAttempts, Is.EqualTo(3));
    }

    [Test]
    public void Evaluate_CanOptIntoManagedRetry_ForHostKickStatuses()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            suppressHostKickReconnect: false,
            notificationStatusName: "Kicked",
            secondsSinceLobbyCreated: 45);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.RunManagedRetryNow));
        Assert.That(decision.Reason, Does.Contain("Visible lobby confirmed"));
    }

    [Test]
    public void Evaluate_SchedulesManagedRetry_DuringInitialLobbyGrace()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 8);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.ScheduleManagedRetry));
        Assert.That(decision.Reason, Does.Contain("lobby grace"));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
    }

    [Test]
    public void Evaluate_SchedulesManagedRetry_WhenVisibleLobbyCannotBeConfirmed()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            visibleLobbyMemberCount: null,
            visibleLobbyMaxPlayers: null);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.ScheduleManagedRetry));
        Assert.That(decision.Reason, Does.Contain("visible lobby"));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
    }

    [Test]
    public void Evaluate_SchedulesManagedRetry_WhenVisibleLobbyHasNoPlayers()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            visibleLobbyMemberCount: 0,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.ScheduleManagedRetry));
        Assert.That(decision.Reason, Does.Contain("0/128"));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
    }

    [Test]
    public void Evaluate_SchedulesManagedRetry_WhenCadenceHasNotElapsed()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 2,
            visibleLobbyMemberCount: 1,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.WaitForManagedRetryCadence));
        Assert.That(decision.Reason, Does.Contain("5s cadence"));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
    }

    [Test]
    public void Evaluate_SuppressesAdditionalReconnectPressure_WhenManagedReconnectIsAlreadyActive()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            isAlreadyReconnecting: true,
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            visibleLobbyMemberCount: 1,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.Noop));
        Assert.That(decision.Reason, Does.Contain("already active"));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
        Assert.That(decision.RecommendedMaxAttempts, Is.EqualTo(3));
    }

    [Test]
    public void Evaluate_StopsManagedRetry_WhenGuardedAttemptLimitIsReached()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            reconnectAttempt: 301,
            maxGuardedAttempts: 3,
            visibleLobbyMemberCount: 1,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.StopManagedRetry));
        Assert.That(decision.Reason, Does.Contain("guarded attempt limit"));
    }

    [Test]
    public void Evaluate_AllowsManagedRetry_WhenVisibleLobbyHasAtLeastOnePlayerAndCadenceElapsed()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            reconnectAttempt: 1,
            visibleLobbyMemberCount: 1,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.RunManagedRetryNow));
        Assert.That(decision.Reason, Is.EqualTo("Visible lobby confirmed at 1/128 players; managed reconnect retry may run now."));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
        Assert.That(decision.RecommendedMaxAttempts, Is.EqualTo(3));
    }

    [Test]
    public void Evaluate_ClampsUnsafeConfigValues_ToSafeReconnectBounds()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            reconnectAttemptIntervalSeconds: 1,
            maxGuardedAttempts: 2000,
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            reconnectAttempt: 1,
            visibleLobbyMemberCount: 1,
            visibleLobbyMaxPlayers: 128);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.RunManagedRetryNow));
        Assert.That(decision.RecommendedAttemptIntervalSeconds, Is.EqualTo(5));
        Assert.That(decision.RecommendedMaxAttempts, Is.EqualTo(3));
    }

    // Round 5b (2026-10-23): the host kept its process and PurrNet alive while its Steam lobby was invalid, so
    // every guarded rejoin got in, dropped with HostLost and started another generation. The budget is per lobby.
    [Test]
    public void Evaluate_StopsWithBudgetSpent_WhenTheLobbyHasUsedItsGenerations()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            reconnectAttempt: 1,
            visibleLobbyMemberCount: 19,
            visibleLobbyMaxPlayers: 128,
            lobbyGenerationsUsed: 5,
            maxGenerationsPerLobby: 5);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.StopManagedRetry));
        Assert.That(decision.BudgetSpent, Is.True);
        Assert.That(decision.Reason, Does.Contain("5/5"));
        Assert.That(decision.Reason, Does.Contain("Rejoin from the menu"));
    }

    [Test]
    public void Evaluate_StillRuns_WhileTheLobbyBudgetHasRoom()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            reconnectAttempt: 1,
            visibleLobbyMemberCount: 19,
            visibleLobbyMaxPlayers: 128,
            lobbyGenerationsUsed: 4,
            maxGenerationsPerLobby: 5);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.RunManagedRetryNow));
        Assert.That(decision.BudgetSpent, Is.False);
    }

    [Test]
    public void Evaluate_BudgetStop_WinsOverScheduleRules_ButNotOverTheKickRule()
    {
        var scheduled = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            visibleLobbyMemberCount: 0,
            visibleLobbyMaxPlayers: 128,
            lobbyGenerationsUsed: 5);
        var kicked = ReconnectGuardInput.DefaultForTests(
            suppressHostKickReconnect: true,
            notificationStatusName: "Kicked",
            secondsSinceLobbyCreated: 45,
            lobbyGenerationsUsed: 5);

        ReconnectGuardDecision kickDecision = ReconnectGuardPolicy.Evaluate(kicked);

        Assert.That(ReconnectGuardPolicy.Evaluate(scheduled).Action, Is.EqualTo(ReconnectGuardAction.StopManagedRetry));
        Assert.That(kickDecision.Action, Is.EqualTo(ReconnectGuardAction.Noop), "A kicked player never hears 'stopped after 5 tries' for a rejoin that was never attempted.");
        Assert.That(kickDecision.BudgetSpent, Is.False);
    }

    // Reviewer case 1 (2026-10-23): a rejoin that gets in, is playable for two seconds and drops again must keep
    // counting; the success confirmation never clears the budget.
    [Test]
    public void Evaluate_KeepsTheSpentBudget_WhenTheDropComesSoonAfterTheLastGuardEvent()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 60,
            visibleLobbyMemberCount: 19,
            visibleLobbyMaxPlayers: 128,
            lobbyGenerationsUsed: 5,
            secondsSinceLastGuardActivity: 30);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.StopManagedRetry));
        Assert.That(decision.BudgetSpent, Is.True);
    }

    // Reviewer case 2 (2026-10-23): after the budget is spent the player rejoins the same lobby by hand and plays
    // for a while; the next drop starts a fresh budget because the guard was idle for the reset window.
    [Test]
    public void Evaluate_ClearsTheBudget_WhenTheGuardWasIdleForTheResetWindow()
    {
        var input = ReconnectGuardInput.DefaultForTests(
            notificationStatusName: "HostLost",
            secondsSinceLobbyCreated: 45,
            secondsSinceLastManagedAttempt: 600,
            visibleLobbyMemberCount: 19,
            visibleLobbyMaxPlayers: 128,
            lobbyGenerationsUsed: 5,
            secondsSinceLastGuardActivity: 120);

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectGuardAction.RunManagedRetryNow));
        Assert.That(decision.BudgetSpent, Is.False);
    }

    [Test]
    public void BudgetClearsAfterIdle_NeedsAKnownIdleTimeAtOrPastTheClampedWindow()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(null, 120), Is.False, "No idle time known: never clear.");
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(-1f, 120), Is.False);
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(119.9f, 120), Is.False);
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(120f, 120), Is.True);
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(29f, 0), Is.False, "A window below 30 s clamps to 30 s.");
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(30f, 0), Is.True);
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(899f, 5000), Is.False, "A window above 900 s clamps to 900 s.");
            Assert.That(ReconnectGuardPolicy.BudgetClearsAfterIdle(900f, 5000), Is.True);
            Assert.That(ReconnectGuardInput.DefaultForTests().BudgetIdleResetSeconds, Is.EqualTo(120));
        });
    }

    [Test]
    public void Evaluate_ClampsTheLobbyBudget_ToOneThroughTen()
    {
        var zero = ReconnectGuardInput.DefaultForTests(lobbyGenerationsUsed: 1, maxGenerationsPerLobby: 0, secondsSinceLobbyCreated: 45, secondsSinceLastManagedAttempt: 60);
        var huge = ReconnectGuardInput.DefaultForTests(lobbyGenerationsUsed: 10, maxGenerationsPerLobby: 500, secondsSinceLobbyCreated: 45, secondsSinceLastManagedAttempt: 60);

        Assert.That(ReconnectGuardPolicy.Evaluate(zero).Action, Is.EqualTo(ReconnectGuardAction.StopManagedRetry), "A budget below 1 clamps to 1.");
        Assert.That(ReconnectGuardPolicy.Evaluate(huge).Action, Is.EqualTo(ReconnectGuardAction.StopManagedRetry), "A budget above 10 clamps to 10.");
    }

    [Test]
    public void Evaluate_DoesNotCountTheBudget_WhenTheGuardIsOffOrTheLocalPlayerHosts()
    {
        var off = ReconnectGuardInput.DefaultForTests(guardEnabled: false, lobbyGenerationsUsed: 9);
        var host = ReconnectGuardInput.DefaultForTests(isHost: true, lobbyGenerationsUsed: 9);

        Assert.That(ReconnectGuardPolicy.Evaluate(off).Action, Is.EqualTo(ReconnectGuardAction.AllowReconnectMod));
        Assert.That(ReconnectGuardPolicy.Evaluate(host).Action, Is.EqualTo(ReconnectGuardAction.AllowReconnectMod));
    }
}
