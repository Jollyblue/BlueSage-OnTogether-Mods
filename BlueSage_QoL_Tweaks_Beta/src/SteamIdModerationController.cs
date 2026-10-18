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
        if (DisplayName.Length == 0) DisplayName = "Saved native ban";
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

    internal static void PublishHostCapability()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        try
        {
            if (!TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId)) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SteamMatchmaking.SetLobbyData(lobby, CapabilityKey, ModerationCapabilityPolicy.Build(ownerSteamId, now));
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
                reason = "You are the verified current lobby host.";
                return LobbySafetyAccessState.Host;
            }

            bool listed = IsPublishedHelper(lobby, owner, local);
            if (!listed) return LobbySafetyAccessState.Denied;

            if (!ModerationCapabilityPolicy.IsFresh(
                    SteamMatchmaking.GetLobbyData(lobby, CapabilityKey),
                    owner,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            {
                reason = "This lobby lists you as a Helper, but the host has not published a fresh compatible 0.2.0 v3 member-data capability. The status tab stays visible, but protected player data and actions remain locked. No request is sent until the capability matches.";
                return LobbySafetyAccessState.HelperWaitingForHost;
            }

            reason = "This host assigned you as a Helper and published a fresh compatible v3 member-data capability.";
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
            message = "The moderation request could not reach the host; no action was taken.";
            return false;
        }
        try { SteamMatchmaking.SetLobbyMemberData(lobby, ModerationProtocolPolicy.MemberDataKey, request); }
        catch (Exception ex)
        {
            message = "The non-chat moderation request failed safely (" + ex.GetType().Name + "); no action was taken.";
            return false;
        }

        message = "Moderation request published through non-chat Steam member data for authoritative QoL-host validation. The host performs the native action; no local ban was written and no chat line was sent.";
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
        bool success = TryExecuteHostAction(request.Action, request.TargetSteamId, request.IncidentId, senderSteamId, out string result);
        Plugin.Log?.LogInfo("Host non-chat moderation request from Steam …" + Suffix(senderSteamId) + ": " + result);
        Plugin.AddLocalNotificationWithToast((success ? "Authorized helper action: " : "Helper action rejected: ") + result);
    }

    internal static bool TryExecuteHostAction(string action, string targetSteamId, string incidentId, string issuerSteamId, out string message)
    {
        message = "No moderation action was taken.";
        if (!TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId))
        {
            message = "Only the active Steam/PurrNet lobby host can write native moderation state.";
            return false;
        }

        if (!ModerationAuthorizationPolicy.IsAuthorizedIssuer(
                issuerSteamId,
                ownerSteamId,
                Plugin.LobbyHelpers?.Value ?? string.Empty,
                Plugin.LobbyHelperOwnerSteamId?.Value ?? string.Empty,
                true))
        {
            message = "The issuing Steam ID is not on this host's Helper list.";
            return false;
        }

        string scopedIncidentId = !string.Equals(issuerSteamId, ownerSteamId, StringComparison.Ordinal) &&
                                  !string.Equals(incidentId, "none", StringComparison.OrdinalIgnoreCase)
            ? "issuer-local:" + incidentId
            : incidentId;
        return action == "ban"
            ? TryBan(lobby, ownerSteamId, issuerSteamId, targetSteamId, scopedIncidentId, out message)
            : action == "unban" && TryUnban(targetSteamId, issuerSteamId, scopedIncidentId, out message);
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
            error = "current client is not the verified host";
            return false;
        }
        try
        {
            BanData bans = MonoSingleton<DataManager>.I?.BanData;
            if (bans?.BanServerPlayers == null || bans.BanServerPlayerNicks == null ||
                bans.BanServerPlayers.Count != bans.BanServerPlayerNicks.Count)
            {
                error = "native ban storage is unavailable or misaligned";
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
                    error = "native ban storage contains duplicate SteamID64 rows";
                    return false;
                }
                result.Add(new NativeBanRecord(steamId, bans.BanServerPlayerNicks[index] ?? string.Empty));
            }

            records = result;
            return true;
        }
        catch (Exception ex)
        {
            error = "native ban storage read failed safely (" + ex.GetType().Name + ")";
            return false;
        }
    }

    private static bool TryBan(CSteamID lobby, string ownerSteamId, string issuerSteamId, string targetSteamId, string incidentId, out string message)
    {
        message = "No ban was made.";
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel?.PlayerSteamIDs == null || panel.PlayerIDs == null || panel.PlayerSteamIDs.Count != panel.PlayerIDs.Count)
        {
            message = "The native player roster is missing or misaligned.";
            return false;
        }

        int targetIndex = panel.PlayerSteamIDs.FindIndex(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        int panelMatches = panel.PlayerSteamIDs.Count(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        int lobbyMatches = CountSteamLobbyMembers(lobby, targetSteamId);
        bool uniquePlayerId = targetIndex >= 0 && targetIndex < panel.PlayerIDs.Count &&
            panel.PlayerIDs.Count(id => id.Equals(panel.PlayerIDs[targetIndex])) == 1;
        bool targetPresent = targetIndex >= 0 && panelMatches == 1 && targetIndex < panel.PlayerIDs.Count &&
            uniquePlayerId && lobbyMatches == 1;
        // PlayerPanelController intentionally retains departed history rows. Steam lobby
        // membership is authoritative for absence; stale native rows must not block an
        // exact host-only offline ban after a player leaves.
        bool targetAbsent = OfflineBanPolicy.IsConfirmedAbsentFromCurrentLobby(lobbyMatches);
        bool protectedVictim = CloneIncidentLedger.IsProtectedVictim(targetSteamId);

        if (!targetPresent && !targetAbsent)
        {
            message = "Target failed the Steam/PurrNet roster alignment check. No live or offline ban was saved.";
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
                message = "Target failed the live Steam/PurrNet safety checks (protected victim, host, helper, issuer, or ambiguous).";
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
            message = "Native ban storage is unavailable or its paired lists are inconsistent.";
            return false;
        }

        if (bans.BanServerPlayers.Contains(targetSteamId))
        {
            message = "Steam …" + Suffix(targetSteamId) + " is already in the host's native ban list.";
            return true;
        }

        string persona = targetAbsent ? SafeOfflinePersona(targetSteamId) : SafePersona(targetSteamId);
        string auditDisplayName = persona;
        string auditPersona = persona;
        string auditRole = targetAbsent ? "ABSENT" : "PLAYER";
        if (targetPresent && PlayerIdentityEvidenceController.TryResolveExact(targetSteamId, out PlayerIdentityEvidence auditEvidence, out _))
        {
            auditDisplayName = auditEvidence.DisplayNameRaw;
            auditPersona = auditEvidence.SteamPersona;
            auditRole = auditEvidence.Role;
        }
        bans.BanServerPlayers.Add(targetSteamId);
        bans.BanServerPlayerNicks.Add(persona);
        SessionAuditController.ArmPersistedModerationAudit(
            "ban-added",
            targetAbsent ? "bluesage-sidban-offline" : "bluesage-sidban-live",
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
            message = "Native ban save failed and was rolled back: " + ex.GetType().Name + ".";
            return false;
        }

        if (targetAbsent)
        {
            message = "Offline native ban saved for exact SteamID64 " + targetSteamId + ". The player was not in the current lobby, so no kick was attempted. Use /sidunban " + targetSteamId + " to reverse it.";
            return true;
        }

        ChalkboardPersistenceController.NotifyModerationBoardReview(targetSteamId);
        PlayerController hostController = NetworkSingleton<TextChannelManager>.I?.MainPlayerController;
        if (hostController == null)
        {
            message = "Persisted native ban for Steam …" + Suffix(targetSteamId) + ", but the current-player kick could not be confirmed.";
            return true;
        }

        try
        {
            hostController.BanRPC(panel.PlayerIDs[targetIndex], true);
            message = "Native ban applied to suspect Steam …" + Suffix(targetSteamId) +
                      (incidentId == "none" ? "." : " from " + incidentId + ".") + " Victim IDs were not targeted.";
            return true;
        }
        catch (Exception ex)
        {
            message = "Persisted native ban for Steam …" + Suffix(targetSteamId) + ", but live kick raised " + ex.GetType().Name + ".";
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
            message = "Native ban storage is unavailable or its paired lists are inconsistent.";
            return false;
        }

        int index = bans.BanServerPlayers.FindIndex(id => string.Equals(id, targetSteamId, StringComparison.Ordinal));
        if (index < 0)
        {
            message = "Steam …" + Suffix(targetSteamId) + " is not in the host's native ban list.";
            return false;
        }

        string id = bans.BanServerPlayers[index];
        string nick = bans.BanServerPlayerNicks[index];
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
            message = "Native ban removed for Steam …" + Suffix(targetSteamId) + ".";
            return true;
        }
        catch (Exception ex)
        {
            SessionAuditController.CancelPersistedModerationAudit();
            bans.BanServerPlayers.Insert(index, id);
            bans.BanServerPlayerNicks.Insert(index, nick);
            message = "Native unban save failed and was rolled back: " + ex.GetType().Name + ".";
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
