#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal static class CompanionExactIdentityFingerprintPolicy
{
    internal static bool TryBuildFingerprint(
        ulong lobbyId,
        ulong ownerSteamId,
        ulong localSteamId,
        IReadOnlyList<string> lobbyMemberSteamIds,
        IReadOnlyList<string> helperSteamIds,
        IReadOnlyList<string> verifiedSteamIds,
        IReadOnlyList<ulong> verifiedPurrNetIds,
        IReadOnlyList<int> verifiedTransformInstanceIds,
        out string fingerprint)
    {
        return TryBuildFingerprint(
            lobbyId,
            ownerSteamId,
            localSteamId,
            lobbyMemberSteamIds,
            helperSteamIds,
            verifiedSteamIds,
            verifiedPurrNetIds,
            verifiedTransformInstanceIds,
            out fingerprint,
            out _);
    }

    internal static bool TryBuildFingerprint(
        ulong lobbyId,
        ulong ownerSteamId,
        ulong localSteamId,
        IReadOnlyList<string> lobbyMemberSteamIds,
        IReadOnlyList<string> helperSteamIds,
        IReadOnlyList<string> verifiedSteamIds,
        IReadOnlyList<ulong> verifiedPurrNetIds,
        IReadOnlyList<int> verifiedTransformInstanceIds,
        out string fingerprint,
        out string reason)
    {
        fingerprint = string.Empty;
        reason = string.Empty;
        if (lobbyId == 0UL ||
            ownerSteamId == 0UL ||
            localSteamId == 0UL ||
            lobbyMemberSteamIds == null ||
            helperSteamIds == null ||
            verifiedSteamIds == null ||
            verifiedPurrNetIds == null ||
            verifiedTransformInstanceIds == null ||
            lobbyMemberSteamIds.Count == 0 ||
            verifiedSteamIds.Count == 0 ||
            verifiedSteamIds.Count != verifiedPurrNetIds.Count ||
            verifiedSteamIds.Count != verifiedTransformInstanceIds.Count)
        {
            reason = "required lobby or exact-row identity data is missing or misaligned";
            return false;
        }

        var members = new HashSet<string>(
            lobbyMemberSteamIds,
            StringComparer.Ordinal);
        if (members.Count != lobbyMemberSteamIds.Count ||
            members.Any(string.IsNullOrWhiteSpace) ||
            !members.Contains(ownerSteamId.ToString(CultureInfo.InvariantCulture)) ||
            !members.Contains(localSteamId.ToString(CultureInfo.InvariantCulture)))
        {
            reason = "Steam lobby membership is invalid, duplicated, or missing the owner/local identity";
            return false;
        }

        var helpers = new HashSet<string>(helperSteamIds, StringComparer.Ordinal);
        if (helpers.Count != helperSteamIds.Count ||
            helpers.Any(string.IsNullOrWhiteSpace))
        {
            reason = "published helper identities are empty or duplicated";
            return false;
        }
        if (helpers.Any(helper => !members.Contains(helper)))
        {
            reason = "published helper is not a current Steam lobby member";
            return false;
        }

        var rows = new List<ExactRow>(verifiedSteamIds.Count);
        var steamIds = new HashSet<string>(StringComparer.Ordinal);
        var purrNetIds = new HashSet<ulong>();
        var transformIds = new HashSet<int>();
        for (int index = 0; index < verifiedSteamIds.Count; index++)
        {
            string steamId = verifiedSteamIds[index];
            ulong purrNetId = verifiedPurrNetIds[index];
            int transformId = verifiedTransformInstanceIds[index];
            if (string.IsNullOrWhiteSpace(steamId) ||
                !members.Contains(steamId) ||
                purrNetId == 0UL ||
                transformId == 0 ||
                !steamIds.Add(steamId) ||
                !purrNetIds.Add(purrNetId) ||
                !transformIds.Add(transformId))
            {
                reason = "verified Steam/PurrNet/native-transform tuples are invalid, duplicated, or outside the current lobby";
                return false;
            }
            rows.Add(new ExactRow(steamId, purrNetId, transformId));
        }

        var canonical = new StringBuilder(256);
        Append(canonical, "scope", "companion-exact-v1");
        Append(canonical, "lobby", lobbyId.ToString(CultureInfo.InvariantCulture));
        Append(canonical, "owner", ownerSteamId.ToString(CultureInfo.InvariantCulture));
        Append(canonical, "local", localSteamId.ToString(CultureInfo.InvariantCulture));
        foreach (string member in members.OrderBy(value => value, StringComparer.Ordinal))
        {
            Append(canonical, "member", member);
        }
        foreach (string helper in helpers.OrderBy(value => value, StringComparer.Ordinal))
        {
            Append(canonical, "helper", helper);
        }
        foreach (ExactRow row in rows.OrderBy(value => value.SteamId, StringComparer.Ordinal))
        {
            Append(canonical, "steam", row.SteamId);
            Append(canonical, "purr", row.PurrNetId.ToString(CultureInfo.InvariantCulture));
            Append(canonical, "transform", row.TransformInstanceId.ToString(CultureInfo.InvariantCulture));
        }

        fingerprint = canonical.ToString();
        if (fingerprint.Length == 0)
        {
            reason = "canonical exact-row identity fingerprint is empty";
            return false;
        }
        return true;
    }

    private static void Append(StringBuilder destination, string label, string value)
    {
        destination.Append(label);
        destination.Append(':');
        destination.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        destination.Append(':');
        destination.Append(value);
        destination.Append(';');
    }

    private readonly struct ExactRow
    {
        internal ExactRow(string steamId, ulong purrNetId, int transformInstanceId)
        {
            SteamId = steamId;
            PurrNetId = purrNetId;
            TransformInstanceId = transformInstanceId;
        }

        internal string SteamId { get; }
        internal ulong PurrNetId { get; }
        internal int TransformInstanceId { get; }
    }
}
