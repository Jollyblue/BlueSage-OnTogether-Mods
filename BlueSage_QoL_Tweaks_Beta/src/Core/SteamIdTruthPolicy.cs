#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum SteamIdTruthState
{
    InvalidClaim,
    LocalSelf,
    TransportVerified,
    TransportMismatch,
    HostAttestedVerified,
    HostAttestedMismatch,
    LobbyCorroborated,
    Unconfirmed,
}

internal readonly struct SteamIdTruthRow
{
    internal SteamIdTruthRow(string purrNetId, string claimedSteamId, SteamIdTruthState state)
    {
        PurrNetId = purrNetId ?? string.Empty;
        ClaimedSteamId = claimedSteamId ?? string.Empty;
        State = state;
    }

    internal string PurrNetId { get; }
    internal string ClaimedSteamId { get; }
    internal SteamIdTruthState State { get; }
}

internal readonly struct SteamIdTruthAttestationEntry
{
    internal SteamIdTruthAttestationEntry(string claimedSteamId, bool verified)
    {
        ClaimedSteamId = claimedSteamId;
        Verified = verified;
    }

    internal string ClaimedSteamId { get; }
    internal bool Verified { get; }
}

// Vanilla's roster SteamID is self-reported by each joining client (UpdateOnNewPeopleJoin sends
// SteamUser.GetSteamID() as a string), so it is a claim, not proof. On the host, PurrNet's
// SteamTransport records each connection's Steam-authenticated SteamID; that is the strongest
// evidence and a mismatch is a spoof that no other evidence can override. Helpers receive the
// host's verdicts by attestation. Everyone else falls back to unique Steam-lobby membership.
internal static class SteamIdTruthPolicy
{
    internal static SteamIdTruthState Classify(
        string? claimedSteamId,
        bool isLocalPlayer,
        bool transportAvailable,
        ulong transportSteamId,
        bool isUniqueCurrentLobbyMember)
    {
        if (!TryParseClaim(claimedSteamId, out ulong claimed))
        {
            return SteamIdTruthState.InvalidClaim;
        }

        if (isLocalPlayer)
        {
            return SteamIdTruthState.LocalSelf;
        }

        if (transportAvailable)
        {
            // No Steam identity for this connection (yet) means transport cannot vouch; lobby
            // membership must not stand in for it, or a spoofer claiming a not-yet-loaded member's
            // SID would pass. Callers switch transportAvailable off on a systemic transport failure.
            if (transportSteamId == 0UL)
            {
                return SteamIdTruthState.Unconfirmed;
            }

            return transportSteamId == claimed
                ? SteamIdTruthState.TransportVerified
                : SteamIdTruthState.TransportMismatch;
        }

        return isUniqueCurrentLobbyMember
            ? SteamIdTruthState.LobbyCorroborated
            : SteamIdTruthState.Unconfirmed;
    }

    internal static SteamIdTruthState ClassifyFromHostAttestation(
        string? claimedSteamId,
        string? purrNetId,
        IReadOnlyDictionary<string, SteamIdTruthAttestationEntry>? attestation,
        bool isUniqueCurrentLobbyMember)
    {
        if (!TryParseClaim(claimedSteamId, out _))
        {
            return SteamIdTruthState.InvalidClaim;
        }

        if (attestation != null &&
            attestation.TryGetValue(purrNetId ?? string.Empty, out SteamIdTruthAttestationEntry entry))
        {
            // The host saw this connection claim a different SID than this client sees: fail closed.
            if (!string.Equals(entry.ClaimedSteamId, claimedSteamId, StringComparison.Ordinal))
            {
                return SteamIdTruthState.Unconfirmed;
            }

            return entry.Verified
                ? SteamIdTruthState.HostAttestedVerified
                : SteamIdTruthState.HostAttestedMismatch;
        }

        return isUniqueCurrentLobbyMember
            ? SteamIdTruthState.LobbyCorroborated
            : SteamIdTruthState.Unconfirmed;
    }

