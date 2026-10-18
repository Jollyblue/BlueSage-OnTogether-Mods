using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReconnectRuntimeHealthPolicyTests
{
    [Test]
    public void Evaluate_KeepsWaiting_WhenTransportIsNotConnected()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: false,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: false,
            localUserIsAuthoritativeLobbyMember: false,
            gameRuntimeUsable: false,
            voiceRuntimeReady: false,
            consecutiveReadySamples: 0,
            secondsSinceClientTransportConnected: null,
            postConnectValidationSeconds: 8);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.KeepWaiting));
        Assert.That(decision.Reason, Does.Contain("not connected"));
    }

    [Test]
    public void Evaluate_ConfirmsSuccess_WhenTransportAndVisibleLobbyAreReady()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: true,
            localUserIsAuthoritativeLobbyMember: true,
            gameRuntimeUsable: true,
            voiceRuntimeReady: true,
            consecutiveReadySamples: 3,
            secondsSinceClientTransportConnected: 1,
            postConnectValidationSeconds: 8,
            readyStableSeconds: 2,
            requiredReadyStabilitySeconds: 2);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.ConfirmSuccess));
        Assert.That(decision.Reason, Does.Contain("authoritative Steam membership"));
    }

    [Test]
    public void Evaluate_RejectsSuccess_WhenLobbyVisibleButLocalPlayerIsStillLoading()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: true,
            localUserIsAuthoritativeLobbyMember: true,
            gameRuntimeUsable: false,
            voiceRuntimeReady: true,
            consecutiveReadySamples: 0,
            secondsSinceClientTransportConnected: 2,
            postConnectValidationSeconds: 8);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.KeepWaiting));
        Assert.That(decision.Reason, Does.Contain("waiting"));
    }

    [Test]
    public void Evaluate_ReturnsToMenu_WhenLobbyVisibleButLocalRuntimeNeverConverges()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: true,
            localUserIsAuthoritativeLobbyMember: true,
            gameRuntimeUsable: false,
            voiceRuntimeReady: true,
            consecutiveReadySamples: 0,
            secondsSinceClientTransportConnected: 8,
            postConnectValidationSeconds: 8);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.ReturnToMenu));
        Assert.That(decision.Reason, Does.Contain("did not converge"));
    }

    [Test]
    public void Evaluate_KeepsWaiting_WhenTransportConnectedButLobbyIsStillWarming()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: false,
            localUserIsAuthoritativeLobbyMember: false,
            gameRuntimeUsable: false,
            voiceRuntimeReady: false,
            consecutiveReadySamples: 0,
            secondsSinceClientTransportConnected: 2,
            postConnectValidationSeconds: 8);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.KeepWaiting));
        Assert.That(decision.Reason, Does.Contain("ghost-member"));
    }

    [Test]
    public void Evaluate_ReturnsToMenu_WhenTransportConnectedButLobbyNeverBecomesUsable()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: false,
            localUserIsAuthoritativeLobbyMember: false,
            gameRuntimeUsable: false,
            voiceRuntimeReady: false,
            consecutiveReadySamples: 0,
            secondsSinceClientTransportConnected: 8,
            postConnectValidationSeconds: 8);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.ReturnToMenu));
        Assert.That(decision.Reason, Does.Contain("did not converge"));
    }

    [Test]
    public void Evaluate_RejectsGhostMember_WhenUiExistsButLocalSteamMembershipIsMissing()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: true,
            localUserIsAuthoritativeLobbyMember: false,
            gameRuntimeUsable: true,
            voiceRuntimeReady: true,
            consecutiveReadySamples: 0,
            secondsSinceClientTransportConnected: 2,
            postConnectValidationSeconds: 8);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.KeepWaiting));
        Assert.That(decision.Reason, Does.Contain("ghost-member"));
    }

    [Test]
    public void Evaluate_RequiresThreeConsecutiveQuorumSamples()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: true,
            localUserIsAuthoritativeLobbyMember: true,
            gameRuntimeUsable: true,
            voiceRuntimeReady: true,
            consecutiveReadySamples: 2,
            secondsSinceClientTransportConnected: 2,
            postConnectValidationSeconds: 8);

        Assert.That(ReconnectRuntimeHealthPolicy.Evaluate(input).Action, Is.EqualTo(ReconnectRuntimeHealthAction.KeepWaiting));
    }

    [Test]
    public void Evaluate_RequiresTwoSecondsOfStablePlayableQuorum()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: true,
            localUserIsAuthoritativeLobbyMember: true,
            gameRuntimeUsable: true,
            voiceRuntimeReady: true,
            consecutiveReadySamples: 120,
            secondsSinceClientTransportConnected: 4,
            postConnectValidationSeconds: 8,
            readyStableSeconds: 1.9f,
            requiredReadyStabilitySeconds: 2);

        Assert.That(ReconnectRuntimeHealthPolicy.Evaluate(input).Action, Is.EqualTo(ReconnectRuntimeHealthAction.KeepWaiting));
    }

    [Test]
    public void Evaluate_PreservesAuthoritativePlayableSession_WhenVoiceIsLate()
    {
        var input = new ReconnectRuntimeHealthInput(
            clientTransportConnected: true,
            requireVisibleLobby: true,
            visibleLobbyConfirmed: true,
            localUserIsAuthoritativeLobbyMember: true,
            gameRuntimeUsable: true,
            voiceRuntimeReady: false,
            consecutiveReadySamples: 120,
            secondsSinceClientTransportConnected: 4,
            postConnectValidationSeconds: 8,
            readyStableSeconds: 2.1f,
            requiredReadyStabilitySeconds: 2);

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(input);

        Assert.That(decision.Action, Is.EqualTo(ReconnectRuntimeHealthAction.ConfirmSuccess));
        Assert.That(decision.Reason, Does.Contain("playable session is preserved"));
    }
}
