using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class PlayerIdentityEvidence
{
    internal string SteamId = string.Empty;
    internal string DisplayName = string.Empty;
    internal string DisplayNameRaw = string.Empty;
    internal string SteamPersona = string.Empty;
    internal string Role = "PLAYER";
    internal int RosterIndex = -1;
    internal string NetworkPlayerId = string.Empty;
    internal ulong PurrNetId;
    internal int TransformInstanceId;
    internal bool IsLocal;
}

internal static class PlayerIdentityEvidenceController
{
    private static readonly FieldInfo PlayerSteamIdField =
        AccessTools.Field(
            typeof(PlayerItemController),
            "_playerSteamId");

    private sealed class SnapshotRoleContext
    {
        internal string LocalSteamId = string.Empty;
        internal string OwnerSteamId = string.Empty;
        internal string[] HelperSteamIds = Array.Empty<string>();
        internal readonly Dictionary<string, string> RoleBySteamId = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class RosterSnapshot
    {
        internal PlayerIdentityEvidence[] Verified = Array.Empty<PlayerIdentityEvidence>();
        // Exact live rows whose claimed SteamID is proven (transport, host attestation, self, or Steam
        // lobby). Superset of Verified; read-only surfaces such as the native ID card use it.
        internal PlayerIdentityEvidence[] IdentityConfirmed = Array.Empty<PlayerIdentityEvidence>();
        internal SteamIdTruthRow[] TruthRows = Array.Empty<SteamIdTruthRow>();
        internal int TransportVerifiedCount;
        internal int HostAttestedCount;
        internal int SteamIdMismatchCount;
        internal int LobbyMemberLimit;
        internal bool TransportUnavailable;
        internal string Error = string.Empty;
        internal string CoverageIssue = string.Empty;
        internal bool CoverageComplete;
        internal int NativeRowCount;
        internal int ActiveNativeRowCount;
        internal int PurrNetRowCount;
        internal int LobbyMemberCount;
        internal int LiveSteamCheckCount;
        internal int LeftStaleCount;
        internal int AmbiguousCount;
        internal ulong LobbyId;
        internal ulong LocalSteamId;
        internal ulong LobbyOwnerSteamId;
        internal string[] NativeSteamIds = Array.Empty<string>();
        internal string[] NativePlayerIds = Array.Empty<string>();
        internal string[] NativePurrNetIds = Array.Empty<string>();
        internal string[] PurrNetActivePlayerIds = Array.Empty<string>();
        internal bool[] HasLiveTransform = Array.Empty<bool>();
        internal int[] TransformInstanceIds = Array.Empty<int>();
        internal string[] LobbyMemberSteamIds = Array.Empty<string>();
        internal string[] PublishedHelperSteamIds = Array.Empty<string>();
        internal long CapturedUtcTicks;
        internal long CaptureStartedTimestamp;
        internal int CapturedFrame;
    }

    private static readonly long SnapshotLifetimeTicks =
        LargeLobbyRefreshPolicy.RosterSnapshotLifetime.Ticks;
    private static RosterSnapshot _cachedSnapshot;
    private static bool _snapshotDirty = true;
    private static long _snapshotCaptureCount;
    private static double _lastSnapshotCaptureMilliseconds;
    private static double _maxSnapshotCaptureMilliseconds;
    private static StableStateLogState _coverageLogState;
    private static string _lastCoverageJournalKey = string.Empty;

    private static int _lastSteamIdMismatchCount;
    private static ulong _lastSteamIdMismatchLobbyId;
    private static bool _steamIdMismatchAlertPending;

    internal static bool ConsumeSteamIdMismatchAlert()
    {
        bool pending = _steamIdMismatchAlertPending;
        _steamIdMismatchAlertPending = false;
        return pending;
    }

    internal static void InvalidateRosterCache()
    {
        _snapshotDirty = true;
    }

    internal static bool TryResolveExact(string steamId, out PlayerIdentityEvidence evidence, out string error)
    {
        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: false);
        error = snapshot.Error;
        evidence = snapshot.Verified
            .SingleOrDefault(candidate => string.Equals(candidate.SteamId, steamId, StringComparison.Ordinal));
        if (evidence != null) return true;
        error = DescribeUnresolvedExactRow(steamId, snapshot);
        return false;
    }

