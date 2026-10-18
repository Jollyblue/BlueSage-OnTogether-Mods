using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerPanelSurfacePolicyTests
{
    [Test]
    public void Active_self_row_reserves_aligned_geometry_without_remote_action_listeners()
    {
        PlayerPanelSurfaceDecision decision = PlayerPanelSurfacePolicy.Evaluate(new PlayerPanelSurfaceContext
        {
            FeatureEnabled = true,
            IsSelf = true,
            RowActive = true,
            SerializedReportButtonPresent = true,
            SerializedReportButtonParentPresent = true,
            OwnedLanePresent = false,
            PomoState = PlayerPanelPomoState.Inactive,
        });

        Assert.Multiple(() =>
        {
            Assert.That(decision.Action, Is.EqualTo(PlayerPanelSurfaceAction.Create));
            Assert.That(decision.ReserveAlignedLane, Is.True);
            Assert.That(decision.ShowMentionAction, Is.False);
            Assert.That(decision.ShowNativeIdAction, Is.False);
            Assert.That(decision.PreserveNativeControls, Is.True);
        });
    }

    [TestCase("Inactive")]
    [TestCase("Syncable")]
    [TestCase("Synced")]
    [TestCase("SyncedWithMe")]
    public void Every_native_pomo_state_keeps_both_BlueSage_actions_without_claiming_native_controls(
        string pomoStateName)
    {
        PlayerPanelPomoState pomoState = Enum.Parse<PlayerPanelPomoState>(pomoStateName);
        PlayerPanelSurfaceDecision decision = PlayerPanelSurfacePolicy.Evaluate(new PlayerPanelSurfaceContext
        {
            FeatureEnabled = true,
            IsSelf = false,
            RowActive = true,
            SerializedReportButtonPresent = true,
            SerializedReportButtonParentPresent = true,
            OwnedLanePresent = false,
            PomoState = pomoState,
        });

        Assert.Multiple(() =>
        {
            Assert.That(decision.Action, Is.EqualTo(PlayerPanelSurfaceAction.Create));
            Assert.That(decision.Anchor, Is.EqualTo(PlayerPanelAnchor.SerializedReportButtonParent));
            Assert.That(decision.ShowMentionAction, Is.True);
            Assert.That(decision.ShowNativeIdAction, Is.True);
            Assert.That(decision.PreserveNativeControls, Is.True);
            Assert.That(decision.AllowUnverifiedHierarchyFallback, Is.False);
        });
    }

    [Test]
    public void Missing_serialized_anchor_fails_closed_and_never_enables_the_hierarchy_hypothesis()
    {
        PlayerPanelSurfaceDecision decision = PlayerPanelSurfacePolicy.Evaluate(new PlayerPanelSurfaceContext
        {
            FeatureEnabled = true,
            IsSelf = false,
            RowActive = true,
            SerializedReportButtonPresent = false,
            SerializedReportButtonParentPresent = false,
            OwnedLanePresent = false,
            RuntimeHierarchyObserved = true,
            PomoState = PlayerPanelPomoState.SyncedWithMe,
        });

        Assert.Multiple(() =>
        {
            Assert.That(decision.Action, Is.EqualTo(PlayerPanelSurfaceAction.None));
            Assert.That(decision.Anchor, Is.EqualTo(PlayerPanelAnchor.None));
            Assert.That(decision.ShowMentionAction, Is.False);
            Assert.That(decision.ShowNativeIdAction, Is.False);
            Assert.That(decision.AllowUnverifiedHierarchyFallback, Is.False);
            Assert.That(PlayerPanelSurfacePolicy.UnverifiedHierarchyFallbackEnabled, Is.False);
        });
    }

    [TestCase(true, false, false, "Refresh", true)]
    [TestCase(false, true, false, "Refresh", true)]
    [TestCase(false, false, true, "Refresh", true)]
    public void Existing_lane_refreshes_and_rebinds_when_row_or_panel_generation_changes(
        bool identityChanged,
        bool panelChanged,
        bool anchorChanged,
        string expectedName,
        bool expectRebind)
    {
        PlayerPanelSurfaceAction expected = Enum.Parse<PlayerPanelSurfaceAction>(expectedName);
        PlayerPanelSurfaceDecision decision = PlayerPanelSurfacePolicy.Evaluate(new PlayerPanelSurfaceContext
        {
            FeatureEnabled = true,
            IsSelf = false,
            RowActive = true,
            SerializedReportButtonPresent = true,
            SerializedReportButtonParentPresent = true,
            OwnedLanePresent = true,
            IdentityChanged = identityChanged,
            PanelChanged = panelChanged,
            AnchorChanged = anchorChanged,
            PomoState = PlayerPanelPomoState.Syncable,
        });

        Assert.Multiple(() =>
        {
            Assert.That(decision.Action, Is.EqualTo(expected));
            Assert.That(decision.RebindIdentityAndListeners, Is.EqualTo(expectRebind));
            Assert.That(decision.RebindAnchor, Is.EqualTo(anchorChanged || panelChanged));
        });
    }

    [Test]
    public void Stable_owned_lane_is_a_no_op_without_listener_or_layout_rebind()
    {
        PlayerPanelSurfaceDecision decision = PlayerPanelSurfacePolicy.Evaluate(new PlayerPanelSurfaceContext
        {
            FeatureEnabled = true,
            IsSelf = false,
            RowActive = true,
            SerializedReportButtonPresent = true,
            SerializedReportButtonParentPresent = true,
            OwnedLanePresent = true,
            PomoState = PlayerPanelPomoState.Syncable,
        });

        Assert.Multiple(() =>
        {
            Assert.That((int)decision.Action, Is.EqualTo(1), "Action slot 1 is reserved for the stable no-op lane decision.");
            Assert.That(decision.RebindIdentityAndListeners, Is.False);
            Assert.That(decision.RebindAnchor, Is.False);
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    public void Disabled_or_inactive_rows_remove_only_an_owned_BlueSage_lane(
        bool featureEnabled,
        bool isSelf,
        bool rowActive)
    {
        PlayerPanelSurfaceDecision decision = PlayerPanelSurfacePolicy.Evaluate(new PlayerPanelSurfaceContext
        {
            FeatureEnabled = featureEnabled,
            IsSelf = isSelf,
            RowActive = rowActive,
            OwnedLanePresent = true,
            SerializedReportButtonPresent = true,
            SerializedReportButtonParentPresent = true,
            PomoState = PlayerPanelPomoState.Inactive,
        });

        Assert.Multiple(() =>
        {
            Assert.That(decision.Action, Is.EqualTo(PlayerPanelSurfaceAction.Remove));
            Assert.That(decision.PreserveNativeControls, Is.True);
        });
    }
}
