using System;
using System.Collections.Generic;
using System.Globalization;
using BlueSage.QoLTweaks.Core;
using Steamworks;

namespace BlueSage.QoLTweaks;

// Host/Helper heads-up toasts for QoL players. Senders write their OWN Steam lobby member data
// (Steam lets only that member write it), receivers trust it only from the current owner or a
// Helper the owner published in owner-only lobby data. Players without QoL never read the key.
internal static class LobbyAnnouncementController
{
    private const float PollIntervalSeconds = 2f;
    private static readonly HashSet<string> SeenAnnouncements = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> LastAcceptedBySender = new Dictionary<string, long>(StringComparer.Ordinal);
    private static string _seenLobbyKey = string.Empty;
    private static float _nextPollAt;
    private static long _lastSentUnixSeconds;
    private static string _motdShownForVisit = string.Empty;

    internal static string Send(string rawText)
    {
        string text = LobbyAnnouncementPolicy.Sanitize(rawText);
        if (text.Length == 0)
        {
            return "Announce: add a short message, like /announce Restarting in 5 minutes.";
        }

        LobbySafetyAccessState access = SteamIdModerationController.GetLocalAccessState(out bool isHost, out _);
        if (access != LobbySafetyAccessState.Host && access != LobbySafetyAccessState.HelperReady)
        {
            return "Announce: only the lobby host or a Helper the host assigned can send announcements.";
        }

        long now = SteamNowUnixSeconds();
        if (!LobbyAnnouncementPolicy.CanSend(_lastSentUnixSeconds, now))
        {
            return "Announce: please wait " + LobbyAnnouncementPolicy.MinSecondsBetweenSends + " seconds between announcements.";
        }

        if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil)
        {
            return "Announce: you need to be in a lobby first.";
        }

        string nonce = Guid.NewGuid().ToString("N").Substring(0, 16);
        SteamMatchmaking.SetLobbyMemberData(lobby, LobbyAnnouncementPolicy.MemberDataKey, LobbyAnnouncementPolicy.Build(now, nonce, text));
        _lastSentUnixSeconds = now;
        string localSteamId = ((ulong)SteamUser.GetSteamID()).ToString(CultureInfo.InvariantCulture);
        SeenAnnouncements.Add(localSteamId + "|" + nonce);
        Plugin.Instance?.ShowHostAnnouncementToast(LobbyAnnouncementPolicy.FormatToast(isHost, text));
        return "Announcement sent to everyone with BlueSage QoL: " + text;
    }

    // Host MOTD: once per lobby visit (a rejoin is a new visit). Waits until the owner's MOTD has
    // arrived in lobby data, so a slow lobby-data sync never loses the message.
    private static void ShowHostMotdOncePerVisit(CSteamID lobby, string owner)
    {
        // The host set it; it is for the people joining.
        string localSteamId = ((ulong)SteamUser.GetSteamID()).ToString(CultureInfo.InvariantCulture);
        if (string.Equals(owner, localSteamId, StringComparison.Ordinal))
        {
            return;
        }

        if (!Plugin.TryGetCurrentLobbyVisitKey(out string visitKey) ||
            !LobbyMotdPolicy.ShouldShow(visitKey, _motdShownForVisit) ||
            !LobbyMotdPolicy.TryParse(SteamMatchmaking.GetLobbyData(lobby, SteamIdModerationController.MotdKey), owner, out string motd))
        {
            return;
        }

        _motdShownForVisit = visitKey;
        string line = LobbyMotdPolicy.FormatToast(motd);
        Plugin.Instance?.ShowHostAnnouncementToast(line);
        Plugin.AddLocalNotification(line);
    }

    // Steam's server clock on both sender and receivers, so PC clock drift cannot hide or delay notices.
    private static long SteamNowUnixSeconds()
    {
        try
        {
            uint serverTime = SteamUtils.GetServerRealTime();
            if (serverTime != 0U)
            {
                return serverTime;
            }
        }
        catch (Exception)
        {
            // Steam not ready: fall back below.
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    internal static void Poll(float now)
    {
        if (now < _nextPollAt)
        {
            return;
        }
        _nextPollAt = now + PollIntervalSeconds;

        try
        {
            if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil || !SteamManager.Initialized)
            {
                return;
            }

            string lobbyKey = ((ulong)lobby).ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(_seenLobbyKey, lobbyKey, StringComparison.Ordinal))
            {
                _seenLobbyKey = lobbyKey;
                SeenAnnouncements.Clear();
                LastAcceptedBySender.Clear();
            }

            string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString(CultureInfo.InvariantCulture);
            ShowHostMotdOncePerVisit(lobby, owner);
            HashSet<string> helpers = SteamIdModerationController.GetPublishedHelpers(lobby, owner);
            long nowUnix = SteamNowUnixSeconds();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < memberCount; index++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, index);
                string senderSteamId = ((ulong)member).ToString(CultureInfo.InvariantCulture);
                bool senderIsHost = string.Equals(senderSteamId, owner, StringComparison.Ordinal);
                if (!senderIsHost && !helpers.Contains(senderSteamId))
                {
                    continue;
                }

                string payload = SteamMatchmaking.GetLobbyMemberData(lobby, member, LobbyAnnouncementPolicy.MemberDataKey);
                if (!LobbyAnnouncementPolicy.TryParse(payload, nowUnix, out LobbyAnnouncement announcement) ||
                    !SeenAnnouncements.Add(senderSteamId + "|" + announcement.Nonce))
                {
                    continue;
                }

                // Receiver-side throttle: a modified Helper client could ignore the send limit.
                LastAcceptedBySender.TryGetValue(senderSteamId, out long lastAccepted);
                if (!LobbyAnnouncementPolicy.ShouldAcceptFromSender(lastAccepted, nowUnix))
                {
                    continue;
                }
                LastAcceptedBySender[senderSteamId] = nowUnix;

                string line = LobbyAnnouncementPolicy.FormatToast(senderIsHost, announcement.Text);
                Plugin.Instance?.ShowHostAnnouncementToast(line);
                Plugin.AddLocalNotification(line);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Lobby announcement poll skipped: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
