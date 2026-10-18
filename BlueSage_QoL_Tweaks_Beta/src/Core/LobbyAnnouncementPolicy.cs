#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct LobbyAnnouncement
{
    internal LobbyAnnouncement(long issuedUnixSeconds, string nonce, string text)
    {
        IssuedUnixSeconds = issuedUnixSeconds;
        Nonce = nonce;
        Text = text;
    }

    internal long IssuedUnixSeconds { get; }
    internal string Nonce { get; }
    internal string Text { get; }
}

// Host/Helper heads-up for QoL players (restarts, lobby breaks). Each sender writes the notice to
// their OWN Steam lobby member data, which only that member can write, so the sender's SteamID is
// Steam-authenticated; receivers show it only when that sender is the current owner or a Helper
// the owner published. Wire format: announce1|<issuedUnixSeconds>|<nonce>|<text>.
internal static class LobbyAnnouncementPolicy
{
    internal const string Version = "announce1";
    internal const string MemberDataKey = "bluesage_announce";
    internal const int MaxTextLength = 160;
    // Long enough for a player loading in to still see it; short enough not to replay old news.
    internal const long MaxAgeSeconds = 120;
    internal const long MinSecondsBetweenSends = 10;
    private const long MaxFutureClockSkewSeconds = 5;

    internal static string Sanitize(string? text)
    {
        var builder = new StringBuilder();
        bool pendingSpace = false;
        foreach (char c in text ?? string.Empty)
        {
            // '<' '>' would become rich-text tags in chat/TMP; '|' is the wire separator; format
            // characters (e.g. right-to-left overrides) could visually reorder the text.
            if (c == '<' || c == '>' || c == '|' ||
                CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                continue;
            }

            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                // A space needs a character after it; with no room left, stop here so the
                // output never ends on a space (ultra review, 2026-10-17).
                if (builder.Length + 1 >= MaxTextLength)
                {
                    break;
                }
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(c);
            if (builder.Length >= MaxTextLength)
            {
                // Never end on half an emoji: a split surrogate pair changes in transit and the
                // receiver would reject the whole message. Dropping it can expose a space, which
                // the receiver's own Sanitize would drop, so trim that too: output must be a
                // fixed point of Sanitize or TryParse rejects it.
                if (char.IsHighSurrogate(builder[builder.Length - 1]))
                {
                    builder.Length--;
                    while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
                    {
                        builder.Length--;
                    }
                }
                break;
            }
        }

        return builder.ToString();
    }

    internal static string Build(long issuedUnixSeconds, string nonce, string text)
    {
        return Version + "|" +
               issuedUnixSeconds.ToString(CultureInfo.InvariantCulture) + "|" +
               nonce + "|" +
               Sanitize(text);
    }

    internal static bool TryParse(string? value, long nowUnixSeconds, out LobbyAnnouncement announcement)
    {
        announcement = default;
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 4 ||
            !string.Equals(parts[0], Version, StringComparison.Ordinal) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long issued) ||
            !IsNonce(parts[2]) ||
            parts[3].Length == 0 ||
            !string.Equals(Sanitize(parts[3]), parts[3], StringComparison.Ordinal))
        {
            return false;
        }

        long age = nowUnixSeconds - issued;
        if (age < -MaxFutureClockSkewSeconds || age > MaxAgeSeconds)
        {
            return false;
        }

        announcement = new LobbyAnnouncement(issued, parts[2], parts[3]);
        return true;
    }

    internal static bool CanSend(long lastSentUnixSeconds, long nowUnixSeconds)
    {
        return lastSentUnixSeconds <= 0L ||
               nowUnixSeconds - lastSentUnixSeconds >= MinSecondsBetweenSends;
    }

    // Receiver-side throttle: the send limit above only binds honest senders.
    internal const long MinSecondsBetweenReceives = 5;

    internal static bool ShouldAcceptFromSender(long lastAcceptedUnixSeconds, long nowUnixSeconds)
    {
        return lastAcceptedUnixSeconds <= 0L ||
               nowUnixSeconds - lastAcceptedUnixSeconds >= MinSecondsBetweenReceives;
    }

    internal static string FormatToast(bool isHost, string text)
    {
        return (isHost ? "Host announcement: " : "Helper announcement: ") + text;
    }

    private static bool IsNonce(string value)
    {
        if (value.Length < 8 || value.Length > 32)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            {
                return false;
            }
        }
        return true;
    }
}

internal static class SteamIdTruthAlertPolicy
{
    // Alert Hosts/Helpers once per new mismatch, not on every snapshot while it persists.
    internal static bool ShouldAlert(int previousMismatchCount, int currentMismatchCount)
    {
        return currentMismatchCount > previousMismatchCount;
    }
}

// Host MOTD (Blue, 2026-10-17): the lobby owner publishes a short welcome in owner-only lobby data;
// QoL players see it once per lobby visit. A host who never sets one shows the friendly default.
// Wire format: motd1|<ownerSteamId>|<text>.
internal static class LobbyMotdPolicy
{
    internal const string Version = "motd1";
    internal const string DefaultMessage = "Welcome to my server!";

    internal static string Effective(string? configured)
    {
        string clean = LobbyAnnouncementPolicy.Sanitize(configured);
        return clean.Length == 0 ? DefaultMessage : clean;
    }

    internal static string Build(string ownerSteamId, string? configured)
    {
        return Version + "|" + ownerSteamId + "|" + Effective(configured);
    }

    internal static bool TryParse(string? value, string expectedOwnerSteamId, out string text)
    {
        text = string.Empty;
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 3 ||
            !string.Equals(parts[0], Version, StringComparison.Ordinal) ||
            !ModerationProtocolPolicy.IsSteamId64(expectedOwnerSteamId ?? string.Empty) ||
            !string.Equals(parts[1], expectedOwnerSteamId, StringComparison.Ordinal) ||
            parts[2].Length == 0 ||
            !string.Equals(LobbyAnnouncementPolicy.Sanitize(parts[2]), parts[2], StringComparison.Ordinal))
        {
            return false;
        }

        text = parts[2];
        return true;
    }

    internal static bool ShouldShow(string? currentVisitKey, string? lastShownVisitKey)
    {
        return !string.IsNullOrEmpty(currentVisitKey) &&
               !string.Equals(currentVisitKey, lastShownVisitKey, StringComparison.Ordinal);
    }

    internal static string FormatToast(string text)
    {
        return "Message from the host: " + text;
    }
}
