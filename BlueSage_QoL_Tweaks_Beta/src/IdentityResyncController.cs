using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using PurrNet;
using Steamworks;

namespace BlueSage.QoLTweaks;

internal static class IdentityResyncController
{
    private const long RuntimePollIntervalMilliseconds = 500L;
    private const long RequestPollIntervalMilliseconds = 500L;
    private const int MaximumRememberedNonces = 128;

    private static readonly IdentityResyncCoordinator Coordinator =
        new IdentityResyncCoordinator();
    private static readonly HashSet<string> SeenNonces =
        new HashSet<string>(StringComparer.Ordinal);
    private static readonly Queue<string> NonceOrder =
        new Queue<string>();

    private static IdentityResyncRuntimeShape _lastShape;
    private static bool _hasLastShape;
    private static long _nextRuntimePollMilliseconds;
    private static long _nextRequestPollMilliseconds;
    private static string _nonceScope = string.Empty;
    private static string _lastTransaction = "none";
    private static long _nextAutoRefreshCheckMilliseconds;
    private static long _lastAutoRefreshUtcTicks;

    internal static void Tick()
    {
        long nowMilliseconds =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMilliseconds >= _nextRuntimePollMilliseconds)
        {
            _nextRuntimePollMilliseconds =
                nowMilliseconds + RuntimePollIntervalMilliseconds;
            ObserveRuntimeShape(nowMilliseconds);
        }
        if (nowMilliseconds >= _nextAutoRefreshCheckMilliseconds)
        {
            _nextAutoRefreshCheckMilliseconds =
                nowMilliseconds + RuntimePollIntervalMilliseconds;
            RunAutomaticRefreshIfDue(nowMilliseconds);
        }
        if (nowMilliseconds >= _nextRequestPollMilliseconds)
        {
            _nextRequestPollMilliseconds =
                nowMilliseconds + RequestPollIntervalMilliseconds;
            PollHostRequests(nowMilliseconds);
        }
    }

    internal static BlueSageCommandResult HandleCommand(
        BlueSageCommandContext context)
    {
        long nowMilliseconds =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ObserveRuntimeShape(nowMilliseconds);
        // Player text stays plain; the full key=value detail goes to the log only.
        Plugin.Log?.LogInfo("[BlueSageIdentityResync] command status: " + BuildStatus());
        LobbySafetyAccessState accessState =
            SteamIdModerationController.GetLocalAccessState(
                out _,
                out _);
        IdentityResyncPlan plan = IdentityResyncPlanPolicy.Plan(
            context?.CommandName ?? "idsync",
            context?.Arguments ?? string.Empty,
            MapAccess(accessState));
        if (!plan.Allowed)
        {
            return BlueSageCommandResult.HandledWithMessages(
                plan.Reason,
                BuildPlainStatus());
        }

        switch (plan.Action)
        {
            case IdentityResyncAction.Status:
                return BlueSageCommandResult.HandledWithMessages(
                    BuildPlainStatus(),
                    "/idsync refresh redraws the player list. The host can use /idsync repair to recheck player matches; an assigned Helper can use /idsync request to ask the host.");
            case IdentityResyncAction.RefreshLocalSurfaces:
                return RefreshLocalSurfaces();
            case IdentityResyncAction.RepairIncompleteRows:
                return ExecuteRepairFromCommand(
                    nowMilliseconds);
            case IdentityResyncAction.PublishHostRequest:
                return PublishHelperRequest(
                    nowMilliseconds);
            default:
                return BlueSageCommandResult.HandledWithMessages(
                    plan.Reason);
        }
    }

    internal static string StatusText()
    {
        ObserveRuntimeShape(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return BuildStatus();
    }

    internal static string ExecuteMenuAction(string action)
    {
        string normalized = (action ?? string.Empty)
            .Trim()
            .ToLowerInvariant();
        BlueSageCommandResult result = HandleCommand(
            new BlueSageCommandContext(
                "/idsync " + normalized,
                "idsync",
                normalized));
        return string.Join(
            " ",
            result.Messages.ToArray());
    }

    internal static void Reset()
    {
        Coordinator.Reset();
        SeenNonces.Clear();
        NonceOrder.Clear();
        _lastShape = default;
        _hasLastShape = false;
        _nextRuntimePollMilliseconds = 0L;
        _nextRequestPollMilliseconds = 0L;
        _nonceScope = string.Empty;
        _lastTransaction = "none";
    }

    // Round 4 Stage E #5 (Blue, 2026-10-19): while hosting, every 15 minutes rescan and relabel the BlueSage-owned
    // row surfaces from already-available native state and report the result in the host journal. The same thing
    // the "safe display refresh" button does; never an authoritative repair, never a moderation action.
    private static void RunAutomaticRefreshIfDue(long nowMilliseconds)
    {
        long nowTicks = DateTime.UtcNow.Ticks;
        bool isHost;
        try
        {
            isHost = NetworkManager.main != null && NetworkManager.main.isHost;
        }
        catch (Exception)
        {
            isHost = false;
        }

        if (!IdentityAutoRefreshPolicy.ShouldRun(isHost, _lastAutoRefreshUtcTicks, nowTicks, out _lastAutoRefreshUtcTicks))
        {
            return;
        }

        try
        {
            // Round 5 Stage B (audit of Blue's 37.5 h host: all 150 journal action rows were this tick, pushing
            // real kicks, mutes and bans out of the 100-row box after ~25 h). The journal records decisions and
            // changes, not heartbeats: a maintenance tick writes no journal row. Its outcome stays visible in
            // Host Tools through _lastTransaction ("local-refresh: rows=N"); the manual button keeps its row.
            RefreshLocalSurfaces();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Automatic identity refresh skipped this round: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static BlueSageCommandResult RefreshLocalSurfaces()
    {
        IReadOnlyList<string> targetedSteamIds =
            PlayerIdentityEvidenceController
                .GetCachedIdentityResyncSurfaceTargets();
        if (targetedSteamIds.Count == 0)
        {
            return BlueSageCommandResult.HandledWithMessages(
                "The player list hasn't loaded yet, so there was nothing to refresh. No player action was taken.",
                BuildPlainStatus());
        }

        Patches.PlayerIdentityRoleHighlightController
            .RefreshRowsFromAlreadyAvailableSnapshot(
                targetedSteamIds);
        _lastTransaction =
            "local-refresh: rows=" + targetedSteamIds.Count;
        return BlueSageCommandResult.HandledWithMessages(
            IdentityResyncHelpPolicy.RefreshDonePrefix +
            " No player action was taken.",
            BuildPlainStatus());
    }

    private static BlueSageCommandResult ExecuteRepairFromCommand(
        long nowMilliseconds)
    {
        if (!TryGetCurrentHostScope(
                out _,
                out _,
                out string scope))
        {
            return BlueSageCommandResult.HandledWithMessages(
                "Only the lobby host can recheck player matches. Nothing changed.");
        }

        long generation = Coordinator.CurrentGeneration;
        if (!TryExecuteRepair(
                "host-command",
                scope,
                generation,
                nowMilliseconds,
                string.Empty,
                string.Empty,
                string.Empty,
                out string result))
        {
            return BlueSageCommandResult.HandledWithMessages(
                result,
                BuildPlainStatus());
        }
        return BlueSageCommandResult.HandledWithMessages(
            result,
            BuildPlainStatus());
    }

    private static BlueSageCommandResult PublishHelperRequest(
        long nowMilliseconds)
    {
        if (SteamIdModerationController.GetLocalAccessState(
                out _,
                out _) != LobbySafetyAccessState.HelperReady ||
            !SteamIdModerationController.TryGetFreshCurrentCapability(
                out CSteamID lobby,
                out string ownerSteamId,
                out string capability))
        {
            return BlueSageCommandResult.HandledWithMessages(
                "Your Helper access isn't ready yet, so the request wasn't sent. Try again in a few seconds.");
        }

        string lobbyIdentity = ((ulong)lobby).ToString();
        string scope = IdentityResyncRequestProtocol.FingerprintScope(
            lobbyIdentity,
            ownerSteamId);
        long generation = Coordinator.CurrentGeneration;
        if (!string.Equals(
                scope,
                Coordinator.CurrentScope,
                StringComparison.Ordinal) ||
            generation <= 0L)
        {
            return BlueSageCommandResult.HandledWithMessages(
                "The lobby is still settling, so the request wasn't sent. Try again in a few seconds.");
        }

        string nonce = Guid.NewGuid()
            .ToString("N")
            .Substring(0, 16);
        if (!IdentityResyncRequestProtocol.TryBuild(
                scope,
                generation,
                IdentityResyncRequestProtocol.FingerprintCapability(
                    capability),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                nonce,
                out string payload,
                out string error))
        {
            Plugin.Log?.LogWarning(
                "[BlueSageIdentityResync] helper request not built: " +
                SafeReason(error));
            return BlueSageCommandResult.HandledWithMessages(
                "The request couldn't be sent right now. Nothing changed.");
        }

        try
        {
            SteamMatchmaking.SetLobbyMemberData(
                lobby,
                IdentityResyncRequestProtocol.MemberDataKey,
                payload);
            _lastTransaction =
                "helper-request-published: generation=" + generation;
            return BlueSageCommandResult.HandledWithMessages(
                "Asked the host to recheck player matches. They'll double-check everything first. No player action was taken.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "[BlueSageIdentityResync] helper request failed safely: " +
                ex.GetType().Name +
                "; no repair or identity mutation ran.");
            return BlueSageCommandResult.HandledWithMessages(
                "The request couldn't be sent right now. Nothing changed.");
        }
    }

    private static void PollHostRequests(long nowMilliseconds)
    {
        if (!TryGetCurrentHostScope(
                out CSteamID lobby,
                out string ownerSteamId,
                out string scope) ||
            !SteamIdModerationController.TryGetFreshCurrentCapability(
                out CSteamID capabilityLobby,
                out string capabilityOwner,
                out string capability) ||
            capabilityLobby != lobby ||
            !string.Equals(
                capabilityOwner,
                ownerSteamId,
                StringComparison.Ordinal) ||
            !string.Equals(
                scope,
                Coordinator.CurrentScope,
                StringComparison.Ordinal))
        {
            return;
        }

        ReconcileNonceScope(scope);
        long generation = Coordinator.CurrentGeneration;
        string capabilityFingerprint =
            IdentityResyncRequestProtocol.FingerprintCapability(
                capability);
        int memberCount =
            SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int index = 0; index < memberCount; index++)
        {
            CSteamID member =
                SteamMatchmaking.GetLobbyMemberByIndex(
                    lobby,
                    index);
            string senderSteamId = ((ulong)member).ToString();
            if (string.Equals(
                    senderSteamId,
                    ownerSteamId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string payload =
                SteamMatchmaking.GetLobbyMemberData(
                    lobby,
                    member,
                    IdentityResyncRequestProtocol.MemberDataKey) ??
                string.Empty;
            if (!IdentityResyncRequestProtocol.TryParse(
                    payload,
                    out IdentityResyncRequest request,
                    out _) ||
                !IdentityResyncRequestProtocol.IsFresh(
                    request,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()) ||
                !string.Equals(
                    request.LobbyFingerprint,
                    scope,
                    StringComparison.Ordinal) ||
                request.Generation != generation ||
                !string.Equals(
                    request.CapabilityFingerprint,
                    capabilityFingerprint,
                    StringComparison.Ordinal) ||
                !SteamIdModerationController
                    .IsUniqueCurrentLobbyMember(
                        lobby,
                        senderSteamId) ||
                !SteamIdModerationController
                    .IsAuthorizedCurrentIssuer(
                        senderSteamId) ||
                IsNonceRemembered(request.Nonce))
            {
                continue;
            }

            TryExecuteRepair(
                "helper-request",
                scope,
                generation,
                nowMilliseconds,
                senderSteamId,
                capabilityFingerprint,
                request.Nonce,
                out string result);
            Plugin.AddLocalNotificationWithToast(
                "A Helper asked for a player recheck. " +
                result);
        }
    }

    private static bool TryExecuteRepair(
        string source,
        string expectedScope,
        long expectedGeneration,
        long nowMilliseconds,
        string helperSenderSteamId,
        string expectedCapabilityFingerprint,
        string requestNonce,
        out string result)
    {
        result =
            "The player recheck didn't run. Nothing changed.";
        if (!TryGetCurrentHostScope(
                out CSteamID originalLobby,
                out string originalOwner,
                out string currentScope) ||
            !string.Equals(
                expectedScope,
                currentScope,
                StringComparison.Ordinal) ||
            expectedGeneration != Coordinator.CurrentGeneration)
        {
            result =
                "The lobby changed before the recheck could start, so it stopped. Nothing changed.";
            return false;
        }

        if (!Coordinator.TryBegin(
                expectedScope,
                expectedGeneration,
                nowMilliseconds,
                out IdentityResyncLease lease,
                out IdentityResyncBeginDecision beginDecision))
        {
            result = FormatBeginRefusal(beginDecision);
            return false;
        }
        if (!string.IsNullOrWhiteSpace(requestNonce) &&
            !RememberNonce(requestNonce))
        {
            Coordinator.Abandon(lease);
            result =
                "That request was already handled. Nothing changed.";
            return false;
        }

        var stopwatch = Stopwatch.StartNew();
        int targetedRows = 0;
        int healedRows = 0;
        bool capturePerformed = false;
        string outcome = "refused";
        IdentityResyncCoverage after =
            PlayerIdentityEvidenceController
                .GetCachedIdentityResyncCoverage();
        try
        {
            PlayerIdentityEvidenceController
                .ForceRefreshIncompleteRosterRowsOnce(
                    out _,
                    out _,
                    out _,
                    out IReadOnlyList<string> targetedSteamIds,
                    out healedRows,
                    out capturePerformed);
            targetedRows = targetedSteamIds.Count;
            after = PlayerIdentityEvidenceController
                .GetCachedIdentityResyncCoverage();

            if (!TryGetCurrentHostScope(
                    out CSteamID revalidatedLobby,
                    out string revalidatedOwner,
                    out string revalidatedScope) ||
                revalidatedLobby != originalLobby ||
                !string.Equals(
                    revalidatedOwner,
                    originalOwner,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    revalidatedScope,
                    expectedScope,
                    StringComparison.Ordinal) ||
                expectedGeneration != Coordinator.CurrentGeneration)
            {
                result =
                    "The lobby changed during the recheck, so it stopped. Nothing changed.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(
                    helperSenderSteamId))
            {
                if (!SteamIdModerationController
                        .TryGetFreshCurrentCapability(
                            out CSteamID capabilityLobby,
                            out string capabilityOwner,
                            out string capability) ||
                    capabilityLobby != originalLobby ||
                    !string.Equals(
                        capabilityOwner,
                        originalOwner,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        IdentityResyncRequestProtocol
                            .FingerprintCapability(capability),
                        expectedCapabilityFingerprint,
                        StringComparison.Ordinal) ||
                    !SteamIdModerationController
                        .IsUniqueCurrentLobbyMember(
                            originalLobby,
                            helperSenderSteamId) ||
                    !SteamIdModerationController
                        .IsAuthorizedCurrentIssuer(
                            helperSenderSteamId) ||
                    !PlayerIdentityEvidenceController
                        .TryResolveExactCached(
                            helperSenderSteamId,
                            out _,
                            out _))
                {
                    result =
                        "The Helper's request couldn't be confirmed, so the recheck stopped. Nothing changed.";
                    return false;
                }
            }

            Patches.PlayerIdentityRoleHighlightController
                .RefreshRows(targetedSteamIds);
            outcome = after.Ready
                ? "ready"
                : "locked";
            result =
                IdentityResyncHelpPolicy.RecheckDonePrefix +
                " " +
                (after.Ready
                    ? "Every visible player is matched safely."
                    : "Some players are still settling, so actions on them stay paused.") +
                " No player action was taken.";
            return true;
        }
        catch (Exception ex)
        {
            outcome = "failed-" +
                      ex.GetType().Name;
            result =
                "The recheck hit a problem and stopped safely. Actions on unmatched players stay paused. No player action was taken.";
            return false;
        }
        finally
        {
            stopwatch.Stop();
            Coordinator.Complete(
                lease,
                DateTimeOffset.UtcNow
                    .ToUnixTimeMilliseconds());
            _lastTransaction =
                source +
                ": outcome=" +
                outcome +
                ", targetedRows=" +
                targetedRows +
                ", healedRows=" +
                healedRows +
                ", lockedRows=" +
                after.LockedRows +
                ", durationMs=" +
                stopwatch.ElapsedMilliseconds;
            string boundedReason =
                SafeReason(after.Reason);
            Plugin.Log?.LogInfo(
                "[BlueSageIdentityResync]" +
                " source=" +
                source +
                "; outcome=" +
                outcome +
                "; targetedRows=" +
                targetedRows +
                "; healedRows=" +
                healedRows +
                "; lockedRows=" +
                after.LockedRows +
                "; capturePerformed=" +
                capturePerformed +
                "; durationMs=" +
                stopwatch.ElapsedMilliseconds +
                "; reason=" +
                boundedReason +
                ".");
            SessionAuditController.RecordIdentityResync(
                source,
                outcome,
                targetedRows,
                healedRows,
                after.LockedRows,
                stopwatch.ElapsedMilliseconds,
                boundedReason);
        }
    }

    private static void ObserveRuntimeShape(
        long nowMilliseconds)
    {
        if (!TryCaptureRuntimeShape(
                out IdentityResyncRuntimeShape current))
        {
            Coordinator.ObserveScope(
                string.Empty,
                string.Empty,
                nowMilliseconds);
            _lastShape = default;
            _hasLastShape = false;
            return;
        }

        IdentityResyncTrigger trigger = _hasLastShape
            ? IdentityResyncTriggerPolicy.Classify(
                _lastShape,
                current)
            : IdentityResyncTrigger
                .LobbyOrOwnerIdentityChanged;
        if (trigger == IdentityResyncTrigger.None)
        {
            Coordinator.ObserveScope(
                current.LobbyFingerprint,
                current.OwnerFingerprint,
                nowMilliseconds);
        }
        else
        {
            Coordinator.ObserveTrigger(
                current.LobbyFingerprint,
                current.OwnerFingerprint,
                trigger,
                nowMilliseconds);
        }
        _lastShape = current;
        _hasLastShape = true;
    }

    private static bool TryCaptureRuntimeShape(
        out IdentityResyncRuntimeShape shape)
    {
        shape = default;
        try
        {
            if (!SteamManager.Initialized ||
                !Plugin.TryGetRescueLobby(
                    out CSteamID lobby) ||
                lobby == CSteamID.Nil)
            {
                return false;
            }
            CSteamID owner =
                SteamMatchmaking.GetLobbyOwner(lobby);
            if (owner == CSteamID.Nil)
            {
                return false;
            }

            string lobbyIdentity = ((ulong)lobby).ToString();
            string ownerIdentity = ((ulong)owner).ToString();
            PlayerPanelController panel =
                NetworkSingleton<PlayerPanelController>.I;
            int panelToken =
                panel != null
                    ? panel.GetInstanceID()
                    : 0;
            int activeRows = 0;
            long signature = 1469598103934665603L;
            if (panel?.PlayerItemControllers != null)
            {
                foreach (PlayerItemController row in
                         panel.PlayerItemControllers)
                {
                    if (row == null ||
                        row.gameObject == null ||
                        !row.gameObject.activeSelf)
                    {
                        continue;
                    }
                    activeRows++;
                    signature = Mix(
                        signature,
                        row.GetInstanceID());
                }
            }
            if (panel?.PlayerSteamIDs != null)
            {
                foreach (string steamId in
                         panel.PlayerSteamIDs)
                {
                    signature = Mix(
                        signature,
                        steamId ?? string.Empty);
                }
            }
            if (panel?.PlayerIDs != null)
            {
                foreach (PlayerID playerId in
                         panel.PlayerIDs)
                {
                    signature = Mix(
                        signature,
                        playerId.GetHashCode());
                }
            }
            if (panel?.PlayerTransforms != null)
            {
                foreach (NetworkTransform playerTransform in
                         panel.PlayerTransforms)
                {
                    signature = Mix(
                        signature,
                        playerTransform != null
                            ? playerTransform.GetInstanceID()
                            : 0);
                }
            }

            var steamMembership = new List<string>();
            int steamMemberCount =
                SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < steamMemberCount; index++)
            {
                steamMembership.Add(
                    ((ulong)SteamMatchmaking.GetLobbyMemberByIndex(
                        lobby,
                        index)).ToString(
                        CultureInfo.InvariantCulture));
            }

            var purrNetMembership = new List<string>();
            NetworkManager manager = NetworkManager.main;
            if (manager?.playerModule == null ||
                manager.playerModule.players == null)
            {
                return false;
            }
            foreach (PlayerID playerId in manager.playerModule.players)
            {
                if (!manager.playerModule.IsValidPlayer(playerId) ||
                    (manager.isServer &&
                     !manager.playerModule.IsPlayerConnected(playerId)))
                {
                    continue;
                }
                purrNetMembership.Add(
                    ((ulong)playerId.id).ToString(
                        CultureInfo.InvariantCulture));
            }

            shape = new IdentityResyncRuntimeShape(
                IdentityResyncRequestProtocol
                    .FingerprintScope(
                        lobbyIdentity,
                        ownerIdentity),
                IdentityResyncRequestProtocol
                    .FingerprintCapability(
                        "owner|" +
                        ownerIdentity),
                panelToken,
                activeRows,
                signature,
                IdentityAuthorityFingerprintPolicy.Build(
                    steamMembership),
                IdentityAuthorityFingerprintPolicy.Build(
                    purrNetMembership));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetCurrentHostScope(
        out CSteamID lobby,
        out string ownerSteamId,
        out string scope)
    {
        scope = string.Empty;
        if (!SteamIdModerationController
                .TryGetOwnedHostLobby(
                    out lobby,
                    out ownerSteamId))
        {
            return false;
        }
        scope = IdentityResyncRequestProtocol
            .FingerprintScope(
                ((ulong)lobby).ToString(),
                ownerSteamId);
        return true;
    }

    private static void ReconcileNonceScope(string scope)
    {
        if (string.Equals(
                _nonceScope,
                scope,
                StringComparison.Ordinal))
        {
            return;
        }
        _nonceScope = scope;
        SeenNonces.Clear();
        NonceOrder.Clear();
    }

    private static bool RememberNonce(string nonce)
    {
        if (!SeenNonces.Add(nonce))
        {
            return false;
        }
        NonceOrder.Enqueue(nonce);
        while (NonceOrder.Count >
               MaximumRememberedNonces)
        {
            SeenNonces.Remove(
                NonceOrder.Dequeue());
        }
        return true;
    }

    private static bool IsNonceRemembered(string nonce)
    {
        return SeenNonces.Contains(nonce ?? string.Empty);
    }

    private static string BuildStatus()
    {
        IdentityResyncCoverage coverage =
            PlayerIdentityEvidenceController
                .GetCachedIdentityResyncCoverage();
        return
            "Identity resync status: coverage=" +
            (coverage.Ready ? "READY" : "LOCKED") +
            "; verifiedRows=" +
            coverage.VerifiedRows +
            "; nativeRows=" +
            coverage.RawNativeRows +
            "; activeNativeRows=" +
            coverage.NativeRows +
            "; purrNetPlayers=" +
            coverage.PurrNetRows +
            "; steamMembers=" +
            coverage.SteamMembers +
            "; lockedRows=" +
            coverage.LockedRows +
            "; excludedStaleRows=" +
            coverage.ExcludedNativeRows +
            "; generation=" +
            Coordinator.CurrentGeneration +
            "; inFlight=" +
            Coordinator.IsInFlight +
            "; why=" +
            SafeReason(coverage.Reason) +
            "; lastTransaction=" +
            _lastTransaction +
            ".";
    }

    private static string BuildPlainStatus()
    {
        IdentityResyncCoverage coverage =
            PlayerIdentityEvidenceController
                .GetCachedIdentityResyncCoverage();
        return coverage.Ready
            ? "Player matching: healthy."
            : "Player matching: still settling.";
    }

    private static string FormatBeginRefusal(
        IdentityResyncBeginDecision decision)
    {
        Plugin.Log?.LogInfo(
            "[BlueSageIdentityResync] repair not started: reason=" +
            decision.Reason +
            "; retryAfterMs=" +
            decision.RetryAfterMilliseconds +
            ".");
        if (decision.Reason ==
            IdentityResyncBeginReason.QuietWindow)
        {
            return
                "The player list is still settling. Try again in a second. Nothing changed.";
        }
        if (decision.Reason ==
            IdentityResyncBeginReason.GlobalCooldown)
        {
            long seconds =
                Math.Max(
                    1L,
                    (decision.RetryAfterMilliseconds +
                     999L) /
                    1000L);
            return
                "Player matches were just rechecked. Try again in " +
                seconds +
                (seconds == 1L ? " second." : " seconds.") +
                " Nothing changed.";
        }
        return
            "The recheck couldn't start right now. Nothing changed.";
    }

    private static IdentityResyncAccess MapAccess(
        LobbySafetyAccessState state)
    {
        switch (state)
        {
            case LobbySafetyAccessState.Host:
                return IdentityResyncAccess.Host;
            case LobbySafetyAccessState.HelperReady:
                return IdentityResyncAccess.HelperReady;
            case LobbySafetyAccessState.HelperWaitingForHost:
                return IdentityResyncAccess
                    .HelperWaitingForHost;
            default:
                return IdentityResyncAccess.Ordinary;
        }
    }

    private static string SafeReason(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value)
            ? "none"
            : value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace(";", ",")
                .Trim();
        return safe.Length <= 160
            ? safe
            : safe.Substring(0, 160);
    }

    private static long Mix(long hash, int value)
    {
        unchecked
        {
            return (hash ^ value) *
                   1099511628211L;
        }
    }

    private static long Mix(long hash, string value)
    {
        unchecked
        {
            string text = value ?? string.Empty;
            for (int index = 0;
                 index < text.Length;
                 index++)
            {
                hash = (hash ^ text[index]) *
                       1099511628211L;
            }
            return hash;
        }
    }
}
