using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct RescueRequest
{
    internal RescueRequest(string targetSteamId, string action, int seconds, long issuedUnixSeconds, string nonce)
        : this("steam", targetSteamId, action, seconds, issuedUnixSeconds, nonce)
    {
    }

    internal RescueRequest(string targetKind, string targetValue, string action, int seconds, long issuedUnixSeconds, string nonce)
    {
        TargetKind = targetKind ?? string.Empty;
        TargetValue = targetValue ?? string.Empty;
        Action = action;
        Seconds = seconds;
        IssuedUnixSeconds = issuedUnixSeconds;
        Nonce = nonce;
    }

    internal string Action { get; }
    internal int Seconds { get; }
    internal long IssuedUnixSeconds { get; }
    internal string Nonce { get; }
    internal string TargetKind { get; }
    internal string TargetValue { get; }
    internal string TargetSteamId => TargetKind == "steam" ? TargetValue : string.Empty;
    internal string TargetPlayerId => TargetKind == "pid" ? TargetValue : string.Empty;
}

internal enum RescueApplyResult
{
    Applied,
    Deferred,
    Rejected
}

internal static class RescueProtocolPolicy
{
    internal const string MemberDataKey = "bluesage_rescue_v2";
    internal const string MemberDataPrefix = "v2|";
    internal const string PlayerIdMemberDataPrefix = "v3|pid|";
    internal const int MaxHookSeconds = 30;
    internal const int RequestExpirySeconds = 15;
    internal const int CooldownSeconds = 1;

    internal static bool ShouldRememberNonce(RescueApplyResult result)
    {
        return result != RescueApplyResult.Deferred;
    }

    internal static bool IsFresh(RescueRequest request, long nowUnixSeconds)
    {
        long age = nowUnixSeconds - request.IssuedUnixSeconds;
        return age >= -2 && age <= RequestExpirySeconds;
    }

    internal static string BuildMemberData(string targetSteamId, string action, int seconds, long issuedUnixSeconds, string nonce)
    {
        if (!IsSteamId64(targetSteamId) || !IsAction(action) || issuedUnixSeconds <= 0 || !IsNonce(nonce)) return string.Empty;
        int bounded = action == "hook" ? Math.Max(1, Math.Min(MaxHookSeconds, seconds)) : action == "hooklong" ? int.MaxValue : 0;
        return MemberDataPrefix + targetSteamId + "|" + action + "|" + bounded + "|" + issuedUnixSeconds + "|" + nonce;
    }

    internal static string BuildPlayerIdMemberData(string targetPlayerId, string action, int seconds, long issuedUnixSeconds, string nonce)
    {
        if (!IsPlayerId(targetPlayerId) || !IsAction(action) || issuedUnixSeconds <= 0 || !IsNonce(nonce)) return string.Empty;
        int bounded = action == "hook" ? Math.Max(1, Math.Min(MaxHookSeconds, seconds)) : action == "hooklong" ? int.MaxValue : 0;
        return PlayerIdMemberDataPrefix + targetPlayerId + "|" + action + "|" + bounded + "|" + issuedUnixSeconds + "|" + nonce;
    }

    internal static bool TryParseMemberData(string value, out RescueRequest request)
    {
        request = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.StartsWith(MemberDataPrefix, StringComparison.Ordinal))
        {
            string[] parts = value.Substring(MemberDataPrefix.Length).Split('|');
            if (parts.Length != 5 || !IsSteamId64(parts[0]) || !IsAction(parts[1]) ||
                !int.TryParse(parts[2], out int seconds) || !long.TryParse(parts[3], out long issued) || issued <= 0 || !IsNonce(parts[4])) return false;
            seconds = parts[1] == "hook" ? Math.Max(1, Math.Min(MaxHookSeconds, seconds)) : parts[1] == "hooklong" ? int.MaxValue : 0;
            request = new RescueRequest(parts[0], parts[1], seconds, issued, parts[4]);
            return true;
        }

        if (!value.StartsWith(PlayerIdMemberDataPrefix, StringComparison.Ordinal)) return false;
        string[] playerParts = value.Substring(PlayerIdMemberDataPrefix.Length).Split('|');
        if (playerParts.Length != 5 || !IsPlayerId(playerParts[0]) || !IsAction(playerParts[1]) ||
            !int.TryParse(playerParts[2], out int playerSeconds) || !long.TryParse(playerParts[3], out long playerIssued) || playerIssued <= 0 || !IsNonce(playerParts[4])) return false;
        playerSeconds = playerParts[1] == "hook" ? Math.Max(1, Math.Min(MaxHookSeconds, playerSeconds)) : playerParts[1] == "hooklong" ? int.MaxValue : 0;
        request = new RescueRequest("pid", playerParts[0], playerParts[1], playerSeconds, playerIssued, playerParts[4]);
        return true;
    }

    internal static bool IsLocalTarget(RescueRequest request, string localSteamId, string localPlayerId)
    {
        return request.TargetKind == "steam"
            ? string.Equals(request.TargetValue, localSteamId, StringComparison.Ordinal)
            : request.TargetKind == "pid" && string.Equals(request.TargetValue, localPlayerId, StringComparison.Ordinal);
    }

    private static bool IsAction(string action) => action == "teleport" || action == "hook" || action == "hooklong" || action == "unhook";

    private static bool IsNonce(string nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce) || nonce.Length < 4 || nonce.Length > 16) return false;
        foreach (char c in nonce) if (!char.IsLetterOrDigit(c)) return false;
        return true;
    }

    private static bool IsSteamId64(string value)
    {
        return value != null && value.Length == 17 && value.StartsWith("7656119", StringComparison.Ordinal) && ulong.TryParse(value, out ulong parsed) && parsed > 0UL;
    }

    private static bool IsPlayerId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 32) return false;
        foreach (char character in value)
        {
            if (!char.IsLetterOrDigit(character) && character != ':' && character != '-' && character != '_') return false;
        }
        return true;
    }
}
