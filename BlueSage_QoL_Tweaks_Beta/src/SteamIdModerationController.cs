using System;
using System.Collections.Generic;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using PurrNet;
using Steamworks;

namespace BlueSage.QoLTweaks;

internal sealed class NativeBanRecord
{
    internal NativeBanRecord(string steamId, string displayNameRaw)
    {
        SteamId = steamId ?? string.Empty;
        DisplayNameRaw = displayNameRaw ?? string.Empty;
        DisplayName = PlayerMentionProvider.StripRichText(DisplayNameRaw).Trim();
        if (DisplayName.Length == 0) DisplayName = "Saved ban";
    }

    internal string SteamId { get; }
    internal string DisplayNameRaw { get; }
    internal string DisplayName { get; }
}

internal enum LobbySafetyAccessState
{
    Denied,
    Host,
    HelperReady,
    HelperWaitingForHost
}

internal static class SteamIdModerationController
{
    private const int MemberDataPollIntervalMilliseconds = 1000;
    internal const string LobbySafetyDeniedMessage = "Lobby Safety is available only to the current host or a Helper assigned by that host. No player or incident details were shown.";
    private const string CapabilityKey = "bluesage_qol_moderation";
    // Round 4 Stage I: advertises that this host accepts Helper requests carried over the game connection.
    internal const string NetworkTransportKey = "bluesage_qol_moderation_net";
    private const string HelpersKey = "bluesage_helpers";
    private const string LegacyHelpersKey = "bluesage_mod_helpers";
    private const string RescueHelpersKey = "bluesage_rescue_helpers";
    // Owner-written transport SteamID verdicts; only ready Helpers act on them.
    internal const string SteamIdTruthKey = "bluesage_sidtruth";
    // Owner-written lobby welcome (MOTD); QoL players show it once per lobby visit.
    internal const string MotdKey = "bluesage_motd";
    private static readonly HashSet<string> SeenNonces = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Queue<string> NonceOrder = new Queue<string>();
    private static readonly Dictionary<string, long> LastIssuerRequest = new Dictionary<string, long>(StringComparer.Ordinal);
    private static long _nextMemberDataPollUnixMilliseconds;
    private static string _memberDataLobbyKey = string.Empty;

    // Round 4 Stage E #3: a kick is a ban that lifts itself once the player's row is gone (KickFollowUpPolicy);
    // a lobby-wide mute is a hosting-session set the chat relay drops by authoritative sender (LobbyMutePolicy).
    private sealed class PendingKick
    {
        public string TargetSteamId;
        public string IssuerSteamId;
        public string IncidentId;
        public long StartedUtcTicks;
        public bool PromotedToBan;
    }

    private static readonly List<PendingKick> PendingKicks = new List<PendingKick>();
    private static readonly HashSet<string> LobbyMutedSteamIds = new HashSet<string>(StringComparer.Ordinal);
    private static bool _muteResolveWarningLogged;

    internal static void PublishHostCapability()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        try
        {
            if (!TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId)) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SteamMatchmaking.SetLobbyData(lobby, CapabilityKey, ModerationCapabilityPolicy.Build(ownerSteamId, now));
            SteamMatchmaking.SetLobbyData(lobby, NetworkTransportKey, ModerationCapabilityPolicy.BuildNetworkTransport(ownerSteamId, now));
            string helpers = LobbyPingAuthorizationPolicy.SerializeDelegates(Plugin.GetUnifiedHelpersForOwner(ownerSteamId, importLegacy: true));
            string value = ownerSteamId + "|" + helpers;
            SteamMatchmaking.SetLobbyData(lobby, HelpersKey, value);
            SteamMatchmaking.SetLobbyData(lobby, LegacyHelpersKey, value);
            SteamMatchmaking.SetLobbyData(lobby, RescueHelpersKey, value);
            Patches.PlayerRoleLabelController.PublishHostHeartbeat();
            try
            {
                string attestation = PlayerIdentityEvidenceController.BuildHostSteamIdTruthAttestation(ownerSteamId, now);
                if (attestation != null)
                {
                    SteamMatchmaking.SetLobbyData(lobby, SteamIdTruthKey, attestation);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug("SteamID truth attestation publish skipped: " + ex.GetType().Name + ": " + ex.Message);
            }

            SteamMatchmaking.SetLobbyData(lobby, MotdKey, LobbyMotdPolicy.Build(ownerSteamId, Plugin.HostMotd?.Value));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Moderation capability publish skipped: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    internal static void ClearHostCapability()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        try
        {
            if (!TryGetOwnedHostLobby(out CSteamID lobby, out _)) return;
            SteamMatchmaking.SetLobbyData(lobby, CapabilityKey, string.Empty);
            SteamMatchmaking.SetLobbyData(lobby, HelpersKey, string.Empty);
            SteamMatchmaking.SetLobbyData(lobby, LegacyHelpersKey, string.Empty);
            SteamMatchmaking.SetLobbyData(lobby, RescueHelpersKey, string.Empty);
            SteamMatchmaking.SetLobbyData(lobby, SteamIdTruthKey, string.Empty);
            SteamMatchmaking.SetLobbyData(lobby, MotdKey, string.Empty);
            Patches.PlayerRoleLabelController.ClearPublishedHostLabel();
        }
        catch
        {
            // Unload cleanup is best-effort; host identity checks still fail closed.
        }
    }

