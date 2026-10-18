using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct CompatibleClientPresence
{
    internal CompatibleClientPresence(int version, long publishedUnixSeconds)
    {
        Version = version;
        PublishedUnixSeconds = publishedUnixSeconds;
    }

    internal int Version { get; }
    internal long PublishedUnixSeconds { get; }
}

internal static class CompatibleClientPresencePolicy
{
    internal const string MemberDataKey = "bluesage_qol_client_v1";
    internal const int ProtocolVersion = 1;
    // The large-lobby presentation pass intentionally runs at a relaxed cadence.
    // Keep a self-published QoL marker visible through one delayed pass; this
    // remains presentation-only and never grants identity-sensitive authority.
    internal const long FreshnessSeconds = 15;
    internal const long MaximumFutureSkewSeconds = 3;
    private const int MaximumPayloadCharacters = 32;

    internal static bool TryBuild(
        long nowUnixSeconds,
        out string payload)
    {
        payload = string.Empty;
        if (nowUnixSeconds <= 0)
        {
            return false;
        }

        payload = ProtocolVersion.ToString(CultureInfo.InvariantCulture) +
                  "|" +
                  nowUnixSeconds.ToString(CultureInfo.InvariantCulture);
        return payload.Length <= MaximumPayloadCharacters;
    }

    internal static bool IsFresh(
        string payload,
        long nowUnixSeconds)
    {
        return TryParse(payload, nowUnixSeconds, out _);
    }

    internal static bool TryParse(
        string payload,
        long nowUnixSeconds,
        out CompatibleClientPresence presence)
    {
        presence = default;
        if (nowUnixSeconds <= 0 ||
            string.IsNullOrWhiteSpace(payload) ||
            payload.Length > MaximumPayloadCharacters)
        {
            return false;
        }

        string[] parts = payload.Split('|');
        if (parts.Length != 2 ||
            !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int version) ||
            version != ProtocolVersion ||
            !long.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long published) ||
            published <= 0)
        {
            return false;
        }

        long age = nowUnixSeconds - published;
        if (age > FreshnessSeconds || age < -MaximumFutureSkewSeconds)
        {
            return false;
        }

        presence = new CompatibleClientPresence(version, published);
        return true;
    }
}
