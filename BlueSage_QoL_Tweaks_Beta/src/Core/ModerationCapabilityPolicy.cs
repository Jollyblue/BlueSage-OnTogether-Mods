using System;

namespace BlueSage.QoLTweaks.Core;

internal static class ModerationCapabilityPolicy
{
    internal const long MaxLeaseAgeSeconds = 20;
    private const long MaxFutureClockSkewSeconds = 5;
    // v3 is the first capability generation whose protected Helper requests
    // use Steam lobby member-data instead of chat-shaped RPC payloads.
    private const string Version = "v3-memberdata";

    internal static string Build(string ownerSteamId, long issuedUnixSeconds)
    {
        return (ownerSteamId ?? string.Empty).Trim() + "|" + Version + "|" + issuedUnixSeconds;
    }

    internal static bool IsFresh(string value, string expectedOwnerSteamId, long nowUnixSeconds)
    {
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 3 || !string.Equals(parts[0], expectedOwnerSteamId, StringComparison.Ordinal) ||
            !string.Equals(parts[1], Version, StringComparison.Ordinal) || !long.TryParse(parts[2], out long issued))
        {
            return false;
        }

        long age = nowUnixSeconds - issued;
        return age >= -MaxFutureClockSkewSeconds && age <= MaxLeaseAgeSeconds;
    }
}
