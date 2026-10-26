using System;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;

namespace BlueSage.QoLTweaks;

internal sealed class NativeIdCardTarget
{
    internal PlayerPanelController Panel;
    internal PlayerItemController Row;
    internal NetworkTransform NetworkTransform;
    internal PlayerController PlayerController;
    internal PlayerIDInfo PlayerInfo;
    internal string SteamId = string.Empty;
    internal string PlayerId = string.Empty;
    internal string DisplayNameRaw = string.Empty;
    internal int PlayerIndex = -1;
}

internal static class NativeIdCardController
{
    private const string NativeIdCardUnconfirmedToast =
        "ID card not opened: BlueSage can't confirm this player's Steam ID yet. Try again in a moment, or ask the host or a Helper.";

    private const float UnconfirmedChatGapSeconds = 30f;
    private static float _lastUnconfirmedChatAt = -UnconfirmedChatGapSeconds;

    private static readonly FieldInfo PlayerSteamIdField =
        AccessTools.Field(typeof(PlayerItemController), "_playerSteamId");

    internal static bool TryOpen(
        string source,
        string requestedSteamId,
        int observedIndex,
        string expectedPlayerId,
        PlayerItemController expectedRow,
        NetworkTransform expectedTransform,
        bool requireObservedIndex,
        NativeIdCardCallerKind callerKind = NativeIdCardCallerKind.OrdinaryUser)
    {
        if (!TryResolveCurrentRow(
                source,
                requestedSteamId,
                observedIndex,
                expectedPlayerId,
                expectedRow,
                expectedTransform,
                requireObservedIndex,
                callerKind,
                out NativeIdCardTarget target,
                out NativeIdCardResolutionDecision decision,
                allowReadOnlyNativeFallback: true))
        {
            if (decision?.State == NativeIdCardResolutionState.FreshEvidenceUnavailable)
            {
                // Round 5h: chat twin too, so the message is not lost when Toast Pop-ups is off, but at most one chat line
                // every 30 seconds (the lobby is often mid-match while a click is refused); the toast dedupes itself.
                float now = UnityEngine.Time.unscaledTime;
                if (now - _lastUnconfirmedChatAt >= UnconfirmedChatGapSeconds)
                {
                    _lastUnconfirmedChatAt = now;
                    Plugin.AddLocalNotificationWithToast(NativeIdCardUnconfirmedToast);
                }
                else
                {
                    Plugin.Instance?.ShowToast(NativeIdCardUnconfirmedToast);
                }
            }
            return false;
        }

        TemporaryPhoto temporaryPhoto = MonoSingleton<TemporaryPhoto>.I;
        if (temporaryPhoto == null)
        {
            LogOutcome(
                source,
                NativeIdCardResolutionState.FreshEvidenceUnavailable,
                observedIndex,
                requestedSteamId,
                "the native ID-card controller is unavailable");
            return false;
        }

        PlayerPanelController currentPanel = NetworkSingleton<PlayerPanelController>.I;
        int playerIndex = target.PlayerIndex;
        NativeIdCardInvocationState invocationState =
            NativeIdCardInvocationGuard.Invoke(
                () => IsFinalTupleStable(target),
                () => currentPanel?.ButtonClosePanel(),
                () => TryOpenExactPlayerCard(temporaryPhoto, target));
        if (invocationState != NativeIdCardInvocationState.Opened)
        {
            LogOutcome(
                source,
                NativeIdCardResolutionState.FinalTupleChanged,
                observedIndex,
                requestedSteamId,
                invocationState ==
                NativeIdCardInvocationState.TupleChangedAfterPanelClose
                    ? "the exact native identity tuple changed during panel close"
                    : "the exact native identity tuple changed before panel close");
            return false;
        }

        LogOutcome(
            source,
            NativeIdCardResolutionState.Ready,
            playerIndex,
            requestedSteamId,
            "native exact-controller ID card opened");
        return true;
    }

