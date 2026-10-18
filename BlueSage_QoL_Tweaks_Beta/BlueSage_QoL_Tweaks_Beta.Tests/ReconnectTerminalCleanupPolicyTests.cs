using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReconnectTerminalCleanupPolicyTests
{
    [Test]
    public void TerminalCleanupReadiness_IgnoresBlueSageJoinGuardActivity()
    {
        var state = CleanMenuState(blueSageJoinIdle: false);

        Assert.That(ReconnectTerminalCleanupPolicy.IsReady(state), Is.True);
    }

    [Test]
    public void NewJoinReadiness_StillRequiresBlueSageJoinGuardToBeIdle()
    {
        var state = CleanMenuState(blueSageJoinIdle: false);

        Assert.That(ReconnectTerminalCleanupPolicy.IsReadyForNewJoin(state), Is.False);
    }

    [Test]
    public void NativeReturn_IsSkipped_WhenTerminalMenuStateIsAlreadyClean()
    {
        var state = CleanMenuState(blueSageJoinIdle: false);

        bool shouldRequestNativeReturn = !ReconnectTerminalCleanupPolicy.IsReady(state);

        Assert.That(shouldRequestNativeReturn, Is.False);
    }

    [Test]
    public void NativeReturn_IsRequested_WhenLobbyHasNotCleared()
    {
        var state = new ReconnectMenuReadinessState(
            sceneLoaded: true,
            activeSceneBuildIndex: 1,
            menuAvailable: true,
            transportStopped: true,
            lobbyCleared: false,
            blueSageJoinIdle: false);

        bool shouldRequestNativeReturn = !ReconnectTerminalCleanupPolicy.IsReady(state);

        Assert.That(shouldRequestNativeReturn, Is.True);
    }

    private static ReconnectMenuReadinessState CleanMenuState(bool blueSageJoinIdle)
    {
        return new ReconnectMenuReadinessState(
            sceneLoaded: true,
            activeSceneBuildIndex: 1,
            menuAvailable: true,
            transportStopped: true,
            lobbyCleared: true,
            blueSageJoinIdle: blueSageJoinIdle);
    }
}
