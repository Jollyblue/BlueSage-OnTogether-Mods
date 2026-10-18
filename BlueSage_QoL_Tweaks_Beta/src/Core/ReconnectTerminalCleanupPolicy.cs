namespace BlueSage.QoLTweaks.Core;

internal readonly struct ReconnectMenuReadinessState
{
    public ReconnectMenuReadinessState(
        bool sceneLoaded,
        int activeSceneBuildIndex,
        bool menuAvailable,
        bool transportStopped,
        bool lobbyCleared,
        bool blueSageJoinIdle)
    {
        SceneLoaded = sceneLoaded;
        ActiveSceneBuildIndex = activeSceneBuildIndex;
        MenuAvailable = menuAvailable;
        TransportStopped = transportStopped;
        LobbyCleared = lobbyCleared;
        BlueSageJoinIdle = blueSageJoinIdle;
    }

    public bool SceneLoaded { get; }
    public int ActiveSceneBuildIndex { get; }
    public bool MenuAvailable { get; }
    public bool TransportStopped { get; }
    public bool LobbyCleared { get; }
    public bool BlueSageJoinIdle { get; }
}

internal static class ReconnectTerminalCleanupPolicy
{
    public static bool IsReady(ReconnectMenuReadinessState state)
    {
        return IsNativeMenuReady(state);
    }

    public static bool IsReadyForNewJoin(ReconnectMenuReadinessState state)
    {
        return IsNativeMenuReady(state) && state.BlueSageJoinIdle;
    }

    private static bool IsNativeMenuReady(ReconnectMenuReadinessState state)
    {
        return state.SceneLoaded &&
               state.ActiveSceneBuildIndex == 1 &&
               state.MenuAvailable &&
               state.TransportStopped &&
               state.LobbyCleared;
    }
}
