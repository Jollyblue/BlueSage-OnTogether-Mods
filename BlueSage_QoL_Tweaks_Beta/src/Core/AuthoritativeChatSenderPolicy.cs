using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal static class AuthoritativeChatSenderPolicy
{
    public static bool ContainsLobbyPingToken(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (Match match in MentionNamePolicy.MentionReferenceRegex.Matches(message))
        {
            if (!string.Equals(match.Groups[1].Value, "lobby", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match.Index == 0)
            {
                return true;
            }

            char preceding = message[match.Index - 1];
            if (!char.IsLetterOrDigit(preceding) && preceding != '_' && preceding != '@')
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveUniqueIndex<T>(T senderPlayerId, IReadOnlyList<T> playerIds, out int index)
    {
        index = -1;
        if (playerIds == null || playerIds.Count == 0)
        {
            return false;
        }

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int candidateIndex = 0; candidateIndex < playerIds.Count; candidateIndex++)
        {
            if (!comparer.Equals(senderPlayerId, playerIds[candidateIndex]))
            {
                continue;
            }

            if (index >= 0)
            {
                index = -1;
                return false;
            }

            index = candidateIndex;
        }

        return index >= 0;
    }
}
