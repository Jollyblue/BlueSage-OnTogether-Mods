using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum IdentityResyncTrigger
{
    None,
    RowInitialized,
    RowDisabledOrDespawned,
    LobbyOrOwnerIdentityChanged,
    PanelReplacement,
    AuthorityMembershipChanged,
}

internal readonly struct IdentityResyncRuntimeShape
{
    internal IdentityResyncRuntimeShape(
        string lobbyFingerprint,
        string ownerFingerprint,
        int panelToken,
        int activeRows,
        long rowSignature,
        string steamMembershipFingerprint,
        string purrNetMembershipFingerprint)
    {
        LobbyFingerprint = lobbyFingerprint ?? string.Empty;
        OwnerFingerprint = ownerFingerprint ?? string.Empty;
        PanelToken = panelToken;
        ActiveRows = Math.Max(0, activeRows);
        RowSignature = rowSignature;
        SteamMembershipFingerprint = steamMembershipFingerprint ?? string.Empty;
        PurrNetMembershipFingerprint = purrNetMembershipFingerprint ?? string.Empty;
    }

    internal string LobbyFingerprint { get; }
    internal string OwnerFingerprint { get; }
    internal int PanelToken { get; }
    internal int ActiveRows { get; }
    internal long RowSignature { get; }
    internal string SteamMembershipFingerprint { get; }
    internal string PurrNetMembershipFingerprint { get; }
    internal bool Available =>
        LobbyFingerprint.Length > 0 &&
        OwnerFingerprint.Length > 0;
}

internal static class IdentityResyncTriggerPolicy
{
    internal static bool AdvancesGeneration(IdentityResyncTrigger trigger)
    {
        return trigger == IdentityResyncTrigger.RowInitialized ||
               trigger == IdentityResyncTrigger.RowDisabledOrDespawned ||
               trigger == IdentityResyncTrigger.LobbyOrOwnerIdentityChanged ||
               trigger == IdentityResyncTrigger.PanelReplacement ||
               trigger == IdentityResyncTrigger.AuthorityMembershipChanged;
    }

    internal static IdentityResyncTrigger Classify(
        IdentityResyncRuntimeShape before,
        IdentityResyncRuntimeShape after)
    {
        if (!after.Available)
        {
            return IdentityResyncTrigger.None;
        }
        if (!before.Available ||
            !string.Equals(
                before.LobbyFingerprint,
                after.LobbyFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                before.OwnerFingerprint,
                after.OwnerFingerprint,
                StringComparison.Ordinal))
        {
            return IdentityResyncTrigger.LobbyOrOwnerIdentityChanged;
        }
        if (before.PanelToken != after.PanelToken)
        {
            return IdentityResyncTrigger.PanelReplacement;
        }
        if (!string.Equals(
                before.SteamMembershipFingerprint,
                after.SteamMembershipFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                before.PurrNetMembershipFingerprint,
                after.PurrNetMembershipFingerprint,
                StringComparison.Ordinal))
        {
            return IdentityResyncTrigger.AuthorityMembershipChanged;
        }
        if (after.ActiveRows < before.ActiveRows)
        {
            return IdentityResyncTrigger.RowDisabledOrDespawned;
        }
        if (after.ActiveRows > before.ActiveRows ||
            after.RowSignature != before.RowSignature)
        {
            return IdentityResyncTrigger.RowInitialized;
        }
        return IdentityResyncTrigger.None;
    }
}

internal static class IdentityAuthorityFingerprintPolicy
{
    internal static string Build(IEnumerable<string> identities)
    {
        string canonical = string.Join(
            "|",
            (identities ?? Enumerable.Empty<string>())
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => value.Length > 0)
                .OrderBy(value => value, StringComparer.Ordinal));
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            byte[] bytes = Encoding.UTF8.GetBytes(canonical);
            for (int index = 0; index < bytes.Length; index++)
            {
                hash ^= bytes[index];
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }
    }
}
