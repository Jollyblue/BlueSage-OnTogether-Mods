namespace BlueSage.QoLTweaks.Core;

internal sealed class PlayerLimitCompanionStatus
{
    private PlayerLimitCompanionStatus(
        bool isLoaded,
        int? effectiveMaxLobbySize,
        int? effectiveDefaultLobbySize,
        int? effectiveShiftSkipRate,
        bool? chatRelayPatchEnabled,
        int? lobbyMemberCount = null,
        int? lobbySteamMaxPlayers = null)
    {
        IsLoaded = isLoaded;
        LobbyMemberCount = lobbyMemberCount;
        LobbySteamMaxPlayers = lobbySteamMaxPlayers;
        EffectiveMaxLobbySize = effectiveMaxLobbySize;
        EffectiveDefaultLobbySize = effectiveDefaultLobbySize;
        EffectiveShiftSkipRate = effectiveShiftSkipRate;
        ChatRelayPatchEnabled = chatRelayPatchEnabled;
    }

    public bool IsLoaded { get; }
    public int? EffectiveMaxLobbySize { get; }
    public int? EffectiveDefaultLobbySize { get; }
    public int? EffectiveShiftSkipRate { get; }
    public bool? ChatRelayPatchEnabled { get; }
    public int? LobbyMemberCount { get; }
    // Raw Steam/PurrLobby MaxPlayers; the game (and PlayerLimitLift's panel) show MaxPlayers - 1.
    public int? LobbySteamMaxPlayers { get; }

    // The current lobby's real numbers (Blue, 2026-10-17: Host Health said cap=128 while the lobby
    // showed 1/111). EffectiveMaxLobbySize is PlayerLimitLift's ceiling, not this lobby's size.
    public PlayerLimitCompanionStatus WithLobby(int? memberCount, int? steamMaxPlayers)
    {
        return new PlayerLimitCompanionStatus(
            IsLoaded,
            EffectiveMaxLobbySize,
            EffectiveDefaultLobbySize,
            EffectiveShiftSkipRate,
            ChatRelayPatchEnabled,
            memberCount,
            steamMaxPlayers);
    }

    public static PlayerLimitCompanionStatus NotLoaded()
    {
        return new PlayerLimitCompanionStatus(false, null, null, null, null);
    }

    public static PlayerLimitCompanionStatus FromLoadedValues(
        int? effectiveMaxLobbySize,
        int? effectiveDefaultLobbySize,
        int? effectiveShiftSkipRate,
        bool? chatRelayPatchEnabled)
    {
        return new PlayerLimitCompanionStatus(
            true,
            effectiveMaxLobbySize,
            effectiveDefaultLobbySize,
            effectiveShiftSkipRate,
            chatRelayPatchEnabled);
    }

    public string ToDiagnosticText()
    {
        if (!IsLoaded)
        {
            return "PlayerLimit=not loaded";
        }

        return $"PlayerLimit=loaded {FormatLobby()} pllMax={FormatInt(EffectiveMaxLobbySize)} default={FormatInt(EffectiveDefaultLobbySize)} shift={FormatInt(EffectiveShiftSkipRate)} chatRelay={FormatBool(ChatRelayPatchEnabled)}";
    }

    private string FormatLobby()
    {
        if (!LobbyMemberCount.HasValue && !LobbySteamMaxPlayers.HasValue)
        {
            return "lobby=?";
        }

        int? visibleMax = LobbySteamMaxPlayers.HasValue && LobbySteamMaxPlayers.Value >= 2
            ? LobbySteamMaxPlayers.Value - 1
            : (int?)null;
        return "lobby=" + FormatInt(LobbyMemberCount) + "/" + FormatInt(visibleMax);
    }

    private static string FormatInt(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "?";
    }

    private static string FormatBool(bool? value)
    {
        if (!value.HasValue)
        {
            return "?";
        }

        return value.Value ? "on" : "off";
    }
}