    internal static bool TryResolveCurrentRow(
        string source,
        string requestedSteamId,
        int observedIndex,
        string expectedPlayerId,
        PlayerItemController expectedRow,
        NetworkTransform expectedTransform,
        bool requireObservedIndex,
        NativeIdCardCallerKind callerKind,
        out NativeIdCardTarget target,
        out NativeIdCardResolutionDecision decision,
        bool allowReadOnlyNativeFallback = false)
    {
        if (TryResolveCurrentRowOnce(
                source,
                requestedSteamId,
                observedIndex,
                expectedPlayerId,
                expectedRow,
                expectedTransform,
                requireObservedIndex,
                callerKind,
                out target,
                out decision,
                allowReadOnlyNativeFallback) ||
            decision.State != NativeIdCardResolutionState.NativeRowStaleRefreshable)
        {
            return target != null;
        }

        // Vanilla re-stamps pooled player rows only in UpdateServerPanel(), which it skips while the
        // player list is closed. Ask vanilla to re-stamp once, then repeat the full exact check once.
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        try
        {
            panel.UpdateServerPanel();
        }
        catch (Exception ex)
        {
            LogOutcome(
                source,
                NativeIdCardResolutionState.RowSteamIdentityChanged,
                observedIndex,
                requestedSteamId,
                "the native player-row refresh failed: " + ex.GetType().Name);
            return false;
        }

        return TryResolveCurrentRowOnce(
            source,
            requestedSteamId,
            observedIndex,
            expectedPlayerId,
            expectedRow,
            expectedTransform,
            requireObservedIndex,
            callerKind,
            out target,
            out decision,
            allowReadOnlyNativeFallback);
    }

    private static bool TryResolveCurrentRowOnce(
        string source,
        string requestedSteamId,
        int observedIndex,
        string expectedPlayerId,
        PlayerItemController expectedRow,
        NetworkTransform expectedTransform,
        bool requireObservedIndex,
        NativeIdCardCallerKind callerKind,
        out NativeIdCardTarget target,
        out NativeIdCardResolutionDecision decision,
        bool allowReadOnlyNativeFallback)
    {
        target = null;
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        bool freshResolutionSucceeded =
            PlayerIdentityEvidenceController.TryResolveConfirmed(
                requestedSteamId,
                out PlayerIdentityEvidence evidence,
                out string freshError);

        bool nativeListsAligned = AreNativeListsAligned(panel);
        int currentRowIndex = panel?.PlayerItemControllers?.IndexOf(expectedRow) ?? -1;
        int effectiveExpectedIndex = requireObservedIndex
            ? observedIndex
            : currentRowIndex;

        PlayerItemController currentRow =
            nativeListsAligned &&
            currentRowIndex >= 0 &&
            currentRowIndex < panel.PlayerItemControllers.Count
                ? panel.PlayerItemControllers[currentRowIndex]
                : null;
        string currentRowSteamId =
            currentRow != null
                ? PlayerSteamIdField?.GetValue(currentRow) as string ?? string.Empty
                : string.Empty;
        string currentRowPlayerId =
            nativeListsAligned &&
            currentRowIndex >= 0 &&
            currentRowIndex < panel.PlayerIDs.Count
                ? PurrNetPlayerIdPolicy.Format(
                    (ulong)panel.PlayerIDs[currentRowIndex].id)
                : string.Empty;
        string effectiveExpectedPlayerId =
            requireObservedIndex
                ? expectedPlayerId ?? string.Empty
                : currentRowPlayerId;

        // Round 4 WS3: view-only callers (opening the ID card / Steam profile) may use the game's own row when our
        // verified evidence is unavailable, e.g. a player who rejoined via a reconnect mod. Every wrong-row guard below
        // still runs. Evidence copies and moderation never pass allowReadOnlyNativeFallback. The card opened this way
        // still carries the game's own Ban button; for the host that click is routed by NativeBanRoutePatch (Stage C P1).
        string gameListSteamIdAtRow =
            currentRow != null && currentRowIndex < panel.PlayerSteamIDs.Count
                ? panel.PlayerSteamIDs[currentRowIndex] ?? string.Empty
                : string.Empty;
        int nativeIndex = -1;
        bool nativeFallback =
            allowReadOnlyNativeFallback &&
            NativeIdCardReadOnlyFallbackPolicy.TryUseNativeRow(
                freshResolutionSucceeded,
                requestedSteamId,
                currentRow != null ? currentRowIndex : -1,
                gameListSteamIdAtRow,
                currentRowSteamId,
                out nativeIndex);
        bool identityReady = freshResolutionSucceeded || nativeFallback;
        string resolvedSteamId = freshResolutionSucceeded ? evidence.SteamId : nativeFallback ? requestedSteamId ?? string.Empty : string.Empty;
        string resolvedPlayerId = freshResolutionSucceeded ? evidence.NetworkPlayerId : nativeFallback ? currentRowPlayerId : string.Empty;
        int resolvedIndex = freshResolutionSucceeded ? evidence.RosterIndex : nativeFallback ? nativeIndex : -1;
        NetworkTransform currentTransform =
            nativeListsAligned &&
            resolvedIndex >= 0 &&
            resolvedIndex < panel.PlayerTransforms.Count
                ? panel.PlayerTransforms[resolvedIndex]
                : null;
        PlayerController currentPlayerController =
            nativeListsAligned &&
            resolvedIndex >= 0 &&
            resolvedIndex < panel.PlayerControllers.Count
                ? panel.PlayerControllers[resolvedIndex]
                : null;
        NetworkTransform currentControllerTransform =
            currentPlayerController != null
                ? currentPlayerController.GetComponent<NetworkTransform>()
                : null;
        PlayerIDInfo currentPlayerInfo =
            nativeListsAligned &&
            resolvedIndex >= 0 &&
            resolvedIndex < panel.IDInfos.Count
                ? panel.IDInfos[resolvedIndex]
                : default;

        var context = new NativeIdCardResolutionContext
        {
            CallerKind = callerKind,
            RequestedSteamId = requestedSteamId ?? string.Empty,
            FreshResolutionSucceeded = identityReady,
            FreshResolvedSteamId = resolvedSteamId,
            FreshResolvedPlayerId = resolvedPlayerId,
            ExpectedPlayerId = effectiveExpectedPlayerId,
            FreshResolvedIndex = resolvedIndex,
            ExpectedIndex = effectiveExpectedIndex,
            CurrentRowIndex = currentRowIndex,
            CurrentRowReferenceMatches =
                currentRow != null &&
                expectedRow != null &&
                ReferenceEquals(currentRow, expectedRow),
            CurrentRowSteamId = currentRowSteamId,
            CurrentRowListSteamId =
                currentRow != null
                    ? panel.PlayerSteamIDs[currentRowIndex] ?? string.Empty
                    : string.Empty,
            CurrentRowPlayerId = currentRowPlayerId,
            HasLiveNetworkTransform = currentTransform != null,
            NativeListsAligned = nativeListsAligned,
            ExpectedNetworkTransformMatches =
                !requireObservedIndex ||
                (expectedTransform != null &&
                 ReferenceEquals(currentTransform, expectedTransform)),
            FinalTupleStable = identityReady &&
                               currentPlayerController != null &&
                               IsCurrentTupleStable(
                                   panel,
                                   resolvedIndex,
                                   requestedSteamId,
                                   resolvedPlayerId,
                                   expectedRow,
                                   currentTransform,
                                   currentPlayerController,
                                   currentPlayerInfo) &&
                               currentControllerTransform != null &&
                               ReferenceEquals(currentControllerTransform, currentTransform),
        };

        decision = NativeIdCardResolutionPolicy.Evaluate(context);
        if (!decision.MayOpenNativeCard)
        {
            string detail = string.IsNullOrWhiteSpace(decision.Reason)
                ? freshError
                : decision.Reason;
            // A stale pooled row is retried by the caller; only the retry's outcome is worth a line.
            if (decision.State != NativeIdCardResolutionState.NativeRowStaleRefreshable)
            {
                LogOutcome(source, decision.State, observedIndex, requestedSteamId, detail);
            }
            return false;
        }

        target = new NativeIdCardTarget
        {
            Panel = panel,
            Row = currentRow,
            NetworkTransform = currentTransform,
            PlayerController = currentPlayerController,
            PlayerInfo = SnapshotPlayerIdInfo(currentPlayerInfo),
            SteamId = resolvedSteamId,
            PlayerId = resolvedPlayerId,
            DisplayNameRaw = freshResolutionSucceeded
                ? evidence.DisplayNameRaw
                : System.Text.Encoding.Unicode.GetString(currentPlayerInfo.Name ?? new byte[0]),
            PlayerIndex = decision.PlayerIndex,
        };
        return true;
    }

