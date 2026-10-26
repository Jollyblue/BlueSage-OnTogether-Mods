#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using PurrNet;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal static class CloneIncidentActionController
{
    private const int MemberDataPollIntervalMilliseconds = 1000;
    private static readonly HashSet<string> SeenResolutionNonces = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Queue<string> ResolutionNonceOrder = new Queue<string>();
    private static readonly Dictionary<string, long> LastResolutionRequestByIssuer = new Dictionary<string, long>(StringComparer.Ordinal);
    private static string _lastLobbyResolutionNonce = string.Empty;
    private static long _nextMemberDataPollUnixMilliseconds;

    internal static void ResetLobbyState()
    {
        SeenResolutionNonces.Clear();
        ResolutionNonceOrder.Clear();
        LastResolutionRequestByIssuer.Clear();
        _lastLobbyResolutionNonce = string.Empty;
        _nextMemberDataPollUnixMilliseconds = 0;
    }

    internal static IReadOnlyList<CloneIncident> GetOpenIncidents()
    {
        if (!Plugin.CanUseLobbySafety(out _)) return Array.Empty<CloneIncident>();
        CloneIncidentLedger.Prune(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return CloneIncidentLedger.Snapshot
            .OrderByDescending(item => item.LastObservedUnixSeconds)
            .ToArray();
    }

    internal static IReadOnlyList<CloneIncident> GetIncidentHistory()
    {
        if (!Plugin.CanUseLobbySafety(out _)) return Array.Empty<CloneIncident>();
        CloneIncidentLedger.Prune(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return CloneIncidentLedger.HistorySnapshot
            .OrderByDescending(item => item.LastObservedUnixSeconds)
            .ToArray();
    }

    internal static bool TryCopySteamId(string incidentId, bool suspect, out string message)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            message = SteamIdModerationController.LobbySafetyDeniedMessage;
            return false;
        }

        if (!CloneIncidentLedger.TryResolveIncident(incidentId, out CloneIncident incident))
        {
            message = "That Clone Shield incident is no longer open.";
            return false;
        }

        string steamId = suspect ? incident.SuspectSteamId : incident.VictimSteamId;
        GUIUtility.systemCopyBuffer = steamId;
        message = (suspect ? "Copier's" : "Original player's") + " Steam ID copied: …" + PlayerIdentityEvidenceController.Suffix(steamId) + ".";
        return true;
    }

    internal static bool TryCopyBanPreviewCommand(string incidentId, out string message)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            message = SteamIdModerationController.LobbySafetyDeniedMessage;
            return false;
        }

        if (!CloneIncidentLedger.TryResolveIncident(incidentId, out CloneIncident incident))
        {
            message = "That Clone Shield incident is no longer open.";
            return false;
        }

        GUIUtility.systemCopyBuffer = "/sidban " + incident.Id;
        message = "Ban preview command copied for " + incident.Id + " (Steam …" +
                  PlayerIdentityEvidenceController.Suffix(incident.SuspectSteamId) + "). Paste it to see a preview; nothing happens until you confirm.";
        return true;
    }

    internal static bool TryResolveForLobby(string incidentId, out string message)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            message = SteamIdModerationController.LobbySafetyDeniedMessage;
            return false;
        }

        if (!CloneIncidentLedger.TryResolveIncident(incidentId, out CloneIncident incident))
        {
            message = "That Clone Shield incident is no longer open.";
            return false;
        }

        if (!SteamIdModerationController.CanLocalIssue(out bool isHost, out string unavailable))
        {
            message = unavailable;
            return false;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var request = new CloneIncidentResolutionRequest(
            incident.VictimSteamId,
            incident.SuspectSteamId,
            incident.StableId,
            now,
            Guid.NewGuid().ToString("N").Substring(0, 16));
        if (isHost)
        {
            if (!TryPublishHostResolution(request, out message)) return false;
            CloneIncidentLedger.MarkResolved(incident.Id, TryGetLocalSteamId(), now);
            Patches.PlayerIdentityRoleHighlightController.RefreshAll();
            RecordLifecycle("resolved", incident, "host-published", "lobby-safety-host");
            message = incident.Id + " resolved. It clears for everyone with QoL; nothing was posted in chat.";
            return true;
        }

        string packet = CloneIncidentResolutionProtocolPolicy.BuildMemberDataRequest(
            request.VictimSteamId, request.SuspectSteamId, request.StableId, request.IssuedUnixSeconds, request.Nonce);
        if (string.IsNullOrWhiteSpace(packet) || !Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil)
        {
            message = "The request couldn't reach the host, so the incident stays open.";
            return false;
        }
        try { SteamMatchmaking.SetLobbyMemberData(lobby, CloneIncidentResolutionProtocolPolicy.MemberDataKey, packet); }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Clone Shield resolution request could not be published: " + ex.GetType().Name + "; the incident stayed open.");
            message = "The request couldn't be sent, so the incident stays open.";
            return false;
        }

        message = "Sent to the host to check and carry out. " + incident.Id + " stays open until the host confirms.";
        return true;
    }

    internal static bool TryDismissLocal(string incidentId, out string message)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            message = SteamIdModerationController.LobbySafetyDeniedMessage;
            return false;
        }

        if (!CloneIncidentLedger.TryResolveIncident(incidentId, out CloneIncident incident) ||
            !CloneIncidentLedger.DismissLocal(incidentId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            message = "That Clone Shield incident is no longer open.";
            return false;
        }

        Patches.PlayerIdentityRoleHighlightController.RefreshAll();
        message = incidentId.ToUpperInvariant() + " dismissed for you only. Nothing changed for anyone else.";
        RecordLifecycle("dismissed-local", incident, "closed", "lobby-safety-local");
        return true;
    }

    internal static bool TryDismissAllOpenLocal(out string message)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            message = SteamIdModerationController.LobbySafetyDeniedMessage;
            return false;
        }

        CloneIncident[] open = CloneIncidentLedger.Snapshot.ToArray();
        int dismissed = CloneIncidentLedger.DismissAllOpenLocal(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        foreach (CloneIncident incident in open)
            RecordLifecycle("dismissed-local", incident, "bulk-closed", "lobby-safety-local");
        if (dismissed > 0) Patches.PlayerIdentityRoleHighlightController.RefreshAll();
        message = dismissed == 0
            ? "No open Clone Shield incidents to dismiss."
            : "Dismissed " + dismissed + " open Clone Shield " + (dismissed == 1 ? "incident" : "incidents") +
              " for you only. No bans or Helpers changed, and nothing changed for anyone else.";
        return true;
    }

    internal static bool MarkStaleAbsent(string incidentId, out CloneIncident incident)
    {
        if (!CloneIncidentLedger.TryResolveIncident(incidentId, out incident)) return false;
        if (!CloneIncidentLedger.MarkStaleAbsent(incidentId, DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return false;
        RecordLifecycle("stale-absent", incident, "closed", "verified-roster-reconciliation");
        return true;
    }

    internal static bool MarkNoLongerMatching(string incidentId, out CloneIncident incident)
    {
        if (!CloneIncidentLedger.TryResolveIncident(incidentId, out incident)) return false;
        if (!CloneIncidentLedger.MarkNoLongerMatching(incidentId, DateTimeOffset.UtcNow.ToUnixTimeSeconds())) return false;
        RecordLifecycle("no-longer-matching", incident, "closed", "verified-roster-reconciliation");
        return true;
    }

    internal static bool TryDismissOpenForVerifiedPlayer(string steamId, out string message)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            message = SteamIdModerationController.LobbySafetyDeniedMessage;
            return false;
        }
        if (!PlayerIdentityEvidenceController.TryResolveExact(steamId, out PlayerIdentityEvidence evidence, out string error))
        {
            Plugin.Log?.LogInfo("Clone Shield player flag cleanup refused: " + error + ".");
            message = "That player couldn't be matched safely, so nothing happened.";
            return false;
        }

        IReadOnlyList<CloneIncident> dismissed = CloneIncidentLedger.DismissOpenForSteamId(
            evidence.SteamId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        foreach (CloneIncident incident in dismissed)
            RecordLifecycle("dismissed-local", incident, "player-flags-cleared", "lobby-safety-local");
        if (dismissed.Count > 0) Patches.PlayerIdentityRoleHighlightController.RefreshAll();
        message = dismissed.Count == 0
            ? "That player has no open Clone Shield flags."
            : "Cleared " + dismissed.Count + " open Clone Shield " + (dismissed.Count == 1 ? "incident" : "incidents") +
              " involving " + evidence.DisplayName + " for you only. Your log keeps the record; no ban or lobby setting changed.";
        return true;
    }

    internal static int ClearClosedHistory()
    {
        if (!Plugin.CanUseLobbySafety(out _)) return 0;
        return CloneIncidentLedger.ClearClosed();
    }

    internal static void RecordLifecycle(string action, CloneIncident incident, string outcome, string source)
    {
        if (incident == null) return;
        string targetRole = "SUSPECT;victim=" + incident.VictimSteamId + ";stable=" + incident.StableId + ";match=" + incident.MatchType;
        SessionAuditController.RecordModeration(
            "clone-incident-" + (action ?? "observed"),
            source ?? "clone-shield",
            outcome ?? string.Empty,
            incident.SuspectSteamId,
            incident.SuspectLabel,
            string.Empty,
            incidentId: incident.Id,
            targetRole: targetRole);
        Plugin.Log?.LogInfo("Clone Shield incident " + incident.Id + " " + action +
                            ": victim=…" + PlayerIdentityEvidenceController.Suffix(incident.VictimSteamId) +
                            ", suspect=…" + PlayerIdentityEvidenceController.Suffix(incident.SuspectSteamId) +
                            ", stable=" + incident.StableId + ", match=" + incident.MatchType + ".");
    }

    internal static void PollHostMemberDataRequests()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs < _nextMemberDataPollUnixMilliseconds) return;
        _nextMemberDataPollUnixMilliseconds = nowMs + MemberDataPollIntervalMilliseconds;
        try
        {
            if (!SteamIdModerationController.IsLocalHost() || !Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil) return;
            string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < memberCount; index++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, index);
                string senderSteamId = ((ulong)member).ToString();
                if (string.Equals(senderSteamId, owner, StringComparison.Ordinal)) continue;
                string payload = SteamMatchmaking.GetLobbyMemberData(lobby, member, CloneIncidentResolutionProtocolPolicy.MemberDataKey);
                if (!CloneIncidentResolutionProtocolPolicy.TryParseMemberDataRequest(payload, out CloneIncidentResolutionRequest request)) continue;
                if (!PlayerIdentityEvidenceController.TryResolveExact(senderSteamId, out _, out _)) continue;
                ProcessResolutionRequest(request, senderSteamId);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Clone Shield non-chat helper request poll failed closed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ProcessResolutionRequest(CloneIncidentResolutionRequest request, string senderSteamId)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!CloneIncidentResolutionProtocolPolicy.IsFresh(request, now) || !RememberResolutionNonce(request.Nonce))
        {
            Plugin.Log?.LogWarning("Rejected expired or replayed Clone Shield resolution request.");
            return;
        }

        if (!PlayerIdentityEvidenceController.TryResolveExact(senderSteamId, out _, out _) ||
            !SteamIdModerationController.IsAuthorizedCurrentIssuer(senderSteamId))
        {
            Plugin.Log?.LogWarning("Rejected Clone Shield resolution request from a non-helper sender.");
            return;
        }

        if (LastResolutionRequestByIssuer.TryGetValue(senderSteamId, out long last) && now - last < 2)
        {
            Plugin.Log?.LogWarning("Rate-limited repeated Clone Shield resolution request from Steam …" + PlayerIdentityEvidenceController.Suffix(senderSteamId) + ".");
            return;
        }

        string expectedStable = CloneIncidentLedger.BuildStableId(request.VictimSteamId, request.SuspectSteamId);
        if (!string.Equals(expectedStable, request.StableId, StringComparison.Ordinal) ||
            !CloneIncidentLedger.Snapshot.Any(item =>
                string.Equals(item.StableId, request.StableId, StringComparison.Ordinal) &&
                string.Equals(item.VictimSteamId, request.VictimSteamId, StringComparison.Ordinal) &&
                string.Equals(item.SuspectSteamId, request.SuspectSteamId, StringComparison.Ordinal)))
        {
            Plugin.Log?.LogWarning("Rejected Clone Shield resolution request that did not match an open host incident.");
            return;
        }

        LastResolutionRequestByIssuer[senderSteamId] = now;
        if (!TryPublishHostResolution(request, out string error))
        {
            Plugin.Log?.LogWarning("Clone Shield host resolution publish failed safely: " + error);
            return;
        }

        CloneIncidentLedger.MarkResolvedPair(request.VictimSteamId, request.SuspectSteamId, senderSteamId, now);
        Patches.PlayerIdentityRoleHighlightController.RefreshAll();
        RecordLifecycle("resolved", CloneIncidentLedger.HistorySnapshot.LastOrDefault(item =>
            string.Equals(item.StableId, request.StableId, StringComparison.Ordinal)), "helper-approved", "lobby-safety-host");
        Plugin.AddLocalNotificationWithToast("Helper action done: Clone Shield " + request.StableId + " resolved. Nothing was posted in chat.");
    }

    internal static void PollHostResolutionMetadata()
    {
        try
        {
            if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil || !SteamManager.Initialized) return;
            string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
            string value = SteamMatchmaking.GetLobbyData(lobby, CloneIncidentResolutionProtocolPolicy.LobbyDataKey) ?? string.Empty;
            if (!CloneIncidentResolutionProtocolPolicy.TryParseLobbyValue(value, owner, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out CloneIncidentResolutionRequest request) ||
                string.Equals(_lastLobbyResolutionNonce, request.Nonce, StringComparison.Ordinal)) return;
            _lastLobbyResolutionNonce = request.Nonce;
            string expectedStable = CloneIncidentLedger.BuildStableId(request.VictimSteamId, request.SuspectSteamId);
            if (!string.Equals(expectedStable, request.StableId, StringComparison.Ordinal)) return;
            if (CloneIncidentLedger.MarkResolvedPair(request.VictimSteamId, request.SuspectSteamId, owner, request.IssuedUnixSeconds))
            {
                Patches.PlayerIdentityRoleHighlightController.RefreshAll();
                RecordLifecycle("resolved", CloneIncidentLedger.HistorySnapshot.LastOrDefault(item =>
                    string.Equals(item.StableId, request.StableId, StringComparison.Ordinal)), "host-confirmed", "lobby-safety-peer");
                if (Plugin.CanUseLobbySafety(out _))
                {
                    Plugin.AddLocalNotificationWithToast("Clone Shield " + request.StableId + " was resolved by the host or a Helper.");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Clone Shield resolution metadata poll skipped safely: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TryPublishHostResolution(CloneIncidentResolutionRequest request, out string message)
    {
        message = "Only the lobby host can close an incident for everyone.";
        try
        {
            if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil || !SteamManager.Initialized ||
                NetworkManager.main == null || !NetworkManager.main.isHost) return false;
            string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
            string local = ((ulong)SteamUser.GetSteamID()).ToString();
            if (!string.Equals(owner, local, StringComparison.Ordinal)) return false;
            string value = CloneIncidentResolutionProtocolPolicy.BuildLobbyValue(owner, request);
            if (!SteamMatchmaking.SetLobbyData(lobby, CloneIncidentResolutionProtocolPolicy.LobbyDataKey, value))
            {
                message = "Steam didn't accept the update, so the incident stays open.";
                return false;
            }
            message = "Incident closed for everyone.";
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Clone Shield host resolution publish failed: " + ex.GetType().Name + ".");
            message = "The incident couldn't be closed right now, so it stays open.";
            return false;
        }
    }

    private static bool RememberResolutionNonce(string nonce)
    {
        if (!SeenResolutionNonces.Add(nonce)) return false;
        ResolutionNonceOrder.Enqueue(nonce);
        while (ResolutionNonceOrder.Count > 128) SeenResolutionNonces.Remove(ResolutionNonceOrder.Dequeue());
        return true;
    }

    private static string TryGetLocalSteamId()
    {
        try { return ((ulong)Steamworks.SteamUser.GetSteamID()).ToString(); }
        catch { return string.Empty; }
    }
}
