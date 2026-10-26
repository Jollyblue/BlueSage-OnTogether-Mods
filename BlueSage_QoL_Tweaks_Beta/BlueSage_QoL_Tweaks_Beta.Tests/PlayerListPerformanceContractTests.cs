using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerListPerformanceContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    [Test]
    public void Player_row_init_never_runs_a_global_identity_refresh()
    {
        string patch = Read("src", "Patches", "EnhancedPlayerPanelPatch.cs");
        string controller = Read("src", "Patches", "PlayerPanelSurfaceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Not.Contain("PlayerIdentityRoleHighlightController.RefreshAll();"));
            Assert.That(patch, Does.Not.Contain("PlayerIdentityEvidenceController.InvalidateRosterCache();"));
            Assert.That(patch, Does.Contain("PlayerPanelSurfaceController.RefreshRow("));
            Assert.That(patch, Does.Not.Contain("GetComponentsInChildren"));
            Assert.That(patch, Does.Not.Contain("Group_Buttons"));
            Assert.That(controller, Does.Not.Contain("GetComponentInChildren<BlueSagePlayerPanelSurfaceMarker>"));
            Assert.That(controller, Does.Contain("OwnedLanesByRowInstanceId"));
        });
    }

    [Test]
    public void Unflagged_highlight_rows_exit_before_identity_resolution()
    {
        string highlights = Read("src", "Patches", "PlayerIdentityRoleHighlightController.cs");
        int noRoleGuard = highlights.IndexOf("(!victim && !suspect)", StringComparison.Ordinal);
        int cachedResolution = highlights.IndexOf("TryResolveExactCached", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(noRoleGuard, Is.GreaterThanOrEqualTo(0));
            Assert.That(cachedResolution, Is.GreaterThan(noRoleGuard));
            Assert.That(highlights, Does.Contain("GetVerifiedCurrentSteamIdSetCached"));
            Assert.That(highlights, Does.Not.Contain("PlayerIdentityEvidenceController.TryResolveExact(steamId"));
        });
    }

    [Test]
    public void Display_surfaces_share_cached_truth_while_actions_keep_fresh_resolution()
    {
        string evidence = Read("src", "PlayerIdentityEvidenceController.cs");
        string tooltip = Read("src", "Patches", "PlayerIdentityTooltipPatch.cs");
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(evidence, Does.Contain("LargeLobbyRefreshPolicy.RosterSnapshotLifetime.Ticks"));
            Assert.That(evidence, Does.Contain("TryResolveExactCached"));
            Assert.That(evidence, Does.Contain("CaptureRosterSnapshot(cacheAllowed: false)"));
            Assert.That(evidence, Does.Contain("TryResolveRosterIndexFresh"));
            Assert.That(tooltip, Does.Contain("TryResolveExactCached"));
            Assert.That(menu, Does.Contain("GetVerifiedRoster(out string rosterStatus)"));
        });
    }

    [Test]
    public void Live_steam_disagreement_has_an_explicit_non_departed_tooltip()
    {
        string tooltip = Read("src", "Patches", "PlayerIdentityTooltipPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(tooltip, Does.Contain("Steam Profile • LIVE / STEAM CHECK"));
            Assert.That(tooltip, Does.Contain("Live in game, but missing from Steam's lobby list (usually an automatic reconnect)."));
            // Round 4 WS3 (Blue: allow moderation always): reconnected rows are labeled, not locked.
            Assert.That(tooltip, Does.Contain("Profile opens; host moderation works and is labeled RECONNECTED."));
            Assert.That(tooltip, Does.Not.Contain("revalidating"));
        });
    }

    [Test]
    public void Idle_moderation_member_data_polling_is_bounded_to_once_per_second()
    {
        string moderation = Read("src", "SteamIdModerationController.cs");
        string incidents = Read("src", "CloneIncidentActionController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(moderation, Does.Contain("MemberDataPollIntervalMilliseconds = 1000"));
            Assert.That(incidents, Does.Contain("MemberDataPollIntervalMilliseconds = 1000"));
            Assert.That(moderation, Does.Not.Contain("MemberDataPollIntervalMilliseconds = 250"));
            Assert.That(incidents, Does.Not.Contain("MemberDataPollIntervalMilliseconds = 250"));
        });
    }

    [Test]
    public void Safety_and_native_ban_rows_are_deselectable_and_never_auto_target()
    {
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("Click again to clear this player selection."));
            Assert.That(menu, Does.Contain("Click again to clear this selection."));
            Assert.That(menu, Does.Contain("Deselect this native ban and disarm every selection-scoped unban control."));
            Assert.That(menu, Does.Contain("No saved ban is selected."));
            Assert.That(menu, Does.Not.Contain("selected = records[start]"));
            Assert.That(menu, Does.Contain("ClearLobbySafetyTransientState();"));
            Assert.That(menu, Does.Contain("_selectedBannedSteamId = string.Empty;"));
            Assert.That(menu, Does.Contain("_nativeBanPage = 0;"));
        });
    }

    [Test]
    public void Roster_roles_and_memory_diagnostics_are_precomputed_and_bounded()
    {
        string evidence = Read("src", "PlayerIdentityEvidenceController.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");
        string panel = Read("src", "Patches", "PlayerPanelSurfaceController.cs");
        string plugin = Read("src", "Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(evidence, Does.Contain("BuildRoleContext(lobby, lobbyMembers)"));
            Assert.That(evidence, Does.Contain("RoleBySteamId.TryGetValue"));
            Assert.That(evidence, Does.Not.Contain("Role = BuildRole(lobby, steamId)"));
            Assert.That(moderation, Does.Contain("GetPublishedHelpers"));
            // Round 4 R9: the buttonsLive/lanesLive counters fed only the removed hidden diagnostics.
            Assert.That(panel, Does.Not.Contain("buttonsLive="));
            Assert.That(panel, Does.Not.Contain("System.Threading.Interlocked"));
            // Round 4 WS2: the hidden memory/hierarchy diagnostics are removed rather than bounded.
            Assert.That(panel, Does.Not.Contain("HierarchyDiagnostic"));
            Assert.That(plugin, Does.Not.Contain("BuildProcessMemoryDiagnosticSummary"));
        });
    }

    [Test]
    public void Badge_compatibility_prefers_the_cached_roster_context_and_keeps_read_only_tags_fresh_per_exact_row()
    {
        string badgeController = Read("src", "Patches", "PlayerBadgeController.cs");
        string identityController = Read("src", "PlayerIdentityEvidenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(identityController, Does.Contain("TryCaptureCompanionIdentityContextCached"));
            Assert.That(badgeController, Does.Contain("RefreshCompatibleClientSnapshots(lobby)"));
            Assert.That(badgeController, Does.Contain("TryGetCompatibleClientSnapshot("));
            Assert.That(badgeController, Does.Contain("CompatibleClientPresencePolicy.IsFresh("));
            Assert.That(badgeController, Does.Not.Contain("TryResolveFreshClientCapabilitySnapshotBySteam(\n                rowSteamId"));
        });
    }

    [Test]
    public void Large_lobby_badge_refresh_captures_shared_host_evidence_once_before_rendering_rows()
    {
        string badgeController = Read("src", "Patches", "PlayerBadgeController.cs");
        int sharedCapture = badgeController.IndexOf(
            "RefreshSharedLobbyEvidence(lobby);",
            StringComparison.Ordinal);
        int rowRefresh = badgeController.IndexOf(
            "foreach (BlueSagePlayerBadgeMarker marker in\n                     OwnedMarkersByRowInstanceId.Values.ToArray())",
            StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(sharedCapture, Is.GreaterThanOrEqualTo(0));
            Assert.That(rowRefresh, Is.GreaterThan(sharedCapture));
            Assert.That(
                badgeController,
                Does.Contain("TryGetSharedLobbyEvidence(")
                    .And.Contain("out SharedLobbyEvidence sharedEvidence"));
            Assert.That(
                badgeController,
                Does.Contain("sharedEvidence.Helpers.Contains(marker.BoundSteamId)"));
        });
    }
}
