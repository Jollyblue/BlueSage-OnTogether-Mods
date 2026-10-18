using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerBadgeLifecycleContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Row_init_routes_all_rows_to_the_badge_controller_without_self_or_name_mutation()
    {
        string patch = Read("src", "Patches", "PlayerRoleLabelPatch.cs");
        string legacyController = Read("src", "Patches", "PlayerRoleLabelController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("PlayerBadgeController.RegisterRow(__instance, playerSteamId);"));
            Assert.That(patch, Does.Not.Contain("isSelf").And.Not.Contain("IsSelf"));
            Assert.That(legacyController, Does.Not.Contain("NameText.text"));
            Assert.That(legacyController, Does.Not.Contain("state.NameText"));
            Assert.That(legacyController, Does.Contain("PlayerBadgeController.Tick();"));
            Assert.That(legacyController, Does.Contain("PlayerBadgeController.ClearAll();"));
        });
    }

    [Test]
    public void Reuse_lobby_change_and_teardown_invalidate_owned_markers_and_extension_context()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("marker.BoundSteamId"));
            Assert.That(controller, Does.Contain("marker.Generation"));
            Assert.That(controller, Does.Contain("marker.IsRetired = true;"));
            Assert.That(controller, Does.Contain("!registered.IsRetired"));
            Assert.That(controller, Does.Contain("!candidate.IsRetired"));
            Assert.That(controller, Does.Contain("ReferenceEquals(registered, marker)"),
                "Deferred destruction of a departed marker must not unregister fresh ownership on the reused row.");
            Assert.That(controller, Does.Contain("PruneInactivePooledRows"));
            Assert.That(controller, Does.Contain("BeginLobby"));
            Assert.That(controller, Does.Contain("BlueSageExtensionRegistry.ClearLobbyState"));
            Assert.That(controller, Does.Contain("internal static void ClearAll()"));
            Assert.That(controller, Does.Contain("BlueSageExtensionRegistry.ClearAll();"));
            Assert.That(marker, Does.Contain("private void OnDestroy()"));
            Assert.That(marker, Does.Contain("PlayerBadgeController.UnregisterOwnedMarker(this);"));
        });
    }

    [Test]
    public void Creator_host_replacement_is_bound_to_the_native_name_and_restored_on_every_exit_path()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        int restorationIndex = controller.IndexOf(
            "marker.RestoreNativeName();",
            StringComparison.Ordinal);
        int labelGuardIndex = controller.IndexOf(
            "if (badgeLabel == null ||",
            StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("marker.BindNativeName(nativeName);"));
            Assert.That(controller, Does.Contain("ResolveNativeHostReplacement("));
            Assert.That(controller, Does.Contain("marker.ApplyNativeNameReplacement(nativeHostReplacement);"));
            Assert.That(restorationIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(labelGuardIndex, Is.GreaterThan(restorationIndex),
                "Native-name restoration must run even when the separate badge label has disappeared.");
            Assert.That(marker, Does.Contain("private void OnDisable()"));
            Assert.That(marker, Does.Contain("private void OnDestroy()"));
            Assert.That(marker.Split("RestoreNativeName();", StringSplitOptions.None).Length - 1,
                Is.GreaterThanOrEqualTo(2));
            Assert.That(marker, Does.Contain("NativeName.text,")
                .And.Contain("_nativeNameApplied,")
                .And.Contain("System.StringComparison.Ordinal"));
        });
    }

    [Test]
    public void Native_name_presentation_lease_restores_before_rebind_and_on_every_exit_path()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        Assert.Multiple(() =>
        {
            Assert.That(marker, Does.Contain("PlayerBadgeNativeTextPresentationLease"));
            Assert.That(marker, Does.Contain("ApplyNativePresentationReservation("));
            Assert.That(marker, Does.Contain("RestoreNativePresentation();"));
            Assert.That(marker.Split("RestoreNativePresentation();", StringSplitOptions.None).Length - 1,
                Is.GreaterThanOrEqualTo(3));
            Assert.That(controller, Does.Contain("marker.ApplyNativePresentationReservation("));
            Assert.That(controller, Does.Contain(
                "if (badgeLabel == null ||\n            ownedHostLabel == null ||\n            nativeName == null)\n        {\n            marker.RestoreNativePresentation();\n            return;\n        }"),
                "Native presentation restoration must run when any bound TMP reference has disappeared.");
        });
    }

    [Test]
    public void Destroyed_tmp_references_are_fake_null_checked_pruned_and_rebound()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Not.Contain("nativeName?.rectTransform"));
            Assert.That(controller, Does.Not.Contain("marker.HostLabel?.rectTransform"));
            Assert.That(controller, Does.Not.Contain("marker.NativeName?.rectTransform"));
            Assert.That(controller, Does.Contain("!marker.HasLivePresentation(nativeName)"),
                "Registration must replace a marker whose native/host/label references were destroyed.");
            Assert.That(controller, Does.Contain("!marker.HasLivePresentation()"),
                "Polling must prune a marker whose owned TMP references were destroyed.");
            Assert.That(marker, Does.Contain("internal bool HasLivePresentation("));
            Assert.That(marker, Does.Contain("NativeName != null")
                .And.Contain("Label != null")
                .And.Contain("HostLabel != null"));
        });
    }

    [Test]
    public void Periodic_tick_prunes_both_owned_surfaces_only_from_complete_authoritative_rows()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string evidence = Read("src", "PlayerIdentityEvidenceController.cs");
        string surface = Read("src", "Patches", "PlayerPanelSurfaceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("TryCaptureCompleteAuthorityEvidenceAndRowsCached("));
            Assert.That(controller, Does.Contain("PlayerPanelSurfaceController.PruneRowsNotIn(currentRows);"));
            Assert.That(controller, Does.Contain("PruneRowsNotIn(currentRows);"));
            Assert.That(evidence, Does.Contain("snapshot.CoverageComplete"));
            Assert.That(evidence, Does.Contain("NativeRosterShapePolicy.HasAlignedActivePrefix("));
            Assert.That(evidence, Does.Contain("PlayerSteamIdField?.GetValue(row) as string"));
            Assert.That(surface, Does.Contain("AuthoritativePlayerRowReconciliationPolicy.Reconcile("));
            Assert.That(controller, Does.Contain("AuthoritativePlayerRowReconciliationPolicy.Reconcile("));
        });
    }

    [Test]
    public void Incomplete_global_roster_keeps_exact_row_read_only_badges_but_invalidates_action_truth()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string incompleteBranch = ExtractMethodBody(
            controller,
            "private static void RefreshPresentationsWithoutCompleteRoster()");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("RefreshPresentationsWithoutCompleteRoster();"),
                "One unrelated stale row must not blank read-only badges for every exact current row.");
            Assert.That(incompleteBranch, Does.Contain("InvalidateEvidenceGeneration();"),
                "Incomplete global truth must still revoke companion/action generations.");
            Assert.That(incompleteBranch, Does.Contain("BeginReadOnlyBadgePresentationContext();"),
                "An exact row may request private read-only badge presentation after action truth is revoked.");
            Assert.That(incompleteBranch, Does.Contain("RefreshMarker(marker);"),
                "Each row must independently fail closed or show only badges supported by its exact cached tuple.");
            Assert.That(incompleteBranch, Does.Not.Contain("PruneRowsNotIn")
                .And.Not.Contain("PlayerPanelSurfaceController.PruneRowsNotIn"),
                "An incomplete roster is never authority to destroy another row's owned surfaces.");
        });
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), signature);
        int openBrace = source.IndexOf('{', signatureIndex);
        Assert.That(openBrace, Is.GreaterThan(signatureIndex), signature);

        int depth = 0;
        for (int index = openBrace; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] != '}') continue;
            depth--;
            if (depth == 0) return source.Substring(openBrace, index - openBrace + 1);
        }

        Assert.Fail("Method body was not balanced: " + signature);
        return string.Empty;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
}
