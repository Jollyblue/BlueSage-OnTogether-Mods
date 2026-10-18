using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal sealed class CommunityBanAddition
{
    internal CommunityBanAddition(string steamId, string nickname)
    {
        SteamId = steamId;
        Nickname = nickname;
    }

    internal string SteamId { get; }
    internal string Nickname { get; }
}

internal static class CommunityBanListMergePolicy
{
    internal const int MaxRemoteRows = 1024;
    private const int MaxNicknameLength = 64;
    private const string FallbackNickname = "Community list";

    internal static bool TryBuildAdditions(
        IReadOnlyList<string> localSteamIds,
        IReadOnlyList<string> localNicknames,
        IReadOnlyList<string> remoteSteamIds,
        IReadOnlyList<string> remoteNicknames,
        out CommunityBanAddition[] additions,
        out string error)
    {
        additions = Array.Empty<CommunityBanAddition>();
        error = string.Empty;

        if (!ArePaired(localSteamIds, localNicknames))
        {
            error = "Local native ban storage paired lists are unavailable or inconsistent.";
            return false;
        }

        if (!ArePaired(remoteSteamIds, remoteNicknames))
        {
            error = "Community BanData paired lists are unavailable or inconsistent.";
            return false;
        }

        if (remoteSteamIds.Count > MaxRemoteRows)
        {
            error = "Community BanData row count exceeds the validation limit.";
            return false;
        }

        HashSet<string> seenRemote = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < remoteSteamIds.Count; index++)
        {
            string steamId = (remoteSteamIds[index] ?? string.Empty).Trim();
            if (!IsSteamId64(steamId))
            {
                error = "Community BanData contains an invalid SteamID64 row.";
                return false;
            }

            if (!seenRemote.Add(steamId))
            {
                error = "Community BanData contains a duplicate SteamID64 row.";
                return false;
            }
        }

        HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < localSteamIds.Count; index++)
        {
            string local = (localSteamIds[index] ?? string.Empty).Trim();
            if (local.Length > 0) existing.Add(local);
        }

        List<CommunityBanAddition> missing = new List<CommunityBanAddition>();
        for (int index = 0; index < remoteSteamIds.Count; index++)
        {
            string steamId = (remoteSteamIds[index] ?? string.Empty).Trim();
            if (existing.Contains(steamId)) continue;

            missing.Add(new CommunityBanAddition(steamId, SanitizeNickname(remoteNicknames[index])));
        }

        additions = missing.ToArray();
        return true;
    }

    private static bool ArePaired(IReadOnlyList<string> steamIds, IReadOnlyList<string> nicknames)
    {
        return steamIds != null && nicknames != null && steamIds.Count == nicknames.Count;
    }

    private static bool IsSteamId64(string value)
    {
        return value != null &&
               value.Length == 17 &&
               value.StartsWith("7656119", StringComparison.Ordinal) &&
               ulong.TryParse(value, out _);
    }

    private static string SanitizeNickname(string value)
    {
        string source = (value ?? string.Empty).Trim();
        StringBuilder clean = new StringBuilder(Math.Min(source.Length, MaxNicknameLength));
        bool previousWhitespace = false;
        for (int index = 0; index < source.Length && clean.Length < MaxNicknameLength; index++)
        {
            char character = source[index];
            if (IsUnsafeDisplayCharacter(character))
            {
                continue;
            }

            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (clean.Length > 0 && !previousWhitespace)
                {
                    clean.Append(' ');
                    previousWhitespace = true;
                }
                continue;
            }

            clean.Append(character);
            previousWhitespace = false;
        }

        string result = clean.ToString().Trim();
        return result.Length == 0 ? FallbackNickname : result;
    }

    private static bool IsUnsafeDisplayCharacter(char character)
    {
        return character == '<' ||
               character == '>' ||
               character == '\u061C' ||
               character == '\u200E' ||
               character == '\u200F' ||
               (character >= '\u202A' && character <= '\u202E') ||
               (character >= '\u2066' && character <= '\u2069') ||
               char.GetUnicodeCategory(character) == UnicodeCategory.Format;
    }
}