    internal static bool TryResolveConfirmed(string steamId, out PlayerIdentityEvidence evidence, out string error)
    {
        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: false);
        error = snapshot.Error;
        evidence = snapshot.IdentityConfirmed
            .SingleOrDefault(candidate => string.Equals(candidate.SteamId, steamId, StringComparison.Ordinal));
        if (evidence != null) return true;
        error = DescribeUnresolvedExactRow(steamId, snapshot);
        return false;
    }

    // Host only: transport verdicts for the owner-written lobby attestation Helpers read. Returns
    // null on an error snapshot so the last good attestation stays published until its lease ends,
    // instead of being blanked and dropping spoof verdicts mid-roster-churn.
    internal static string BuildHostSteamIdTruthAttestation(string ownerSteamId, long issuedUnixSeconds)
    {
        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: true);
        if (!string.IsNullOrWhiteSpace(snapshot.Error))
        {
            return null;
        }

        return SteamIdTruthAttestationPolicy.Build(ownerSteamId, issuedUnixSeconds, snapshot.TruthRows);
    }

    private static Dictionary<string, SteamIdTruthAttestationEntry> ReadHelperAttestation(CSteamID lobby)
    {
        if (SteamIdModerationController.GetLocalAccessState(out _, out _) != LobbySafetyAccessState.HelperReady)
        {
            return null;
        }

        string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString(CultureInfo.InvariantCulture);
        return SteamIdTruthAttestationPolicy.TryParse(
            SteamMatchmaking.GetLobbyData(lobby, SteamIdModerationController.SteamIdTruthKey),
            owner,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            out Dictionary<string, SteamIdTruthAttestationEntry> entries)
            ? entries
            : null;
    }

    internal static bool TryResolveExactWithAuthorityFingerprint(
        string steamId,
        ulong expectedLobbyId,
        out PlayerIdentityEvidence evidence,
        out string fingerprint,
        out bool contextValid,
        out string error)
    {
        fingerprint = string.Empty;
        RosterSnapshot snapshot =
            CaptureRosterSnapshot(cacheAllowed: true);
        contextValid =
            TryBuildAuthorityFingerprint(
                snapshot,
                expectedLobbyId,
                out fingerprint);
        error = snapshot.Error;
        if (!contextValid && string.IsNullOrWhiteSpace(error))
        {
            error = !string.IsNullOrWhiteSpace(snapshot.CoverageIssue)
                ? snapshot.CoverageIssue
                : "the bounded current roster snapshot is incomplete";
        }
        evidence = contextValid
            ? snapshot.Verified.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.SteamId,
                    steamId,
                    StringComparison.Ordinal))
            : null;
        if (evidence != null)
        {
            return true;
        }

        if (contextValid)
        {
            error = DescribeUnresolvedExactRow(
                steamId,
                snapshot);
        }
        return false;
    }

    internal static bool TryResolveExactWithCompanionFingerprint(
        string steamId,
        ulong expectedLobbyId,
        out PlayerIdentityEvidence evidence,
        out string fingerprint,
        out bool contextValid,
        out string error)
    {
        evidence = null;
        if (!TryCaptureCompanionIdentityContextCached(
                expectedLobbyId,
                out PlayerIdentityEvidence[] verified,
                out fingerprint,
                out contextValid,
                out error))
        {
            return false;
        }

        evidence = contextValid
            ? verified.SingleOrDefault(candidate =>
                string.Equals(candidate.SteamId, steamId, StringComparison.Ordinal))
            : null;
        if (evidence != null)
        {
            return true;
        }

        if (contextValid)
        {
            error = "the requested exact row is absent from the cached companion identity context";
        }
        return false;
    }

    internal static bool TryCaptureCompanionIdentityContextCached(
        ulong expectedLobbyId,
        out PlayerIdentityEvidence[] verified,
        out string fingerprint,
        out bool contextValid,
        out string error)
    {
        verified = Array.Empty<PlayerIdentityEvidence>();
        fingerprint = string.Empty;
        error = string.Empty;
        string fingerprintError = string.Empty;
        RosterSnapshot snapshot =
            CaptureRosterSnapshot(cacheAllowed: true);
        contextValid =
            snapshot != null &&
            string.IsNullOrWhiteSpace(snapshot.Error) &&
            snapshot.LobbyId == expectedLobbyId &&
            CompanionExactIdentityFingerprintPolicy.TryBuildFingerprint(
                snapshot.LobbyId,
                snapshot.LobbyOwnerSteamId,
                snapshot.LocalSteamId,
                snapshot.LobbyMemberSteamIds,
                snapshot.PublishedHelperSteamIds,
                snapshot.Verified.Select(candidate => candidate.SteamId).ToArray(),
                snapshot.Verified.Select(candidate => candidate.PurrNetId).ToArray(),
                snapshot.Verified.Select(candidate => candidate.TransformInstanceId).ToArray(),
                out fingerprint,
                out fingerprintError);
        error = snapshot?.Error ?? "the bounded current roster snapshot is unavailable";
        if (!contextValid)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = string.IsNullOrWhiteSpace(fingerprintError)
                    ? "the bounded exact-row companion identity context is invalid"
                    : fingerprintError;
            }
            return false;
        }

        verified = snapshot.Verified ?? Array.Empty<PlayerIdentityEvidence>();
        return true;
    }

    internal static bool TryCaptureAuthorityEvidenceFingerprint(
        ulong expectedLobbyId,
        out string fingerprint)
    {
        RosterSnapshot snapshot =
            CaptureRosterSnapshot(cacheAllowed: false);
        return TryBuildAuthorityFingerprint(
            snapshot,
            expectedLobbyId,
            out fingerprint);
    }

    internal static bool TryCaptureAuthorityEvidenceFingerprintCached(
        ulong expectedLobbyId,
        out string fingerprint)
    {
        RosterSnapshot snapshot =
            CaptureRosterSnapshot(cacheAllowed: true);
        return TryBuildAuthorityFingerprint(
            snapshot,
            expectedLobbyId,
            out fingerprint);
    }

    internal static bool TryCaptureCompleteAuthorityEvidenceAndRowsCached(
        ulong expectedLobbyId,
        out string fingerprint,
        out HashSet<PlayerItemController> currentRows)
    {
        fingerprint = string.Empty;
        currentRows = new HashSet<PlayerItemController>();
        RosterSnapshot snapshot =
            CaptureRosterSnapshot(cacheAllowed: true);
        if (snapshot == null ||
            !snapshot.CoverageComplete ||
            !TryBuildAuthorityFingerprint(
                snapshot,
                expectedLobbyId,
                out fingerprint))
        {
            return false;
        }

        return TryCaptureAlignedCurrentRows(
            snapshot,
            out currentRows);
    }

    private static bool TryCaptureAlignedCurrentRows(
        RosterSnapshot snapshot,
        out HashSet<PlayerItemController> currentRows)
    {
        currentRows = new HashSet<PlayerItemController>();
        PlayerPanelController panel =
            NetworkSingleton<PlayerPanelController>.I;
        if (snapshot == null ||
            panel == null ||
            panel.PlayerSteamIDs == null ||
            panel.PlayerIDs == null ||
            panel.PlayerTransforms == null ||
            panel.IDInfos == null ||
            panel.PlayerItemControllers == null ||
            !NativeRosterShapePolicy.HasAlignedActivePrefix(
                panel.PlayerSteamIDs.Count,
                panel.PlayerIDs.Count,
                panel.PlayerTransforms.Count,
                panel.IDInfos.Count,
                panel.PlayerItemControllers.Count) ||
            snapshot.Verified == null ||
            snapshot.Verified.Length == 0 ||
            snapshot.Verified.Length != snapshot.LobbyMemberCount ||
            !PanelAuthorityTupleMatchesSnapshot(
                panel,
                snapshot))
        {
            return false;
        }

        var rowsByIndex =
            new Dictionary<int, PlayerItemController>();
        foreach (PlayerIdentityEvidence identity in
                 snapshot.Verified)
        {
            int index = identity?.RosterIndex ?? -1;
            if (index < 0 ||
                index >= panel.PlayerItemControllers.Count)
            {
                return false;
            }

            PlayerItemController row =
                panel.PlayerItemControllers[index];
            if (row == null ||
                row.gameObject == null ||
                !string.Equals(
                    PlayerSteamIdField?.GetValue(row) as string,
                    identity.SteamId,
                    StringComparison.Ordinal) ||
                !currentRows.Add(row))
            {
                return false;
            }
            rowsByIndex[index] = row;
        }

        if (currentRows.Count != snapshot.Verified.Length ||
            !ReferenceEquals(
                NetworkSingleton<PlayerPanelController>.I,
                panel) ||
            !PanelAuthorityTupleMatchesSnapshot(
                panel,
                snapshot))
        {
            currentRows.Clear();
            return false;
        }

        foreach (KeyValuePair<int, PlayerItemController> pair in
                 rowsByIndex)
        {
            if (pair.Key < 0 ||
                pair.Key >= panel.PlayerItemControllers.Count ||
                !ReferenceEquals(
                    panel.PlayerItemControllers[pair.Key],
                    pair.Value) ||
                !string.Equals(
                    PlayerSteamIdField?.GetValue(pair.Value) as string,
                    snapshot.NativeSteamIds[pair.Key],
                    StringComparison.Ordinal))
            {
                currentRows.Clear();
                return false;
            }
        }
        return true;
    }

    private static bool PanelAuthorityTupleMatchesSnapshot(
        PlayerPanelController panel,
        RosterSnapshot snapshot)
    {
        return panel != null &&
               snapshot != null &&
               panel.PlayerSteamIDs != null &&
               panel.PlayerIDs != null &&
               panel.PlayerTransforms != null &&
               panel.IDInfos != null &&
               panel.PlayerItemControllers != null &&
               NativeRosterShapePolicy.HasAlignedActivePrefix(
                   panel.PlayerSteamIDs.Count,
                   panel.PlayerIDs.Count,
                   panel.PlayerTransforms.Count,
                   panel.IDInfos.Count,
                   panel.PlayerItemControllers.Count) &&
               panel.PlayerSteamIDs.SequenceEqual(
                   snapshot.NativeSteamIds) &&
               panel.PlayerIDs.Select(id =>
                       ((ulong)id.id).ToString(
                           CultureInfo.InvariantCulture))
                   .SequenceEqual(snapshot.NativePurrNetIds) &&
               panel.PlayerTransforms.Select(transform =>
                       transform == null
                           ? 0
                           : transform.GetInstanceID())
                   .SequenceEqual(snapshot.TransformInstanceIds);
    }

    internal static bool TryResolveExactCached(string steamId, out PlayerIdentityEvidence evidence, out string error)
    {
        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: true);
        error = snapshot.Error;
        evidence = snapshot.Verified
            .SingleOrDefault(candidate => string.Equals(candidate.SteamId, steamId, StringComparison.Ordinal));
        if (evidence != null) return true;
        error = DescribeUnresolvedExactRow(steamId, snapshot);
        return false;
    }

    internal static bool TryResolveRosterIndex(int senderIndex, out PlayerIdentityEvidence evidence, out string error)
    {
        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: true);
        return TryResolveRosterIndex(snapshot, senderIndex, out evidence, out error);
    }

    internal static bool TryResolveRosterIndexFresh(int senderIndex, out PlayerIdentityEvidence evidence, out string error)
    {
        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: false);
        return TryResolveRosterIndex(snapshot, senderIndex, out evidence, out error);
    }

    private static bool TryResolveRosterIndex(RosterSnapshot snapshot, int senderIndex, out PlayerIdentityEvidence evidence, out string error)
    {
        error = snapshot.Error;
        evidence = snapshot.Verified.SingleOrDefault(candidate => candidate.RosterIndex == senderIndex);
        if (evidence != null) return true;
        if (string.IsNullOrWhiteSpace(error)) error = "the sender index is not a verified current player row";
        return false;
    }

    internal static bool TryResolveLookup(string query, out PlayerIdentityEvidence evidence, out string error)
    {
        evidence = null;
        IReadOnlyList<PlayerIdentityEvidence> matches = ResolveLookupMatches(query, out error);
        if (matches.Count == 1)
        {
            evidence = matches[0];
            error = string.Empty;
            return true;
        }

        if (matches.Count > 1) error = "That query matches " + matches.Count + " verified current players. Select the exact row or use more name/SteamID detail.";
        return false;
    }

    internal static IReadOnlyList<PlayerIdentityEvidence> ResolveLookupMatches(string query, out string error)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            error = SteamIdModerationController.LobbySafetyDeniedMessage;
            return Array.Empty<PlayerIdentityEvidence>();
        }

        string clean = (query ?? string.Empty).Trim();
        RosterSnapshot freshSnapshot = CaptureRosterSnapshot(cacheAllowed: false);
        IReadOnlyList<PlayerIdentityEvidence> roster = freshSnapshot.Verified;
        string rosterError = freshSnapshot.Error;
        if (!string.IsNullOrWhiteSpace(rosterError) && roster.Count == 0)
        {
            error = rosterError;
            return Array.Empty<PlayerIdentityEvidence>();
        }

        if (CloneIncidentLedger.TryResolveIncident(clean, out CloneIncident incident))
        {
            PlayerIdentityEvidence suspect = roster.SingleOrDefault(item => string.Equals(item.SteamId, incident.SuspectSteamId, StringComparison.Ordinal));
            error = suspect == null
                ? (!string.IsNullOrWhiteSpace(rosterError) ? rosterError : "The incident suspect is no longer a verified current player.")
                : string.Empty;
            return suspect == null ? Array.Empty<PlayerIdentityEvidence>() : new[] { suspect };
        }

        if (ModerationProtocolPolicy.IsSteamId64(clean))
        {
            PlayerIdentityEvidence exact = roster.SingleOrDefault(item => string.Equals(item.SteamId, clean, StringComparison.Ordinal));
            error = exact == null
                ? (!string.IsNullOrWhiteSpace(rosterError) ? rosterError : "That exact SteamID64 is not a verified current player.")
                : string.Empty;
            return exact == null ? Array.Empty<PlayerIdentityEvidence>() : new[] { exact };
        }

        var candidates = roster.Select(item => new PlayerLookupCandidate
        {
            SteamId = item.SteamId,
            DisplayNameRaw = item.DisplayNameRaw,
            DisplayNamePlain = item.DisplayName,
            SteamPersona = item.SteamPersona,
            Role = item.Role,
            IsLocal = item.IsLocal,
        }).ToArray();
        IReadOnlyList<PlayerLookupCandidate> matched = PlayerLookupPolicy.FindMatches(clean, candidates, out error);
        var ids = new HashSet<string>(matched.Select(item => item.SteamId), StringComparer.Ordinal);
        PlayerIdentityEvidence[] resolved = roster.Where(item => ids.Contains(item.SteamId)).ToArray();
        if (resolved.Length == 0 && !string.IsNullOrWhiteSpace(rosterError))
            error = rosterError + " Verified rows remain usable, but Clone Shield and absence-sensitive actions wait for complete coverage.";
        return resolved;
    }

    internal static IReadOnlyList<PlayerIdentityEvidence> FilterVerifiedRoster(
        IReadOnlyList<PlayerIdentityEvidence> roster,
        string query,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            error = string.Empty;
            return roster ?? Array.Empty<PlayerIdentityEvidence>();
        }

        var candidates = (roster ?? Array.Empty<PlayerIdentityEvidence>()).Select(item => new PlayerLookupCandidate
        {
            SteamId = item.SteamId,
            DisplayNameRaw = item.DisplayNameRaw,
            DisplayNamePlain = item.DisplayName,
            SteamPersona = item.SteamPersona,
            Role = item.Role,
            IsLocal = item.IsLocal,
        }).ToArray();
        IReadOnlyList<PlayerLookupCandidate> matched = PlayerLookupPolicy.FindMatches(query, candidates, out error);
        var ids = new HashSet<string>(matched.Select(item => item.SteamId), StringComparer.Ordinal);
        return (roster ?? Array.Empty<PlayerIdentityEvidence>()).Where(item => ids.Contains(item.SteamId)).ToArray();
    }

    internal static IReadOnlyList<PlayerIdentityEvidence> GetVerifiedRoster()
    {
        if (!Plugin.CanUseLobbySafety(out _)) return Array.Empty<PlayerIdentityEvidence>();
        return CaptureRosterSnapshot(cacheAllowed: true).Verified;
    }

    internal static IReadOnlyList<PlayerIdentityEvidence> GetVerifiedRoster(out string status)
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            status = SteamIdModerationController.LobbySafetyDeniedMessage;
            return Array.Empty<PlayerIdentityEvidence>();
        }

        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: true);
        IReadOnlyList<PlayerIdentityEvidence> roster = snapshot.Verified;
        string error = snapshot.Error;
        string coverageIssue = snapshot.CoverageIssue;
        int nativeRowCount = snapshot.NativeRowCount;
        int lobbyMemberCount = snapshot.LobbyMemberCount;
        int excludedHistory = Math.Max(0, nativeRowCount - roster.Count);
        if (!string.IsNullOrWhiteSpace(error) && roster.Count == 0)
        {
            status = "Verification paused: " + error + ". No hidden or guessed row is actionable.";
            return roster;
        }

        status = roster.Count + " active verified • " + nativeRowCount + " player-list rows • " +
                 snapshot.PurrNetRowCount + " connected PurrNet players • " +
                 lobbyMemberCount + " Steam lobby members • " + excludedHistory + " non-actionable.";
        if (snapshot.LeftStaleCount > 0)
            status += " " + snapshot.LeftStaleCount + " left/stale native-history " + (snapshot.LeftStaleCount == 1 ? "row is" : "rows are") + " excluded pending game cleanup.";
        if (snapshot.AmbiguousCount > 0)
            status += " " + snapshot.AmbiguousCount + " ambiguous/syncing " + (snapshot.AmbiguousCount == 1 ? "row is" : "rows are") + " locked.";
        if (!string.IsNullOrWhiteSpace(coverageIssue))
            status += " " + coverageIssue + " Exact verified rows remain usable; Clone Shield and absence-sensitive actions wait for complete coverage.";
        else if (!string.IsNullOrWhiteSpace(error))
            status += " " + error;
        status += " Mod installation is not part of identity verification.";
        return roster;
    }

    // Round 4b (Blue, live UAT 2026-10-20): the Host Tools "Player matching" section reads this. It re-captures
    // under the normal 2 s lifetime rule first, so the menu never shows the one-frame "cached snapshot is stale"
    // state that the roster box below it was about to clear. Callers that want fail-closed cached semantics
    // (host console, resync controller) keep GetCachedIdentityResyncCoverage.
    internal static IdentityResyncCoverage GetIdentityResyncCoverage()
    {
        if (!Plugin.CanUseLobbySafety(out _))
        {
            return GetCachedIdentityResyncCoverage();
        }

        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: true);
        return new IdentityResyncCoverage(
            snapshot.Verified.Length,
            snapshot.ActiveNativeRowCount,
            snapshot.NativeRowCount,
            snapshot.PurrNetRowCount,
            snapshot.LobbyMemberCount,
            snapshot.LiveSteamCheckCount,
            snapshot.LeftStaleCount,
            snapshot.AmbiguousCount,
            captureAvailable: true,
            snapshot.Error);
    }

    internal static IdentityResyncCoverage GetCachedIdentityResyncCoverage()
    {
        RosterSnapshot snapshot = _cachedSnapshot;
        if (snapshot == null)
        {
            return IdentityResyncCoverage.Unavailable(
                "no authoritative identity snapshot is available yet");
        }

        long ageTicks = DateTime.UtcNow.Ticks - snapshot.CapturedUtcTicks;
        string error = snapshot.Error;
        if ((_snapshotDirty || ageTicks < 0L || ageTicks > SnapshotLifetimeTicks) &&
            string.IsNullOrWhiteSpace(error))
        {
            error = "the cached identity snapshot is stale; status remains fail-closed";
        }
        return new IdentityResyncCoverage(
            snapshot.Verified.Length,
            snapshot.ActiveNativeRowCount,
            snapshot.NativeRowCount,
            snapshot.PurrNetRowCount,
            snapshot.LobbyMemberCount,
            snapshot.LiveSteamCheckCount,
            snapshot.LeftStaleCount,
            snapshot.AmbiguousCount,
            captureAvailable: true,
            error);
    }

    internal static IReadOnlyList<string> GetCachedIdentityResyncSurfaceTargets()
    {
        RosterSnapshot snapshot = _cachedSnapshot;
        if (snapshot == null)
        {
            return Array.Empty<string>();
        }
        return snapshot.NativeSteamIds
            .Where(ModerationProtocolPolicy.IsSteamId64)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static HashSet<string> GetVerifiedCurrentSteamIdSetAlreadyCached()
    {
        RosterSnapshot snapshot = _cachedSnapshot;
        return new HashSet<string>(
            snapshot?.Verified.Select(item => item.SteamId) ??
            Enumerable.Empty<string>(),
            StringComparer.Ordinal);
    }

    internal static void ForceRefreshIncompleteRosterRowsOnce(
        out string before,
        out string after,
        out bool coverageComplete,
        out IReadOnlyList<string> targetedSteamIds,
        out int healedRowCount,
        out bool capturePerformed)
    {
        RosterSnapshot previous = _cachedSnapshot;
        if (previous == null)
        {
            before = "not-yet-captured";
            RosterSnapshot baseline = CaptureRosterSnapshot(cacheAllowed: false);
            targetedSteamIds = PlayerIdentityEvidencePolicy.SelectManualResyncTargets(
                baseline.NativeSteamIds,
                baseline.LobbyMemberSteamIds,
                baseline.Verified.Select(item => item.SteamId).ToArray());
            after = FormatDiagnosticRosterSummary(baseline);
            coverageComplete = baseline.CoverageComplete;
            healedRowCount = 0;
            capturePerformed = true;
            return;
        }

        before = FormatDiagnosticRosterSummary(previous);
        IReadOnlyList<string> initialTargets = PlayerIdentityEvidencePolicy.SelectManualResyncTargets(
            previous.NativeSteamIds,
            previous.LobbyMemberSteamIds,
            previous.Verified.Select(item => item.SteamId).ToArray());
        long nowUtcTicks = DateTime.UtcNow.Ticks;
        long snapshotAgeTicks = nowUtcTicks >= previous.CapturedUtcTicks
            ? nowUtcTicks - previous.CapturedUtcTicks
            : SnapshotLifetimeTicks + 1L;
        bool snapshotExpired = snapshotAgeTicks > SnapshotLifetimeTicks;
        bool requiresDiscoveryCapture =
            _snapshotDirty || snapshotExpired || !previous.CoverageComplete || !string.IsNullOrWhiteSpace(previous.Error);
        if (initialTargets.Count == 0 && !requiresDiscoveryCapture)
        {
            targetedSteamIds = initialTargets;
            after = before;
            coverageComplete = previous.CoverageComplete;
            healedRowCount = 0;
            capturePerformed = false;
            return;
        }

        InvalidateRosterCache();
        RosterSnapshot refreshed = CaptureRosterSnapshot(cacheAllowed: false);
        after = FormatDiagnosticRosterSummary(refreshed);
        coverageComplete = refreshed.CoverageComplete;
        var refreshedVerified = new HashSet<string>(
            refreshed.Verified.Select(item => item.SteamId),
            StringComparer.Ordinal);
        IReadOnlyList<string> refreshedTargets = PlayerIdentityEvidencePolicy.SelectManualResyncTargets(
            refreshed.NativeSteamIds,
            refreshed.LobbyMemberSteamIds,
            refreshed.Verified.Select(item => item.SteamId).ToArray());
        targetedSteamIds = initialTargets
            .Concat(refreshedTargets)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        healedRowCount = initialTargets.Count(refreshedVerified.Contains);
        capturePerformed = true;
    }

    private static string FormatDiagnosticRosterSummary(RosterSnapshot snapshot)
    {
        IReadOnlyList<PlayerIdentityEvidence> roster = snapshot.Verified;
        int nativeRowCount = snapshot.NativeRowCount;
        int lobbyMemberCount = snapshot.LobbyMemberCount;
        int excluded = Math.Max(0, nativeRowCount - roster.Count);
        return "verified=" + roster.Count + ",native=" + nativeRowCount +
               ",activeNative=" + snapshot.ActiveNativeRowCount +
               ",purrNetActive=" + snapshot.PurrNetRowCount +
               ",steam=" + lobbyMemberCount +
               ",excluded=" + excluded + ",liveSteamCheck=" + snapshot.LiveSteamCheckCount +
               ",leftStale=" + snapshot.LeftStaleCount + ",ambiguous=" + snapshot.AmbiguousCount +
               ",complete=" + snapshot.CoverageComplete +
               ",snapshotCaptures=" + _snapshotCaptureCount +
               ",snapshotLastMs=" + _lastSnapshotCaptureMilliseconds.ToString("0.0") +
               ",snapshotMaxMs=" + _maxSnapshotCaptureMilliseconds.ToString("0.0") +
               ",error=" + DiagnosticValue(snapshot.Error) +
               ",coverageIssue=" + DiagnosticValue(snapshot.CoverageIssue);
    }

    private static string DiagnosticValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Replace(",", ";").Replace("\r", " ").Replace("\n", " ").Trim();
    }

    internal static IReadOnlyList<string> GetVerifiedCurrentSteamIds(out string error)
    {
        return GetVerifiedCurrentSteamIds(out error, out _);
    }

    internal static IReadOnlyList<string> GetVerifiedCurrentSteamIds(out string error, out bool coverageComplete)
    {
        RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: true);
        coverageComplete = snapshot.CoverageComplete;
        error = !string.IsNullOrWhiteSpace(snapshot.Error) ? snapshot.Error : snapshot.CoverageIssue;
        return snapshot.Verified
            .Select(item => item.SteamId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static HashSet<string> GetVerifiedCurrentSteamIdSetCached()
    {
        return new HashSet<string>(CaptureRosterSnapshot(cacheAllowed: true).Verified.Select(item => item.SteamId), StringComparer.Ordinal);
    }

    internal static IReadOnlyList<string> GetLookupSuggestions()
    {
        if (!Plugin.CanUseLobbySafety(out _)) return Array.Empty<string>();
        var values = new List<string> { "list" };
        values.AddRange(CloneIncidentLedger.Snapshot.Reverse().Take(12).Select(item => item.Id));
        string[] ids = GetVerifiedCurrentSteamIds(out _).Distinct(StringComparer.Ordinal).ToArray();
        foreach (string id in ids.Take(64))
        {
            string suffix = Suffix(id);
            values.Add(ids.Count(other => other.EndsWith(suffix, StringComparison.Ordinal)) == 1 ? suffix : id);
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static string Suffix(string steamId)
    {
        return steamId != null && steamId.Length > 6 ? steamId.Substring(steamId.Length - 6) : steamId ?? "unknown";
    }

    internal static string BuildCopyableEvidence(PlayerIdentityEvidence evidence)
    {
        if (evidence == null) return string.Empty;
        return "BlueSage verified live identity" +
               "\nDisplayed name: " + RecordValue(evidence.DisplayName) +
               "\nRaw rendered name/TMP: " + RecordValue(evidence.DisplayNameRaw) +
               "\nSteam persona: " + RecordValue(string.IsNullOrWhiteSpace(evidence.SteamPersona) ? "not cached" : evidence.SteamPersona) +
               "\nSteamID64: " + evidence.SteamId +
               "\nVerified live row: #" + (evidence.RosterIndex + 1) +
               "\nPlayerID: " + RecordValue(evidence.NetworkPlayerId) +
               "\nCurrent role: " + RecordValue(evidence.Role);
    }

    private static string RecordValue(string value)
    {
        return (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n").Trim();
    }

    private static bool TryBuildAuthorityFingerprint(
        RosterSnapshot snapshot,
        ulong expectedLobbyId,
        out string fingerprint)
    {
        fingerprint = string.Empty;
        return snapshot != null &&
               string.IsNullOrWhiteSpace(snapshot.Error) &&
               snapshot.LobbyId == expectedLobbyId &&
               IdentityEvidenceGenerationPolicy
                   .TryBuildFingerprint(
                       snapshot.LobbyId,
                       snapshot.LobbyOwnerSteamId,
                       snapshot.NativeSteamIds,
                       snapshot.NativePurrNetIds,
                       snapshot.TransformInstanceIds,
                       snapshot.LobbyMemberSteamIds,
                       snapshot.LocalSteamId,
                       snapshot.PublishedHelperSteamIds,
                       snapshot.CoverageComplete,
                       snapshot.LiveSteamCheckCount,
                       snapshot.LeftStaleCount,
                       snapshot.AmbiguousCount,
                       out fingerprint);
    }

    private static bool TryReadCurrentLobbyMembers(out CSteamID lobby, out List<string> members, out string error)
    {
        lobby = CSteamID.Nil;
        members = new List<string>();
        error = "the current Steam lobby is unavailable";
        try
        {
            if (!SteamManager.Initialized || !Plugin.TryGetRescueLobby(out lobby) || lobby == CSteamID.Nil) return false;
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (int index = 0; index < count; index++) members.Add(((ulong)SteamMatchmaking.GetLobbyMemberByIndex(lobby, index)).ToString());
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = "Steam lobby verification failed safely (" + ex.GetType().Name + ")";
            return false;
        }
    }

    private static bool TryReadCurrentPurrNetPlayers(
        out List<string> players,
        out string error)
    {
        players = new List<string>();
        error = "the current PurrNet connected-player set is unavailable";
        try
        {
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
                players.Add(
                    ((ulong)playerId.id).ToString(
                        CultureInfo.InvariantCulture));
            }
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error =
                "PurrNet connected-player verification failed safely (" +
                ex.GetType().Name +
                ")";
            return false;
        }
    }

    private static SnapshotRoleContext BuildRoleContext(CSteamID lobby, IReadOnlyCollection<string> lobbyMembers)
    {
        var context = new SnapshotRoleContext();
        try
        {
            context.LocalSteamId =
                ((ulong)SteamUser.GetSteamID()).ToString(
                    CultureInfo.InvariantCulture);
        }
        catch { }
        context.OwnerSteamId =
            ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString(
                CultureInfo.InvariantCulture);
        var currentMembers = new HashSet<string>(
            lobbyMembers ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        HashSet<string> publishedHelpers =
            SteamIdModerationController.GetPublishedHelpers(
                lobby,
                context.OwnerSteamId);
        context.HelperSteamIds = CurrentLobbyHelperPolicy.FilterCurrent(
            publishedHelpers,
            currentMembers);
        var helpers = new HashSet<string>(
            context.HelperSteamIds,
            StringComparer.Ordinal);
        if (!Plugin.CanUseLobbySafety(out _))
        {
            return context;
        }
        var roles = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (string steamId in currentMembers)
        {
            if (string.Equals(context.LocalSteamId, steamId, StringComparison.Ordinal)) AddRole(roles, steamId, "SELF");
            if (string.Equals(context.OwnerSteamId, steamId, StringComparison.Ordinal)) AddRole(roles, steamId, "HOST");
            if (helpers.Contains(steamId)) AddRole(roles, steamId, "HELPER");
        }

        foreach (CloneIncident incident in CloneIncidentLedger.Snapshot)
        {
            if (currentMembers.Contains(incident.VictimSteamId)) AddRole(roles, incident.VictimSteamId, "VICTIM " + incident.Id + " (PROTECTED)");
            if (currentMembers.Contains(incident.SuspectSteamId)) AddRole(roles, incident.SuspectSteamId, "SUSPECT " + incident.Id);
        }

        foreach (KeyValuePair<string, List<string>> pair in roles)
            context.RoleBySteamId[pair.Key] = string.Join(" • ", pair.Value.Distinct(StringComparer.Ordinal));
        return context;
    }

    private static void AddRole(Dictionary<string, List<string>> roles, string steamId, string role)
    {
        if (!roles.TryGetValue(steamId, out List<string> values))
        {
            values = new List<string>();
            roles[steamId] = values;
        }
        values.Add(role);
    }

    private static RosterSnapshot CaptureRosterSnapshot(bool cacheAllowed)
    {
        long now = DateTime.UtcNow.Ticks;
        RosterSnapshot cached = _cachedSnapshot;
        if (cacheAllowed && cached != null)
        {
            // Unity IMGUI can run Layout and Repaint in the same frame. Never let a
            // join/leave change the render model between those passes; a dirty
            // snapshot refreshes on the following frame instead.
            if (cached.CapturedFrame == Time.frameCount || (!_snapshotDirty && now - cached.CapturedUtcTicks <= SnapshotLifetimeTicks))
                return cached;
        }

        var snapshot = new RosterSnapshot
        {
            CapturedUtcTicks = now,
            CaptureStartedTimestamp = Stopwatch.GetTimestamp(),
            CapturedFrame = Time.frameCount,
        };
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel == null || panel.PlayerSteamIDs == null || panel.PlayerIDs == null || panel.PlayerTransforms == null || panel.IDInfos == null)
        {
            snapshot.Error = "the native player roster is not ready";
            return Cache(snapshot);
        }

        int count = panel.PlayerSteamIDs.Count;
        snapshot.NativeRowCount = count;
        if (panel.PlayerIDs.Count != count || panel.PlayerTransforms.Count != count || panel.IDInfos.Count != count)
        {
            snapshot.Error = "the native Steam, network-player, transform, and identity lists are not aligned";
            return Cache(snapshot);
        }

        snapshot.NativeSteamIds = panel.PlayerSteamIDs.ToArray();
        snapshot.NativePlayerIds = panel.PlayerIDs
            .Select(id => PurrNetPlayerIdPolicy.Format((ulong)id.id))
            .ToArray();
        snapshot.NativePurrNetIds =
            panel.PlayerIDs.Select(id =>
                    ((ulong)id.id).ToString(
                        CultureInfo.InvariantCulture))
                .ToArray();
        snapshot.TransformInstanceIds =
            panel.PlayerTransforms.Select(transform =>
                    transform == null
                        ? 0
                        : transform.GetInstanceID())
                .ToArray();
        snapshot.HasLiveTransform =
            snapshot.TransformInstanceIds.Select(
                    instanceId => instanceId != 0)
                .ToArray();

        if (!TryReadCurrentPurrNetPlayers(
                out List<string> purrNetPlayers,
                out string purrNetError))
        {
            snapshot.Error = purrNetError;
            return Cache(snapshot);
        }
        snapshot.PurrNetActivePlayerIds =
            purrNetPlayers.ToArray();
        snapshot.PurrNetRowCount = purrNetPlayers.Count;
        if (purrNetPlayers.Any(value =>
                !ulong.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong parsed) ||
                parsed == 0UL) ||
            purrNetPlayers.Distinct(
                StringComparer.Ordinal).Count() !=
            purrNetPlayers.Count)
        {
            snapshot.Error =
                "the current PurrNet connected-player set is invalid or duplicated";
            return Cache(snapshot);
        }

        if (!TryReadCurrentLobbyMembers(out CSteamID lobby, out List<string> lobbyMembers, out string lobbyError))
        {
            snapshot.Error = lobbyError;
            return Cache(snapshot);
        }
        snapshot.LobbyId = (ulong)lobby;
        snapshot.LobbyMemberSteamIds = lobbyMembers.ToArray();
        snapshot.LobbyMemberCount = lobbyMembers.Count;

        if (lobbyMembers.Any(id => !ModerationProtocolPolicy.IsSteamId64(id)) ||
            lobbyMembers.Distinct(StringComparer.Ordinal).Count() != lobbyMembers.Count)
        {
            snapshot.Error = "the current Steam lobby member list is invalid or duplicated";
            return Cache(snapshot);
        }

        string[] steamIds = snapshot.NativeSteamIds;
        string[] playerIds = snapshot.NativePurrNetIds;
        var steamCounts = CountValues(steamIds);
        var playerCounts = CountValues(playerIds);
        var lobbySet = new HashSet<string>(lobbyMembers, StringComparer.Ordinal);
        var purrNetSet = new HashSet<string>(
            purrNetPlayers,
            StringComparer.Ordinal);
        snapshot.ActiveNativeRowCount = playerIds.Count(
            purrNetSet.Contains);
        snapshot.AmbiguousCount += purrNetPlayers.Count(
            activePlayerId => !playerCounts.ContainsKey(
                activePlayerId));
        SnapshotRoleContext roleContext = BuildRoleContext(lobby, lobbyMembers);
        if (!ulong.TryParse(
                roleContext.LocalSteamId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out snapshot.LocalSteamId) ||
            snapshot.LocalSteamId == 0UL ||
            !ulong.TryParse(
                roleContext.OwnerSteamId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out snapshot.LobbyOwnerSteamId) ||
            snapshot.LobbyOwnerSteamId == 0UL ||
            !lobbySet.Contains(roleContext.LocalSteamId) ||
            !lobbySet.Contains(roleContext.OwnerSteamId))
        {
            snapshot.Error =
                "the current local or lobby-owner Steam identity is unavailable";
            return Cache(snapshot);
        }
        snapshot.PublishedHelperSteamIds =
            roleContext.HelperSteamIds;

        snapshot.LobbyMemberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobby);
        bool transportTruth = SteamIdTruthReader.IsTransportTruthAvailable();
        Dictionary<string, SteamIdTruthAttestationEntry> hostAttestation =
            transportTruth ? null : ReadHelperAttestation(lobby);
        NetworkTransform localTransform =
            NetworkSingleton<TextChannelManager>.I?.MainNetTransform;
        PlayerID localPlayerId = NetworkManager.main != null ? NetworkManager.main.localPlayer : default;
        bool localPlayerKnown = (ulong)localPlayerId.id != 0UL;
        // The roster SteamID is the joining client's own claim. "Self" needs the claim AND our own
        // network object; transport SIDs are read up front so a systemic failure is detectable.
        var localRows = new bool[steamIds.Length];
        var transportIds = new ulong[steamIds.Length];
        int remoteRows = 0;
        int transportVouched = 0;
        for (int index = 0; index < steamIds.Length; index++)
        {
            localRows[index] =
                string.Equals(roleContext.LocalSteamId, steamIds[index], StringComparison.Ordinal) &&
                ((localTransform != null && ReferenceEquals(panel.PlayerTransforms[index], localTransform)) ||
                 (localPlayerKnown && panel.PlayerIDs[index].Equals(localPlayerId)));
            if (!transportTruth || localRows[index]) continue;
            remoteRows++;
            transportIds[index] = SteamIdTruthReader.ReadTransportSteamId(panel.PlayerIDs[index]);
            if (transportIds[index] != 0UL) transportVouched++;
        }

        if (transportTruth && remoteRows > 0 && transportVouched == 0)
        {
            // Every remote read failed: the transport map is unusable, not every player a spoofer.
            // Fall back to Steam-lobby evidence rather than lock the host out of its own lobby.
            transportTruth = false;
            snapshot.TransportUnavailable = true;
        }

        var truthRows = new List<SteamIdTruthRow>();
        var confirmed = new List<PlayerIdentityEvidence>();
        var result = new List<PlayerIdentityEvidence>();
        for (int index = 0; index < steamIds.Length; index++)
        {
            string steamId = steamIds[index];
                string playerId = playerIds[index];
            bool validSteamId = ModerationProtocolPolicy.IsSteamId64(steamId);
            int steamMatches = steamCounts.TryGetValue(steamId ?? string.Empty, out int countedSteamMatches) ? countedSteamMatches : 0;
            int playerMatches = playerCounts.TryGetValue(playerId ?? string.Empty, out int countedPlayerMatches) ? countedPlayerMatches : 0;
            bool uniqueSteamId = validSteamId && steamMatches == 1;
            bool uniquePlayerId =
                ulong.TryParse(
                    playerId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong exactPurrNetId) &&
                exactPurrNetId != 0UL &&
                playerMatches == 1;
            bool liveTransform = snapshot.HasLiveTransform[index];
            bool currentSteamMember = validSteamId && lobbySet.Contains(steamId);
            bool currentPurrNetPlayer =
                uniquePlayerId &&
                purrNetSet.Contains(playerId);
            bool exactLiveRow = uniqueSteamId && uniquePlayerId && liveTransform && currentPurrNetPlayer;
            bool isLocalRow = localRows[index];
            SteamIdTruthState truth = hostAttestation != null && !isLocalRow
                ? SteamIdTruthPolicy.ClassifyFromHostAttestation(
                    steamId,
                    playerId,
                    hostAttestation,
                    currentSteamMember)
                : SteamIdTruthPolicy.Classify(
                    steamId,
                    isLocalRow,
                    transportTruth,
                    transportIds[index],
                    currentSteamMember);
            if (truth == SteamIdTruthState.TransportVerified) snapshot.TransportVerifiedCount++;
            else if (truth == SteamIdTruthState.HostAttestedVerified) snapshot.HostAttestedCount++;
            else if (SteamIdTruthPolicy.IsSpoofed(truth)) snapshot.SteamIdMismatchCount++;
            if (exactLiveRow)
            {
                truthRows.Add(new SteamIdTruthRow(playerId, steamId, truth));
            }

            bool confirmedRow = exactLiveRow && SteamIdTruthPolicy.IsTrusted(truth);
            bool verifiedRow = confirmedRow && currentSteamMember;
            if (!verifiedRow)
            {
                PlayerIdentityEvidencePolicy.UnresolvedRowState rowState = PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                    steamId,
                    snapshot.LobbyMemberSteamIds,
                    uniqueSteamId ? 1 : steamMatches,
                    uniquePlayerId,
                    liveTransform,
                    currentPurrNetPlayer);
                if (rowState == PlayerIdentityEvidencePolicy.UnresolvedRowState.LiveSteamCheck) snapshot.LiveSteamCheckCount++;
                else if (rowState == PlayerIdentityEvidencePolicy.UnresolvedRowState.LeftStale) snapshot.LeftStaleCount++;
                else snapshot.AmbiguousCount++;
                if (!confirmedRow)
                {
                    continue;
                }
            }

            string raw = ReadRosterNameRaw(panel, index);
            bool isLocal = string.Equals(roleContext.LocalSteamId, steamId, StringComparison.Ordinal);
            string displayName = NormalizeName(raw);
            var rowEvidence = new PlayerIdentityEvidence
            {
                SteamId = steamId,
                DisplayNameRaw = raw,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName,
                SteamPersona = ReadSteamPersona(steamId),
                Role = roleContext.RoleBySteamId.TryGetValue(steamId, out string role) ? role : "PLAYER",
                RosterIndex = index,
                NetworkPlayerId = playerId,
                PurrNetId = exactPurrNetId,
                TransformInstanceId =
                    snapshot.TransformInstanceIds[index],
                IsLocal = isLocal,
            };
            confirmed.Add(rowEvidence);
            if (verifiedRow)
            {
                result.Add(rowEvidence);
            }
        }

        if (panel.PlayerSteamIDs.Count != steamIds.Length ||
            !panel.PlayerSteamIDs.SequenceEqual(steamIds) ||
            panel.PlayerIDs.Count != playerIds.Length ||
            !panel.PlayerIDs.Select(id =>
                    ((ulong)id.id).ToString(
                        CultureInfo.InvariantCulture))
                .SequenceEqual(playerIds) ||
            panel.PlayerTransforms.Count !=
                snapshot.TransformInstanceIds.Length ||
            !panel.PlayerTransforms.Select(transform =>
                    transform == null
                        ? 0
                        : transform.GetInstanceID())
                .SequenceEqual(snapshot.TransformInstanceIds))
        {
            snapshot.Error =
                "the native Steam/PurrNet/transform roster changed while BlueSage was reading it; refresh and try again";
            snapshot.Verified = Array.Empty<PlayerIdentityEvidence>();
            return Cache(snapshot);
        }

        if (!TryReadCurrentLobbyMembers(
                out CSteamID finalLobby,
                out List<string> finalLobbyMembers,
                out _) ||
            finalLobby != lobby ||
            !finalLobbyMembers.OrderBy(
                    value => value,
                    StringComparer.Ordinal)
                .SequenceEqual(
                    lobbyMembers.OrderBy(
                        value => value,
                        StringComparer.Ordinal)))
        {
            snapshot.Error =
                "the Steam lobby membership changed while BlueSage was reading it; refresh and try again";
            snapshot.Verified =
                Array.Empty<PlayerIdentityEvidence>();
            return Cache(snapshot);
        }

        if (!TryReadCurrentPurrNetPlayers(
                out List<string> finalPurrNetPlayers,
                out _) ||
            !finalPurrNetPlayers.OrderBy(
                    value => value,
                    StringComparer.Ordinal)
                .SequenceEqual(
                    purrNetPlayers.OrderBy(
                        value => value,
                        StringComparer.Ordinal)))
        {
            snapshot.Error =
                "the PurrNet connected-player set changed while BlueSage was reading it; refresh and try again";
            snapshot.Verified =
                Array.Empty<PlayerIdentityEvidence>();
            return Cache(snapshot);
        }

        SnapshotRoleContext finalRoleContext =
            BuildRoleContext(finalLobby, finalLobbyMembers);
        if (!string.Equals(
                finalRoleContext.LocalSteamId,
                roleContext.LocalSteamId,
                StringComparison.Ordinal) ||
            !string.Equals(
                finalRoleContext.OwnerSteamId,
                roleContext.OwnerSteamId,
                StringComparison.Ordinal) ||
            !finalRoleContext.HelperSteamIds.SequenceEqual(
                roleContext.HelperSteamIds))
        {
            snapshot.Error =
                "the local/owner/helper authority context changed while BlueSage was reading it; refresh and try again";
            snapshot.Verified =
                Array.Empty<PlayerIdentityEvidence>();
            return Cache(snapshot);
        }

        var verifiedIds = new HashSet<string>(result.Select(item => item.SteamId), StringComparer.Ordinal);
        string[] missingCurrentMembers = lobbyMembers.Where(member => !verifiedIds.Contains(member)).ToArray();
        snapshot.CoverageComplete = PlayerIdentityEvidencePolicy.IsCoverageComplete(
            captureReady: string.IsNullOrWhiteSpace(snapshot.Error),
            missingCurrentSteamMembers: missingCurrentMembers.Length,
            liveNativeOnlyRows: snapshot.LiveSteamCheckCount,
            ambiguousRows: snapshot.AmbiguousCount);
        snapshot.CoverageIssue = BuildCoverageIssue(snapshot, missingCurrentMembers.Length);

        snapshot.Verified = result.OrderBy(item => item.RosterIndex).ToArray();
        snapshot.IdentityConfirmed = confirmed.OrderBy(item => item.RosterIndex).ToArray();
        snapshot.TruthRows = truthRows.ToArray();
        // Raised here, shown from Update: posting chat from inside a roster capture could re-enter it.
        // A new lobby starts from zero so its first spoofer always alerts.
        if (snapshot.LobbyId != _lastSteamIdMismatchLobbyId)
        {
            _lastSteamIdMismatchLobbyId = snapshot.LobbyId;
            _lastSteamIdMismatchCount = 0;
        }

        if (SteamIdTruthAlertPolicy.ShouldAlert(_lastSteamIdMismatchCount, snapshot.SteamIdMismatchCount))
        {
            _steamIdMismatchAlertPending = true;
        }
        _lastSteamIdMismatchCount = snapshot.SteamIdMismatchCount;
        return Cache(snapshot);
    }

    private static RosterSnapshot Cache(RosterSnapshot snapshot)
    {
        double elapsedMilliseconds = (Stopwatch.GetTimestamp() - snapshot.CaptureStartedTimestamp) * 1000d / Stopwatch.Frequency;
        _snapshotCaptureCount++;
        _lastSnapshotCaptureMilliseconds = elapsedMilliseconds;
        if (elapsedMilliseconds > _maxSnapshotCaptureMilliseconds) _maxSnapshotCaptureMilliseconds = elapsedMilliseconds;
        _snapshotDirty = false;
        _cachedSnapshot = snapshot;
        LogCoverageTransition(snapshot);
        return snapshot;
    }

    private static string BuildCoverageIssue(RosterSnapshot snapshot, int missingCurrentMembers)
    {
        var parts = new List<string>();
        if (missingCurrentMembers > 0)
        {
            parts.Add(missingCurrentMembers + " current Steam " +
                      (missingCurrentMembers == 1 ? "member is" : "members are") +
                      " still waiting for one unique live player row");
        }
        if (snapshot.LiveSteamCheckCount > 0)
        {
            parts.Add(snapshot.LiveSteamCheckCount + " live game/PurrNet " +
                      (snapshot.LiveSteamCheckCount == 1 ? "row is" : "rows are") +
                      " outside current Steam lobby membership. Profile access remains available; identity-sensitive actions stay locked");
        }
        if (snapshot.AmbiguousCount > 0)
        {
            parts.Add(snapshot.AmbiguousCount + " live native " +
                      (snapshot.AmbiguousCount == 1 ? "row is" : "rows are") +
                      " ambiguous and locked");
        }
        return string.Join("; ", parts);
    }

    private static void LogCoverageTransition(RosterSnapshot snapshot)
    {
        // Round 4 Stage D, log diet #6: the log key carries the safety state only (coverage complete, capture error,
        // transport unavailable, any Steam-ID mismatch). The counts change on every join/leave wave and used to turn
        // each refresh into a warning; they now key the journal status row instead, so they are still recorded.
        string logKey = IdentityCoverageLogPolicy.BuildStateKey(
            snapshot.LobbyId,
            snapshot.CoverageComplete,
            snapshot.Error,
            snapshot.Verified.Length,
            snapshot.NativeRowCount,
            snapshot.ActiveNativeRowCount,
            snapshot.PurrNetRowCount,
            snapshot.LobbyMemberCount,
            snapshot.LiveSteamCheckCount,
            snapshot.LeftStaleCount,
            snapshot.AmbiguousCount) +
            "|" + (snapshot.SteamIdMismatchCount > 0) +
            "|" + snapshot.TransportUnavailable;
        string journalKey = logKey +
            "|" + snapshot.IdentityConfirmed.Length +
            "|" + snapshot.TransportVerifiedCount +
            "|" + snapshot.HostAttestedCount +
            "|" + snapshot.LobbyMemberCount +
            "|" + snapshot.LobbyMemberLimit +
            "|" + snapshot.ActiveNativeRowCount;
        if (!string.Equals(_lastCoverageJournalKey, journalKey, StringComparison.Ordinal))
        {
            _lastCoverageJournalKey = journalKey;
            // The WS3 transport line is a journal status row (hosts only; the controller gates and de-duplicates).
            HostJournalController.RecordCoverage(snapshot.LobbyId != 0UL, snapshot.LobbyMemberCount, snapshot.LobbyMemberLimit, snapshot.ActiveNativeRowCount, snapshot.TransportVerifiedCount);
        }

        // Round 5 F: the safety state flipped 216 times in Blue's 37.5 h host because a public lobby has someone
        // loading in or dropping out most of the time. A flip is logged only once it has held for a minute, and a
        // persisting fail-closed state reminds once an hour (StableStateLogPolicy, shared with Clone Shield).
        float coverageNow = Time.unscaledTime;
        StableStateLogDecision coverageDecision = StableStateLogPolicy.Observe(
            ref _coverageLogState,
            logKey,
            remindWhileHeld: !snapshot.CoverageComplete,
            coverageNow);
        if (coverageDecision == StableStateLogDecision.None) return;
        string held = coverageDecision == StableStateLogDecision.Reminder
            ? "; held=" + (int)(_coverageLogState.AnnouncedAgeSeconds(coverageNow) / 60f) + "min"
            : string.Empty;

        string message = "[BlueSageIdentityCoverage] complete=" + snapshot.CoverageComplete +
                         "; verified=" + snapshot.Verified.Length +
                         "; native=" + snapshot.NativeRowCount +
                         "; activeNative=" + snapshot.ActiveNativeRowCount +
                         "; purrNetActive=" + snapshot.PurrNetRowCount +
                         "; steam=" + snapshot.LobbyMemberCount +
                         "; liveNativeOnly=" + snapshot.LiveSteamCheckCount +
                         "; leftStale=" + snapshot.LeftStaleCount +
                         "; ambiguous=" + snapshot.AmbiguousCount +
                         "; confirmed=" + snapshot.IdentityConfirmed.Length +
                         "; transportVerified=" + snapshot.TransportVerifiedCount +
                         "; hostAttested=" + snapshot.HostAttestedCount +
                         "; steamIdMismatch=" + snapshot.SteamIdMismatchCount +
                         "; lobbyLimit=" + snapshot.LobbyMemberLimit +
                         "; transport=" + (snapshot.TransportUnavailable ? "unavailable" : "ok") +
                         "; identitySensitiveActions=" + (snapshot.CoverageComplete ? "ready" : "fail-closed") + held + ".";
        if (snapshot.CoverageComplete) Plugin.Log?.LogInfo(message);
        else Plugin.Log?.LogWarning(message);
    }

    private static Dictionary<string, int> CountValues(IEnumerable<string> values)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            string key = value ?? string.Empty;
            counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
        }
        return counts;
    }

    private static string DescribeUnresolvedExactRow(string steamId, RosterSnapshot snapshot)
    {
        int nativeMatches = snapshot.NativeSteamIds.Count(candidate => string.Equals(candidate, steamId, StringComparison.Ordinal));
        bool uniquePlayerId = false;
        bool hasLiveTransform = false;
        bool isPurrNetConnected = false;
        if (nativeMatches == 1)
        {
            int index = Array.FindIndex(snapshot.NativeSteamIds, candidate => string.Equals(candidate, steamId, StringComparison.Ordinal));
            if (index >= 0 &&
                index < snapshot.NativePurrNetIds.Length)
            {
                string playerId =
                    snapshot.NativePurrNetIds[index];
                uniquePlayerId = !string.IsNullOrWhiteSpace(playerId) &&
                                 snapshot.NativePurrNetIds.Count(candidate =>
                                     string.Equals(
                                         candidate,
                                         playerId,
                                         StringComparison.Ordinal)) == 1;
                hasLiveTransform = index < snapshot.HasLiveTransform.Length && snapshot.HasLiveTransform[index];
                isPurrNetConnected =
                    snapshot.PurrNetActivePlayerIds.Contains(
                        playerId,
                        StringComparer.Ordinal);
            }
        }

        return PlayerIdentityEvidencePolicy.DescribeUnresolvedExactRow(
            steamId,
            snapshot.LobbyMemberSteamIds,
            snapshot.Error,
            nativeMatches,
            uniquePlayerId,
            hasLiveTransform,
            isPurrNetConnected);
    }

    private static string ReadRosterNameRaw(PlayerPanelController panel, int index)
    {
        try
        {
            IList idInfos = panel.IDInfos;
            if (idInfos == null || index < 0 || index >= idInfos.Count) return "Player";
            object value = AccessTools.Field(idInfos[index].GetType(), "Name")?.GetValue(idInfos[index]);
            string text = value is byte[] bytes ? Encoding.Unicode.GetString(bytes).TrimEnd('\0') : value as string ?? string.Empty;
            return string.IsNullOrWhiteSpace(text) ? "Player" : Truncate(text, 4096);
        }
        catch
        {
            return "Player";
        }
    }

    private static string ReadSteamPersona(string steamId)
    {
        try
        {
            if (!ulong.TryParse(steamId, out ulong parsed)) return string.Empty;
            return Truncate(NormalizeName(SteamFriends.GetFriendPersonaName(new CSteamID(parsed)) ?? string.Empty), 96);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeName(string value)
    {
        string clean = Regex.Replace(value ?? string.Empty, "<.*?>", string.Empty);
        return clean.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string Truncate(string value, int maximum)
    {
        return value.Length <= maximum ? value : value.Substring(0, maximum - 1) + "…";
    }
}
