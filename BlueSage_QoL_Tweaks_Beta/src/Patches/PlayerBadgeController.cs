using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using BlueSage.QoLTweaks.Contracts;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

internal static class PlayerBadgeController
{
    private const string BadgeStripName = "BlueSage_BadgeStrip";
    private const float MinimumAccessibleFontSize = 14f;
    private const float MaximumAccessibleFontSize = 18f;
    private const float BadgeHorizontalPadding = 0f;
    private const float BadgeSegmentGap = 4f;
    private const float NativeNameBadgeGap = 0f;
    private const float OverflowLaneGap = 2f;
    private const int BadgeCapacity = 7;
    private const float MinimumReadableNativeNameWidth = 160f;
    private static readonly FieldInfo PlayerNameTextField =
        AccessTools.Field(
            typeof(PlayerItemController),
            "_playerNameText");
    private static readonly Dictionary<int, BlueSagePlayerBadgeMarker>
        OwnedMarkersByRowInstanceId =
            new Dictionary<int, BlueSagePlayerBadgeMarker>();
    private static readonly Dictionary<string, BlueSageClientCapabilitySnapshotV1>
        CompatibleClientSnapshotsBySteamId =
            new Dictionary<string, BlueSageClientCapabilitySnapshotV1>(
                StringComparer.Ordinal);
    private static readonly Dictionary<string, string>
        CompatibleClientPayloadsBySteamId =
            new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly HashSet<string>
        CurrentLobbyMemberSteamIds =
            new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string>
        FreshCompatibleClientSteamIds =
            new HashSet<string>(StringComparer.Ordinal);

    private readonly struct SharedLobbyEvidence
    {
        internal SharedLobbyEvidence(
            ulong lobbyId,
            CSteamID owner,
            string ownerSteamId,
            bool moderationCapabilityFresh,
            HashSet<string> helpers,
            bool localSafetyAuthorized)
        {
            LobbyId = lobbyId;
            Owner = owner;
            OwnerSteamId = ownerSteamId;
            ModerationCapabilityFresh = moderationCapabilityFresh;
            Helpers = helpers;
            LocalSafetyAuthorized = localSafetyAuthorized;
        }

        internal ulong LobbyId { get; }
        internal CSteamID Owner { get; }
        internal string OwnerSteamId { get; }
        internal bool ModerationCapabilityFresh { get; }
        internal HashSet<string> Helpers { get; }
        internal bool LocalSafetyAuthorized { get; }
    }

    private static ulong _lobbyKey;
    private static long _lobbyEpoch;
    private static long _evidenceGeneration;
    private static long _companionEvidenceGeneration;
    private static long _badgePresentationGeneration;
    private static string _evidenceFingerprint =
        string.Empty;
    private static string _companionEvidenceFingerprint =
        string.Empty;
    private static string _selfLayoutDiagnosticFingerprint =
        string.Empty;
    private static bool _evidenceContextValid;
    private static bool _companionEvidenceContextValid;
    private static long _compatibleClientSnapshotGeneration;
    private static bool _compatibleClientPresentationContextValid;
    private static SharedLobbyEvidence _sharedLobbyEvidence;
    private static bool _sharedLobbyEvidenceValid;
    private static long _requestNonce;
    private static float _nextPollAt;

    internal static void RegisterRow(
        PlayerItemController row,
        string playerSteamId)
    {
        if (row == null ||
            string.IsNullOrWhiteSpace(playerSteamId))
        {
            return;
        }

        try
        {
            TMP_Text nativeName =
                PlayerNameTextField?.GetValue(row) as TMP_Text;
            if (nativeName == null)
            {
                return;
            }

            RectTransform nameRect = nativeName.rectTransform;
            if (nameRect == null || nameRect.parent == null)
            {
                return;
            }

            int rowId = row.GetInstanceID();
            BlueSagePlayerBadgeMarker marker =
                FindOwnedMarker(row);
            int anchorId = nameRect.GetInstanceID();
            if (marker != null &&
                (!string.Equals(
                     marker.BoundSteamId,
                     playerSteamId,
                     StringComparison.Ordinal) ||
                 marker.NameAnchorInstanceId != anchorId ||
                 !marker.HasLivePresentation(nativeName)))
            {
                DestroyOwnedMarker(marker);
                marker = null;
            }

            if (marker == null)
            {
                marker = CreateOwnedMarker(
                    row,
                    nativeName,
                    nameRect);
            }
            if (marker == null)
            {
                return;
            }

            marker.BoundSteamId =
                playerSteamId;
            marker.OwnerRow = row;
            marker.OwnerRowInstanceId = rowId;
            marker.NameAnchorInstanceId = anchorId;
            marker.BindNativeName(nativeName);
            marker.Generation++;
            marker.BeginRowLifecycle();
            marker.gameObject.SetActive(true);
            OwnedMarkersByRowInstanceId[rowId] =
                marker;
            RefreshMarker(marker);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug(
                "Player badge row registration failed closed: " +
                ex.GetType().Name);
        }
    }

