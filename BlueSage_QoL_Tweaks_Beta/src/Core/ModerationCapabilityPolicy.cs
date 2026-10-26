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
        return IsFreshWithVersion(value, expectedOwnerSteamId, Version, nowUnixSeconds);
    }

    // Round 4 Stage I: the network-carried Helper path is advertised under its OWN lobby-data key with its own
    // version. The v3 string above is matched exactly by every 0.2.5 Helper; changing it would lock them out of
    // every new host. Old hosts never publish this key, so a new Helper on an old host keeps the honest "rejoin" path.
    private const string NetworkTransportVersion = "net-v1-chat";

    internal static string BuildNetworkTransport(string ownerSteamId, long issuedUnixSeconds)
    {
        return (ownerSteamId ?? string.Empty).Trim() + "|" + NetworkTransportVersion + "|" + issuedUnixSeconds;
    }

    internal static bool IsNetworkTransportFresh(string value, string expectedOwnerSteamId, long nowUnixSeconds)
    {
        return IsFreshWithVersion(value, expectedOwnerSteamId, NetworkTransportVersion, nowUnixSeconds);
    }

    private static bool IsFreshWithVersion(string value, string expectedOwnerSteamId, string expectedVersion, long nowUnixSeconds)
    {
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 3 || !string.Equals(parts[0], expectedOwnerSteamId, StringComparison.Ordinal) ||
            !string.Equals(parts[1], expectedVersion, StringComparison.Ordinal) || !long.TryParse(parts[2], out long issued))
        {
            return false;
        }

        long age = nowUnixSeconds - issued;
        return age >= -MaxFutureClockSkewSeconds && age <= MaxLeaseAgeSeconds;
    }
}