    private static bool AreNativeListsAligned(PlayerPanelController panel)
    {
        if (panel == null ||
            panel.PlayerSteamIDs == null ||
            panel.PlayerIDs == null ||
            panel.PlayerTransforms == null ||
            panel.PlayerControllers == null ||
            panel.IDInfos == null ||
            panel.PlayerItemControllers == null)
        {
            return false;
        }

        return NativeRosterShapePolicy.HasAlignedActivePrefix(
            panel.PlayerSteamIDs.Count,
            panel.PlayerIDs.Count,
            panel.PlayerTransforms.Count,
            panel.IDInfos.Count,
            panel.PlayerItemControllers.Count) &&
               panel.PlayerControllers.Count >= panel.PlayerSteamIDs.Count;
    }

    private static bool IsCurrentTupleStable(
        PlayerPanelController panel,
        int index,
        string steamId,
        string playerId,
        PlayerItemController row,
        NetworkTransform networkTransform,
        PlayerController playerController,
        PlayerIDInfo playerInfo)
    {
        if (!AreNativeListsAligned(panel) ||
            index < 0 ||
            index >= panel.PlayerSteamIDs.Count)
        {
            return false;
        }

        PlayerController currentPlayerController = panel.PlayerControllers[index];
        NetworkTransform currentControllerTransform =
            currentPlayerController != null
                ? currentPlayerController.GetComponent<NetworkTransform>()
                : null;
        bool controllerTupleStable = playerController != null &&
                                     ReferenceEquals(panel.PlayerControllers[index], playerController) &&
                                     currentControllerTransform != null &&
                                     ReferenceEquals(currentControllerTransform, networkTransform);

        return controllerTupleStable &&
               PlayerIdInfoMatches(panel.IDInfos[index], playerInfo) &&
               string.Equals(
                   panel.PlayerSteamIDs[index],
                   steamId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   PurrNetPlayerIdPolicy.Format(
                       (ulong)panel.PlayerIDs[index].id),
                   playerId,
                   StringComparison.Ordinal) &&
               panel.PlayerTransforms[index] != null &&
               ReferenceEquals(panel.PlayerTransforms[index], networkTransform) &&
               panel.PlayerItemControllers[index] != null &&
               ReferenceEquals(panel.PlayerItemControllers[index], row) &&
               string.Equals(
                   PlayerSteamIdField?.GetValue(panel.PlayerItemControllers[index]) as string,
                   steamId,
                   StringComparison.Ordinal);
    }