    internal static void ClearHostCapabilityForShutdown(SteamRuntimeSnapshot snapshot)
    {
        if (snapshot == null ||
            !snapshot.SteamWasReady ||
            !snapshot.LocalWasOwner ||
            snapshot.Lobby == CSteamID.Nil)
        {
            return;
        }

        try
        {
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, CapabilityKey, string.Empty);
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, HelpersKey, string.Empty);
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, LegacyHelpersKey, string.Empty);
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, RescueHelpersKey, string.Empty);
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, SteamIdTruthKey, string.Empty);
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, MotdKey, string.Empty);
        }
        catch
        {
            // Cached-lobby cleanup is best-effort and never re-resolves runtime singletons.
        }
    }

    internal static bool CanLocalIssue(out bool isHost, out string reason)
    {
        LobbySafetyAccessState state = GetLocalAccessState(out isHost, out reason);
        return state == LobbySafetyAccessState.Host || state == LobbySafetyAccessState.HelperReady;
    }

    internal static bool ShouldShowLocalSurface(out LobbySafetyAccessState state, out string reason)
    {
        state = GetLocalAccessState(out _, out reason);
        return state != LobbySafetyAccessState.Denied;
    }

    internal static LobbySafetyAccessState GetLocalAccessState(out bool isHost, out string reason)
    {
        isHost = false;
        reason = LobbySafetyDeniedMessage;
        try
        {
            if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil || !SteamManager.Initialized)
                return LobbySafetyAccessState.Denied;
            string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
            string local = ((ulong)SteamUser.GetSteamID()).ToString();
            isHost = string.Equals(owner, local, StringComparison.Ordinal) && NetworkManager.main != null && NetworkManager.main.isHost;
            if (isHost)
            {
                reason = "You're the lobby host.";
                return LobbySafetyAccessState.Host;
            }

            bool listed = IsPublishedHelper(lobby, owner, local);
            if (!listed) return LobbySafetyAccessState.Denied;

            if (!ModerationCapabilityPolicy.IsFresh(
                    SteamMatchmaking.GetLobbyData(lobby, CapabilityKey),
                    owner,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            {
                reason = "The host made you a Helper, but their game hasn't confirmed it yet (they may need QoL 0.2.0 or newer). Helper tools stay locked and nothing is sent until it does.";
                return LobbySafetyAccessState.HelperWaitingForHost;
            }

            reason = "You're an assigned Helper in this lobby.";
            return LobbySafetyAccessState.HelperReady;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Lobby Safety access check failed closed: " + ex.GetType().Name + ": " + ex.Message);
            reason = LobbySafetyDeniedMessage;
            return LobbySafetyAccessState.Denied;
        }
    }

    internal static bool IsLocalHost()
    {
        return CanLocalIssue(out bool isHost, out _) && isHost;
    }

    internal static bool IsAuthorizedCurrentIssuer(string steamId)
    {
        try
        {
            if (!ModerationProtocolPolicy.IsSteamId64(steamId) || !Plugin.TryGetRescueLobby(out CSteamID lobby) ||
                lobby == CSteamID.Nil || !SteamManager.Initialized) return false;
            string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
            if (string.Equals(owner, steamId, StringComparison.Ordinal)) return true;

            if (NetworkManager.main != null && NetworkManager.main.isHost && SteamUser.GetSteamID() == SteamMatchmaking.GetLobbyOwner(lobby))
            {
                return ModerationAuthorizationPolicy.IsAuthorizedIssuer(
                    steamId,
                    owner,
                    Plugin.LobbyHelpers?.Value ?? string.Empty,
                    Plugin.LobbyHelperOwnerSteamId?.Value ?? string.Empty,
                    true);
            }

            return ModerationCapabilityPolicy.IsFresh(
                       SteamMatchmaking.GetLobbyData(lobby, CapabilityKey),
                       owner,
                       DateTimeOffset.UtcNow.ToUnixTimeSeconds()) &&
                   IsPublishedHelper(lobby, owner, steamId);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsPublishedHelper(CSteamID lobby, string ownerSteamId, string steamId)
    {
        return GetPublishedHelpers(lobby, ownerSteamId).Contains(steamId);
    }

    internal static bool TryGetFreshCurrentCapability(
        out CSteamID lobby,
        out string ownerSteamId,
        out string capability)
    {
        lobby = CSteamID.Nil;
        ownerSteamId = string.Empty;
        capability = string.Empty;
        try
        {
            if (!SteamManager.Initialized ||
                !Plugin.TryGetRescueLobby(out lobby) ||
                lobby == CSteamID.Nil)
            {
                return false;
            }

            CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby);
            if (owner == CSteamID.Nil)
            {
                return false;
            }
            ownerSteamId = ((ulong)owner).ToString();
            capability =
                SteamMatchmaking.GetLobbyData(lobby, CapabilityKey) ??
                string.Empty;
            return ModerationCapabilityPolicy.IsFresh(
                capability,
                ownerSteamId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        catch
        {
            lobby = CSteamID.Nil;
            ownerSteamId = string.Empty;
            capability = string.Empty;
            return false;
        }
    }

    internal static bool IsUniqueCurrentLobbyMember(
        CSteamID lobby,
        string steamId)
    {
        return lobby != CSteamID.Nil &&
               IsUniqueSteamLobbyMember(lobby, steamId);
    }

    internal static HashSet<string> GetPublishedHelpers(CSteamID lobby, string ownerSteamId)
    {
        string helperMetadata = SteamMatchmaking.GetLobbyData(lobby, HelpersKey);
        if (string.IsNullOrWhiteSpace(helperMetadata)) helperMetadata = SteamMatchmaking.GetLobbyData(lobby, LegacyHelpersKey);
        string[] parts = (helperMetadata ?? string.Empty).Split(new[] { '|' }, 2);
        if (parts.Length != 2 || !string.Equals(parts[0], ownerSteamId, StringComparison.Ordinal))
            return new HashSet<string>(StringComparer.Ordinal);
        return new HashSet<string>(LobbyPingAuthorizationPolicy.ParseDelegates(parts[1]), StringComparer.Ordinal);
    }

    internal const string HelperNetworkPathMessage =
        "You're not in Steam's lobby list (usually after an automatic reconnect), so the request went over the game connection instead. The host checks it against your live connection and acts or refuses it in their journal; you won't get a reply here either way.";

    internal const string HelperNotInSteamLobbyMessage =
        "You're in the game but dropped out of Steam's lobby list (this often happens after an automatic reconnect), so the host can't receive Helper requests from you. Leave and rejoin the lobby to moderate again. No action was taken.";

    internal static bool TrySendHelperRequest(string action, string targetSteamId, string incidentId, out string message)
    {
        if (!CanLocalIssue(out bool isHost, out message)) return false;
        if (isHost)
        {
            string issuer = ((ulong)SteamUser.GetSteamID()).ToString();
            return TryExecuteHostAction(action, targetSteamId, incidentId, issuer, out message);
        }

        string nonce = Guid.NewGuid().ToString("N").Substring(0, 16);
        string request = ModerationProtocolPolicy.BuildMemberData(action, targetSteamId, incidentId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), nonce);
        if (string.IsNullOrWhiteSpace(request) || !Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil)
        {
            message = "The request couldn't reach the host. Nothing happened.";
            return false;
        }
        // Round 4 WS3: the host reads member-data requests only from Steam lobby members. A Helper who rejoined through
        // a reconnect mod is in the game but not in Steam's lobby list, so that request would silently never arrive.
        // Stage I: if the host advertises the game-connection path, the request rides a chat RPC instead; the host
        // intercepts it before relay and validates it by the authoritative network sender. The packet keeps the
        // !bsmod:v1: prefix on purpose: a 0.2.5 host swallows exactly that prefix without relaying it to chat.
        string localSteamId = ((ulong)SteamUser.GetSteamID()).ToString();
        if (CountSteamLobbyMembers(lobby, localSteamId) == 0)
        {
            string ownerSteamId = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
            bool networkPath = ModerationCapabilityPolicy.IsNetworkTransportFresh(
                SteamMatchmaking.GetLobbyData(lobby, NetworkTransportKey), ownerSteamId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (networkPath && Plugin.TrySendLobbyChatMessage(ModerationProtocolPolicy.Build(action, targetSteamId, incidentId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), nonce)))
            {
                message = HelperNetworkPathMessage;
                return true;
            }

            message = HelperNotInSteamLobbyMessage;
            return false;
        }

        try { SteamMatchmaking.SetLobbyMemberData(lobby, ModerationProtocolPolicy.MemberDataKey, request); }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Helper moderation request could not be published: " + ex.GetType().Name + "; no action was taken.");
            message = "The request couldn't be sent. Nothing happened.";
            return false;
        }

        message = "Sent to the host to check and carry out.";
        return true;
    }

    internal static void PollHostMemberDataRequests()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs < _nextMemberDataPollUnixMilliseconds) return;
        _nextMemberDataPollUnixMilliseconds = nowMs + MemberDataPollIntervalMilliseconds;
        if (!TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId)) return;
        string lobbyKey = ((ulong)lobby).ToString();
        if (!string.Equals(_memberDataLobbyKey, lobbyKey, StringComparison.Ordinal))
        {
            _memberDataLobbyKey = lobbyKey;
            SeenNonces.Clear();
            NonceOrder.Clear();
            LastIssuerRequest.Clear();
            // A new hosting session starts clean: mutes are session-only, and a pending kick belongs to the old lobby.
            LobbyMutedSteamIds.Clear();
            PendingKicks.Clear();
        }

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int index = 0; index < memberCount; index++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, index);
            string senderSteamId = ((ulong)member).ToString();
            if (string.Equals(senderSteamId, ownerSteamId, StringComparison.Ordinal)) continue;
            string payload = SteamMatchmaking.GetLobbyMemberData(lobby, member, ModerationProtocolPolicy.MemberDataKey);
            if (!ModerationProtocolPolicy.TryParseMemberData(payload, out ModerationRequest request)) continue;
            ProcessMemberDataRequest(lobby, ownerSteamId, senderSteamId, request);
        }
    }

    private static void ProcessMemberDataRequest(CSteamID lobby, string ownerSteamId, string senderSteamId, ModerationRequest request)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!ModerationProtocolPolicy.IsFresh(request, now) ||
            !PlayerIdentityEvidenceController.TryResolveExact(senderSteamId, out _, out _) ||
            !IsUniqueSteamLobbyMember(lobby, senderSteamId) ||
            !ModerationAuthorizationPolicy.IsAuthorizedIssuer(senderSteamId, ownerSteamId,
                Plugin.LobbyHelpers?.Value ?? string.Empty, Plugin.LobbyHelperOwnerSteamId?.Value ?? string.Empty, true) ||
            !RememberNonce(request.Nonce)) return;
        if (LastIssuerRequest.TryGetValue(senderSteamId, out long last) && now - last < 2) return;
        LastIssuerRequest[senderSteamId] = now;
        bool success;
        string result;
        HostJournalController.BeginSurface("helper relay");
        try
        {
            success = TryExecuteHostAction(request.Action, request.TargetSteamId, request.IncidentId, senderSteamId, out result);
        }
        finally
        {
            HostJournalController.EndSurface();
        }
        Plugin.Log?.LogInfo("Host non-chat moderation request from Steam …" + Suffix(senderSteamId) + ": " + result);
        Plugin.AddLocalNotificationWithToast((success ? "Helper action done: " : "Helper request not carried out: ") + result);
    }

    // Round 4 Stage I: a Helper request that arrived as a chat RPC on the host (ModerationRelayPatch, before relay).
    // Bound by the authoritative sender of the RPC: exactly one roster row owns it, its Steam ID is on the Helper
    // list, and the transport's Steam-authenticated ID agrees when it is exposed. Shares the nonce memory and the
    // 2 s rate limit with the member-data path, so one request cannot be doubled across transports. Returns true
    // when the packet was consumed (acted on or refused with a journal row); false means "not for this host".
    internal static bool TryAcceptNetworkCarriedRequest(RPCInfo info, ModerationRequest request)
    {
        try
        {
            if (!TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId)) return false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!ModerationProtocolPolicy.IsFresh(request, now)) return false;

            string senderKey = info.sender.ToString();
            int senderRowMatches = CountSenderRows(info.sender, out string claimedSteamId);
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            int claimedLiveRows = panel != null && TryCountLiveTargetRows(panel, claimedSteamId, out int counted, out _) ? counted : 0;
            ulong transportSteamId = SteamIdTruthReader.ReadTransportSteamId(info.sender);
            bool senderInSteamLobby = senderRowMatches == 1 && CountSteamLobbyMembers(lobby, claimedSteamId) == 1;
            NetworkCarriedRequestDecision decision = NetworkCarriedRequestPolicy.Decide(senderRowMatches, claimedSteamId, claimedLiveRows, transportSteamId, senderInSteamLobby);
            if (!decision.Accepted)
            {
                RecordNetworkRefusal(senderKey, claimedSteamId, request.TargetSteamId, decision.Reason);
                return true;
            }

            if (!ModerationAuthorizationPolicy.IsAuthorizedIssuer(claimedSteamId, ownerSteamId,
                    Plugin.LobbyHelpers?.Value ?? string.Empty, Plugin.LobbyHelperOwnerSteamId?.Value ?? string.Empty, true))
            {
                RecordNetworkRefusal(senderKey, claimedSteamId, request.TargetSteamId, "not on this host's Helper list");
                return true;
            }

            if (!RememberNonce(request.Nonce)) return true;
            if (LastIssuerRequest.TryGetValue(claimedSteamId, out long last) && now - last < 2) return true;
            LastIssuerRequest[claimedSteamId] = now;

            bool success;
            string result;
            HostJournalController.BeginSurface(decision.Surface);
            try
            {
                success = TryExecuteHostAction(request.Action, request.TargetSteamId, request.IncidentId, claimedSteamId, out result);
            }
            finally
            {
                HostJournalController.EndSurface();
            }

            Plugin.Log?.LogInfo("Host game-connection moderation request from Steam …" + Suffix(claimedSteamId) + ": " + result);
            Plugin.AddLocalNotificationWithToast((success ? "Helper action done: " : "Helper request not carried out: ") + result);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Game-connection moderation request failed closed: " + ex.GetType().Name + ": " + ex.Message);
            return true;
        }
    }

    // Refusals are the abuse monitor (Blue): one journal action row and one warning per sender per interval, so a
    // flood of bad packets cannot fill the box or the log. Actor is the claimed Steam ID when there is one.
    private const int NetworkRefusalIntervalSeconds = 10;
    private static readonly Dictionary<string, long> LastNetworkRefusal = new Dictionary<string, long>(StringComparer.Ordinal);

    private static void RecordNetworkRefusal(string senderKey, string claimedSteamId, string targetSteamId, string reason)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (LastNetworkRefusal.TryGetValue(senderKey, out long last) && now - last < NetworkRefusalIntervalSeconds)
        {
            return;
        }

        LastNetworkRefusal[senderKey] = now;
        if (LastNetworkRefusal.Count > 256)
        {
            LastNetworkRefusal.Clear();
        }

        string actor = ModerationProtocolPolicy.IsSteamId64(claimedSteamId) ? claimedSteamId : "unknown sender " + senderKey;
        Plugin.Log?.LogWarning("Game-connection moderation request from " + actor + " refused: " + reason + ". No action was taken.");
        HostJournalController.BeginSurface(NetworkCarriedRequestPolicy.RefusedSurface);
        try
        {
            HostJournalController.RecordAction("request", actor, string.Empty, targetSteamId ?? string.Empty, false, "Refused: " + reason + ".");
        }
        finally
        {
            HostJournalController.EndSurface();
        }
    }

    // Round 4 Stage I (advisor): a Helper who is no longer a Steam member only sees cached lobby data, which goes
    // stale after the 20 s capability lease. Steam refreshes a lobby you are not in on request (LobbyDataUpdate_t
    // lands asynchronously and GetLobbyData answers from the refreshed cache). Called on the client's 5 s poll.
    internal static void RefreshLobbyDataIfNotMember()
    {
        try
        {
            if (!SteamManager.Initialized || !Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil) return;
            string localSteamId = ((ulong)SteamUser.GetSteamID()).ToString();
            if (CountSteamLobbyMembers(lobby, localSteamId) == 1) return;
            SteamMatchmaking.RequestLobbyData(lobby);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Lobby data refresh for a non-member skipped: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // How many roster rows claim this network sender, and the Steam ID on the (last) matching row.
    private static int CountSenderRows(PlayerID sender, out string claimedSteamId)
    {
        claimedSteamId = string.Empty;
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel?.PlayerSteamIDs == null || panel.PlayerIDs == null || panel.PlayerSteamIDs.Count != panel.PlayerIDs.Count)
        {
            return 0;
        }

        int matches = 0;
        for (int index = 0; index < panel.PlayerIDs.Count; index++)
        {
            if (panel.PlayerIDs[index].Equals(sender))
            {
                matches++;
                claimedSteamId = panel.PlayerSteamIDs[index] ?? string.Empty;
            }
        }

        return matches;
    }

    internal static bool TryExecuteHostAction(string action, string targetSteamId, string incidentId, string issuerSteamId, out string message)
    {
        message = "Nothing happened.";
        if (!TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId))
        {
            message = "Only the lobby host can do that. Nothing happened.";
            return false;
        }

        if (!ModerationAuthorizationPolicy.IsAuthorizedIssuer(
                issuerSteamId,
                ownerSteamId,
                Plugin.LobbyHelpers?.Value ?? string.Empty,
                Plugin.LobbyHelperOwnerSteamId?.Value ?? string.Empty,
                true))
        {
            message = "The person asking isn't an assigned Helper in this lobby. Nothing happened.";
            return false;
        }

        string scopedIncidentId = !string.Equals(issuerSteamId, ownerSteamId, StringComparison.Ordinal) &&
                                  !string.Equals(incidentId, "none", StringComparison.OrdinalIgnoreCase)
            ? "issuer-local:" + incidentId
            : incidentId;
        bool success;
        if (action == "ban")
        {
            // An explicit ban promotes a pending kick: its follow-up unban must never lift this ban.
            CancelPendingKick(targetSteamId);
            success = TryBan(lobby, ownerSteamId, issuerSteamId, targetSteamId, scopedIncidentId, out message);
        }
        else if (action == "unban")
        {
            success = TryUnban(targetSteamId, issuerSteamId, scopedIncidentId, out message);
        }
        else if (action == "kick")
        {
            success = TryKick(lobby, ownerSteamId, issuerSteamId, targetSteamId, scopedIncidentId, out message);
        }
        else if (action == "mute")
        {
            success = TryMute(ownerSteamId, issuerSteamId, targetSteamId, out message);
        }
        else if (action == "unmute")
        {
            success = TryUnmute(targetSteamId, out message);
        }
        else
        {
            message = "That action isn't recognized. Nothing happened.";
            success = false;
        }

        // Round 4 Stage D: every host action, from any surface, is one journal row (success or refusal).
        HostJournalController.RecordAction(action, issuerSteamId, ownerSteamId, targetSteamId, success, message);
        return success;
    }

    internal static string[] GetCurrentPlayerSteamIds()
    {
        return PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIds(out _).ToArray();
    }

    internal static string[] GetBannedSteamIds()
    {
        try
        {
            return MonoSingleton<DataManager>.I?.BanData?.BanServerPlayers?
                .Where(ModerationProtocolPolicy.IsSteamId64).Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }

    internal static IReadOnlyList<NativeBanRecord> GetNativeBanRecords()
    {
        return TryGetNativeBanRecords(out IReadOnlyList<NativeBanRecord> records, out _)
            ? records
            : Array.Empty<NativeBanRecord>();
    }

    internal static bool TryGetNativeBanRecords(out IReadOnlyList<NativeBanRecord> records, out string error)
    {
        records = Array.Empty<NativeBanRecord>();
        error = string.Empty;
        if (!Plugin.CanManageHelpers())
        {
            error = "only the lobby host can see the ban list";
            return false;
        }
        try
        {
            BanData bans = MonoSingleton<DataManager>.I?.BanData;
            if (bans?.BanServerPlayers == null || bans.BanServerPlayerNicks == null ||
                bans.BanServerPlayers.Count != bans.BanServerPlayerNicks.Count)
            {
                error = "the ban list couldn't be read right now";
                return false;
            }

            var result = new List<NativeBanRecord>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < bans.BanServerPlayers.Count; index++)
            {
                string steamId = bans.BanServerPlayers[index] ?? string.Empty;
                if (!ModerationProtocolPolicy.IsSteamId64(steamId)) continue;
                if (!seen.Add(steamId))
                {
                    error = "the ban list has a repeated entry, so it can't be shown right now";
                    return false;
                }
                result.Add(new NativeBanRecord(steamId, bans.BanServerPlayerNicks[index] ?? string.Empty));
            }

            records = result;
            return true;
        }
        catch (Exception ex)
        {
            error = "the ban list couldn't be read right now (" + ex.GetType().Name + ")";
            return false;
        }
    }

    private static bool TryBan(CSteamID lobby, string ownerSteamId, string issuerSteamId, string targetSteamId, string incidentId, out string message)
    {
        message = "No ban was made.";
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel?.PlayerSteamIDs == null || panel.PlayerIDs == null || panel.PlayerSteamIDs.Count != panel.PlayerIDs.Count)
        {
            message = "The game's player list isn't ready yet. Nothing happened.";
            return false;
        }

        int targetIndex = panel.PlayerSteamIDs.FindIndex(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        int panelMatches = panel.PlayerSteamIDs.Count(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        int lobbyMatches = CountSteamLobbyMembers(lobby, targetSteamId);

        // Round 4 WS3/WS4: live rows come from the host's PurrNet connection table. A player who rejoined through a
        // reconnect mod is connected but missing from Steam's lobby list; PlayerPanelController also keeps stale
        // history rows, so only rows that are connected right now count.
        bool livenessKnown = TryCountLiveTargetRows(panel, targetSteamId, out int liveRowMatches, out int liveIndex);
        if (livenessKnown && liveRowMatches == 1)
        {
            targetIndex = liveIndex;
        }

        bool uniquePlayerId = targetIndex >= 0 && targetIndex < panel.PlayerIDs.Count &&
            panel.PlayerIDs.Count(id => id.Equals(panel.PlayerIDs[targetIndex])) == 1;
        BanTargetPresence presence;
        if (livenessKnown)
        {
            presence = BanTargetPresencePolicy.Classify(lobbyMatches, panelMatches, liveRowMatches, uniquePlayerId);
        }
        else
        {
            // Connection table unavailable: exactly the previous Steam-membership rules, so hosts keep today's bans.
            presence = BanTargetPresencePolicy.ClassifyWithoutLiveness(targetIndex >= 0, panelMatches, uniquePlayerId, lobbyMatches);
        }

        bool targetPresent = presence == BanTargetPresence.InSteamLobby || presence == BanTargetPresence.Reconnected;
        bool targetAbsent = presence == BanTargetPresence.Absent;
        bool targetLoading = presence == BanTargetPresence.Loading;
        bool protectedVictim = CloneIncidentLedger.IsProtectedVictim(targetSteamId);
        HostJournalController.NoteTarget(string.Empty, targetIndex >= 0 && targetIndex < panel.PlayerIDs.Count ? panel.PlayerIDs[targetIndex].ToString() : string.Empty, presence.ToString().ToUpperInvariant());

        if (!targetPresent && !targetAbsent && !targetLoading)
        {
            message = "That player couldn't be matched safely, so nothing happened.";
            return false;
        }

        if (targetPresent)
        {
            if (!ModerationAuthorizationPolicy.CanBanTarget(
                    targetSteamId,
                    ownerSteamId,
                    Plugin.LobbyHelpers?.Value ?? string.Empty,
                    issuerSteamId,
                    protectedVictim,
                    true))
            {
                message = "That player can't be banned from here (they're the host, a Helper, you, someone being copied, or couldn't be matched safely). Nothing happened.";
                return false;
            }
        }
        else if (targetLoading)
        {
            // Stage C P2: in Steam's lobby, no live row yet. Same host-only rules as an offline save, without claiming absence.
            if (!OfflineBanPolicy.CanSaveWithoutLiveRow(
                    ownerSteamId,
                    issuerSteamId,
                    targetSteamId,
                    Plugin.LobbyHelpers?.Value ?? string.Empty,
                    protectedVictim,
                    out message))
            {
                return false;
            }
        }
        else if (!OfflineBanPolicy.CanSave(
                     ownerSteamId,
                     issuerSteamId,
                     targetSteamId,
                     Plugin.LobbyHelpers?.Value ?? string.Empty,
                     protectedVictim,
                     true,
                     out message))
        {
            return false;
        }

        DataManager data = MonoSingleton<DataManager>.I;
        BanData bans = data?.BanData;
        if (bans?.BanServerPlayers == null || bans.BanServerPlayerNicks == null || bans.BanServerPlayers.Count != bans.BanServerPlayerNicks.Count)
        {
            message = "The ban list couldn't be read right now. Nothing happened.";
            return false;
        }

        if (bans.BanServerPlayers.Contains(targetSteamId))
        {
            message = "Steam …" + Suffix(targetSteamId) + " is already on the game's ban list.";
            return true;
        }

        string persona = targetAbsent ? SafeOfflinePersona(targetSteamId) : SafePersona(targetSteamId);
        string auditDisplayName = persona;
        string auditPersona = persona;
        string auditRole = targetAbsent ? "ABSENT" : targetLoading ? "LOADING" : presence == BanTargetPresence.Reconnected ? "RECONNECTED" : "PLAYER";
        if (targetPresent && PlayerIdentityEvidenceController.TryResolveExact(targetSteamId, out PlayerIdentityEvidence auditEvidence, out _))
        {
            auditDisplayName = auditEvidence.DisplayNameRaw;
            auditPersona = auditEvidence.SteamPersona;
            auditRole = auditEvidence.Role;
        }
        HostJournalController.NoteTarget(auditDisplayName, targetIndex >= 0 && targetIndex < panel.PlayerIDs.Count ? panel.PlayerIDs[targetIndex].ToString() : string.Empty, auditRole);
        bans.BanServerPlayers.Add(targetSteamId);
        bans.BanServerPlayerNicks.Add(persona);
        SessionAuditController.ArmPersistedModerationAudit(
            "ban-added",
            targetAbsent ? "bluesage-sidban-offline" : targetLoading ? "bluesage-sidban-loading" : "bluesage-sidban-live",
            targetSteamId,
            auditDisplayName,
            auditPersona,
            issuerSteamId,
            incidentId,
            auditRole);
        try
        {
            data.SaveBanData();
        }
        catch (Exception ex)
        {
            SessionAuditController.CancelPersistedModerationAudit();
            bans.BanServerPlayers.RemoveAt(bans.BanServerPlayers.Count - 1);
            bans.BanServerPlayerNicks.RemoveAt(bans.BanServerPlayerNicks.Count - 1);
            Plugin.Log?.LogWarning("Ban save failed and was rolled back: " + ex.GetType().Name + ".");
            message = "The ban couldn't be saved, so it was undone. Nothing changed.";
            return false;
        }

        if (!targetPresent)
        {
            // Absent or Loading: there is no live row to kick. The saved id is refused by the game at the Steam join
            // request and when a PurrNet join completes (decompiled build 25966942: LobbyRequestManager, UpdateOnNewPeopleJoin).
            message = targetAbsent
                ? "Ban saved for Steam ID " + targetSteamId + ". They weren't in the lobby, so nobody was kicked. Use /sidunban " + targetSteamId + " to undo it."
                : "Ban saved for Steam ID " + targetSteamId + ". They're in the Steam lobby but haven't connected to the game yet, so the game will turn them away when they connect and on any later join. Use /sidunban " + targetSteamId + " to undo it.";
            return true;
        }

        ChalkboardPersistenceController.NotifyModerationBoardReview(targetSteamId);
        PlayerController hostController = NetworkSingleton<TextChannelManager>.I?.MainPlayerController;
        if (hostController == null)
        {
            message = "Ban saved for Steam …" + Suffix(targetSteamId) + ", but removing them from the lobby right now couldn't be confirmed.";
            return true;
        }

        try
        {
            hostController.BanRPC(panel.PlayerIDs[targetIndex], true);
            message = "Banned Steam …" + Suffix(targetSteamId) +
                      (incidentId == "none" ? "." : " (" + incidentId + "). The player being copied was not touched.") +
                      (presence == BanTargetPresence.Reconnected ? " They had reconnected, so they were in the game but not in Steam's lobby list." : string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Ban saved but the live kick raised " + ex.GetType().Name + " for Steam …" + Suffix(targetSteamId) + ".");
            message = "Ban saved for Steam …" + Suffix(targetSteamId) + ", but removing them from the lobby right now didn't work.";
            return true;
        }
    }

    private static bool TryUnban(string targetSteamId, string issuerSteamId, string incidentId, out string message)
    {
        message = "No unban was made.";
        DataManager data = MonoSingleton<DataManager>.I;
        BanData bans = data?.BanData;
        if (bans?.BanServerPlayers == null || bans.BanServerPlayerNicks == null || bans.BanServerPlayers.Count != bans.BanServerPlayerNicks.Count)
        {
            message = "The ban list couldn't be read right now. Nothing happened.";
            return false;
        }

        int index = bans.BanServerPlayers.FindIndex(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        if (index < 0)
        {
            message = "Steam …" + Suffix(targetSteamId) + " isn't on the game's ban list.";
            return false;
        }

        string id = bans.BanServerPlayers[index];
        string nick = bans.BanServerPlayerNicks[index];
        HostJournalController.NoteTarget(nick, string.Empty, "SAVED BAN");
        bans.BanServerPlayers.RemoveAt(index);
        bans.BanServerPlayerNicks.RemoveAt(index);
        SessionAuditController.ArmPersistedModerationAudit(
            "ban-removed",
            "bluesage-sidunban",
            targetSteamId,
            nick,
            SafePersona(targetSteamId),
            issuerSteamId,
            incidentId,
            "SAVED BAN");
        try
        {
            data.SaveBanData();
            message = "Ban lifted for Steam …" + Suffix(targetSteamId) + ".";
            return true;
        }
        catch (Exception ex)
        {
            SessionAuditController.CancelPersistedModerationAudit();
            bans.BanServerPlayers.Insert(index, id);
            bans.BanServerPlayerNicks.Insert(index, nick);
            Plugin.Log?.LogWarning("Unban save failed and was rolled back: " + ex.GetType().Name + ".");
            message = "The ban couldn't be lifted, so nothing changed.";
            return false;
        }
    }

    // Round 4 Stage E #3 (Blue: kick = ban via the vanilla flow, wait until their row is gone, max ~20 s, then unban).
    private static bool TryKick(CSteamID lobby, string ownerSteamId, string issuerSteamId, string targetSteamId, string incidentId, out string message)
    {
        message = "No kick was made.";
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel?.PlayerSteamIDs == null || panel.PlayerIDs == null || panel.PlayerSteamIDs.Count != panel.PlayerIDs.Count)
        {
            message = "The game's player list isn't ready yet. Nothing happened.";
            return false;
        }

        BanData bans = MonoSingleton<DataManager>.I?.BanData;
        if (bans?.BanServerPlayers != null && bans.BanServerPlayers.Contains(targetSteamId))
        {
            message = "Steam …" + Suffix(targetSteamId) + " is already banned; a kick would lift that ban. Use /sidunban " + targetSteamId + " if that's what you want.";
            return false;
        }

        if (!TryCountLiveTargetRows(panel, targetSteamId, out int liveRowMatches, out int liveIndex))
        {
            message = "The game couldn't say who is connected right now, so a kick can't confirm the player left. Use ban if you want them kept out.";
            return false;
        }

        int targetIndex = liveRowMatches == 1 ? liveIndex : panel.PlayerSteamIDs.FindIndex(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        int panelMatches = panel.PlayerSteamIDs.Count(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        bool uniquePlayerId = targetIndex >= 0 && targetIndex < panel.PlayerIDs.Count &&
            panel.PlayerIDs.Count(id => id.Equals(panel.PlayerIDs[targetIndex])) == 1;
        BanTargetPresence presence = BanTargetPresencePolicy.Classify(CountSteamLobbyMembers(lobby, targetSteamId), panelMatches, liveRowMatches, uniquePlayerId);
        if (!(presence == BanTargetPresence.InSteamLobby || presence == BanTargetPresence.Reconnected))
        {
            Plugin.Log?.LogInfo("Kick skipped for Steam …" + Suffix(targetSteamId) + ": presence=" + presence + ".");
            message = "Nothing to kick: Steam …" + Suffix(targetSteamId) + " isn't connected right now. Use ban if you want them kept out.";
            return false;
        }

        if (!TryBan(lobby, ownerSteamId, issuerSteamId, targetSteamId, incidentId, out message))
        {
            return false;
        }

        PendingKicks.Add(new PendingKick
        {
            TargetSteamId = targetSteamId,
            IssuerSteamId = issuerSteamId,
            IncidentId = incidentId,
            StartedUtcTicks = DateTime.UtcNow.Ticks,
        });
        Plugin.Log?.LogInfo("Kick started for Steam …" + Suffix(targetSteamId) + " via the game's ban flow; ban step: " + message);
        message = "Kick started: the player is removed and their ban lifts by itself within 20 s.";
        return true;
    }

    private static void CancelPendingKick(string targetSteamId)
    {
        foreach (PendingKick pending in PendingKicks)
        {
            if (string.Equals(pending.TargetSteamId, targetSteamId, StringComparison.Ordinal))
            {
                pending.PromotedToBan = true;
            }
        }
    }

    // Runs every frame on the host (Plugin.Update). Gated on the network host, not the Steam-owner check: a Steam
    // read failure must not freeze a kick with the ban in place. The unban runs directly as the owner, so a Helper
    // removed during the window cannot block it; the journal row names the original issuer.
    internal static void TickPendingKicks()
    {
        if (PendingKicks.Count == 0)
        {
            return;
        }

        bool isHost;
        try
        {
            isHost = NetworkManager.main != null && NetworkManager.main.isHost;
        }
        catch (Exception)
        {
            isHost = false;
        }

        string ownerSteamId;
        try
        {
            ownerSteamId = ((ulong)SteamUser.GetSteamID()).ToString();
        }
        catch (Exception)
        {
            ownerSteamId = string.Empty;
        }

        if (!isHost)
        {
            // Hosting ended with kicks pending: the bans stay (fail toward ban) and each one is finalized in the
            // journal with the Fable wording, so a host crash is the only silent case.
            foreach (PendingKick pending in PendingKicks)
            {
                HostJournalController.BeginSurface("kick follow-up");
                try
                {
                    HostJournalController.RecordAction("kick-timeout", ownerSteamId, ownerSteamId, pending.TargetSteamId, false, KickFollowUpPolicy.IncompleteMessage + " (hosting ended while the kick was pending; requested by Steam " + pending.IssuerSteamId + ")");
                }
                finally
                {
                    HostJournalController.EndSurface();
                }
            }

            PendingKicks.Clear();
            return;
        }

        if (ownerSteamId.Length == 0)
        {
            return;
        }

        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        long now = DateTime.UtcNow.Ticks;
        for (int index = PendingKicks.Count - 1; index >= 0; index--)
        {
            PendingKick pending = PendingKicks[index];
            int liveRows = 1;
            if (panel?.PlayerSteamIDs != null && panel.PlayerIDs != null && TryCountLiveTargetRows(panel, pending.TargetSteamId, out int counted, out _))
            {
                liveRows = counted;
            }

            KickFollowUp decision = KickFollowUpPolicy.Decide(liveRows, now - pending.StartedUtcTicks, pending.PromotedToBan);
            if (decision == KickFollowUp.Wait)
            {
                continue;
            }

            PendingKicks.RemoveAt(index);
            HostJournalController.BeginSurface("kick follow-up");
            try
            {
                if (decision == KickFollowUp.Unban)
                {
                    bool lifted = TryUnban(pending.TargetSteamId, ownerSteamId, pending.IncidentId, out string unbanMessage);
                    HostJournalController.RecordAction("unban", ownerSteamId, ownerSteamId, pending.TargetSteamId, lifted, "Kick complete (requested by Steam " + pending.IssuerSteamId + "): " + unbanMessage);
                    Plugin.AddLocalNotificationWithToast(Plugin.FormatBlueSageAction("Kick", (lifted ? "complete: the player left and the ban was lifted. " : "the player left but the ban could not be lifted: ") + unbanMessage, !lifted));
                }
                else if (decision == KickFollowUp.FailTowardBan)
                {
                    HostJournalController.RecordAction("kick-timeout", ownerSteamId, ownerSteamId, pending.TargetSteamId, false, KickFollowUpPolicy.IncompleteMessage + " (requested by Steam " + pending.IssuerSteamId + ")");
                    Plugin.AddLocalNotificationWithToast(Plugin.FormatBlueSageAction("Kick", KickFollowUpPolicy.IncompleteMessage, true));
                }
                else
                {
                    HostJournalController.RecordAction("kick-cancelled", ownerSteamId, ownerSteamId, pending.TargetSteamId, true, "An explicit ban was issued while the kick was pending; the ban stays.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Kick follow-up failed safely for Steam …" + Suffix(pending.TargetSteamId) + ": " + ex.GetType().Name + ": " + ex.Message + ". " + KickFollowUpPolicy.IncompleteMessage);
                // Round 5h: a kick whose follow-up failed leaves a ban in place; the host is told, not left guessing.
                Plugin.AddLocalNotificationWithToast(Plugin.FormatBlueSageAction("Kick", KickFollowUpPolicy.IncompleteMessage, true));
            }
            finally
            {
                HostJournalController.EndSurface();
            }
        }
    }

    // Round 4 Stage E #3 (Blue: mute = lobby-wide, keyed by the authoritative network sender, never the packet's claimed id).
    private static bool TryMute(string ownerSteamId, string issuerSteamId, string targetSteamId, out string message)
    {
        message = "No mute was made.";
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel?.PlayerSteamIDs == null || panel.PlayerIDs == null || panel.PlayerSteamIDs.Count != panel.PlayerIDs.Count)
        {
            message = "The game's player list isn't ready yet. Nothing happened.";
            return false;
        }

        if (!TryCountLiveTargetRows(panel, targetSteamId, out int liveRowMatches, out int liveIndex) || liveRowMatches != 1)
        {
            message = "That player couldn't be matched safely, so nothing happened.";
            return false;
        }

        if (!ModerationAuthorizationPolicy.CanBanTarget(targetSteamId, ownerSteamId, Plugin.LobbyHelpers?.Value ?? string.Empty, issuerSteamId, CloneIncidentLedger.IsProtectedVictim(targetSteamId), true))
        {
            message = "That player can't be muted from here (they're the host, a Helper, you, or someone being copied). Nothing happened.";
            return false;
        }

        bool added = LobbyMutedSteamIds.Add(targetSteamId);
        HostJournalController.NoteTarget(SafePersona(targetSteamId), panel.PlayerIDs[liveIndex].ToString(), "PLAYER");
        message = (added ? "Lobby-wide mute on: " : "Already muted lobby-wide: ") + "the host no longer relays chat from Steam …" + Suffix(targetSteamId) +
                  " to anyone, including you. Their own messages stop appearing for them as well, so they will notice. Voice is unaffected. Lasts for this hosting session; /sidunmute " + targetSteamId + " reverses it.";
        return true;
    }

    private static bool TryUnmute(string targetSteamId, out string message)
    {
        bool removed = LobbyMutedSteamIds.Remove(targetSteamId);
        HostJournalController.NoteTarget(SafePersona(targetSteamId), string.Empty, "PLAYER");
        message = removed
            ? "Lobby-wide mute off for Steam …" + Suffix(targetSteamId) + "; the host relays their chat again."
            : "Steam …" + Suffix(targetSteamId) + " was not muted lobby-wide.";
        return removed;
    }

    // Called by the chat relay prefix on the host for every incoming chat RPC. Exactly one roster row must claim the
    // sender's network id; otherwise the chat is let through (fail open) and the mismatch is logged once.
    internal static bool ShouldDropChatFromSender(PlayerID sender)
    {
        if (LobbyMutedSteamIds.Count == 0)
        {
            return false;
        }

        try
        {
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel?.PlayerSteamIDs == null || panel.PlayerIDs == null || panel.PlayerSteamIDs.Count != panel.PlayerIDs.Count)
            {
                return false;
            }

            int matches = 0;
            string senderSteamId = string.Empty;
            for (int index = 0; index < panel.PlayerIDs.Count; index++)
            {
                if (panel.PlayerIDs[index].Equals(sender))
                {
                    matches++;
                    senderSteamId = panel.PlayerSteamIDs[index] ?? string.Empty;
                }
            }

            if (!LobbyMutePolicy.TryResolveSender(matches))
            {
                if (!_muteResolveWarningLogged)
                {
                    _muteResolveWarningLogged = true;
                    Plugin.Log?.LogWarning("Lobby-wide mute: a chat sender could not be mapped to exactly one roster row (" + matches + " matches); the message was let through. Logged once per session.");
                }

                return false;
            }

            return LobbyMutePolicy.ShouldDrop(senderSteamId, LobbyMutedSteamIds);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Lobby-wide mute check failed open: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    internal static bool TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId)
    {
        lobby = CSteamID.Nil;
        ownerSteamId = string.Empty;
        if (!Plugin.TryGetRescueLobby(out lobby) || lobby == CSteamID.Nil || !SteamManager.Initialized ||
            NetworkManager.main == null || !NetworkManager.main.isHost) return false;
        CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby);
        CSteamID local = SteamUser.GetSteamID();
        if (owner == CSteamID.Nil || owner != local) return false;
        ownerSteamId = ((ulong)owner).ToString();
        return true;
    }

    private static bool IsUniqueSteamLobbyMember(CSteamID lobby, string steamId)
    {
        return CountSteamLobbyMembers(lobby, steamId) == 1;
    }

    // Host-only: how many player rows with this Steam ID are connected over PurrNet right now. False when the
    // connection table can't be read, so callers fall back to the Steam-membership rules.
    private static bool TryCountLiveTargetRows(PlayerPanelController panel, string steamId, out int liveRows, out int liveIndex)
    {
        liveRows = 0;
        liveIndex = -1;
        try
        {
            NetworkManager manager = NetworkManager.main;
            if (manager == null || !manager.isServer || manager.playerModule == null)
            {
                return false;
            }

            for (int index = 0; index < panel.PlayerSteamIDs.Count && index < panel.PlayerIDs.Count; index++)
            {
                if (string.Equals(panel.PlayerSteamIDs[index], steamId, StringComparison.Ordinal) &&
                    manager.playerModule.IsPlayerConnected(panel.PlayerIDs[index]))
                {
                    liveRows++;
                    liveIndex = index;
                }
            }

            return true;
        }
        catch (Exception)
        {
            liveRows = 0;
            liveIndex = -1;
            return false;
        }
    }

    private static int CountSteamLobbyMembers(CSteamID lobby, string steamId)
    {
        int matches = 0;
        int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int index = 0; index < count; index++)
        {
            if (string.Equals(((ulong)SteamMatchmaking.GetLobbyMemberByIndex(lobby, index)).ToString(), steamId, StringComparison.Ordinal)) matches++;
        }
        return matches;
    }

    private static bool RememberNonce(string nonce)
    {
        if (!SeenNonces.Add(nonce)) return false;
        NonceOrder.Enqueue(nonce);
        while (NonceOrder.Count > 128) SeenNonces.Remove(NonceOrder.Dequeue());
        return true;
    }

    private static string SafePersona(string steamId)
    {
        if (!ulong.TryParse(steamId, out ulong parsed)) return "Steam …" + Suffix(steamId);
        string name = SteamFriends.GetFriendPersonaName(new CSteamID(parsed)) ?? string.Empty;
        name = name.Replace("\r", " ").Replace("\n", " ").Trim();
        if (string.IsNullOrWhiteSpace(name)) return "Steam …" + Suffix(steamId);
        return name.Length > 48 ? name.Substring(0, 48) : name;
    }

    private static string SafeOfflinePersona(string steamId)
    {
        string persona = SafePersona(steamId);
        string value = persona + " (offline)";
        return value.Length > 48 ? value.Substring(0, 48) : value;
    }

    private static string Suffix(string steamId) => steamId != null && steamId.Length > 6 ? steamId.Substring(steamId.Length - 6) : steamId ?? "unknown";
}
