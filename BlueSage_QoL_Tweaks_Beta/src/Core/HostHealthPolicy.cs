#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal enum HostHealthIssue
{
    LobbyOwnerInvalid,
    HostIdentityLost,
    HostMissingFromPlayerList,
    HostPersonaEmpty,
}

internal enum HostHealthAction
{
    RefreshLobbyMetadata,
    RequestPersonaRefresh,
}

internal sealed class HostHealthInput
{
    public HostHealthInput(
        bool isHost,
        bool lobbyValid,
        string localSteamId,
        string lobbyOwnerSteamId,
        IEnumerable<string> playerSteamIds,
        int serverPlayerCount,
        string hostPersonaName,
        double lobbyAgeHours,
        double warnAfterHours)
    {
        IsHost = isHost;
        LobbyValid = lobbyValid;
        LocalSteamId = localSteamId ?? string.Empty;
        LobbyOwnerSteamId = lobbyOwnerSteamId ?? string.Empty;
        PlayerSteamIds = (playerSteamIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        ServerPlayerCount = serverPlayerCount;
        HostPersonaName = hostPersonaName ?? string.Empty;
        LobbyAgeHours = lobbyAgeHours;
        WarnAfterHours = warnAfterHours;
    }

    public bool IsHost { get; }
    public bool LobbyValid { get; }
    public string LocalSteamId { get; }
    public string LobbyOwnerSteamId { get; }
    public IReadOnlyList<string> PlayerSteamIds { get; }
    public int ServerPlayerCount { get; }
    public string HostPersonaName { get; }
    public double LobbyAgeHours { get; }
    public double WarnAfterHours { get; }

    public bool IsHostMachine =>
        IsHost ||
        (!IsInvalidSteamId(LocalSteamId) &&
         !IsInvalidSteamId(LobbyOwnerSteamId) &&
         string.Equals(LocalSteamId, LobbyOwnerSteamId, StringComparison.Ordinal));

    public static HostHealthInput DefaultForTests(
        bool isHost = true,
        bool lobbyValid = true,
        string localSteamId = "111",
        string lobbyOwnerSteamId = "111",
        IEnumerable<string>? playerSteamIds = null,
        int serverPlayerCount = 1,
        string hostPersonaName = "Blue",
        double lobbyAgeHours = 1,
        double warnAfterHours = 12)
    {
        return new HostHealthInput(
            isHost,
            lobbyValid,
            localSteamId,
            lobbyOwnerSteamId,
            playerSteamIds ?? new[] { lobbyOwnerSteamId },
            serverPlayerCount,
            hostPersonaName,
            lobbyAgeHours,
            warnAfterHours);
    }

    private static bool IsInvalidSteamId(string steamId)
    {
        return string.IsNullOrWhiteSpace(steamId) || steamId == "0";
    }
}

internal sealed class HostHealthDecision
{
    public HostHealthDecision(bool shouldRunDiagnostics, IReadOnlyList<HostHealthIssue> issues, IReadOnlyList<HostHealthAction> actions)
    {
        ShouldRunDiagnostics = shouldRunDiagnostics;
        Issues = issues;
        Actions = actions;
    }

    public bool ShouldRunDiagnostics { get; }
    public IReadOnlyList<HostHealthIssue> Issues { get; }
    public IReadOnlyList<HostHealthAction> Actions { get; }
    public bool IsHealthy => ShouldRunDiagnostics && Issues.Count == 0;
}

internal static class HostHealthPolicy
{
    public static HostHealthDecision Evaluate(HostHealthInput input)
    {
        if (input == null || !input.IsHostMachine)
        {
            return new HostHealthDecision(false, Array.Empty<HostHealthIssue>(), Array.Empty<HostHealthAction>());
        }

        List<HostHealthIssue> issues = new();
        HashSet<HostHealthAction> actions = new();

        actions.Add(HostHealthAction.RefreshLobbyMetadata);

        bool lobbyOwnerInvalid = !input.LobbyValid || string.IsNullOrWhiteSpace(input.LobbyOwnerSteamId) || input.LobbyOwnerSteamId == "0";
        if (lobbyOwnerInvalid)
        {
            issues.Add(HostHealthIssue.LobbyOwnerInvalid);
        }

        if (!lobbyOwnerInvalid && !input.PlayerSteamIds.Contains(input.LobbyOwnerSteamId))
        {
            issues.Add(HostHealthIssue.HostMissingFromPlayerList);
        }

        if (!lobbyOwnerInvalid &&
            !issues.Contains(HostHealthIssue.HostMissingFromPlayerList) &&
            string.IsNullOrWhiteSpace(input.HostPersonaName))
        {
            issues.Add(HostHealthIssue.HostPersonaEmpty);
            actions.Add(HostHealthAction.RequestPersonaRefresh);
        }

        return new HostHealthDecision(true, issues, actions.ToArray());
    }

    public static int ClampIntervalSeconds(int value)
    {
        if (value < 60)
        {
            return 60;
        }

        return value > 3600 ? 3600 : value;
    }
}
