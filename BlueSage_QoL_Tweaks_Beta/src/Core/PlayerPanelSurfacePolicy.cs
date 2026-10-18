#nullable enable

namespace BlueSage.QoLTweaks.Core;

internal enum PlayerPanelPomoState
{
    Inactive,
    Syncable,
    Synced,
    SyncedWithMe,
}

internal enum PlayerPanelAnchor
{
    None,
    SerializedReportButtonParent,
}

internal enum PlayerPanelSurfaceAction
{
    None,
    // The owned lane already matches this live row. Avoid layout, listener, and
    // highlight work until identity or hierarchy evidence actually changes.
    Keep,
    Create,
    Refresh,
    Remove,
}

internal sealed class PlayerPanelSurfaceContext
{
    internal bool FeatureEnabled;
    internal bool IsSelf;
    internal bool RowActive;
    internal bool SerializedReportButtonPresent;
    internal bool SerializedReportButtonParentPresent;
    internal bool OwnedLanePresent;
    internal bool IdentityChanged;
    internal bool PanelChanged;
    internal bool AnchorChanged;
    internal bool RuntimeHierarchyObserved;
    internal PlayerPanelPomoState PomoState;
}

internal sealed class PlayerPanelSurfaceDecision
{
    internal PlayerPanelSurfaceAction Action;
    internal PlayerPanelAnchor Anchor;
    internal bool ShowMentionAction;
    internal bool ShowNativeIdAction;
    internal bool PreserveNativeControls = true;
    internal bool ReserveAlignedLane;
    internal bool AllowUnverifiedHierarchyFallback;
    internal bool RebindIdentityAndListeners;
    internal bool RebindAnchor;
}

internal static class PlayerPanelSurfacePolicy
{
    // The current resources.assets receipt proves file identity, not hierarchy.
    // A runtime observation is diagnostic evidence only and can never flip this.
    internal const bool UnverifiedHierarchyFallbackEnabled = false;

    internal static PlayerPanelSurfaceDecision Evaluate(PlayerPanelSurfaceContext? context)
    {
        var decision = new PlayerPanelSurfaceDecision
        {
            PreserveNativeControls = true,
            AllowUnverifiedHierarchyFallback = UnverifiedHierarchyFallbackEnabled,
        };

        if (context == null)
        {
            return decision;
        }

        decision.ReserveAlignedLane =
            context.FeatureEnabled &&
            context.IsSelf &&
            context.RowActive &&
            context.SerializedReportButtonPresent &&
            context.SerializedReportButtonParentPresent;

        bool eligible = context.FeatureEnabled && context.RowActive;
        if (!eligible)
        {
            decision.Action = context.OwnedLanePresent
                ? PlayerPanelSurfaceAction.Remove
                : PlayerPanelSurfaceAction.None;
            return decision;
        }

        if (!context.SerializedReportButtonPresent ||
            !context.SerializedReportButtonParentPresent)
        {
            decision.Action = context.OwnedLanePresent
                ? PlayerPanelSurfaceAction.Remove
                : PlayerPanelSurfaceAction.None;
            return decision;
        }

        // Pomo and voice are game-owned state. All four Pomo states receive the
        // same independent BlueSage lane; none authorizes native mutation.
        _ = context.PomoState;
        _ = context.RuntimeHierarchyObserved;

        decision.Anchor = PlayerPanelAnchor.SerializedReportButtonParent;
        decision.ShowMentionAction = !context.IsSelf;
        decision.ShowNativeIdAction = !context.IsSelf;
        decision.Action = !context.OwnedLanePresent
            ? PlayerPanelSurfaceAction.Create
            : context.IdentityChanged || context.PanelChanged || context.AnchorChanged
                ? PlayerPanelSurfaceAction.Refresh
                : PlayerPanelSurfaceAction.Keep;
        decision.RebindIdentityAndListeners =
            !context.OwnedLanePresent ||
            context.IdentityChanged ||
            context.PanelChanged ||
            context.AnchorChanged;
        decision.RebindAnchor =
            !context.OwnedLanePresent ||
            context.PanelChanged ||
            context.AnchorChanged;
        return decision;
    }
}
