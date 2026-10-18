namespace BlueSage.QoLTweaks.Core;

internal enum ReconnectRuntimeHealthAction
{
    KeepWaiting,
    ConfirmSuccess,
    ReturnToMenu,
}

internal sealed class ReconnectRuntimeHealthDecision
{
    public ReconnectRuntimeHealthDecision(ReconnectRuntimeHealthAction action, string reason)
    {
        Action = action;
        Reason = reason;
    }

    public ReconnectRuntimeHealthAction Action { get; }
    public string Reason { get; }
}

internal sealed class ReconnectRuntimeHealthInput
{
    public ReconnectRuntimeHealthInput(
        bool clientTransportConnected,
        bool requireVisibleLobby,
        bool visibleLobbyConfirmed,
        bool localUserIsAuthoritativeLobbyMember,
        bool gameRuntimeUsable,
        bool voiceRuntimeReady,
        int consecutiveReadySamples,
        float? secondsSinceClientTransportConnected,
        int postConnectValidationSeconds,
        float readyStableSeconds = 0f,
        int requiredReadyStabilitySeconds = 2)
    {
        ClientTransportConnected = clientTransportConnected;
        RequireVisibleLobby = requireVisibleLobby;
        VisibleLobbyConfirmed = visibleLobbyConfirmed;
        LocalUserIsAuthoritativeLobbyMember = localUserIsAuthoritativeLobbyMember;
        GameRuntimeUsable = gameRuntimeUsable;
        VoiceRuntimeReady = voiceRuntimeReady;
        ConsecutiveReadySamples = consecutiveReadySamples;
        SecondsSinceClientTransportConnected = secondsSinceClientTransportConnected;
        PostConnectValidationSeconds = postConnectValidationSeconds;
        ReadyStableSeconds = readyStableSeconds;
        RequiredReadyStabilitySeconds = requiredReadyStabilitySeconds;
    }

    public bool ClientTransportConnected { get; }
    public bool RequireVisibleLobby { get; }
    public bool VisibleLobbyConfirmed { get; }
    public bool LocalUserIsAuthoritativeLobbyMember { get; }
    public bool GameRuntimeUsable { get; }
    public bool VoiceRuntimeReady { get; }
    public int ConsecutiveReadySamples { get; }
    public float? SecondsSinceClientTransportConnected { get; }
    public int PostConnectValidationSeconds { get; }
    public float ReadyStableSeconds { get; }
    public int RequiredReadyStabilitySeconds { get; }
}

internal static class ReconnectRuntimeHealthPolicy
{
    public static ReconnectRuntimeHealthDecision Evaluate(ReconnectRuntimeHealthInput input)
    {
        if (!input.ClientTransportConnected)
        {
            return new ReconnectRuntimeHealthDecision(
                ReconnectRuntimeHealthAction.KeepWaiting,
                "Client transport is not connected yet.");
        }

        bool lobbyReady = !input.RequireVisibleLobby || input.VisibleLobbyConfirmed;
        bool quorumReady = lobbyReady &&
                           input.LocalUserIsAuthoritativeLobbyMember &&
                           input.GameRuntimeUsable;
        int requiredStableSeconds = Clamp(input.RequiredReadyStabilitySeconds, 1, 10);
        if (quorumReady && input.ConsecutiveReadySamples >= 3 && input.ReadyStableSeconds >= requiredStableSeconds)
        {
            return new ReconnectRuntimeHealthDecision(
                ReconnectRuntimeHealthAction.ConfirmSuccess,
                input.VoiceRuntimeReady
                    ? "Client transport, authoritative Steam membership, local player runtime, and voice state are stable."
                    : "Client transport, authoritative Steam membership, and local player runtime are stable; voice is still unavailable, so the playable session is preserved instead of being disconnected.");
        }

        int validationSeconds = Clamp(input.PostConnectValidationSeconds, 3, 30);
        if (input.SecondsSinceClientTransportConnected.HasValue &&
            input.SecondsSinceClientTransportConnected.Value >= validationSeconds)
        {
            return new ReconnectRuntimeHealthDecision(
                ReconnectRuntimeHealthAction.ReturnToMenu,
                $"Client transport connected, but authoritative lobby/player/voice state did not converge within {validationSeconds}s.");
        }

        return new ReconnectRuntimeHealthDecision(
            ReconnectRuntimeHealthAction.KeepWaiting,
            !input.LocalUserIsAuthoritativeLobbyMember
                ? "Client transport is connected, but the local user is not an authoritative Steam lobby member; rejecting ghost-member state."
                : "Client transport is connected; waiting for lobby, local player, and voice readiness to remain stable before declaring reconnect success.");
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
