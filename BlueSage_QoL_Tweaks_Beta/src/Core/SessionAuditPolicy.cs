#nullable enable
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal static class SessionAuditPolicy
{
    internal const int MaximumAuditFieldCharacters = 8192;
    private static readonly Regex RichTextTagPattern = new Regex("<[^>]*>", RegexOptions.Compiled);

    internal static string ToPlainText(string? value)
    {
        string plain = RichTextTagPattern.Replace(value ?? string.Empty, string.Empty);
        return Limit(plain.Replace("\r", " ").Replace("\n", " ").Trim());
    }

    internal static string BuildChatRecord(
        string utc,
        string channel,
        int senderIndex,
        string identityState,
        string observerAuthority,
        string senderRole,
        string steamId64,
        string displayNameRaw,
        string rosterNameRaw,
        string steamPersona,
        string messageRaw,
        string lobbyId)
    {
        return "{" +
               Field("schema", "1") + "," +
               Field("utc", utc) + "," +
               Field("event", "chat") + "," +
               Field("captureStage", "rendered-ui") + "," +
               Field("channel", channel) + "," +
               NumberField("senderIndex", senderIndex) + "," +
               Field("identityState", identityState) + "," +
               Field("observerAuthority", observerAuthority) + "," +
               Field("senderRole", senderRole) + "," +
               Field("steamId64", steamId64) + "," +
               Field("displayNameRaw", displayNameRaw) + "," +
               Field("displayNamePlain", ToPlainText(displayNameRaw)) + "," +
               Field("rosterNameRaw", rosterNameRaw) + "," +
               Field("rosterNamePlain", ToPlainText(rosterNameRaw)) + "," +
               Field("steamPersona", steamPersona) + "," +
               Field("messageRaw", messageRaw) + "," +
               Field("messagePlain", ToPlainText(messageRaw)) + "," +
               Field("lobbyId", lobbyId) +
               "}";
    }

    internal static string BuildNotificationRecord(string utc, string textRaw, string lobbyId)
    {
        return "{" +
               Field("schema", "1") + "," +
               Field("utc", utc) + "," +
               Field("event", "notification") + "," +
               Field("textRaw", textRaw) + "," +
               Field("textPlain", ToPlainText(textRaw)) + "," +
               Field("lobbyId", lobbyId) +
               "}";
    }

    internal static string BuildRunMarkerRecord(
        string utc,
        string classification,
        string ageBucket,
        string markerName)
    {
        return "{" +
               Field("schema", "1") + "," +
               Field("utc", utc) + "," +
               Field("event", "run-marker") + "," +
               Field("classification", classification) + "," +
               Field("ageBucket", ageBucket) + "," +
               Field("markerName", markerName) +
               "}";
    }

    internal static string BuildModerationRecord(
        string utc,
        string action,
        string source,
        string outcome,
        string steamId64,
        string displayNameRaw,
        string steamPersona,
        string lobbyId,
        string issuerSteamId64 = "",
        string incidentId = "",
        string targetRole = "")
    {
        return "{" +
               Field("schema", "1") + "," +
               Field("utc", utc) + "," +
               Field("event", "moderation") + "," +
               Field("action", action) + "," +
               Field("source", source) + "," +
               Field("outcome", outcome) + "," +
               Field("steamId64", steamId64) + "," +
               Field("displayNameRaw", displayNameRaw) + "," +
               Field("displayNamePlain", ToPlainText(displayNameRaw)) + "," +
               Field("steamPersona", steamPersona) + "," +
               Field("issuerSteamId64", issuerSteamId64) + "," +
               Field("incidentId", incidentId) + "," +
               Field("targetRole", targetRole) + "," +
               Field("lobbyId", lobbyId) +
               "}";
    }

    private static string Field(string name, string? value)
    {
        return "\"" + name + "\":\"" + Escape(Limit(value ?? string.Empty)) + "\"";
    }

    private static string NumberField(string name, int value)
    {
        return "\"" + name + "\":" + value;
    }

    private static string Limit(string value)
    {
        return value.Length <= MaximumAuditFieldCharacters
            ? value
            : value.Substring(0, MaximumAuditFieldCharacters);
    }

    private static string Escape(string value)
    {
        var result = new StringBuilder(value.Length + 16);
        foreach (char character in value)
        {
            switch (character)
            {
                case '\"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (character < 0x20) result.Append("\\u").Append(((int)character).ToString("X4"));
                    else result.Append(character);
                    break;
            }
        }
        return result.ToString();
    }
}