    private static bool IsFinalTupleStable(NativeIdCardTarget target)
    {
        if (target == null)
        {
            return false;
        }

        PlayerPanelController currentPanel = NetworkSingleton<PlayerPanelController>.I;
        return currentPanel != null &&
               ReferenceEquals(currentPanel, target.Panel) &&
               IsCurrentTupleStable(
                   currentPanel,
                   target.PlayerIndex,
                   target.SteamId,
                   target.PlayerId,
                   target.Row,
                   target.NetworkTransform,
                   target.PlayerController,
                   target.PlayerInfo);
    }

    private static void TryOpenExactPlayerCard(
        TemporaryPhoto temporaryPhoto,
        NativeIdCardTarget target)
    {
        temporaryPhoto.CapturePhoto(target.PlayerController);
    }

    private static PlayerIDInfo SnapshotPlayerIdInfo(PlayerIDInfo source)
    {
        return new PlayerIDInfo
        {
            Name = CloneBytes(source.Name),
            DateOfBirth = CloneBytes(source.DateOfBirth),
            HoroscopeSign = CloneBytes(source.HoroscopeSign),
            Location = CloneBytes(source.Location),
            AreaOfStudy = CloneBytes(source.AreaOfStudy),
            FavoriteQuote = CloneBytes(source.FavoriteQuote),
            IDCardColorIndex = source.IDCardColorIndex,
            IsGold = source.IsGold,
        };
    }

    private static bool PlayerIdInfoMatches(
        PlayerIDInfo current,
        PlayerIDInfo expected)
    {
        return ByteArraysEqual(current.Name, expected.Name) &&
               ByteArraysEqual(current.DateOfBirth, expected.DateOfBirth) &&
               ByteArraysEqual(current.HoroscopeSign, expected.HoroscopeSign) &&
               ByteArraysEqual(current.Location, expected.Location) &&
               ByteArraysEqual(current.AreaOfStudy, expected.AreaOfStudy) &&
               ByteArraysEqual(current.FavoriteQuote, expected.FavoriteQuote) &&
               current.IDCardColorIndex == expected.IDCardColorIndex &&
               current.IsGold == expected.IsGold;
    }

    private static byte[] CloneBytes(byte[] source)
    {
        return source == null ? null : (byte[])source.Clone();
    }

    private static bool ByteArraysEqual(byte[] left, byte[] right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void LogOutcome(
        string source,
        NativeIdCardResolutionState state,
        int index,
        string steamId,
        string detail)
    {
        string safeSource = string.IsNullOrWhiteSpace(source)
            ? "unknown"
            : source.Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (safeSource.Length > 32)
        {
            safeSource = safeSource.Substring(0, 32);
        }

        string safeDetail = string.IsNullOrWhiteSpace(detail)
            ? "none"
            : detail.Replace("\r", " ").Replace("\n", " ");
        if (safeDetail.Length > 160)
        {
            safeDetail = safeDetail.Substring(0, 159) + "…";
        }

        string message =
            "[BlueSageNativeId] source=" + safeSource +
            "; outcome=" + state +
            "; index=" + index +
            "; steamIdPresent=" + !string.IsNullOrWhiteSpace(steamId) +
            "; detail=" + safeDetail + ".";
        if (state == NativeIdCardResolutionState.Ready)
        {
            Plugin.Log?.LogInfo(message);
        }
        else
        {
            Plugin.Log?.LogWarning(message);
        }
    }
}
