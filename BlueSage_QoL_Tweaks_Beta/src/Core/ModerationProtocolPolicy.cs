using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct ModerationRequest
{
    internal ModerationRequest(string action, string targetSteamId, string incidentId, long issuedUnixSeconds, string nonce)
    {
        Action = action;
        TargetSteamId = targetSteamId;
        IncidentId = incidentId;
        IssuedUnixSeconds = issuedUnixSeconds;
        Nonce = nonce;
    }

    internal string Action { get; }
    internal string TargetSteamId { get; }
    internal string IncidentId { get; }
    internal long IssuedUnixSeconds { get; }
    internal string Nonce { get; }
}

internal readonly struct ModerationConfirmationReadiness
{
    internal ModerationConfirmationReadiness(bool isReady, int secondsRemaining)
    {
        IsReady = isReady;
        SecondsRemaining = secondsRemaining;
    }

    internal bool IsReady { get; }
    internal int SecondsRemaining { get; }
}

internal static class ModerationProtocolPolicy
{
    internal const string Prefix = "!bsmod:v1:";
    internal const string MemberDataKey = "bluesage_moderation_request_v2";
    internal const string MemberDataPrefix = "v2|";
    internal const int RequestExpirySeconds = 15;

    internal static string Build(string action, string targetSteamId, string incidentId, long issuedUnixSeconds, string nonce)
    {
        return Prefix + action + ":" + targetSteamId + ":" + NormalizeIncident(incidentId) + ":" + issuedUnixSeconds + ":" + nonce;
    }

    internal static string BuildMemberData(string action, string targetSteamId, string incidentId, long issuedUnixSeconds, string nonce)
    {
        if ((action != "ban" && action != "unban") || !IsSteamId64(targetSteamId) ||
            !IsIncident(NormalizeIncident(incidentId)) || issuedUnixSeconds <= 0 || !IsNonce(nonce)) return string.Empty;
        return MemberDataPrefix + action + "|" + targetSteamId + "|" + NormalizeIncident(incidentId) + "|" + issuedUnixSeconds + "|" + nonce;
    }

    internal static bool TryParseMemberData(string value, out ModerationRequest request)
    {
        request = default;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(MemberDataPrefix, StringComparison.Ordinal)) return false;
        string[] parts = value.Substring(MemberDataPrefix.Length).Split('|');
        return parts.Length == 5 && TryParse(Prefix + string.Join(":", parts), out request);
    }

    internal static bool TryParse(string text, out ModerationRequest request)
    {
        request = default;
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string[] parts = text.Substring(Prefix.Length).Split(':');
        if (parts.Length != 5 || (parts[0] != "ban" && parts[0] != "unban"))
        {
            return false;
        }

        if (!IsSteamId64(parts[1]) || !IsIncident(parts[2]) || !long.TryParse(parts[3], out long issued) || issued <= 0)
        {
            return false;
        }

        string nonce = parts[4];
        if (nonce.Length < 8 || nonce.Length > 24)
        {
            return false;
        }

        foreach (char c in nonce)
        {
            if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        request = new ModerationRequest(parts[0], parts[1], parts[2], issued, nonce);
        return true;
    }

    internal static bool IsFresh(ModerationRequest request, long nowUnixSeconds)
    {
        long age = nowUnixSeconds - request.IssuedUnixSeconds;
        return age >= -2 && age <= RequestExpirySeconds;
    }

    internal static ModerationConfirmationReadiness EvaluateConfirmation(
        string requestedAction,
        string requestedTarget,
        string currentLobbyCode,
        long nowUnixSeconds,
        string pendingAction,
        string pendingTargetSteamId,
        string pendingIncidentId,
        string pendingLobbyCode,
        long pendingExpiresUnixSeconds)
    {
        bool targetMatches = string.Equals(requestedTarget, pendingTargetSteamId, StringComparison.Ordinal) ||
                             (!string.Equals(pendingIncidentId, "none", StringComparison.Ordinal) &&
                              string.Equals(requestedTarget, pendingIncidentId, StringComparison.OrdinalIgnoreCase));
        long remaining = pendingExpiresUnixSeconds - nowUnixSeconds;
        bool ready = !string.IsNullOrWhiteSpace(requestedAction) &&
                     !string.IsNullOrWhiteSpace(requestedTarget) &&
                     string.Equals(requestedAction, pendingAction, StringComparison.Ordinal) &&
                     targetMatches &&
                     string.Equals(currentLobbyCode ?? string.Empty, pendingLobbyCode ?? string.Empty, StringComparison.Ordinal) &&
                     remaining >= 0;

        return ready
            ? new ModerationConfirmationReadiness(true, (int)Math.Min(int.MaxValue, remaining))
            : new ModerationConfirmationReadiness(false, 0);
    }

    internal static bool IsSteamId64(string value)
    {
        return value != null && value.Length == 17 && ulong.TryParse(value, out ulong parsed) && parsed > 0UL;
    }

    private static bool IsIncident(string value)
    {
        if (string.Equals(value, "none", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrEmpty(value) || value.Length < 2 || value.Length > 8 || value[0] != 'C')
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            if (!char.IsDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeIncident(string value)
    {
        return IsIncident(value) ? value : "none";
    }

    private static bool IsNonce(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 8 || value.Length > 24) return false;
        foreach (char c in value) if (!char.IsLetterOrDigit(c)) return false;
        return true;
    }
}