    internal static bool IsTrusted(SteamIdTruthState state)
    {
        return state == SteamIdTruthState.LocalSelf ||
               state == SteamIdTruthState.TransportVerified ||
               state == SteamIdTruthState.HostAttestedVerified ||
               state == SteamIdTruthState.LobbyCorroborated;
    }

    internal static bool IsSpoofed(SteamIdTruthState state)
    {
        return state == SteamIdTruthState.TransportMismatch ||
               state == SteamIdTruthState.HostAttestedMismatch;
    }

    private static bool TryParseClaim(string? claimedSteamId, out ulong claimed)
    {
        claimed = 0UL;
        return ModerationProtocolPolicy.IsSteamId64(claimedSteamId ?? string.Empty) &&
               ulong.TryParse(claimedSteamId, NumberStyles.None, CultureInfo.InvariantCulture, out claimed);
    }
}

// Wire format: sidtruth1|<ownerSteamId>|<issuedUnixSeconds>|<purrNetId>=<claimedSid>:<V|X>;...
// Only transport verdicts are relayed; lobby-corroborated rows are something every client sees.
internal static class SteamIdTruthAttestationPolicy
{
    internal const string Version = "sidtruth1";
    // Steam caps a lobby data value at 8192 bytes; stay clear of it.
    internal const int MaxValueLength = 8000;
    internal const long MaxLeaseAgeSeconds = 20;
    private const long MaxFutureClockSkewSeconds = 5;

    internal static string Build(string ownerSteamId, long issuedUnixSeconds, IEnumerable<SteamIdTruthRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(Version).Append('|')
            .Append(ownerSteamId).Append('|')
            .Append(issuedUnixSeconds.ToString(CultureInfo.InvariantCulture)).Append('|');
        bool first = true;
        foreach (SteamIdTruthRow row in rows ?? Array.Empty<SteamIdTruthRow>())
        {
            char verdict;
            if (row.State == SteamIdTruthState.TransportVerified) verdict = 'V';
            else if (row.State == SteamIdTruthState.TransportMismatch) verdict = 'X';
            else continue;

            string entry = (first ? string.Empty : ";") + row.PurrNetId + "=" + row.ClaimedSteamId + ":" + verdict;
            if (builder.Length + entry.Length > MaxValueLength)
            {
                break;
            }
            builder.Append(entry);
            first = false;
        }
        return builder.ToString();
    }

    internal static bool TryParse(
        string? value,
        string expectedOwnerSteamId,
        long nowUnixSeconds,
        out Dictionary<string, SteamIdTruthAttestationEntry> entries)
    {
        entries = new Dictionary<string, SteamIdTruthAttestationEntry>(StringComparer.Ordinal);
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 4 ||
            !string.Equals(parts[0], Version, StringComparison.Ordinal) ||
            !ModerationProtocolPolicy.IsSteamId64(expectedOwnerSteamId ?? string.Empty) ||
            !string.Equals(parts[1], expectedOwnerSteamId, StringComparison.Ordinal) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long issued))
        {
            return false;
        }

        long age = nowUnixSeconds - issued;
        if (age < -MaxFutureClockSkewSeconds || age > MaxLeaseAgeSeconds)
        {
            return false;
        }

        if (parts[3].Length == 0)
        {
            return true;
        }

        foreach (string item in parts[3].Split(';'))
        {
            int equals = item.IndexOf('=');
            int colon = item.LastIndexOf(':');
            if (equals <= 0 || colon <= equals + 1 || colon != item.Length - 2)
            {
                entries.Clear();
                return false;
            }

            string purrNetId = item.Substring(0, equals);
            string claimed = item.Substring(equals + 1, colon - equals - 1);
            char verdict = item[item.Length - 1];
            if (!ulong.TryParse(purrNetId, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedId) ||
                parsedId == 0UL ||
                !ModerationProtocolPolicy.IsSteamId64(claimed) ||
                (verdict != 'V' && verdict != 'X') ||
                entries.ContainsKey(purrNetId))
            {
                entries.Clear();
                return false;
            }

            entries[purrNetId] = new SteamIdTruthAttestationEntry(claimed, verdict == 'V');
        }
        return true;
    }
}
