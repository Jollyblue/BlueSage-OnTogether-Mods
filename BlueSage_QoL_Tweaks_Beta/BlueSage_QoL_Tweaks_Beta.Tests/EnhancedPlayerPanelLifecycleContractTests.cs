using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class EnhancedPlayerPanelLifecycleContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    [Test]
    public void Init_patch_is_a_thin_dispatcher_and_never_discovers_or_edits_native_controls()
    {
        string patch = Read("src", "Patches", "EnhancedPlayerPanelPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(PlayerItemController), \"Init\")]"));
            Assert.That(patch, Does.Not.Contain("PlayerIdentityEvidenceController.InvalidateRosterCache();"),
                "A recycled presentation row is not authoritative evidence that the Steam/PurrNet roster changed.");
            Assert.That(patch, Does.Contain("PlayerPanelSurfaceController.RefreshRow("));
            Assert.That(patch, Does.Not.Contain("Group_Buttons"));
            Assert.That(patch, Does.Not.Contain("RowHasPomoSurface"));
            Assert.That(patch, Does.Not.Contain("GetComponentsInChildren<Transform>"));
            Assert.That(patch, Does.Not.Contain("ButtonOpenIdCheck"));
            Assert.That(patch, Does.Not.Contain("TemporaryPhoto"));
            Assert.That(patch, Does.Not.Contain("UnityEngine.Object.Destroy"));
        });
    }

    [Test]
    public void Recycled_row_presentation_cannot_force_uncached_authority_capture_churn()
    {
        string patch = Read("src", "Patches", "EnhancedPlayerPanelPatch.cs");
        string badgeController = Read("src", "Patches", "PlayerBadgeController.cs");
        string evidenceController = Read("src", "PlayerIdentityEvidenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Not.Contain("InvalidateRosterCache"));
            Assert.That(badgeController, Does.Not.Contain("_nextPollAt = 0f;\n            RefreshMarker(marker);"));
            Assert.That(badgeController, Does.Contain("TryCaptureCompleteAuthorityEvidenceAndRowsCached"));
            Assert.That(evidenceController, Does.Contain("internal static bool TryCaptureCompleteAuthorityEvidenceAndRowsCached("));
            Assert.That(evidenceController, Does.Contain("CaptureRosterSnapshot(cacheAllowed: true)"));
        });
    }

    [Test]
    public void Controller_owns_one_ignore_layout_lane_from_the_serialized_report_button_parent()
    {
        string controller = Read("src", "Patches", "PlayerPanelSurfaceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("AccessTools.Field(typeof(PlayerItemController), \"_reportButton\")"));
            Assert.That(controller, Does.Contain("reportButton.transform.parent"));
            Assert.That(controller, Does.Contain("BlueSage_ActionLane"));
            Assert.That(controller, Does.Contain("BlueSagePlayerPanelSurfaceMarker"));
            Assert.That(controller, Does.Contain("layoutElement.ignoreLayout = true"));
            Assert.That(controller, Does.Contain("Button_BlueSageMention"));
            Assert.That(controller, Does.Contain("Button_BlueSageNativeId"));
            Assert.That(controller, Does.Not.Contain("transform.Find(\"Group_Buttons\")"));
            Assert.That(controller, Does.Not.Contain("RowHasPomoSurface"));
            Assert.That(controller, Does.Not.Contain("IndexOf(\"pomo\""));
            Assert.That(controller, Does.Not.Contain("IndexOf(\"pomodoro\""));
            Assert.That(controller, Does.Not.Contain("SetParent(reportButton.transform"));
        });
    }

    [Test]
    public void Owned_action_lane_reflows_for_a_bounded_number_of_late_layout_frames()
    {
        string controller = Read("src", "Patches", "PlayerPanelSurfaceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("private const int LayoutSettleFrameBudget = 30;"));
            Assert.That(controller, Does.Contain("marker.RequestLayoutSettle(reportButton);"));
            Assert.That(controller, Does.Contain("private void OnEnable()"));
            Assert.That(controller, Does.Contain("private void LateUpdate()"));
            Assert.That(controller, Does.Contain("PlayerPanelSurfaceController.RefreshOwnedLaneGeometry(this);"));
            Assert.That(controller, Does.Contain("_layoutSettleFramesRemaining--;"));
            Assert.That(controller, Does.Not.Contain("PlayerPanelSurfaceController.RefreshRow(OwnerRow"),
                "Late settling may follow native geometry only; it must not recapture identity or rebind actions every frame.");
        });
    }

    [Test]
    public void Row_reuse_panel_replacement_and_teardown_rebind_or_destroy_only_marker_owned_objects()
    {
        string controller = Read("src", "Patches", "PlayerPanelSurfaceController.cs");
        string plugin = Read("src", "Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("marker.BoundSteamId"));
            Assert.That(controller, Does.Contain("marker.AnchorInstanceId"));
            Assert.That(controller, Does.Contain("marker.Generation"));
            Assert.That(controller, Does.Contain("button.onClick.RemoveAllListeners();"));
            Assert.That(controller, Does.Contain("PlayerPanelSurfaceAction.Remove"));
            Assert.That(controller, Does.Contain("DestroyOwnedLane"));
            Assert.That(controller, Does.Contain("private void OnDestroy()"));
            // Round 4 R9: the lane/button counters fed only the removed hidden diagnostics.
            Assert.That(controller, Does.Not.Contain("System.Threading.Interlocked"));
            Assert.That(controller, Does.Not.Contain("Destroy(reportButton"));
            Assert.That(controller, Does.Not.Contain("Destroy(syncButton"));
            Assert.That(controller, Does.Not.Contain("Destroy(steamButton"));
            Assert.That(controller, Does.Contain("OwnedLanesByRowInstanceId"));
            Assert.That(controller, Does.Contain("PruneInactivePooledRows"));
            Assert.That(controller, Does.Contain("internal static void ClearAll()"));
            Assert.That(controller, Does.Contain("[HarmonyPatch(typeof(PlayerPanelController), \"UpdateServerPanel\")]"));
            Assert.That(controller, Does.Contain("marker.gameObject.SetActive(false);"));
            Assert.That(controller, Does.Contain("RemoveOwnedListeners"));
            Assert.That(controller, Does.Contain("marker.IsRetired = true;"));
            Assert.That(controller, Does.Contain("!registered.IsRetired"));
            Assert.That(controller, Does.Contain("!candidate.IsRetired"));
            Assert.That(controller, Does.Contain("ReferenceEquals(registered, marker)"),
                "Deferred destruction of a departed marker must not unregister fresh ownership on the reused row.");
            Assert.That(controller, Does.Not.Contain("GetComponentInChildren<BlueSagePlayerPanelSurfaceMarker>"));
            Assert.That(plugin, Does.Contain("PlayerPanelSurfaceController.ClearAll();"));
            Assert.That(plugin, Does.Contain("EnableEnhancedPlayerPanel.SettingChanged"));
            Assert.That(
                plugin.IndexOf("PlayerPanelSurfaceController.ClearAll();", StringComparison.Ordinal),
                Is.LessThan(plugin.IndexOf("_harmony.UnpatchSelf();", StringComparison.Ordinal)));
        });
    }

    [Test]
    // Round 4 WS2 (Blue, 2026-10-19): the hidden hierarchy diagnostic went with /bluedevdebug. The unverified
    // hierarchy fallback stays disabled exactly as before.
    public void Hierarchy_diagnostic_is_removed_and_the_unverified_fallback_stays_disabled()
    {
        string controller = Read("src", "Patches", "PlayerPanelSurfaceController.cs");
        string policy = Read("src", "Core", "PlayerPanelSurfacePolicy.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Not.Contain("HierarchyDiagnostic"));
            Assert.That(controller, Does.Not.Contain("ShouldEmitHiddenDiagnostics"));
            Assert.That(policy, Does.Contain("UnverifiedHierarchyFallbackEnabled = false"));
            Assert.That(policy, Does.Not.Contain("RuntimeHierarchyObserved &&"));
        });
    }

    [Test]
    public void Shared_native_ID_controller_is_the_only_extension_surface_that_invokes_the_game_API()
    {
        string panel = Read("src", "Patches", "PlayerPanelSurfaceController.cs");
        string miniMap = Read("src", "Patches", "MiniMapPlayerLabelPatch.cs");
        string nativeId = Read("src", "NativeIdCardController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(panel, Does.Contain("NativeIdCardController.TryOpen"));
            Assert.That(miniMap, Does.Contain("NativeIdCardController.TryOpen"));
            Assert.That(panel, Does.Not.Contain("ButtonOpenIdCheck"));
            Assert.That(miniMap, Does.Not.Contain("ButtonOpenIdCheck"));
            Assert.That(nativeId, Does.Contain("temporaryPhoto.CapturePhoto(target.PlayerController)"));
            Assert.That(nativeId, Does.Not.Contain("ButtonOpenIdCheck("),
                "Missing exact controller evidence must fail closed, never reopen by a potentially stale index.");
            Assert.That(nativeId, Does.Contain("NativeIdCardInvocationGuard.Invoke"));
            Assert.That(nativeId, Does.Contain("PlayerIdentityEvidenceController.TryResolveConfirmed"));
            Assert.That(nativeId, Does.Contain("NativeIdCardResolutionPolicy.Evaluate"));
            Assert.That(nativeId, Does.Not.Contain("playerName"));
            Assert.That(nativeId, Does.Not.Contain("ReadIdInfoName"));
            Assert.That(nativeId, Does.Not.Contain("DisplayName =="));
        });
    }

    [Test]
    public void Player_rows_follow_the_exact_row_object_while_minimap_icons_fail_on_any_observed_index_or_tuple_drift()
    {
        string panel = Read("src", "Patches", "PlayerPanelSurfaceController.cs");
        string miniMap = Read("src", "Patches", "MiniMapPlayerLabelPatch.cs");
        string nativeId = Read("src", "NativeIdCardController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(panel, Does.Contain("requireObservedIndex: false"));
            Assert.That(miniMap, Does.Contain("requireObservedIndex: true"));
            Assert.That(miniMap, Does.Contain("panel.PlayerSteamIDs[index]"));
            Assert.That(miniMap, Does.Contain("panel.PlayerIDs[index]"));
            Assert.That(miniMap, Does.Contain("panel.PlayerTransforms[index]"));
            Assert.That(miniMap, Does.Contain("panel.PlayerItemControllers[index]"));
            Assert.That(nativeId, Does.Contain("effectiveExpectedPlayerId"));
            Assert.That(nativeId, Does.Contain("requireObservedIndex"));
            Assert.That(nativeId, Does.Contain("IsFinalTupleStable(target)"));
            Assert.That(nativeId, Does.Contain("ReferenceEquals(panel.PlayerItemControllers[index], row)"));
            Assert.That(nativeId, Does.Contain("ReferenceEquals(panel.PlayerTransforms[index], networkTransform)"));
            Assert.That(nativeId, Does.Contain("ReferenceEquals(panel.PlayerControllers[index], playerController)"));
            Assert.That(nativeId, Does.Contain("PlayerIdInfoMatches(panel.IDInfos[index], playerInfo)"));
        });
    }
}
