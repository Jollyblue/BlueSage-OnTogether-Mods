using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HostHealthPolicyTests
{
    [Test]
    public void Evaluate_DoesNothing_ForClients()
    {
        HostHealthDecision decision = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(
            isHost: false,
            localSteamId: "222",
            lobbyOwnerSteamId: "111"));

        Assert.That(decision.ShouldRunDiagnostics, Is.False);
        Assert.That(decision.Issues, Is.Empty);
        Assert.That(decision.Actions, Is.Empty);
    }

    [TestCase("")]
    [TestCase("0")]
    public void Evaluate_DetectsInvalidLobbyOwner_AndRequestsLobbyRefresh(string lobbyOwnerSteamId)
    {
        HostHealthDecision decision = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(lobbyOwnerSteamId: lobbyOwnerSteamId));

        Assert.That(decision.Issues, Does.Contain(HostHealthIssue.LobbyOwnerInvalid));
        Assert.That(decision.Actions, Does.Contain(HostHealthAction.RefreshLobbyMetadata));
    }

    // Round 5b (2026-10-23): on 10-23 the host saw LobbyOwnerInvalid for 40 minutes and was told nothing while
    // guests cycled through reconnects. The policy now asks for one host notice; the plugin shows it once per streak.
    [Test]
    public void Evaluate_AsksForAHostNotice_WhenTheSteamLobbyIsGone()
    {
        HostHealthDecision gone = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(lobbyValid: false));
        HostHealthDecision healthy = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(
            localSteamId: "111",
            lobbyOwnerSteamId: "111",
            hostPersonaName: "Blue"));

        Assert.That(gone.Issues, Does.Contain(HostHealthIssue.LobbyOwnerInvalid));
        Assert.That(gone.Actions, Does.Contain(HostHealthAction.NotifyHostLobbyLost));
        Assert.That(healthy.Actions, Does.Not.Contain(HostHealthAction.NotifyHostLobbyLost));
    }

    [Test]
    public void Evaluate_DoesNotWarn_WhenOnlyServerFlagIsMissing()
    {
        HostHealthDecision decision = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(
            localSteamId: "111",
            lobbyOwnerSteamId: "111",
            serverPlayerCount: 0));

        Assert.That(decision.Issues, Does.Not.Contain(HostHealthIssue.HostIdentityLost));
        Assert.That(decision.Actions, Does.Contain(HostHealthAction.RefreshLobbyMetadata));
    }

    [Test]
    public void Evaluate_DetectsHostMissingFromPlayerList_WithoutWarningDuringPanelChurn()
    {
        HostHealthDecision decision = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(
            lobbyOwnerSteamId: "111",
            playerSteamIds: new[] { "222", "333" }));

        Assert.That(decision.Issues, Does.Contain(HostHealthIssue.HostMissingFromPlayerList));
        Assert.That(decision.Actions, Does.Contain(HostHealthAction.RefreshLobbyMetadata));
    }

    [Test]
    public void Evaluate_DetectsEmptyHostPersona_AndRequestsPersonaRefresh()
    {
        HostHealthDecision decision = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(hostPersonaName: ""));

        Assert.That(decision.Issues, Does.Contain(HostHealthIssue.HostPersonaEmpty));
        Assert.That(decision.Actions, Does.Contain(HostHealthAction.RequestPersonaRefresh));
    }

    [Test]
    public void Evaluate_DoesNotWarn_WhenOnlySignalIsLongRunningLobbyAge()
    {
        HostHealthDecision decision = HostHealthPolicy.Evaluate(HostHealthInput.DefaultForTests(
            lobbyAgeHours: 13,
            warnAfterHours: 12));

        Assert.That(decision.Issues, Is.Empty);
        Assert.That(decision.Actions, Does.Contain(HostHealthAction.RefreshLobbyMetadata));
    }

    [TestCase(10, 60)]
    [TestCase(300, 300)]
    [TestCase(5000, 3600)]
    public void ClampIntervalSeconds_KeepsHostHealthCadenceBounded(int value, int expected)
    {
        Assert.That(HostHealthPolicy.ClampIntervalSeconds(value), Is.EqualTo(expected));
    }
}