    internal static void Tick()
    {
        if (Time.unscaledTime < _nextPollAt)
        {
            return;
        }

        _nextPollAt =
            Time.unscaledTime +
            LargeLobbyRefreshPolicy.BadgePollIntervalSecondsForRoster(
                CountActiveRegisteredRows());
        try
        {
            if (!TryGetLobby(out CSteamID lobby))
            {
                LeaveLobby();
                HideAllPresentations();
                PruneInactivePooledRows();
                return;
            }

            RefreshCompatibleClientPresentationContext(lobby);
            RefreshSharedLobbyEvidence(lobby);

            if (!BeginLobby((ulong)lobby) ||
                !PlayerIdentityEvidenceController
                    .TryCaptureCompleteAuthorityEvidenceAndRowsCached(
                        (ulong)lobby,
                        out string fingerprint,
                        out HashSet<PlayerItemController> currentRows) ||
                !RefreshEvidenceGeneration(fingerprint))
            {
                RefreshPresentationsWithoutCompleteRoster();
                return;
            }
            PlayerPanelSurfaceController.PruneRowsNotIn(currentRows);
            PruneRowsNotIn(currentRows);
            PruneInactivePooledRows();
            RefreshCompatibleClientSnapshots(lobby);
            foreach (BlueSagePlayerBadgeMarker marker in
                     OwnedMarkersByRowInstanceId.Values.ToArray())
            {
                RefreshMarker(marker);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug(
                "Player badge refresh failed closed: " +
                ex.GetType().Name);
            InvalidateEvidenceGeneration();
            HideAllPresentations();
        }
    }

    private static void RefreshPresentationsWithoutCompleteRoster()
    {
        // Global coverage gates companion/action identity generations and
        // authoritative pruning. Read-only row badges can still be composed
        // independently from each exact cached Steam/PurrNet/native tuple.
        // This prevents one unrelated stale row from blanking the entire list
        // while every unresolved row still fails closed inside RefreshMarker.
        InvalidateEvidenceGeneration();
        BeginReadOnlyBadgePresentationContext();
        PruneInactivePooledRows();
        foreach (BlueSagePlayerBadgeMarker marker in
                 OwnedMarkersByRowInstanceId.Values.ToArray())
        {
            RefreshMarker(marker);
        }
    }

    private static int CountActiveRegisteredRows()
    {
        int activeRows = 0;
        foreach (BlueSagePlayerBadgeMarker marker in
                 OwnedMarkersByRowInstanceId.Values)
        {
            if (marker?.OwnerRow?.gameObject != null &&
                marker.OwnerRow.gameObject.activeInHierarchy &&
                marker.HasLivePresentation())
            {
                activeRows++;
            }
        }

        return activeRows;
    }

    internal static void PruneInactivePooledRows()
    {
        foreach (BlueSagePlayerBadgeMarker marker in
                 OwnedMarkersByRowInstanceId.Values.ToArray())
        {
            if (marker == null ||
                marker.OwnerRow == null ||
                marker.OwnerRow.gameObject == null ||
                !marker.OwnerRow.gameObject.activeInHierarchy ||
                !marker.HasLivePresentation())
            {
                DestroyOwnedMarker(marker);
            }
        }
    }

    internal static void PruneRowsNotIn(
        ISet<PlayerItemController> currentRows)
    {
        if (currentRows == null || currentRows.Count == 0)
        {
            return;
        }

        var currentRowInstanceIds = new List<int>();
        foreach (PlayerItemController row in currentRows)
        {
            if (row == null || row.gameObject == null)
            {
                return;
            }
            currentRowInstanceIds.Add(row.GetInstanceID());
        }

        AuthoritativePlayerRowReconciliationPolicy.Reconcile(
            authoritativeCoverageComplete: true,
            currentRowInstanceIds,
            OwnedMarkersByRowInstanceId.Keys.ToArray(),
            rowInstanceId =>
            {
                if (!OwnedMarkersByRowInstanceId.TryGetValue(
                        rowInstanceId,
                        out BlueSagePlayerBadgeMarker marker) ||
                    marker == null)
                {
                    OwnedMarkersByRowInstanceId.Remove(rowInstanceId);
                    return;
                }
                DestroyOwnedMarker(marker);
            });
    }

    internal static void ClearAll()
    {
        foreach (BlueSagePlayerBadgeMarker marker in
                 OwnedMarkersByRowInstanceId.Values.ToArray())
        {
            DestroyOwnedMarker(marker);
        }
        OwnedMarkersByRowInstanceId.Clear();
        _lobbyKey = 0;
        _lobbyEpoch = 0;
        _evidenceGeneration = 0;
        _companionEvidenceGeneration = 0;
        _badgePresentationGeneration = 0;
        _evidenceFingerprint = string.Empty;
        _companionEvidenceFingerprint = string.Empty;
        _selfLayoutDiagnosticFingerprint = string.Empty;
        _evidenceContextValid = false;
        _companionEvidenceContextValid = false;
        ClearCompatibleClientSnapshots();
        ClearCompatibleClientPresentationContext();
        ClearSharedLobbyEvidence();
        _requestNonce = 0;
        _nextPollAt = 0f;
        BlueSageExtensionRegistry.ClearAll();
    }

    internal static void UnregisterOwnedMarker(
        BlueSagePlayerBadgeMarker marker)
    {
        if (marker == null)
        {
            return;
        }

        marker.ResetGeometryReadiness();
        int rowId = marker.OwnerRowInstanceId;
        if (rowId != 0 &&
            OwnedMarkersByRowInstanceId.TryGetValue(
                rowId,
                out BlueSagePlayerBadgeMarker registered) &&
            ReferenceEquals(registered, marker))
        {
            OwnedMarkersByRowInstanceId.Remove(rowId);
        }
    }

    private static bool BeginLobby(ulong lobbyKey)
    {
        if (_lobbyKey == lobbyKey)
        {
            return _lobbyEpoch > 0;
        }

        BlueSageExtensionRegistry.ClearLobbyState();
        _lobbyKey = lobbyKey;
        if (_lobbyEpoch == long.MaxValue)
        {
            _lobbyKey = 0;
            _evidenceGeneration = 0;
            _companionEvidenceGeneration = 0;
            _badgePresentationGeneration = 0;
            _evidenceFingerprint = string.Empty;
            _companionEvidenceFingerprint = string.Empty;
            _evidenceContextValid = false;
            _companionEvidenceContextValid = false;
            ClearCompatibleClientSnapshots();
            ClearCompatibleClientPresentationContext();
            ClearSharedLobbyEvidence();
            return false;
        }
        _lobbyEpoch = Math.Max(1L, _lobbyEpoch + 1L);
        _evidenceGeneration = 0;
        _companionEvidenceGeneration = 0;
        _badgePresentationGeneration = 0;
        _evidenceFingerprint = string.Empty;
        _companionEvidenceFingerprint = string.Empty;
        _selfLayoutDiagnosticFingerprint = string.Empty;
        _evidenceContextValid = false;
        _companionEvidenceContextValid = false;
        ClearCompatibleClientSnapshots();
        ClearCompatibleClientPresentationContext();
        ClearSharedLobbyEvidence();
        _requestNonce = 0;
        return true;
    }

    private static void LeaveLobby()
    {
        if (_lobbyKey == 0)
        {
            return;
        }

        _lobbyKey = 0;
        _evidenceGeneration = 0;
        _companionEvidenceGeneration = 0;
        _badgePresentationGeneration = 0;
        _evidenceFingerprint = string.Empty;
        _companionEvidenceFingerprint = string.Empty;
        _evidenceContextValid = false;
        _companionEvidenceContextValid = false;
        ClearCompatibleClientSnapshots();
        ClearCompatibleClientPresentationContext();
        ClearSharedLobbyEvidence();
        _requestNonce = 0;
        BlueSageExtensionRegistry.ClearLobbyState();
    }

    private static void RefreshCompatibleClientSnapshots(
        CSteamID lobby)
    {
        ClearCompatibleClientSnapshots();
        if (lobby == CSteamID.Nil ||
            !_evidenceContextValid ||
            _evidenceGeneration <= 0 ||
            !PlayerIdentityEvidenceController
                .TryCaptureCompanionIdentityContextCached(
                    (ulong)lobby,
                    out PlayerIdentityEvidence[] verified,
                    out string companionFingerprint,
                    out _,
                    out _) ||
            !RefreshCompanionEvidenceGeneration(
                companionFingerprint))
        {
            return;
        }

        long nowUnix =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long capturedUtcTicks = DateTime.UtcNow.Ticks;
        foreach (PlayerIdentityEvidence identity in verified)
        {
            if (identity == null ||
                string.IsNullOrWhiteSpace(identity.SteamId) ||
                !ulong.TryParse(
                    identity.SteamId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong exactSteamId) ||
                identity.PurrNetId == 0UL ||
                !ClientCapabilitySnapshotController.TryResolve(
                    new BlueSageIdentityTruthSnapshotV1(
                        exactSteamId,
                        identity.PurrNetId,
                        _lobbyEpoch,
                        _companionEvidenceGeneration,
                        capturedUtcTicks,
                        capturedUtcTicks + TimeSpan.TicksPerSecond),
                    (ulong)lobby,
                    GetCompatibleClientPayload(
                        lobby,
                        identity.SteamId,
                        exactSteamId),
                    SteamMatchmaking.GetLobbyMemberData(
                        lobby,
                        new CSteamID(exactSteamId),
                        RescueReceiverCapabilityPolicy.MemberDataKey),
                    nowUnix,
                    out BlueSageClientCapabilitySnapshotV1 snapshot))
            {
                continue;
            }

            CompatibleClientSnapshotsBySteamId[
                identity.SteamId] = snapshot;
        }

        _compatibleClientSnapshotGeneration =
            _companionEvidenceGeneration;
    }

    private static bool TryGetCompatibleClientSnapshot(
        string exactSteamId,
        out BlueSageClientCapabilitySnapshotV1 snapshot)
    {
        snapshot = default;
        return _companionEvidenceContextValid &&
               _companionEvidenceGeneration > 0 &&
               _compatibleClientSnapshotGeneration ==
               _companionEvidenceGeneration &&
               !string.IsNullOrWhiteSpace(exactSteamId) &&
               CompatibleClientSnapshotsBySteamId.TryGetValue(
                   exactSteamId,
                   out snapshot);
    }

    private static void ClearCompatibleClientSnapshots()
    {
        CompatibleClientSnapshotsBySteamId.Clear();
        _compatibleClientSnapshotGeneration = 0L;
    }

    private static void RefreshCompatibleClientPresentationContext(
        CSteamID lobby)
    {
        ClearCompatibleClientPresentationContext();
        if (lobby == CSteamID.Nil)
        {
            return;
        }

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
        if (memberCount < 0)
        {
            return;
        }

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int index = 0; index < memberCount; index++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(
                lobby,
                index);
            if (member == CSteamID.Nil)
            {
                continue;
            }

            string steamId = ((ulong)member).ToString(
                CultureInfo.InvariantCulture);
            string payload = SteamMatchmaking.GetLobbyMemberData(
                lobby,
                member,
                CompatibleClientPresencePolicy.MemberDataKey) ?? string.Empty;
            CurrentLobbyMemberSteamIds.Add(steamId);
            CompatibleClientPayloadsBySteamId[steamId] = payload;
            if (CompatibleClientPresencePolicy.IsFresh(payload, nowUnix))
            {
                FreshCompatibleClientSteamIds.Add(steamId);
            }
        }

        _compatibleClientPresentationContextValid = true;
    }

