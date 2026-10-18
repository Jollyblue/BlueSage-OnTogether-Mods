#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal static class IdentityEvidenceGenerationPolicy
{
    internal static bool TryBuildFingerprint(
        ulong lobbyId,
        ulong ownerSteamId,
        IReadOnlyList<string> nativeSteamIds,
        IReadOnlyList<string> nativePurrNetIds,
        IReadOnlyList<int> transformInstanceIds,
        IReadOnlyList<string> lobbyMemberSteamIds,
        ulong localSteamId,
        IReadOnlyList<string> helperSteamIds,
        bool coverageComplete,
        int liveSteamCheckCount,
        int leftStaleCount,
        int ambiguousCount,
        out string fingerprint)
    {
        fingerprint = string.Empty;
        if (lobbyId == 0UL ||
            ownerSteamId == 0UL ||
            localSteamId == 0UL ||
            nativeSteamIds == null ||
            nativePurrNetIds == null ||
            transformInstanceIds == null ||
            lobbyMemberSteamIds == null ||
            helperSteamIds == null ||
            nativeSteamIds.Count == 0 ||
            nativeSteamIds.Count != nativePurrNetIds.Count ||
            nativeSteamIds.Count != transformInstanceIds.Count ||
            lobbyMemberSteamIds.Count == 0 ||
            lobbyMemberSteamIds.Any(string.IsNullOrWhiteSpace) ||
            lobbyMemberSteamIds.Distinct(StringComparer.Ordinal).Count() !=
                lobbyMemberSteamIds.Count ||
            helperSteamIds.Any(string.IsNullOrWhiteSpace) ||
            helperSteamIds.Distinct(StringComparer.Ordinal).Count() !=
                helperSteamIds.Count ||
            transformInstanceIds.Any(value => value == 0) ||
            liveSteamCheckCount < 0 ||
            leftStaleCount < 0 ||
            ambiguousCount < 0)
        {
            return false;
        }

        var canonical = new StringBuilder(256);
        Append(
            canonical,
            "lobby",
            lobbyId.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            "owner",
            ownerSteamId.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            "local",
            localSteamId.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            "coverage",
            coverageComplete ? "1" : "0");
        Append(
            canonical,
            "live-native-only",
            liveSteamCheckCount.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            "left-stale",
            leftStaleCount.ToString(CultureInfo.InvariantCulture));
        Append(
            canonical,
            "ambiguous",
            ambiguousCount.ToString(CultureInfo.InvariantCulture));
        for (int index = 0; index < nativeSteamIds.Count; index++)
        {
            Append(
                canonical,
                "row",
                index.ToString(CultureInfo.InvariantCulture));
            Append(canonical, "steam", nativeSteamIds[index]);
            Append(canonical, "purr", nativePurrNetIds[index]);
            Append(
                canonical,
                "live",
                transformInstanceIds[index].ToString(
                    CultureInfo.InvariantCulture));
        }

        foreach (string member in
                 lobbyMemberSteamIds.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            Append(canonical, "member", member);
        }

        foreach (string helper in
                 helperSteamIds.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            Append(canonical, "helper", helper);
        }

        fingerprint = canonical.ToString();
        return fingerprint.Length > 0;
    }

    internal static bool TryObserve(
        string acceptedFingerprint,
        long acceptedGeneration,
        bool acceptedValid,
        string observedFingerprint,
        bool observedValid,
        out string nextFingerprint,
        out long nextGeneration,
        out bool nextValid,
        out bool changed,
        out bool requiresEpochAdvance)
    {
        nextFingerprint = acceptedFingerprint ?? string.Empty;
        nextGeneration = acceptedGeneration;
        nextValid = false;
        changed = false;
        requiresEpochAdvance = false;
        if (acceptedGeneration < 0L ||
            (observedValid &&
             string.IsNullOrWhiteSpace(observedFingerprint)))
        {
            return false;
        }

        if (acceptedValid == observedValid &&
            (!observedValid ||
             (acceptedGeneration > 0L &&
              string.Equals(
                  acceptedFingerprint,
                  observedFingerprint,
                  StringComparison.Ordinal))))
        {
            nextValid = acceptedValid;
            return true;
        }

        if (acceptedGeneration == long.MaxValue)
        {
            requiresEpochAdvance = true;
            return false;
        }

        if (observedValid &&
            string.Equals(
                acceptedFingerprint,
                observedFingerprint,
                StringComparison.Ordinal))
        {
            nextFingerprint = observedFingerprint;
        }
        else if (observedValid)
        {
            nextFingerprint = observedFingerprint;
        }

        nextGeneration =
            Math.Max(1L, acceptedGeneration + 1L);
        nextValid = observedValid;
        changed = true;
        return true;
    }

    private static void Append(
        StringBuilder destination,
        string label,
        string value)
    {
        destination.Append(label);
        destination.Append(':');
        destination.Append(value.Length.ToString(
            CultureInfo.InvariantCulture));
        destination.Append(':');
        destination.Append(value);
        destination.Append(';');
    }
}
