#nullable enable
using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct CloneIncidentResolutionRequest
{
    internal CloneIncidentResolutionRequest(string victimSteamId, string suspectSteamId, string stableId, long issuedUnixSeconds, string nonce)
    {
        VictimSteamId = victimSteamId;
        SuspectSteamId = suspectSteamId;
        StableId = stableId;
        IssuedUnixSeconds = issuedUnixSeconds;
        Nonce = nonce;
    }

    internal string VictimSteamId { get; }
    internal string SuspectSteamId { get; }
    internal string StableId { get; }
    internal long IssuedUnixSeconds { get; }
    internal string Nonce { get; }
}

internal static class CloneIncidentResolutionProtocolPolicy
{
    internal const string Prefix = "!bsincident:v1:resolve:";
    internal const string MemberDataKey = "bluesage_incident_request_v2";
    internal const string MemberDataPrefix = "v2|";
    internal const string LobbyDataKey = "bluesage_clone_resolution";
    internal const int RequestExpirySeconds = 15;
    internal const int LobbyResolutionExpirySeconds = 300;

    internal static string BuildRequest(string victimSteamId, string suspectSteamId, string stableId, long issuedUnixSeconds, string nonce)
    {
        return Prefix + victimSteamId + ":" + suspectSteamId + ":" + stableId + ":" + issuedUnixSeconds + ":" + nonce;
    }

    internal static string BuildMemberDataRequest(string victimSteamId, string suspectSteamId, string stableId, long issuedUnixSeconds, string nonce)
    {
        string legacy = BuildRequest(victimSteamId, suspectSteamId, stableId, issuedUnixSeconds, nonce);
        return TryParseRequest(legacy, out _) ? MemberDataPrefix + victimSteamId + "|" + suspectSteamId + "|" + stableId + "|" + issuedUnixSeconds + "|" + nonce : string.Empty;
    }

    internal static bool TryParseMemberDataRequest(string value, out CloneIncidentResolutionRequest request)
    {
        request = default;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(MemberDataPrefix, StringComparison.Ordinal)) return false;
        string[] parts = value.Substring(MemberDataPrefix.Length).Split('|');
        return parts.Length == 5 && TryParseRequest(Prefix + string.Join(":", parts), out request);
    }

    internal static bool TryParseRequest(string text, out CloneIncidentResolutionRequest request)
    {
        request = default;
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        string[] parts = text.Substring(Prefix.Length).Split(':');
        if (parts.Length != 5 ||
            !ModerationProtocolPolicy.IsSteamId64(parts[0]) ||
            !ModerationProtocolPolicy.IsSteamId64(parts[1]) ||
            string.Equals(parts[0], parts[1], StringComparison.Ordinal) ||
            !IsStableId(parts[2]) ||
            !long.TryParse(parts[3], out long issued) || issued <= 0 ||
            !IsNonce(parts[4])) return false;
        request = new CloneIncidentResolutionRequest(parts[0], parts[1], parts[2], issued, parts[4]);
        return true;
    }

    internal static bool IsFresh(CloneIncidentResolutionRequest request, long nowUnixSeconds)
    {
        long age = nowUnixSeconds - request.IssuedUnixSeconds;
        return age >= -2 && age <= RequestExpirySeconds;
    }

    internal static string BuildLobbyValue(string ownerSteamId, CloneIncidentResolutionRequest request)
    {
        return ownerSteamId + "|" + request.StableId + "|" + request.VictimSteamId + "|" + request.SuspectSteamId + "|" + request.IssuedUnixSeconds + "|" + request.Nonce;
    }

    internal static bool TryParseLobbyValue(string value, string expectedOwnerSteamId, long nowUnixSeconds, out CloneIncidentResolutionRequest request)
    {
        request = default;
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 6 || !string.Equals(parts[0], expectedOwnerSteamId, StringComparison.Ordinal)) return false;
        string requestText = Prefix + parts[2] + ":" + parts[3] + ":" + parts[1] + ":" + parts[4] + ":" + parts[5];
        if (!TryParseRequest(requestText, out request)) return false;
        long age = nowUnixSeconds - request.IssuedUnixSeconds;
        return age >= -2 && age <= LobbyResolutionExpirySeconds;
    }

    private static bool IsStableId(string value)
    {
        if (value == null || value.Length != 9 || value[0] != 'I') return false;
        for (int index = 1; index < value.Length; index++)
        {
            char c = value[index];
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
        }
        return true;
    }

    private static bool IsNonce(string value)
    {
        if (value == null || value.Length < 8 || value.Length > 24) return false;
        foreach (char c in value) if (!char.IsLetterOrDigit(c)) return false;
        return true;
    }
}
