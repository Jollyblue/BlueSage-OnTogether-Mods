using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal static class UnifiedHelperPolicy
{
    internal static string MergeForOwner(
        string ownerSteamId,
        string canonicalDelegates,
        string canonicalOwnerSteamId,
        string legacyLobbyDelegates,
        string legacyLobbyOwnerSteamId,
        string legacyModerationDelegates,
        string legacyModerationOwnerSteamId)
    {
        if (!IsSteamId(ownerSteamId)) return string.Empty;

        var merged = new HashSet<string>(StringComparer.Ordinal);
        AddScoped(merged, ownerSteamId, canonicalDelegates, canonicalOwnerSteamId);
        AddScoped(merged, ownerSteamId, legacyLobbyDelegates, legacyLobbyOwnerSteamId);
        AddScoped(merged, ownerSteamId, legacyModerationDelegates, legacyModerationOwnerSteamId);
        merged.Remove(ownerSteamId);
        return LobbyPingAuthorizationPolicy.SerializeDelegates(merged);
    }

    private static void AddScoped(HashSet<string> target, string ownerSteamId, string values, string scopedOwnerSteamId)
    {
        if (!string.Equals(ownerSteamId, scopedOwnerSteamId, StringComparison.Ordinal)) return;
        foreach (string value in LobbyPingAuthorizationPolicy.ParseDelegates(values)) target.Add(value);
    }

    private static bool IsSteamId(string value) => ulong.TryParse(value, out ulong parsed) && parsed > 0UL;
}
