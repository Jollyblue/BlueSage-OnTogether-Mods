#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal static class CurrentLobbyHelperPolicy
{
    internal static string[] FilterCurrent(
        IReadOnlyCollection<string> publishedHelperSteamIds,
        IReadOnlyCollection<string> currentLobbyMemberSteamIds)
    {
        var members = new HashSet<string>(
            currentLobbyMemberSteamIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        return (publishedHelperSteamIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value) && members.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
