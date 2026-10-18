using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace BlueSage.QoLTweaks.Core;

internal static class PlayerMentionProvider
{
    private static readonly Regex RichTextTagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);

    public static string GetLocalPlayerName()
    {
        try
        {
            TextChannelManager textChannelManager = NetworkSingleton<TextChannelManager>.I;
            string name = StripRichText(textChannelManager?.UserName);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch
        {
        }

        try
        {
            return StripRichText(MonoSingleton<DataManager>.I?.PlayerData?.Name);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static IReadOnlyList<string> GetAllPlayerNames()
    {
        var names = new List<string>();

        try
        {
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            object idInfos = AccessTools.Field(typeof(PlayerPanelController), "IDInfos")?.GetValue(panel);
            if (idInfos is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    string name = ReadNameFromIdInfo(item);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Ping Mentions could not read PlayerPanelController.IDInfos: {ex.GetType().Name}: {ex.Message}");
        }

        return names
            .Select(MentionNamePolicy.ToMentionDisplayLabel)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string FindBestMentionMatch(string query)
    {
        string cleanQuery = MentionNamePolicy.ToCanonicalMentionName(query);
        if (string.IsNullOrWhiteSpace(cleanQuery))
        {
            return string.Empty;
        }

        return MentionNamePolicy.FindUniqueMentionMatch(cleanQuery, GetAllPlayerNames());
    }

    public static IReadOnlyList<string> FindMentionMatches(string query, int maxMatches)
    {
        string cleanQuery = MentionNamePolicy.ToCanonicalMentionName(query);
        if (string.IsNullOrWhiteSpace(cleanQuery) || maxMatches <= 0)
        {
            return Array.Empty<string>();
        }

        var matches = new List<string>();
        if (Plugin.CanUseLobbySafety(out _) && "lobby".StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase))
        {
            matches.Add("lobby");
        }

        IReadOnlyList<string> names = GetAllPlayerNames()
            .GroupBy(MentionNamePolicy.ToCanonicalMentionName, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
            .Select(group => group.Single())
            .ToArray();
        matches.AddRange(names.Where(name => MentionNamePolicy.ToCanonicalMentionName(name).StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase)));
        matches.AddRange(names.Where(name =>
            MentionNamePolicy.ToCanonicalMentionName(name).IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0 &&
            !matches.Contains(name, StringComparer.OrdinalIgnoreCase)));

        return matches
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxMatches)
            .ToArray();
    }

    public static string StripRichText(string value)
    {
        return RichTextTagRegex.Replace(value ?? string.Empty, string.Empty).Trim().Trim('\0');
    }

    private static string ReadNameFromIdInfo(object idInfo)
    {
        if (idInfo == null)
        {
            return string.Empty;
        }

        FieldInfo nameField = idInfo.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object value = nameField?.GetValue(idInfo);
        if (value is string text)
        {
            return text;
        }

        if (value is byte[] bytes && bytes.Length > 0)
        {
            return Encoding.Unicode.GetString(bytes).Trim('\0');
        }

        return string.Empty;
    }
}
