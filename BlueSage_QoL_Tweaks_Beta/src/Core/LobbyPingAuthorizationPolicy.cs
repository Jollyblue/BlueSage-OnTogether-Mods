using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal static class LobbyPingAuthorizationPolicy
{
    public static string[] ParseDelegates(string persisted)
    {
        return (persisted ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => ulong.TryParse(value, out ulong parsed) && parsed > 0UL)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static string SerializeDelegates(IEnumerable<string> values) => string.Join(",", ParseDelegates(string.Join(",", values ?? Array.Empty<string>())));

    public static bool IsAuthorized(string senderSteamId, string ownerSteamId, string persistedDelegates, string persistedDelegateOwnerSteamId, bool hasLobby)
    {
        if (!hasLobby || !ulong.TryParse(senderSteamId, out ulong sender) || sender == 0UL)
        {
            return false;
        }

        return string.Equals(senderSteamId, ownerSteamId, StringComparison.Ordinal) ||
               (string.Equals(ownerSteamId, persistedDelegateOwnerSteamId, StringComparison.Ordinal) &&
                ParseDelegates(persistedDelegates).Contains(senderSteamId, StringComparer.Ordinal));
    }
}