    private static void ClearCompatibleClientPresentationContext()
    {
        CompatibleClientPayloadsBySteamId.Clear();
        CurrentLobbyMemberSteamIds.Clear();
        FreshCompatibleClientSteamIds.Clear();
        _compatibleClientPresentationContextValid = false;
    }

    private static void RefreshSharedLobbyEvidence(CSteamID lobby)
    {
        ClearSharedLobbyEvidence();
        if (lobby == CSteamID.Nil)
        {
            return;
        }

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby);
        if (owner == CSteamID.Nil)
        {
            return;
        }

        string ownerSteamId = ((ulong)owner).ToString(
            CultureInfo.InvariantCulture);
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _sharedLobbyEvidence = new SharedLobbyEvidence(
            (ulong)lobby,
            owner,
            ownerSteamId,
            ModerationCapabilityPolicy.IsFresh(
                SteamMatchmaking.GetLobbyData(
                    lobby,
                    "bluesage_qol_moderation"),
                ownerSteamId,
                nowUnix),
            SteamIdModerationController.GetPublishedHelpers(
                lobby,
                ownerSteamId),
            Plugin.CanUseLobbySafety(out _));
        _sharedLobbyEvidenceValid = true;
    }

    private static void ClearSharedLobbyEvidence()
    {
        _sharedLobbyEvidence = default;
        _sharedLobbyEvidenceValid = false;
    }

    private static bool TryGetSharedLobbyEvidence(
        CSteamID lobby,
        out SharedLobbyEvidence sharedEvidence)
    {
        sharedEvidence = default;
        return lobby != CSteamID.Nil &&
               _sharedLobbyEvidenceValid &&
               _sharedLobbyEvidence.LobbyId == (ulong)lobby &&
               _sharedLobbyEvidence.Owner != CSteamID.Nil &&
               !string.IsNullOrWhiteSpace(
                   _sharedLobbyEvidence.OwnerSteamId) &&
               (sharedEvidence = _sharedLobbyEvidence).Helpers != null;
    }

    private static string GetCompatibleClientPayload(
        CSteamID lobby,
        string steamId,
        ulong exactSteamId)
    {
        if (_compatibleClientPresentationContextValid &&
            CompatibleClientPayloadsBySteamId.TryGetValue(
                steamId,
                out string cachedPayload))
        {
            return cachedPayload;
        }

        return SteamMatchmaking.GetLobbyMemberData(
            lobby,
            new CSteamID(exactSteamId),
            CompatibleClientPresencePolicy.MemberDataKey) ?? string.Empty;
    }

    private static bool IsCurrentCompatiblePresentationRow(
        BlueSagePlayerBadgeMarker marker)
    {
        return marker != null &&
               _compatibleClientPresentationContextValid &&
               !string.IsNullOrWhiteSpace(marker.BoundSteamId) &&
               CurrentLobbyMemberSteamIds.Contains(marker.BoundSteamId) &&
               FreshCompatibleClientSteamIds.Contains(marker.BoundSteamId);
    }

    private static void RefreshMarker(
        BlueSagePlayerBadgeMarker marker)
    {
        string hostLabel = string.Empty;
        if (!IsActive(marker) ||
            !TryBuildEvidence(
                marker,
                out PlayerBadgeEvidence evidence,
                out hostLabel))
        {
            ApplyPresentation(
                marker,
                PlayerBadgePolicy.Compose(
                    default,
                    BadgeCapacity),
                string.Empty);
            return;
        }

        ApplyPresentation(
            marker,
            PlayerBadgePolicy.Compose(
                evidence,
                BadgeCapacity),
            hostLabel,
            ResolveNativeHostReplacement(
                marker,
                evidence));
    }

    private static string ResolveNativeHostReplacement(
        BlueSagePlayerBadgeMarker marker,
        PlayerBadgeEvidence evidence)
    {
        if (marker == null)
        {
            return string.Empty;
        }

        return PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
            marker.GetNativeNameSourceText(),
            MonoSingleton<SettingsController>.I?.HostString?.String ?? string.Empty,
            evidence.UniqueExactRow,
            evidence.OwnerMatchesRow,
            evidence.NativeHostAgreement,
            evidence.OwnerPublishedCreatorHostLabelFresh,
            out string replacement)
            ? replacement
            : string.Empty;
    }

    private static bool TryBuildEvidence(
        BlueSagePlayerBadgeMarker marker,
        out PlayerBadgeEvidence evidence,
        out string hostLabel)
    {
        evidence = default;
        hostLabel = string.Empty;
        if (!TryGetLobby(out CSteamID lobby) ||
            !ulong.TryParse(
                marker.BoundSteamId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong rowSteamId))
        {
            return false;
        }

        bool exactCurrentRow =
            PlayerIdentityEvidenceController.TryResolveExactCached(
                marker.BoundSteamId,
                out PlayerIdentityEvidence identity,
                out _) &&
            IsExactCurrentRow(marker.OwnerRow, identity);
        if (!exactCurrentRow)
        {
            if (!IsCurrentCompatiblePresentationRow(marker))
            {
                return false;
            }

            // The native callback bound this row to its Steam ID and the
            // current Steam lobby still contains that ID.  A fresh heartbeat
            // can therefore render [U] without becoming identity truth for
            // moderation, rescue, clone, creator, or host decisions.
            evidence = new PlayerBadgeEvidence(
                uniqueExactRow: false,
                uniqueCurrentMember: false,
                ownerMatchesRow: false,
                nativeHostAgreement: false,
                moderationCapabilityFresh: false,
                ownerListsRowAsHelper: false,
                compatibleHeartbeatFresh: true,
                cloneFeatureEnabled: false,
                localSafetyAuthorized: false,
                openVictim: false,
                openSuspect: false,
                creatorContributionFresh: false,
                compatiblePresentationRow: true);
            return true;
        }

        bool hasSharedLobbyEvidence = TryGetSharedLobbyEvidence(
            lobby,
            out SharedLobbyEvidence sharedEvidence);
        CSteamID owner = hasSharedLobbyEvidence
            ? sharedEvidence.Owner
            : SteamMatchmaking.GetLobbyOwner(lobby);
        if (owner == CSteamID.Nil)
        {
            return false;
        }

        string ownerSteamId = hasSharedLobbyEvidence
            ? sharedEvidence.OwnerSteamId
            : ((ulong)owner).ToString(
                CultureInfo.InvariantCulture);
        long nowUnix =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool ownerMatchesRow =
            rowSteamId == (ulong)owner;
        PlayerHostLabelDisplayPolicy.TryResolve(
            ownerMatchesRow
                ? SteamMatchmaking.GetLobbyMemberData(
                    lobby,
                    owner,
                    PlayerRoleLabelController.MemberDataKey)
                : string.Empty,
            nowUnix,
            ownerMatchesRow && marker.OwnerRow.IsHost,
            out hostLabel);
        bool ownerPublishedCreatorHostLabelFresh =
            ownerMatchesRow &&
            marker.OwnerRow.IsHost &&
            !string.IsNullOrWhiteSpace(hostLabel);
        bool capabilityFresh = hasSharedLobbyEvidence
            ? sharedEvidence.ModerationCapabilityFresh
            : ModerationCapabilityPolicy.IsFresh(
                SteamMatchmaking.GetLobbyData(
                    lobby,
                    "bluesage_qol_moderation"),
                ownerSteamId,
                nowUnix);
        bool ownerListsRowAsHelper = hasSharedLobbyEvidence
            ? sharedEvidence.Helpers.Contains(marker.BoundSteamId)
            : SteamIdModerationController.GetPublishedHelpers(
                    lobby,
                    ownerSteamId)
                .Contains(marker.BoundSteamId);
        // Prefer the once-per-poll snapshot so a healthy large roster does
        // not repeat capability construction for every rendered row.  When a
        // different row temporarily prevents a complete companion snapshot,
        // this read-only marker may still use its own already exact-bound row
        // and that player's fresh self-published heartbeat.  It never grants
        // moderation, rescue, or host authority.
        bool compatibleFresh =
            (TryGetCompatibleClientSnapshot(
                 marker.BoundSteamId,
                 out BlueSageClientCapabilitySnapshotV1 capabilitySnapshot) &&
             capabilitySnapshot.CompatibleClientPresent) ||
            (_compatibleClientPresentationContextValid
                ? FreshCompatibleClientSteamIds.Contains(marker.BoundSteamId)
                : CompatibleClientPresencePolicy.IsFresh(
                    GetCompatibleClientPayload(
                        lobby,
                        marker.BoundSteamId,
                        rowSteamId),
                    nowUnix));
        bool safetyAuthorized = hasSharedLobbyEvidence
            ? sharedEvidence.LocalSafetyAuthorized
            : Plugin.CanUseLobbySafety(out _);
        CloneIncidentLedger.GetRoleState(
            marker.BoundSteamId,
            out bool openVictim,
            out bool openSuspect);

        bool contributedBadgePresent =
            TryReadCreatorContribution(
                rowSteamId,
                identity);
        evidence = new PlayerBadgeEvidence(
            uniqueExactRow: true,
            uniqueCurrentMember: true,
            ownerMatchesRow:
                ownerMatchesRow,
            nativeHostAgreement:
                marker.OwnerRow.IsHost,
            moderationCapabilityFresh:
                capabilityFresh,
            ownerListsRowAsHelper:
                ownerListsRowAsHelper,
            compatibleHeartbeatFresh:
                compatibleFresh,
            cloneFeatureEnabled:
                Plugin.ShouldApplyCloneShield,
            localSafetyAuthorized:
                safetyAuthorized,
            openVictim,
            openSuspect,
            creatorContributionFresh:
                contributedBadgePresent,
            supporterIdentityHashMatched:
                SupporterBadgeIdentityPolicy.IsRecognizedExactIdentity(marker.BoundSteamId),
            ownerPublishedCreatorHostLabelFresh:
                ownerPublishedCreatorHostLabelFresh);
        return true;
    }

    private static bool TryReadCreatorContribution(
        ulong rowSteamId,
        PlayerIdentityEvidence identity)
    {
        long badgeGeneration = _evidenceContextValid
            ? _evidenceGeneration
            : _badgePresentationGeneration;
        if (_lobbyEpoch <= 0 ||
            badgeGeneration <= 0 ||
            identity == null ||
            !TryGetExactPurrNetId(
                identity,
                out ulong purrNetId))
        {
            return false;
        }

        long nonce =
            Interlocked.Increment(ref _requestNonce);
        if (nonce <= 0)
        {
            Interlocked.Exchange(
                ref _requestNonce,
                1);
            nonce = 1;
        }
        long nowTicks =
            DateTime.UtcNow.Ticks;
        var request =
            new BlueSageBadgeContributionRequestV1(
                rowSteamId,
                purrNetId,
                _lobbyEpoch,
                badgeGeneration,
                nonce,
                nowTicks);
        return BlueSageExtensionRegistry.TryReadBadgeContribution(
                   request,
                   nowTicks,
                   out BlueSageBadgeContributionResponseV1 response) &&
               response.ContributedBadgePresent;
    }

    private static void BeginReadOnlyBadgePresentationContext()
    {
        if (_lobbyEpoch <= 0)
        {
            _badgePresentationGeneration = 0;
            return;
        }

        _badgePresentationGeneration = Math.Max(
            1L,
            _badgePresentationGeneration);
        BlueSageExtensionRegistry.BeginBadgePresentation(
            _lobbyEpoch,
            _badgePresentationGeneration);
    }

    internal static bool TryResolveFreshLocalCreatorContribution(
        CSteamID expectedLobby,
        out bool contributedBadgePresent)
    {
        contributedBadgePresent = false;
        if (!SteamManager.Initialized ||
            expectedLobby == CSteamID.Nil)
        {
            return false;
        }

        CSteamID localSteamId = SteamUser.GetSteamID();
        if (localSteamId == CSteamID.Nil ||
            !TryResolveFreshIdentityTruthBySteam(
                (ulong)localSteamId,
                out BlueSageIdentityTruthSnapshotV1 truth,
                out _) ||
            _lobbyKey != (ulong)expectedLobby ||
            truth.SteamId != (ulong)localSteamId ||
            truth.PurrNetId == 0UL ||
            truth.LobbyEpoch != _lobbyEpoch ||
            truth.EvidenceGeneration != _evidenceGeneration)
        {
            return false;
        }

        long nonce =
            Interlocked.Increment(ref _requestNonce);
        if (nonce <= 0)
        {
            Interlocked.Exchange(
                ref _requestNonce,
                1);
            nonce = 1;
        }
        long nowTicks = DateTime.UtcNow.Ticks;
        if (truth.FreshUntilUtcTicks < nowTicks)
        {
            return false;
        }

        var request =
            new BlueSageBadgeContributionRequestV1(
                truth.SteamId,
                truth.PurrNetId,
                truth.LobbyEpoch,
                truth.EvidenceGeneration,
                nonce,
                nowTicks);
        if (!BlueSageExtensionRegistry.TryReadBadgeContribution(
                request,
                nowTicks,
                out BlueSageBadgeContributionResponseV1 response))
        {
            return false;
        }

        contributedBadgePresent = response.ContributedBadgePresent;
        return true;
    }

    private static bool TryGetExactPurrNetId(
        PlayerIdentityEvidence identity,
        out ulong purrNetId)
    {
        purrNetId = identity?.PurrNetId ?? 0UL;
        return purrNetId != 0UL;
    }

    internal static bool TryResolveFreshIdentityTruth(
        ulong exactSteamId,
        ulong exactPurrNetId,
        out BlueSageIdentityTruthSnapshotV1 truth,
        out string resultMessage)
    {
        if (!TryResolveFreshIdentityTruthBySteam(
                exactSteamId,
                out truth,
                out resultMessage))
        {
            return false;
        }

        if (exactPurrNetId == 0 || truth.PurrNetId != exactPurrNetId)
        {
            truth = default;
            resultMessage =
                "public QoL exact Steam/PurrNet tuple does not match the current native row";
            return false;
        }

        return true;
    }

    internal static bool TryResolveFreshIdentityTruthBySteam(
        ulong exactSteamId,
        out BlueSageIdentityTruthSnapshotV1 truth,
        out string resultMessage)
    {
        truth = default;
        resultMessage =
            "public QoL exact Steam identity truth is unavailable";
        if (exactSteamId == 0)
        {
            resultMessage = "public QoL rejected an empty Steam identity";
            return false;
        }

        try
        {
            if (!TryGetLobby(out CSteamID lobby) ||
                lobby == CSteamID.Nil)
            {
                LeaveLobby();
                resultMessage = "public QoL has no current Steam lobby";
                return false;
            }

            if (!BeginLobby((ulong)lobby))
            {
                resultMessage = "public QoL could not begin the current lobby identity epoch";
                return false;
            }
            string steamId = exactSteamId.ToString(
                CultureInfo.InvariantCulture);
            bool exactResolved =
                PlayerIdentityEvidenceController
                    .TryResolveExactWithCompanionFingerprint(
                    steamId,
                    (ulong)lobby,
                    out PlayerIdentityEvidence identity,
                    out string fingerprint,
                    out bool contextValid,
                    out string resolutionError);
            if (!contextValid)
            {
                InvalidateCompanionEvidenceGeneration();
                resultMessage = "public QoL identity context is incomplete: " + resolutionError;
                return false;
            }
            if (!RefreshCompanionEvidenceGeneration(fingerprint) ||
                !exactResolved ||
                !TryGetExactPurrNetId(
                    identity,
                    out ulong resolvedPurrNetId))
            {
                resultMessage = exactResolved
                    ? "public QoL could not publish a fresh identity generation"
                    : "public QoL could not resolve one exact current native row: " + resolutionError;
                return false;
            }

            int matchingLobbyMembers = 0;
            int memberCount =
                SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < memberCount; index++)
            {
                if ((ulong)SteamMatchmaking.GetLobbyMemberByIndex(
                        lobby,
                        index) == exactSteamId)
                {
                    matchingLobbyMembers++;
                }
            }
            if (matchingLobbyMembers != 1)
            {
                InvalidateCompanionEvidenceGeneration();
                resultMessage = "public QoL Steam membership is not unique for the requested identity";
                return false;
            }

            long capturedUtcTicks = DateTime.UtcNow.Ticks;
            truth = new BlueSageIdentityTruthSnapshotV1(
                exactSteamId,
                resolvedPurrNetId,
                _lobbyEpoch,
                _companionEvidenceGeneration,
                capturedUtcTicks,
                capturedUtcTicks + TimeSpan.TicksPerSecond);
            resultMessage =
                "public QoL exact canonical identity truth resolved";
            return true;
        }
        catch (Exception ex)
        {
            InvalidateCompanionEvidenceGeneration();
            resultMessage =
                "public QoL exact identity truth failed closed: " +
                ex.GetType().Name;
            truth = default;
            return false;
        }
    }

    internal static bool TryResolveFreshClientCapabilitySnapshotBySteam(
        ulong exactSteamId,
        out BlueSageClientCapabilitySnapshotV1 snapshot,
        out string resultMessage)
    {
        snapshot = default;
        if (!TryResolveFreshIdentityTruthBySteam(
                exactSteamId,
                out BlueSageIdentityTruthSnapshotV1 identity,
                out resultMessage))
        {
            return false;
        }

        try
        {
            if (!TryGetLobby(out CSteamID lobby) ||
                lobby == CSteamID.Nil ||
                _lobbyKey != (ulong)lobby)
            {
                resultMessage = "public QoL capability snapshot has no matching current lobby";
                return false;
            }

            CSteamID member = new CSteamID(exactSteamId);
            bool resolved = ClientCapabilitySnapshotController.TryResolve(
                identity,
                (ulong)lobby,
                SteamMatchmaking.GetLobbyMemberData(
                    lobby,
                    member,
                    CompatibleClientPresencePolicy.MemberDataKey),
                SteamMatchmaking.GetLobbyMemberData(
                    lobby,
                    member,
                    RescueReceiverCapabilityPolicy.MemberDataKey),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                out snapshot);
            resultMessage = resolved
                ? snapshot.StatusText
                : "public QoL capability snapshot failed exact identity validation";
            return resolved;
        }
        catch (Exception ex)
        {
            snapshot = default;
            resultMessage =
                "public QoL capability snapshot failed closed: " +
                ex.GetType().Name;
            return false;
        }
    }

    private static bool RefreshEvidenceGeneration(
        string fingerprint)
    {
        return ObserveEvidenceGeneration(
            fingerprint,
            observedValid: true);
    }

    private static void InvalidateEvidenceGeneration()
    {
        ObserveEvidenceGeneration(
            string.Empty,
            observedValid: false);
    }

    private static bool RefreshCompanionEvidenceGeneration(
        string fingerprint)
    {
        return ObserveCompanionEvidenceGeneration(
            fingerprint,
            observedValid: true);
    }

    private static void InvalidateCompanionEvidenceGeneration()
    {
        ObserveCompanionEvidenceGeneration(
            string.Empty,
            observedValid: false);
    }

    private static bool ObserveCompanionEvidenceGeneration(
        string fingerprint,
        bool observedValid)
    {
        if (!IdentityEvidenceGenerationPolicy.TryObserve(
                _companionEvidenceFingerprint,
                _companionEvidenceGeneration,
                _companionEvidenceContextValid,
                fingerprint,
                observedValid,
                out string nextFingerprint,
                out long nextGeneration,
                out bool nextValid,
                out _,
                out bool requiresEpochAdvance))
        {
            if (!requiresEpochAdvance || !TryAdvanceEvidenceEpoch())
            {
                _companionEvidenceContextValid = false;
                return false;
            }

            if (!IdentityEvidenceGenerationPolicy.TryObserve(
                    _companionEvidenceFingerprint,
                    _companionEvidenceGeneration,
                    _companionEvidenceContextValid,
                    fingerprint,
                    observedValid,
                    out nextFingerprint,
                    out nextGeneration,
                    out nextValid,
                    out _,
                    out _))
            {
                _companionEvidenceContextValid = false;
                return false;
            }
        }

        _companionEvidenceFingerprint = nextFingerprint;
        _companionEvidenceGeneration = nextGeneration;
        _companionEvidenceContextValid = nextValid;
        return _companionEvidenceContextValid;
    }

    private static bool ObserveEvidenceGeneration(
        string fingerprint,
        bool observedValid)
    {
        if (!IdentityEvidenceGenerationPolicy.TryObserve(
                _evidenceFingerprint,
                _evidenceGeneration,
                _evidenceContextValid,
                fingerprint,
                observedValid,
                out string nextFingerprint,
                out long nextGeneration,
                out bool nextValid,
                out _,
                out bool requiresEpochAdvance))
        {
            if (!requiresEpochAdvance ||
                !TryAdvanceEvidenceEpoch())
            {
                _evidenceContextValid = false;
                BlueSageExtensionRegistry.ClearLobbyState();
                return false;
            }

            if (!IdentityEvidenceGenerationPolicy.TryObserve(
                    _evidenceFingerprint,
                    _evidenceGeneration,
                    _evidenceContextValid,
                    fingerprint,
                    observedValid,
                    out nextFingerprint,
                    out nextGeneration,
                    out nextValid,
                    out _,
                    out _))
            {
                _evidenceContextValid = false;
                BlueSageExtensionRegistry.ClearLobbyState();
                return false;
            }
        }

        _evidenceFingerprint = nextFingerprint;
        _evidenceGeneration = nextGeneration;
        _evidenceContextValid = nextValid;
        if (!_evidenceContextValid)
        {
            BlueSageExtensionRegistry.ClearLobbyState();
            return false;
        }

        BlueSageExtensionRegistry.BeginLobby(
            _lobbyEpoch,
            _evidenceGeneration);
        return true;
    }

    private static bool TryAdvanceEvidenceEpoch()
    {
        if (_lobbyKey == 0UL ||
            _lobbyEpoch == long.MaxValue)
        {
            return false;
        }

        _lobbyEpoch++;
        _evidenceGeneration = 0L;
        _companionEvidenceGeneration = 0L;
        _badgePresentationGeneration = 0L;
        _evidenceFingerprint = string.Empty;
        _companionEvidenceFingerprint = string.Empty;
        _evidenceContextValid = false;
        _companionEvidenceContextValid = false;
        ClearCompatibleClientSnapshots();
        ClearCompatibleClientPresentationContext();
        BlueSageExtensionRegistry.ClearLobbyState();
        return true;
    }

    private static bool IsExactCurrentRow(
        PlayerItemController row,
        PlayerIdentityEvidence identity)
    {
        PlayerPanelController panel =
            NetworkSingleton<PlayerPanelController>.I;
        int index =
            identity?.RosterIndex ?? -1;
        return panel != null &&
               panel.PlayerItemControllers != null &&
               index >= 0 &&
               index < panel.PlayerItemControllers.Count &&
               ReferenceEquals(
                   panel.PlayerItemControllers[index],
                   row);
    }

    private static BlueSagePlayerBadgeMarker
        CreateOwnedMarker(
            PlayerItemController row,
            TMP_Text nativeName,
            RectTransform nameRect)
    {
        var markerObject = new GameObject(
            BadgeStripName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(LayoutElement),
            typeof(TextMeshProUGUI),
            typeof(BlueSagePlayerBadgeMarker));
        markerObject.transform.SetParent(
            nameRect.parent,
            false);

        LayoutElement layoutElement =
            markerObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        var marker =
            markerObject.GetComponent<
                BlueSagePlayerBadgeMarker>();
        marker.OwnerRow = row;
        marker.OwnerRowInstanceId =
            row.GetInstanceID();
        marker.NameAnchorInstanceId =
            nameRect.GetInstanceID();
        marker.BindNativeName(nativeName);
        ConfigureGeometry(
            marker,
            nameRect,
            default);

        TextMeshProUGUI label =
            markerObject.GetComponent<TextMeshProUGUI>();
        label.font = nativeName.font;
        label.fontSize =
            AccessibleFontSize(nativeName.fontSize);
        label.fontStyle = FontStyles.Bold;
        label.alignment =
            TextAlignmentOptions.MidlineRight;
        label.textWrappingMode =
            TextWrappingModes.NoWrap;
        label.overflowMode =
            TextOverflowModes.Ellipsis;
        label.richText = true;
        label.raycastTarget = true;
        label.text = string.Empty;
        marker.Label = label;

        var hostLabelObject = new GameObject(
            "BlueSage_HostLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        hostLabelObject.transform.SetParent(markerObject.transform, false);
        RectTransform hostLabelRect =
            hostLabelObject.GetComponent<RectTransform>();
        hostLabelRect.anchorMin = new Vector2(0f, 0f);
        hostLabelRect.anchorMax = new Vector2(0f, 1f);
        hostLabelRect.offsetMin = Vector2.zero;
        hostLabelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI hostLabel =
            hostLabelObject.GetComponent<TextMeshProUGUI>();
        hostLabel.font = nativeName.font;
        hostLabel.fontSize = AccessibleFontSize(nativeName.fontSize);
        hostLabel.fontStyle = FontStyles.Bold;
        hostLabel.alignment = TextAlignmentOptions.MidlineLeft;
        hostLabel.textWrappingMode = TextWrappingModes.NoWrap;
        hostLabel.overflowMode = TextOverflowModes.Ellipsis;
        hostLabel.richText = true;
        hostLabel.raycastTarget = true;
        hostLabel.text = string.Empty;
        marker.HostLabel = hostLabel;
        return marker;
    }

    private static float AccessibleFontSize(float nativeFontSize)
    {
        return Mathf.Clamp(
            Mathf.Max(nativeFontSize, MinimumAccessibleFontSize),
            MinimumAccessibleFontSize,
            MaximumAccessibleFontSize);
    }

    private static void ConfigureGeometry(
        BlueSagePlayerBadgeMarker marker,
        RectTransform nameRect,
        PlayerBadgeLayout layout)
    {
        RectTransform badgeRect =
            marker.GetComponent<RectTransform>();
        float rightAnchor = nameRect.anchorMax.x;
        badgeRect.anchorMin = new Vector2(
            rightAnchor, nameRect.anchorMin.y);
        badgeRect.anchorMax = new Vector2(
            rightAnchor, nameRect.anchorMax.y);
        badgeRect.pivot = new Vector2(1f, nameRect.pivot.y);
        badgeRect.offsetMin = new Vector2(
            nameRect.offsetMax.x -
            layout.LaneWidth,
            nameRect.offsetMin.y);
        badgeRect.offsetMax = new Vector2(
            nameRect.offsetMax.x,
            nameRect.offsetMax.y);

        if (marker.Label != null)
        {
            float badgeRight = Mathf.Max(
                0f,
                layout.LaneWidth -
                layout.BadgeStart -
                layout.BadgeWidth);
            marker.Label.margin = new Vector4(
                layout.BadgeStart,
                0f,
                badgeRight,
                0f);
        }

        TextMeshProUGUI hostLabel = marker.HostLabel;
        RectTransform hostRect = hostLabel != null
            ? hostLabel.rectTransform
            : null;
        if (hostRect != null)
        {
            hostRect.anchorMin = new Vector2(0f, 0f);
            hostRect.anchorMax = new Vector2(0f, 1f);
            hostRect.offsetMin = new Vector2(
                layout.HostStart,
                0f);
            hostRect.offsetMax = new Vector2(
                layout.HostStart +
                layout.HostWidth,
                0f);
        }
    }

    private static void ApplyPresentation(
        BlueSagePlayerBadgeMarker marker,
        PlayerBadgePresentation presentation,
        string hostLabel = "",
        string nativeHostReplacement = "")
    {
        if (marker == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nativeHostReplacement))
        {
            marker.RestoreNativeName();
        }
        else
        {
            marker.ApplyNativeNameReplacement(nativeHostReplacement);
        }

        string resolvedHostText = hostLabel ?? string.Empty;
        if (!string.Equals(
                marker.TooltipText,
                presentation.TooltipText,
                StringComparison.Ordinal))
        {
            marker.TooltipText =
                presentation.TooltipText;
        }

        TextMeshProUGUI badgeLabel = marker.Label;
        TextMeshProUGUI ownedHostLabel = marker.HostLabel;
        TMP_Text nativeName = marker.NativeName;
        if (badgeLabel == null ||
            ownedHostLabel == null ||
            nativeName == null)
        {
            marker.RestoreNativePresentation();
            return;
        }

        if (!string.Equals(
                ownedHostLabel.text,
                resolvedHostText,
                StringComparison.Ordinal))
        {
            ownedHostLabel.text = resolvedHostText;
        }
        bool hasHost =
            !string.IsNullOrWhiteSpace(resolvedHostText);
        if (ownedHostLabel.enabled != hasHost)
        {
            ownedHostLabel.enabled = hasHost;
        }

        RectTransform nameRect = nativeName.rectTransform;
        if (nameRect == null)
        {
            marker.RestoreNativePresentation();
            return;
        }

        float nativeNameWidth = nameRect.rect.width;
        int geometryFingerprint = RectGeometryFingerprint(
            nameRect,
            nameRect.lossyScale.y);
        if (!marker.ObserveGeometry(
                nativeNameWidth,
                geometryFingerprint))
        {
            badgeLabel.enabled = false;
            ownedHostLabel.enabled = false;
            marker.RestoreNativePresentation();
            ConfigureGeometry(
                marker,
                nameRect,
                default);
            return;
        }

        PlayerBadgeLayoutCacheKey cacheKey =
            CreateLayoutCacheKey(
                badgeLabel,
                ownedHostLabel,
                nativeName,
                nameRect,
                presentation.DisplayText,
                resolvedHostText);
        if (marker.TryGetCachedLayout(
                cacheKey,
                out _))
        {
            return;
        }

        int maximumBadgeCapacity = Math.Min(
            BadgeCapacity,
            presentation.AllBadges.Count);
        bool hasBadge = maximumBadgeCapacity > 0;
        if (!hasBadge && !hasHost)
        {
            badgeLabel.text = string.Empty;
            badgeLabel.enabled = false;
            marker.RestoreNativePresentation();
            ConfigureGeometry(
                marker,
                nameRect,
                default);
            marker.StoreCachedLayout(
                cacheKey,
                default);
            return;
        }

        badgeLabel.margin = Vector4.zero;
        float hostPreferred = hasHost
            ? ownedHostLabel.GetPreferredValues(
                ownedHostLabel.text).x
            : 0f;
        float[] badgePreferredWidths =
            new float[maximumBadgeCapacity];
        string[] badgeRichTexts =
            new string[maximumBadgeCapacity];
        for (int capacity = 1;
             capacity <= maximumBadgeCapacity;
             capacity++)
        {
            PlayerBadgePresentation collapsed =
                PlayerBadgePolicy.Collapse(
                    presentation,
                    capacity);
            string richText = PlayerBadgeRenderPolicy.RenderCompactRichText(
                collapsed.DisplayText);
            badgeRichTexts[capacity - 1] = richText;
            badgePreferredWidths[capacity - 1] =
                badgeLabel.GetPreferredValues(
                    richText).x;
        }

        float nativePreferredWidth =
            nativeName.GetPreferredValues(
                nativeName.text ?? string.Empty).x;
        float overflowLaneAvailableWidth =
            MeasureRightOverflowCapacity(
                marker.OwnerRow,
                nameRect,
                nativePreferredWidth);
        PlayerBadgeAllocation allocation =
            PlayerBadgeLayoutPolicy.ResolveResponsiveAllocation(
                nativeNameWidth,
                nativePreferredWidth,
                Mathf.Min(
                    nativePreferredWidth,
                    MinimumReadableNativeNameWidth),
                BadgeHorizontalPadding,
                hostPreferred,
                BadgeSegmentGap,
                NativeNameBadgeGap,
                badgePreferredWidths,
                overflowLaneAvailableWidth);
        EmitSelfLayoutDiagnostic(
            marker,
            nameRect,
            nativeNameWidth,
            nativePreferredWidth,
            hostPreferred,
            badgePreferredWidths,
            overflowLaneAvailableWidth,
            allocation);
        float maximumLaneWidth = allocation.OwnedLaneWidth;
        int badgeCapacity = allocation.BadgeCapacity;
        string badgeText = badgeCapacity > 0
            ? badgeRichTexts[badgeCapacity - 1]
            : string.Empty;
        float badgePreferred = badgeCapacity > 0
            ? badgePreferredWidths[badgeCapacity - 1]
            : 0f;
        if (!string.Equals(
                badgeLabel.text,
                badgeText,
                StringComparison.Ordinal))
        {
            badgeLabel.text = badgeText;
        }
        bool showBadge = !string.IsNullOrEmpty(badgeText);
        if (badgeLabel.enabled != showBadge)
        {
            badgeLabel.enabled = showBadge;
        }
        PlayerBadgeLayout layout =
            PlayerBadgeLayoutPolicy.Resolve(
                hostPreferred,
                badgePreferred,
                maximumLaneWidth,
                BadgeHorizontalPadding,
                BadgeSegmentGap,
                NativeNameBadgeGap);
        if (layout.LaneWidth <= 0f)
        {
            marker.RestoreNativePresentation();
            ConfigureGeometry(
                marker,
                nameRect,
                default);
            marker.StoreCachedLayout(
                cacheKey,
                default);
            return;
        }

        if (allocation.UseOverflowLane)
        {
            ConfigureOverflowGeometry(
                marker,
                nameRect,
                layout,
                nativePreferredWidth);
            marker.RestoreNativePresentation();
        }
        else
        {
            ConfigureGeometry(
                marker,
                nameRect,
                layout);
            marker.ApplyNativePresentationReservation(
                layout.NativeNameReservation);
        }
        marker.StoreCachedLayout(
            cacheKey,
            layout);
    }

    private static float MeasureRightOverflowCapacity(
        PlayerItemController row,
        RectTransform nameRect,
        float nativePreferredWidth)
    {
        if (row == null || nameRect == null)
        {
            return 0f;
        }

        float scale = Mathf.Abs(nameRect.lossyScale.x);
        if (scale <= 0.0001f)
        {
            return 0f;
        }

        var corners = new Vector3[4];
        nameRect.GetWorldCorners(corners);
        float nameLeft = corners.Min(corner => corner.x);
        float textRight = nameLeft +
                          (Mathf.Max(0f, nativePreferredWidth) * scale);
        float nearestRightBoundary = float.PositiveInfinity;

        // The ID/@ actions are BlueSage-owned siblings of the native action
        // buttons. Their lane is the first hard boundary for presentation
        // text; measuring only individual Buttons can skip that boundary when
        // the text already reaches into the lane and falsely report the next
        // vanilla button as free space.
        BlueSagePlayerPanelSurfaceMarker ownedActionLane =
            row.GetComponentInChildren<BlueSagePlayerPanelSurfaceMarker>(true);
        RectTransform ownedActionRect = ownedActionLane != null
            ? ownedActionLane.transform as RectTransform
            : null;
        if (ownedActionRect != null &&
            ownedActionRect.gameObject.activeInHierarchy)
        {
            ownedActionRect.GetWorldCorners(corners);
            nearestRightBoundary = corners.Min(corner => corner.x);
        }

        foreach (Button button in
                 row.GetComponentsInChildren<Button>(true))
        {
            RectTransform buttonRect =
                button != null ? button.transform as RectTransform : null;
            if (buttonRect == null ||
                !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            buttonRect.GetWorldCorners(corners);
            float buttonLeft = corners.Min(corner => corner.x);
            if (buttonLeft > textRight)
            {
                nearestRightBoundary = Mathf.Min(
                    nearestRightBoundary,
                    buttonLeft);
            }
        }

        if (float.IsPositiveInfinity(nearestRightBoundary))
        {
            RectTransform rowRect = row.transform as RectTransform;
            if (rowRect == null)
            {
                return 0f;
            }

            rowRect.GetWorldCorners(corners);
            nearestRightBoundary = corners.Max(corner => corner.x);
        }

        return Mathf.Max(
            0f,
            ((nearestRightBoundary - textRight) / scale) -
            OverflowLaneGap);
    }

    private static void ConfigureOverflowGeometry(
        BlueSagePlayerBadgeMarker marker,
        RectTransform nameRect,
        PlayerBadgeLayout layout,
        float nativePreferredWidth)
    {
        RectTransform badgeRect =
            marker.GetComponent<RectTransform>();
        float rightAnchor = nameRect.anchorMax.x;
        float overflow = Mathf.Max(
            0f,
            nativePreferredWidth - nameRect.rect.width);
        float left = nameRect.offsetMax.x +
                     overflow +
                     OverflowLaneGap;
        badgeRect.anchorMin = new Vector2(
            rightAnchor, nameRect.anchorMin.y);
        badgeRect.anchorMax = new Vector2(
            rightAnchor, nameRect.anchorMax.y);
        badgeRect.pivot = new Vector2(0f, nameRect.pivot.y);
        badgeRect.offsetMin = new Vector2(
            left,
            nameRect.offsetMin.y);
        badgeRect.offsetMax = new Vector2(
            left + layout.LaneWidth,
            nameRect.offsetMax.y);

        if (marker.Label != null)
        {
            float badgeRight = Mathf.Max(
                0f,
                layout.LaneWidth -
                layout.BadgeStart -
                layout.BadgeWidth);
            marker.Label.margin = new Vector4(
                layout.BadgeStart,
                0f,
                badgeRight,
                0f);
        }

        TextMeshProUGUI hostLabel = marker.HostLabel;
        RectTransform hostRect = hostLabel != null
            ? hostLabel.rectTransform
            : null;
        if (hostRect != null)
        {
            hostRect.anchorMin = new Vector2(0f, 0f);
            hostRect.anchorMax = new Vector2(0f, 1f);
            hostRect.offsetMin = new Vector2(
                layout.HostStart,
                0f);
            hostRect.offsetMax = new Vector2(
                layout.HostStart + layout.HostWidth,
                0f);
        }
    }

    private static void EmitSelfLayoutDiagnostic(
        BlueSagePlayerBadgeMarker marker,
        RectTransform nameRect,
        float nativeNameWidth,
        float nativePreferredWidth,
        float hostPreferredWidth,
        IReadOnlyList<float> badgePreferredWidths,
        float overflowLaneAvailableWidth,
        PlayerBadgeAllocation allocation)
    {
        if (marker == null ||
            nameRect == null ||
            string.IsNullOrWhiteSpace(marker.BoundSteamId) ||
            !string.Equals(
                marker.BoundSteamId,
                ((ulong)SteamUser.GetSteamID()).ToString(
                    CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            return;
        }

        string badgeWidths = badgePreferredWidths == null
            ? string.Empty
            : string.Join(
                ",",
                badgePreferredWidths.Select(
                    width => width.ToString("0.0", CultureInfo.InvariantCulture)));
        string fingerprint = string.Join(
            "|",
            nativeNameWidth.ToString("0.0", CultureInfo.InvariantCulture),
            nativePreferredWidth.ToString("0.0", CultureInfo.InvariantCulture),
            hostPreferredWidth.ToString("0.0", CultureInfo.InvariantCulture),
            badgeWidths,
            allocation.NativeNameWidth.ToString("0.0", CultureInfo.InvariantCulture),
            allocation.OwnedLaneWidth.ToString("0.0", CultureInfo.InvariantCulture),
            allocation.BadgeCapacity.ToString(CultureInfo.InvariantCulture),
            overflowLaneAvailableWidth.ToString("0.0", CultureInfo.InvariantCulture),
            allocation.UseOverflowLane ? "1" : "0",
            nameRect.anchorMin.x.ToString("0.000", CultureInfo.InvariantCulture),
            nameRect.anchorMax.x.ToString("0.000", CultureInfo.InvariantCulture),
            nameRect.offsetMin.x.ToString("0.0", CultureInfo.InvariantCulture),
            nameRect.offsetMax.x.ToString("0.0", CultureInfo.InvariantCulture));
        if (string.Equals(
                fingerprint,
                _selfLayoutDiagnosticFingerprint,
                StringComparison.Ordinal))
        {
            return;
        }

        _selfLayoutDiagnosticFingerprint = fingerprint;
        Plugin.Log?.LogInfo(
            "[BlueSagePlayerRowLayout] self=true; nativeWidth=" +
            nativeNameWidth.ToString("0.0", CultureInfo.InvariantCulture) +
            "; nativePreferred=" +
            nativePreferredWidth.ToString("0.0", CultureInfo.InvariantCulture) +
            "; hostPreferred=" +
            hostPreferredWidth.ToString("0.0", CultureInfo.InvariantCulture) +
            "; badgePreferredByCapacity=" + badgeWidths +
            "; allocatedName=" +
            allocation.NativeNameWidth.ToString("0.0", CultureInfo.InvariantCulture) +
            "; allocatedLane=" +
            allocation.OwnedLaneWidth.ToString("0.0", CultureInfo.InvariantCulture) +
            "; badgeCapacity=" +
            allocation.BadgeCapacity.ToString(CultureInfo.InvariantCulture) +
            "; overflowAvailable=" +
            overflowLaneAvailableWidth.ToString("0.0", CultureInfo.InvariantCulture) +
            "; useOverflow=" +
            allocation.UseOverflowLane.ToString() +
            "; anchor=" +
            nameRect.anchorMin.x.ToString("0.000", CultureInfo.InvariantCulture) +
            ".." +
            nameRect.anchorMax.x.ToString("0.000", CultureInfo.InvariantCulture) +
            "; offsets=" +
            nameRect.offsetMin.x.ToString("0.0", CultureInfo.InvariantCulture) +
            ".." +
            nameRect.offsetMax.x.ToString("0.0", CultureInfo.InvariantCulture) +
            "; hierarchy=" + DescribeRectHierarchy(nameRect));
    }

    private static string DescribeRectHierarchy(RectTransform start)
    {
        var parts = new List<string>();
        Transform current = start;
        for (int depth = 0; current != null && depth < 8; depth++, current = current.parent)
        {
            RectTransform rect = current as RectTransform;
            if (rect == null)
            {
                parts.Add(current.name + "[non-rect]");
                continue;
            }

            parts.Add(
                current.name +
                "[w=" + rect.rect.width.ToString("0.0", CultureInfo.InvariantCulture) +
                ",x=" + rect.anchoredPosition.x.ToString("0.0", CultureInfo.InvariantCulture) +
                ",a=" + rect.anchorMin.x.ToString("0.00", CultureInfo.InvariantCulture) +
                ".." + rect.anchorMax.x.ToString("0.00", CultureInfo.InvariantCulture) +
                ",o=" + rect.offsetMin.x.ToString("0.0", CultureInfo.InvariantCulture) +
                ".." + rect.offsetMax.x.ToString("0.0", CultureInfo.InvariantCulture) +
                "]");
        }

        return string.Join(" <- ", parts);
    }

    private static PlayerBadgeLayoutCacheKey CreateLayoutCacheKey(
        TMP_Text badgeLabel,
        TMP_Text hostLabel,
        TMP_Text nativeName,
        RectTransform nameRect,
        string badgeText,
        string hostText)
    {
        Canvas canvas =
            nameRect.GetComponentInParent<Canvas>();
        float canvasScale = canvas != null
            ? canvas.scaleFactor
            : 1f;
        Vector3 lossyScale = nameRect.lossyScale;
        float relevantScale = canvasScale *
                              Mathf.Abs(lossyScale.x);
        int measurementFingerprint = 17;
        AddFingerprint(
            ref measurementFingerprint,
            TextMeasurementFingerprint(badgeLabel));
        AddFingerprint(
            ref measurementFingerprint,
            TextMeasurementFingerprint(hostLabel));
        AddFingerprint(
            ref measurementFingerprint,
            TextMeasurementFingerprint(nativeName));
        int geometryFingerprint =
            RectGeometryFingerprint(
                nameRect,
                lossyScale.y);
        return new PlayerBadgeLayoutCacheKey(
            badgeText,
            hostText,
            nativeName.text ?? string.Empty,
            measurementFingerprint,
            geometryFingerprint,
            nameRect.rect.width,
            relevantScale);
    }

    private static int TextMeasurementFingerprint(
        TMP_Text text)
    {
        int hash = 17;
        TMP_FontAsset font = text.font;
        Material material = text.fontSharedMaterial;
        AddFingerprint(
            ref hash,
            font != null ? font.GetInstanceID() : 0);
        AddFingerprint(
            ref hash,
            material != null
                ? material.GetInstanceID()
                : 0);
        AddFingerprint(ref hash, text.fontSize);
        AddFingerprint(ref hash, (int)text.fontStyle);
        AddFingerprint(ref hash, (int)text.fontWeight);
        AddFingerprint(ref hash, text.characterSpacing);
        AddFingerprint(ref hash, text.wordSpacing);
        AddFingerprint(ref hash, text.enableAutoSizing ? 1 : 0);
        AddFingerprint(ref hash, text.fontSizeMin);
        AddFingerprint(ref hash, text.fontSizeMax);
        AddFingerprint(ref hash, text.richText ? 1 : 0);
        return hash;
    }

    private static int RectGeometryFingerprint(
        RectTransform rect,
        float lossyScaleY)
    {
        int hash = 17;
        AddFingerprint(ref hash, rect.anchorMin.x);
        AddFingerprint(ref hash, rect.anchorMin.y);
        AddFingerprint(ref hash, rect.anchorMax.x);
        AddFingerprint(ref hash, rect.anchorMax.y);
        AddFingerprint(ref hash, rect.pivot.x);
        AddFingerprint(ref hash, rect.pivot.y);
        AddFingerprint(ref hash, rect.offsetMin.x);
        AddFingerprint(ref hash, rect.offsetMin.y);
        AddFingerprint(ref hash, rect.offsetMax.x);
        AddFingerprint(ref hash, rect.offsetMax.y);
        AddFingerprint(ref hash, rect.rect.height);
        AddFingerprint(ref hash, lossyScaleY);
        return hash;
    }

    private static void AddFingerprint(
        ref int hash,
        int value)
    {
        unchecked
        {
            hash = (hash * 31) + value;
        }
    }

    private static void AddFingerprint(
        ref int hash,
        float value)
    {
        AddFingerprint(
            ref hash,
            value.GetHashCode());
    }

    private static BlueSagePlayerBadgeMarker
        FindOwnedMarker(PlayerItemController row)
    {
        int rowId = row.GetInstanceID();
        if (OwnedMarkersByRowInstanceId.TryGetValue(
                rowId,
                out BlueSagePlayerBadgeMarker registered))
        {
            if (registered != null &&
                !registered.IsRetired &&
                registered.OwnerRow != null &&
                ReferenceEquals(
                    registered.OwnerRow,
                    row))
            {
                return registered;
            }
            OwnedMarkersByRowInstanceId.Remove(rowId);
        }

        return row.GetComponentsInChildren<
                BlueSagePlayerBadgeMarker>(true)
            .FirstOrDefault(candidate =>
                candidate != null &&
                !candidate.IsRetired &&
                candidate.OwnerRow != null &&
                ReferenceEquals(
                    candidate.OwnerRow,
                    row));
    }

    private static void DestroyOwnedMarker(
        BlueSagePlayerBadgeMarker marker)
    {
        if (marker == null)
        {
            return;
        }

        marker.IsRetired = true;
        UnregisterOwnedMarker(marker);
        marker.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(
            marker.gameObject);
    }

    private static void HideAllPresentations()
    {
        PlayerBadgePresentation empty =
            PlayerBadgePolicy.Compose(
                default,
                BadgeCapacity);
        foreach (BlueSagePlayerBadgeMarker marker in
                 OwnedMarkersByRowInstanceId.Values.ToArray())
        {
            ApplyPresentation(marker, empty);
        }
    }

    private static bool IsActive(
        BlueSagePlayerBadgeMarker marker)
    {
        return marker != null &&
               marker.OwnerRow != null &&
               marker.OwnerRow.gameObject != null &&
               marker.OwnerRow.gameObject.activeInHierarchy &&
               marker.HasLivePresentation();
    }

    private static bool TryGetLobby(
        out CSteamID lobby)
    {
        lobby = CSteamID.Nil;
        return SteamManager.Initialized &&
               Plugin.TryGetRescueLobby(out lobby) &&
               lobby != CSteamID.Nil;
    }

}
