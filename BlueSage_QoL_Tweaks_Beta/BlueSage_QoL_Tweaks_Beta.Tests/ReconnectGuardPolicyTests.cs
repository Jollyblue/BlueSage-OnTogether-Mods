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
}
