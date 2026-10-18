using System;
using System.Text.RegularExpressions;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TextChannelManager), "AddMessageUI", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]
internal static class MentionHighlighterPatch
{
    private static readonly Regex MentionRegex = MentionNamePolicy.MentionReferenceRegex;

    [HarmonyPrefix]
    private static void Prefix(ref string text, ref string userName, bool isLocal, int senderIndex)
    {
        if (Plugin.EnablePingMentions == null ||
            !Plugin.EnablePingMentions.Value ||
            string.IsNullOrWhiteSpace(text) ||
            text.StartsWith("/", StringComparison.Ordinal) ||
            senderIndex < 0)
        {
            return;
        }

        try
        {
            string localName = PlayerMentionProvider.GetLocalPlayerName();
            bool shouldPing = false;
            string mentionColor = "#" + Plugin.GetPingMentionColor();

            text = MentionRegex.Replace(text, match =>
            {
                string token = match.Groups[1].Value;
                if (string.Equals(token, "lobby", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Plugin.IsLobbyMassPingAuthorized(ChatSenderContextPatch.CurrentSenderSteamId))
                    {
                        return match.Value;
                    }
                    shouldPing = true;
                    return "<size=115%><b><color=" + mentionColor + ">@lobby</color></b></size>";
                }

                string matchedPlayer = PlayerMentionProvider.FindBestMentionMatch(token);
                if (string.IsNullOrWhiteSpace(matchedPlayer))
                {
                    return match.Value;
                }

                string cleanMatched = MentionNamePolicy.ToMentionDisplayLabel(matchedPlayer);
                if (MentionNamePolicy.IsLocalMentionMatch(matchedPlayer, localName, token))
                {
                    shouldPing = true;
                }

                string displayToken = string.IsNullOrWhiteSpace(cleanMatched) ? MentionNamePolicy.ToMentionDisplayName(token) : cleanMatched;
                return "<size=115%><b><color=" + mentionColor + ">@" + displayToken + "</color></b></size>";
            });

            if (shouldPing)
            {
                text = "<mark=#" + Plugin.GetPingHighlightColor() + "30><b><color=" + mentionColor + ">[PING]</color></b> " + text + "</mark>";
                userName = "<b><color=" + mentionColor + ">" + userName + "</color></b>";
                MentionPingController.PlayConfiguredPing();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Ping Mentions skipped message highlight safely: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
