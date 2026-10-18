#nullable enable

using System;

namespace BlueSage.QoLTweaks.Core;

internal enum NativeIdCardCallerKind
{
    OrdinaryUser,
    Host,
    AuthenticatedHelper,
    BluePrivateConsumer,
}

internal enum NativeIdCardResolutionState
{
    Ready,
    InvalidSteamIdentity,
    FreshEvidenceUnavailable,
    FreshSteamIdentityMismatch,
    NativeListsMisaligned,
    ExpectedIndexUnavailable,
    StaleExpectedIndex,
    NativeRowIndexMismatch,
    NativeRowReferenceMismatch,
    NativeRowStaleRefreshable,
    RowSteamIdentityChanged,
    PurrNetIdentityUnavailable,
    ObservedPurrNetIdentityChanged,
    PurrNetIdentityMismatch,
    UnityTransformUnavailable,
    UnityTransformIdentityChanged,
    FinalTupleChanged,
}

internal sealed class NativeIdCardResolutionContext
{
    internal NativeIdCardCallerKind CallerKind;
    internal string RequestedSteamId = string.Empty;
    internal bool FreshResolutionSucceeded;
    internal string FreshResolvedSteamId = string.Empty;
    internal string FreshResolvedPlayerId = string.Empty;
    internal string ExpectedPlayerId = string.Empty;
    internal int FreshResolvedIndex = -1;
    internal int ExpectedIndex = -1;
    internal int CurrentRowIndex = -1;
    internal bool CurrentRowReferenceMatches;
    internal string CurrentRowSteamId = string.Empty;
    // Authoritative PlayerSteamIDs entry at CurrentRowIndex; the pooled row's own field can lag it.
    internal string CurrentRowListSteamId = string.Empty;
    internal string CurrentRowPlayerId = string.Empty;
    internal bool HasLiveNetworkTransform;
    internal bool NativeListsAligned;
    internal bool ExpectedNetworkTransformMatches;
    internal bool FinalTupleStable;
}

internal sealed class NativeIdCardResolutionDecision
{
    internal NativeIdCardResolutionState State;
    internal int PlayerIndex = -1;
    internal bool MayOpenNativeCard;
    internal string Reason = string.Empty;
}

