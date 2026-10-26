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

    // Round 5c (Blue, 2026-10-23): "QoL vX.Y.Z User". The version travels on its own Steam lobby member-data
    // key so every 0.2.3-0.2.7 client keeps parsing the v1 heartbeat unchanged (its parser rejects a third
    // field). A peer without the key (0.2.3-0.2.7) is shown as a pre-indicator user (Blue's wording); nothing
    // older than that can be told apart.
    internal const string VersionMemberDataKey = "bluesage_qol_client_version";
    internal const string OlderPeerLabel = "Pre-indicator QoL User";
    private const int MaximumVersionCharacters = 16;

    internal static bool TryBuildVersionPayload(string pluginVersion, out string payload)
    {
        payload = string.Empty;
        if (!IsPlainVersion(pluginVersion))
        {
            return false;
        }

        payload = pluginVersion;
        return true;
    }

    internal static bool TryParseVersion(string payload, out string version)
    {
        version = string.Empty;
        if (!IsPlainVersion(payload))
        {
            return false;
        }

        version = payload;
        return true;
    }

    // The long badge text for a compatible peer: its own reported version, never the local one.
    internal static string LongLabel(string peerVersion)
    {
        return IsPlainVersion(peerVersion) ? "QoL v" + peerVersion + " User" : OlderPeerLabel;
    }

    private static bool IsPlainVersion(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumVersionCharacters)
        {
            return false;
        }

        string[] parts = value.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        foreach (string part in parts)
        {
            if (part.Length == 0 || part.Length > 4)
            {
                return false;
            }

            foreach (char c in part)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
        }

        return true;
    }
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
