using System;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal sealed class IdentityResyncRequest
{
    internal int Version;
    internal string LobbyFingerprint = string.Empty;
    internal long Generation;
    internal string CapabilityFingerprint = string.Empty;
    internal long IssuedUnixSeconds;
    internal string Nonce = string.Empty;
}

internal static class IdentityResyncRequestProtocol
{
    internal const string MemberDataKey = "bluesage_idsync_request_v1";
    internal const int MaximumPayloadBytes = 256;
    internal const long TimeToLiveSeconds = 15L;
    private const long FutureSkewSeconds = 2L;

    internal static bool TryBuild(
        string lobbyFingerprint,
        long generation,
        string capabilityFingerprint,
        long issuedUnixSeconds,
        string nonce,
        out string payload,
        out string error)
    {
        payload = string.Empty;
        error = string.Empty;
        if (!IsFingerprint(lobbyFingerprint) ||
            !IsFingerprint(capabilityFingerprint) ||
            generation <= 0L ||
            issuedUnixSeconds <= 0L ||
            !IsNonce(nonce))
        {
            error = "Identity-resync request fields were invalid.";
            return false;
        }

        payload = string.Join(
            "|",
            "v1",
            "request",
            lobbyFingerprint,
            generation.ToString(CultureInfo.InvariantCulture),
            capabilityFingerprint,
            issuedUnixSeconds.ToString(CultureInfo.InvariantCulture),
            nonce);
        if (Encoding.UTF8.GetByteCount(payload) > MaximumPayloadBytes)
        {
            payload = string.Empty;
            error = "Identity-resync request exceeded the 256-byte member-data limit.";
            return false;
        }
        return true;
    }

    internal static bool TryParse(
        string payload,
        out IdentityResyncRequest request,
        out string error)
    {
        request = new IdentityResyncRequest();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(payload) ||
            Encoding.UTF8.GetByteCount(payload) > MaximumPayloadBytes)
        {
            error = "Identity-resync request was empty or oversized.";
            return false;
        }

        string[] parts = payload.Split('|');
        if (parts.Length != 7 ||
            !string.Equals(parts[0], "v1", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "request", StringComparison.Ordinal) ||
            !IsFingerprint(parts[2]) ||
            !long.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long generation) ||
            generation <= 0L ||
            !IsFingerprint(parts[4]) ||
            !long.TryParse(
                parts[5],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long issued) ||
            issued <= 0L ||
            !IsNonce(parts[6]))
        {
            error = "Identity-resync request format or version was invalid.";
            return false;
        }

        request = new IdentityResyncRequest
        {
            Version = 1,
            LobbyFingerprint = parts[2],
            Generation = generation,
            CapabilityFingerprint = parts[4],
            IssuedUnixSeconds = issued,
            Nonce = parts[6],
        };
        return true;
    }

    internal static bool IsFresh(
        IdentityResyncRequest request,
        long nowUnixSeconds)
    {
        return request != null &&
               request.IssuedUnixSeconds <= nowUnixSeconds + FutureSkewSeconds &&
               nowUnixSeconds - request.IssuedUnixSeconds <= TimeToLiveSeconds;
    }

    internal static string FingerprintScope(
        string lobbyIdentity,
        string ownerIdentity)
    {
        return Fingerprint(
            (lobbyIdentity ?? string.Empty) +
            "|" +
            (ownerIdentity ?? string.Empty));
    }

    internal static string FingerprintCapability(string capability)
    {
        return Fingerprint(capability ?? string.Empty);
    }

    private static string Fingerprint(string value)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }
    }

    private static bool IsFingerprint(string value)
    {
        if (value == null || value.Length != 16)
        {
            return false;
        }
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool digit = character >= '0' && character <= '9';
            bool upperHex = character >= 'A' && character <= 'F';
            if (!digit && !upperHex)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsNonce(string value)
    {
        if (value == null || value.Length != 16)
        {
            return false;
        }
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }
        }
        return true;
    }
}