internal static class NativeIdCardResolutionPolicy
{
    internal static NativeIdCardResolutionDecision Evaluate(NativeIdCardResolutionContext? context)
    {
        if (context == null ||
            !ModerationProtocolPolicy.IsSteamId64(context.RequestedSteamId))
        {
            return Reject(
                NativeIdCardResolutionState.InvalidSteamIdentity,
                "the request does not contain one valid SteamID64");
        }

        // CallerKind is deliberately not an authorization bypass. Ordinary,
        // Host, authenticated Helper, and Blue/private consumers all require
        // the same exact Steam/PurrNet/Unity/native-row agreement.
        _ = context.CallerKind;

        if (!context.FreshResolutionSucceeded)
        {
            return Reject(
                NativeIdCardResolutionState.FreshEvidenceUnavailable,
                "fresh current identity evidence is unavailable");
        }

        if (!string.Equals(
                context.FreshResolvedSteamId,
                context.RequestedSteamId,
                System.StringComparison.Ordinal))
        {
            return Reject(
                NativeIdCardResolutionState.FreshSteamIdentityMismatch,
                "fresh evidence resolved a different Steam identity");
        }

        if (!context.NativeListsAligned)
        {
            return Reject(
                NativeIdCardResolutionState.NativeListsMisaligned,
                "the current native roster lists are not aligned");
        }

        if (context.ExpectedIndex < 0)
        {
            return Reject(
                NativeIdCardResolutionState.ExpectedIndexUnavailable,
                "the clicked surface has no current native roster index");
        }

        if (context.ExpectedIndex != context.FreshResolvedIndex)
        {
            return Reject(
                NativeIdCardResolutionState.StaleExpectedIndex,
                "the clicked surface index is stale");
        }

        if (context.CurrentRowIndex != context.FreshResolvedIndex)
        {
            return Reject(
                NativeIdCardResolutionState.NativeRowIndexMismatch,
                "the current player-row index disagrees with fresh evidence");
        }

        if (!context.CurrentRowReferenceMatches)
        {
            return Reject(
                NativeIdCardResolutionState.NativeRowReferenceMismatch,
                "the current native row object no longer matches the clicked surface");
        }

        if (!string.Equals(
                context.CurrentRowSteamId,
                context.RequestedSteamId,
                System.StringComparison.Ordinal))
        {
            // Vanilla re-stamps pooled rows only while the player list is open, so after a leave the
            // row can lag the authoritative list. Still a rejection: the caller must refresh and re-check.
            if (string.Equals(
                    context.CurrentRowListSteamId,
                    context.RequestedSteamId,
                    System.StringComparison.Ordinal))
            {
                return Reject(
                    NativeIdCardResolutionState.NativeRowStaleRefreshable,
                    "the native row has not been re-stamped since the roster changed");
            }

            return Reject(
                NativeIdCardResolutionState.RowSteamIdentityChanged,
                "the native row now belongs to another Steam identity");
        }

        if (string.IsNullOrWhiteSpace(context.ExpectedPlayerId) ||
            string.IsNullOrWhiteSpace(context.CurrentRowPlayerId) ||
            string.IsNullOrWhiteSpace(context.FreshResolvedPlayerId))
        {
            return Reject(
                NativeIdCardResolutionState.PurrNetIdentityUnavailable,
                "the current PurrNet player identity is unavailable");
        }

        if (!string.Equals(
                context.ExpectedPlayerId,
                context.CurrentRowPlayerId,
                System.StringComparison.Ordinal))
        {
            return Reject(
                NativeIdCardResolutionState.ObservedPurrNetIdentityChanged,
                "the clicked surface now points at another PurrNet identity");
        }

        if (!string.Equals(
                context.CurrentRowPlayerId,
                context.FreshResolvedPlayerId,
                System.StringComparison.Ordinal))
        {
            return Reject(
                NativeIdCardResolutionState.PurrNetIdentityMismatch,
                "the current PurrNet player identity changed");
        }

        if (!context.HasLiveNetworkTransform)
        {
            return Reject(
                NativeIdCardResolutionState.UnityTransformUnavailable,
                "the current Unity/PurrNet transform is unavailable");
        }

        if (!context.ExpectedNetworkTransformMatches)
        {
            return Reject(
                NativeIdCardResolutionState.UnityTransformIdentityChanged,
                "the clicked surface now points at another Unity/PurrNet transform");
        }

        if (!context.FinalTupleStable)
        {
            return Reject(
                NativeIdCardResolutionState.FinalTupleChanged,
                "the native identity tuple changed before invocation");
        }

        // Names are presentation only. No displayed name, rich TMP text,
        // persona, role, or badge participates in this decision.
        return new NativeIdCardResolutionDecision
        {
            State = NativeIdCardResolutionState.Ready,
            PlayerIndex = context.FreshResolvedIndex,
            MayOpenNativeCard = true,
            Reason = "exact current Steam/PurrNet/Unity/native-row agreement",
        };
    }

    private static NativeIdCardResolutionDecision Reject(
        NativeIdCardResolutionState state,
        string reason)
    {
        return new NativeIdCardResolutionDecision
        {
            State = state,
            PlayerIndex = -1,
            MayOpenNativeCard = false,
            Reason = reason,
        };
    }
}

internal enum NativeIdCardInvocationState
{
    Opened,
    TupleUnavailableBeforePanelClose,
    TupleChangedAfterPanelClose,
}

internal static class NativeIdCardInvocationGuard
{
    internal static NativeIdCardInvocationState Invoke(
        Func<bool> isTupleStable,
        Action closePanel,
        Action openNativeCard)
    {
        if (isTupleStable == null ||
            closePanel == null ||
            openNativeCard == null ||
            !isTupleStable())
        {
            return NativeIdCardInvocationState.TupleUnavailableBeforePanelClose;
        }

        closePanel();
        if (!isTupleStable())
        {
            return NativeIdCardInvocationState.TupleChangedAfterPanelClose;
        }

        openNativeCard();
        return NativeIdCardInvocationState.Opened;
    }
}
