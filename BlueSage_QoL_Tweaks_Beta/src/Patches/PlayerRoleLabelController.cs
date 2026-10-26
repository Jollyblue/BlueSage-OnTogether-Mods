using System;
using System.Threading;
using BlueSage.QoLTweaks.Core;
using BlueSage.QoLTweaks.Contracts;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

internal static class PlayerRoleLabelController
{
    internal const string MemberDataKey =
        "bluesage_role_label_v1";
    private const float PublishIntervalSeconds = 2f;
    // Round 5c: the version string never changes inside a lobby, so it is written on lobby entry and then only
    // every 30 s (member-data writes are broadcast to every member; the 2 s cadence belongs to the heartbeat).
    private const float VersionPublishIntervalSeconds = 30f;
    private static float _nextVersionPublishAt;

    private static string _localHostLabelOverride =
        string.Empty;
    private static float _nextRolePublishAt;
    private static float _nextCompatiblePublishAt;
    private static ulong _compatibleLobbyKey;
    private static long _hostAuthorityNonce;

    internal static void Tick()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        PublishHostHeartbeat();
        PublishCompatibleClientHeartbeat();
        PlayerBadgeController.Tick();
    }

    internal static bool TrySetLocalHostLabelOverride(
        string richText,
        out string result)
    {
        result = string.Empty;
        if (!SteamIdModerationController.TryGetOwnedHostLobby(
                out CSteamID lobby,
                out _))
        {
            result =
                "Host role label was not changed because this client is not the exact current Steam lobby owner and active host.";
            return false;
        }

        if (!RoleLabelProtocolPolicy.TryValidateLabel(
                richText,
                out string clean,
                out string validationError))
        {
            result = validationError;
            return false;
        }

        bool exactIdentityGenerationFresh =
            PlayerBadgeController.TryResolveFreshLocalCreatorContribution(
                lobby,
                out bool contributedBadgePresent,
                out string freshnessReason);
        bool privateHostAuthorityFresh =
            TryResolveFreshPrivateHostAuthority();
        if (!CreatorHostLabelPublicationPolicy.CanPublish(
                exactOwnedHost: true,
                exactIdentityGenerationFresh,
                contributedBadgePresent,
                privateHostAuthorityFresh))
        {
            StopAndClearPublishedHostLabel(
                lobby,
                out _);
            // Round 5 C2 step 1: name the failing input so the refusal is diagnosable from the log.
            result =
                "Host role label was not changed because fresh exact creator or approved private-fleet host authority is unavailable." +
                " Exact-identity check: " +
                (exactIdentityGenerationFresh ? "fresh" : (freshnessReason.Length > 0 ? freshnessReason : "not fresh")) +
                "; creator badge: " +
                (contributedBadgePresent ? "present" : "absent") +
                "; private host authority: " +
                (privateHostAuthorityFresh ? "ready" : "unavailable") +
                // Round 5d (advisor): the two counters the per-row lock (round 5e) will be built from, so the first
                // live refusal confirms or refutes the "earlier evidence generation" prediction with numbers.
                "; companion generation " + PlayerBadgeController.CompanionEvidenceGenerationForDiagnostics +
                "; authority generation " + PlayerBadgeController.AuthorityEvidenceGenerationForDiagnostics +
                "; registry keyed to " + PlayerBadgeController.RegistryKeyForDiagnostics +
                ".";
            return false;
        }

        _localHostLabelOverride = clean;
        if (!TryPublishRoleLabel(
                lobby,
                RoleLabelProtocolAction.Set,
                clean,
                out string publishError))
        {
            _localHostLabelOverride = string.Empty;
            result = publishError;
            return false;
        }

        result =
            "Host role-label compatibility heartbeat published. Current public QoL rows use the separate exact-truth badge strip and never rewrite player names.";
        return true;
    }

    internal static bool TryClearLocalHostLabelOverride(
        out string result)
    {
        result = string.Empty;
        if (!SteamIdModerationController.TryGetOwnedHostLobby(
                out CSteamID lobby,
                out _))
        {
            result =
                "Host role label was not cleared because this client is not the exact current Steam lobby owner and active host.";
            return false;
        }

        if (!StopAndClearPublishedHostLabel(
                lobby,
                out string publishError))
        {
            result = publishError;
            return false;
        }

        result =
            "Host role-label compatibility heartbeat cleared. The exact-truth badge strip is unchanged.";
        return true;
    }

    internal static void PublishHostHeartbeat()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        if (string.IsNullOrWhiteSpace(
                _localHostLabelOverride) ||
            Time.unscaledTime < _nextRolePublishAt)
        {
            return;
        }

        bool exactOwnedHost =
            SteamIdModerationController.TryGetOwnedHostLobby(
                out CSteamID lobby,
                out _);
        bool exactIdentityGenerationFresh = false;
        bool contributedBadgePresent = false;
        bool privateHostAuthorityFresh = false;
        if (exactOwnedHost)
        {
            exactIdentityGenerationFresh =
                PlayerBadgeController.TryResolveFreshLocalCreatorContribution(
                    lobby,
                    out contributedBadgePresent);
            privateHostAuthorityFresh =
                TryResolveFreshPrivateHostAuthority();
        }
        CreatorHostLabelHeartbeatDecision decision =
            CreatorHostLabelPublicationPolicy.DecideHeartbeat(
                labelActive: true,
                heartbeatDue: true,
                exactOwnedHost,
                exactIdentityGenerationFresh,
                contributedBadgePresent,
                privateHostAuthorityFresh);
        if (decision ==
            CreatorHostLabelHeartbeatDecision.ClearAndStop)
        {
            StopAndClearPublishedHostLabel(
                lobby,
                out string clearError);
            Plugin.Log?.LogDebug(
                "Host role-label compatibility heartbeat stopped: " +
                clearError);
            return;
        }
        if (decision != CreatorHostLabelHeartbeatDecision.Publish)
        {
            return;
        }

        if (!TryPublishRoleLabel(
                lobby,
                RoleLabelProtocolAction.Set,
                _localHostLabelOverride,
                out string error))
        {
            Plugin.Log?.LogDebug(
                "Host role-label compatibility heartbeat skipped: " +
                error);
        }
    }

    internal static void ClearPublishedHostLabel()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        try
        {
            if (SteamIdModerationController.TryGetOwnedHostLobby(
                    out CSteamID lobby,
                    out _))
            {
                TryPublishRoleLabel(
                    lobby,
                    RoleLabelProtocolAction.Clear,
                    string.Empty,
                    out _);
            }
            ClearCompatibleClientHeartbeat();
        }
        catch
        {
            // Member-data expiry and lobby teardown remain fail-closed.
        }

        _localHostLabelOverride = string.Empty;
        _nextRolePublishAt = 0f;
    }

    internal static void ClearPublishedStateForShutdown(SteamRuntimeSnapshot snapshot)
    {
        if (snapshot != null &&
            snapshot.SteamWasReady &&
            snapshot.Lobby != CSteamID.Nil)
        {
            try
            {
                SteamMatchmaking.SetLobbyMemberData(
                    snapshot.Lobby,
                    MemberDataKey,
                    string.Empty);
                SteamMatchmaking.SetLobbyMemberData(
                    snapshot.Lobby,
                    CompatibleClientPresencePolicy.MemberDataKey,
                    string.Empty);
                SteamMatchmaking.SetLobbyMemberData(
                    snapshot.Lobby,
                    CompatibleClientPresencePolicy.VersionMemberDataKey,
                    string.Empty);
            }
            catch
            {
                // Cached-lobby cleanup is best-effort and expires fail-closed.
            }
        }

        _localHostLabelOverride = string.Empty;
        _nextRolePublishAt = 0f;
        _nextCompatiblePublishAt = 0f;
        _compatibleLobbyKey = 0;
    }

    internal static void RestoreAll()
    {
        ClearCompatibleClientHeartbeat();
        PlayerBadgeController.ClearAll();
        _localHostLabelOverride = string.Empty;
        _nextRolePublishAt = 0f;
        _nextCompatiblePublishAt = 0f;
        _compatibleLobbyKey = 0;
    }

    private static bool StopAndClearPublishedHostLabel(
        CSteamID preferredLobby,
        out string error)
    {
        _localHostLabelOverride = string.Empty;
        _nextRolePublishAt = 0f;

        CSteamID lobby = preferredLobby;
        if (lobby == CSteamID.Nil &&
            (!SteamManager.Initialized ||
             !Plugin.TryGetRescueLobby(out lobby) ||
             lobby == CSteamID.Nil))
        {
            error =
                "Role-label member-data clear could not reach a current Steam lobby; the stale packet will expire fail closed.";
            return false;
        }

        bool cleared = TryPublishRoleLabel(
            lobby,
            RoleLabelProtocolAction.Clear,
            string.Empty,
            out error);
        _localHostLabelOverride = string.Empty;
        _nextRolePublishAt = 0f;
        return cleared;
    }

    private static void PublishCompatibleClientHeartbeat()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        if (!SteamManager.Initialized ||
            !Plugin.TryGetRescueLobby(out CSteamID lobby) ||
            lobby == CSteamID.Nil)
        {
            _compatibleLobbyKey = 0;
            _nextCompatiblePublishAt = 0f;
            return;
        }

        ulong lobbyKey = (ulong)lobby;
        if (_compatibleLobbyKey != lobbyKey)
        {
            _compatibleLobbyKey = lobbyKey;
            _nextCompatiblePublishAt = 0f;
            _nextVersionPublishAt = 0f;
        }
        if (Time.unscaledTime < _nextCompatiblePublishAt)
        {
            return;
        }

        long now =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!CompatibleClientPresencePolicy.TryBuild(
                now,
                out string payload))
        {
            return;
        }

        try
        {
            // Round 5c: the version rides on its own key so older clients keep parsing the v1 heartbeat. It is
            // written BEFORE the heartbeat (a peer polling between the two never sees a fresh heartbeat without a
            // version) and only on lobby entry or every 30 s (the value never changes inside a lobby).
            if (Time.unscaledTime >= _nextVersionPublishAt &&
                CompatibleClientPresencePolicy.TryBuildVersionPayload(
                    Plugin.PluginVersion,
                    out string versionPayload))
            {
                SteamMatchmaking.SetLobbyMemberData(
                    lobby,
                    CompatibleClientPresencePolicy.VersionMemberDataKey,
                    versionPayload);
                _nextVersionPublishAt =
                    Time.unscaledTime +
                    VersionPublishIntervalSeconds;
            }

            SteamMatchmaking.SetLobbyMemberData(
                lobby,
                CompatibleClientPresencePolicy.MemberDataKey,
                payload);
            _nextCompatiblePublishAt =
                Time.unscaledTime +
                PublishIntervalSeconds;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug(
                "Compatible-client heartbeat skipped: " +
                ex.GetType().Name);
        }
    }

    private static void ClearCompatibleClientHeartbeat()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        try
        {
            if (SteamManager.Initialized &&
                Plugin.TryGetRescueLobby(out CSteamID lobby) &&
                lobby != CSteamID.Nil)
            {
                SteamMatchmaking.SetLobbyMemberData(
                    lobby,
                    CompatibleClientPresencePolicy.MemberDataKey,
                    string.Empty);
                SteamMatchmaking.SetLobbyMemberData(
                    lobby,
                    CompatibleClientPresencePolicy.VersionMemberDataKey,
                    string.Empty);
            }
        }
        catch
        {
            // Best-effort unload cleanup; stale heartbeats expire quickly.
        }
        _compatibleLobbyKey = 0;
        _nextCompatiblePublishAt = 0f;
    }

    private static bool TryPublishRoleLabel(
        CSteamID lobby,
        RoleLabelProtocolAction action,
        string label,
        out string error)
    {
        if (PluginShutdownController.IsShuttingDown)
        {
            error = "Role-label member-data publish is unavailable during shutdown.";
            return false;
        }
        string nonce =
            Guid.NewGuid().ToString("N").Substring(0, 12);
        long now =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool built =
            action == RoleLabelProtocolAction.Set
                ? RoleLabelProtocolPolicy.TryBuildSet(
                    label,
                    now,
                    nonce,
                    out string payload,
                    out error)
                : RoleLabelProtocolPolicy.TryBuildClear(
                    now,
                    nonce,
                    out payload,
                    out error);
        if (!built)
        {
            return false;
        }

        try
        {
            SteamMatchmaking.SetLobbyMemberData(
                lobby,
                MemberDataKey,
                payload);
            _nextRolePublishAt =
                Time.unscaledTime +
                PublishIntervalSeconds;
            return true;
        }
        catch (Exception ex)
        {
            error =
                "Role-label member-data publish failed: " +
                ex.GetType().Name +
                ".";
            return false;
        }
    }

    private static bool TryResolveFreshPrivateHostAuthority()
    {
        long nonce = Interlocked.Increment(ref _hostAuthorityNonce);
        if (nonce <= 0)
        {
            Interlocked.Exchange(ref _hostAuthorityNonce, 1);
            nonce = 1;
        }

        long nowUtcTicks = DateTime.UtcNow.Ticks;
        return BlueSageExtensionRegistry.TryReadHostCapability(
                   isHost: true,
                   nonce,
                   nowUtcTicks,
                   out BlueSageHostCapabilitySnapshotV1 snapshot) &&
               snapshot.State == BlueSageHostCapabilityStateV1.Ready;
    }
}
