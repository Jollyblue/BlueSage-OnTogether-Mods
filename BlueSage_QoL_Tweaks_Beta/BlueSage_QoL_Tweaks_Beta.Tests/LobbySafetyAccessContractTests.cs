using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class LobbySafetyAccessContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Normal_users_have_no_discoverable_lobby_safety_or_helper_surface()
    {
        string plugin = Read("src", "Plugin.cs");
        string menu = Read("src", "QolMenuWindow.cs");
        string typeahead = Read("src", "CommandTypeaheadController.cs");
        string registry = Read("src", "Core", "BlueSageCommandRegistry.cs");
        string mentions = Read("src", "Core", "PlayerMentionProvider.cs");

        Assert.Multiple(() =>
        {
            Assert.That(registry, Does.Contain("Func<BlueSageCommand, bool>? include = null"));
            Assert.That(plugin, Does.Contain("_commandRegistry.Suggest(input, maxResults, CanSuggestCommand)"));
            Assert.That(typeahead, Does.Contain("Plugin.IsHelperManagementCommandName(command) && !Plugin.CanManageHelpers()"));
            Assert.That(typeahead, Does.Contain("Plugin.IsLobbySafetyCommandName(command) && !Plugin.CanUseLobbySafety(out _)"));
            Assert.That(menu, Does.Contain("if (_tab == 4 && !showLobbySafetyTab)"));
            Assert.That(menu, Does.Contain("Plugin.ShouldShowLobbySafetyTab(out LobbySafetyAccessState accessState"));
            Assert.That(menu, Does.Contain("LobbySafetyAccessState.HelperWaitingForHost"));
            Assert.That(menu, Does.Contain("Helper assigned • waiting for the host"));
            Assert.That(menu.IndexOf("HelperWaitingForHost", StringComparison.Ordinal), Is.LessThan(menu.IndexOf("DrawVerifiedSafetyRoster", StringComparison.Ordinal)));
            Assert.That(menu, Does.Contain("string hostOnly = Plugin.CanManageHelpers()"));
            Assert.That(mentions, Does.Contain("Plugin.CanUseLobbySafety(out _) && \"lobby\".StartsWith"));
        });
    }

    [Test]
    public void Incident_roster_copy_and_lookup_apis_fail_closed_before_reading_details()
    {
        string actions = Read("src", "CloneIncidentActionController.cs");
        string evidence = Read("src", "PlayerIdentityEvidenceController.cs");
        string plugin = Read("src", "Plugin.cs");

        Assert.Multiple(() =>
        {
            AssertMethodGatePrecedes(actions, "GetOpenIncidents()", "CloneIncidentLedger.Snapshot");
            AssertMethodGatePrecedes(actions, "GetIncidentHistory()", "CloneIncidentLedger.HistorySnapshot");
            AssertMethodGatePrecedes(actions, "TryCopySteamId(", "CloneIncidentLedger.TryResolveIncident");
            AssertMethodGatePrecedes(actions, "TryCopyBanPreviewCommand(", "CloneIncidentLedger.TryResolveIncident");
            AssertMethodGatePrecedes(actions, "TryResolveForLobby(", "CloneIncidentLedger.TryResolveIncident");
            AssertMethodGatePrecedes(actions, "TryDismissLocal(", "CloneIncidentLedger.DismissLocal");
            AssertMethodGatePrecedes(actions, "ClearClosedHistory()", "CloneIncidentLedger.ClearClosed");
            AssertMethodGatePrecedes(evidence, "TryResolveLookup(", "CloneIncidentLedger.TryResolveIncident");
            AssertMethodGatePrecedes(evidence, "GetVerifiedRoster()", "CaptureRosterSnapshot(cacheAllowed: true)");
            Assert.That(evidence, Does.Contain("if (!Plugin.CanUseLobbySafety(out _)) return Array.Empty<string>();"));
            AssertMethodGatePrecedes(plugin, "private BlueSageCommandResult HandleSidListCommand(", "CloneIncidentLedger.Snapshot");
            AssertMethodGatePrecedes(plugin, "private BlueSageCommandResult HandleSidWhoCommand(", "PlayerIdentityEvidenceController.GetVerifiedRoster");
            AssertMethodGatePrecedes(plugin, "private BlueSageCommandResult HandleSidBanCommand(", "ModerationTargetResolver.TryResolve");
            AssertMethodGatePrecedes(plugin, "private BlueSageCommandResult HandleSidUnbanCommand(", "SteamIdModerationController.CanLocalIssue");
        });
    }

    [Test]
    public void Revoke_clears_pending_moderation_and_host_only_data_never_drives_helper_suggestions()
    {
        string plugin = Read("src", "Plugin.cs");
        string actions = Read("src", "CloneIncidentActionController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("if (_hadLobbySafetyAccess && !hasLobbySafetyAccess)"));
            Assert.That(plugin, Does.Contain("_pendingModerationAction = null;"));
            Assert.That(plugin, Does.Contain("if (!CanUseLobbySafety(out _))\n        {\n            _pendingModerationAction = null;\n            return LobbySafetyDeniedResult();"));
            Assert.That(plugin, Does.Contain("if (!isHost) return Array.Empty<string>();"));
            Assert.That(actions, Does.Contain("if (Plugin.CanUseLobbySafety(out _))\n                {\n                    Plugin.AddLocalNotificationWithToast(\"Clone Shield \" + request.StableId"));
        });
    }

    [Test]
    public void Host_owned_helper_list_persists_and_is_revalidated_by_authoritative_sender()
    {
        string plugin = Read("src", "Plugin.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");
        string pingPatch = Read("src", "Patches", "MentionHighlighterPatch.cs");
        string senderContext = Read("src", "Patches", "ChatSenderContextPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("Config.Bind(\"Helpers\", \"LobbyHelpers\""));
            Assert.That(plugin, Does.Contain("Config.Bind(\"Helpers\", \"LobbyHelperOwnerSteamId\""));
            Assert.That(plugin, Does.Contain("UnifiedHelperPolicy.MergeForOwner"));
            Assert.That(plugin, Does.Contain("Instance?.Config.Save();"));
            Assert.That(plugin, Does.Contain("SteamIdModerationController.IsAuthorizedCurrentIssuer"));
            Assert.That(moderation, Does.Contain("ownerSteamId + \"|\" + helpers"));
            Assert.That(moderation, Does.Contain("GetLobbyMemberData(lobby, member"));
            Assert.That(moderation, Does.Contain("PlayerIdentityEvidenceController.TryResolveExact(senderSteamId"));
            Assert.That(moderation, Does.Contain("IsUniqueSteamLobbyMember(lobby, senderSteamId)"));
            Assert.That(moderation, Does.Contain("ModerationAuthorizationPolicy.IsAuthorizedIssuer"));
            Assert.That(moderation, Does.Contain("ModerationCapabilityPolicy.IsFresh"));
            Assert.That(moderation, Does.Contain("bool listed = IsPublishedHelper(lobby, owner, local);"));
            Assert.That(moderation, Does.Contain("return LobbySafetyAccessState.HelperWaitingForHost;"));
            Assert.That(pingPatch, Does.Contain("Plugin.IsLobbyMassPingAuthorized(ChatSenderContextPatch.CurrentSenderSteamId)"));
            Assert.That(senderContext, Does.Contain("AuthoritativeChatSenderPolicy.TryResolveUniqueIndex("));
            Assert.That(senderContext, Does.Contain("info.sender"));
            Assert.That(senderContext, Does.Contain("panel.PlayerIDs"));
        });
    }

    [Test]
    public void Helper_capability_and_live_identity_refresh_without_a_required_command()
    {
        string plugin = Read("src", "Plugin.cs");
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("_nextModerationCapabilityPublishAt = Time.unscaledTime + 5f"));
            Assert.That(plugin, Does.Contain("SteamIdModerationController.PublishHostCapability()"));
            Assert.That(menu, Does.Contain("LobbySafetyAccessState accessState = SteamIdModerationController.GetLocalAccessState"));
            // Round 5d voice pass: player words for the same automatic unlock and optional recheck.
            Assert.That(menu, Does.Contain("Your tools unlock by themselves once the host's BlueSage answers. No command needed."));
            Assert.That(menu, Does.Contain("\"Check Again\", \"Check again now. BlueSage also keeps checking on its own.\""));
            Assert.That(menu, Does.Contain("GetVerifiedRoster(out string rosterStatus)"));
            Assert.That(menu, Does.Not.Contain("v3 member-data capability"), "protocol words stay out of player text");
        });
    }

    [Test]
    public void Ordinary_profile_tooltip_and_role_highlights_hide_moderation_details()
    {
        string tooltip = Read("src", "Patches", "PlayerIdentityTooltipPatch.cs");
        string highlights = Read("src", "Patches", "PlayerIdentityRoleHighlightController.cs");
        string evidence = Read("src", "PlayerIdentityEvidenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(tooltip, Does.Contain("if (!hasLobbySafetyAccess)"));
            Assert.That(tooltip, Does.Contain("Current Steam-lobby member."));
            Assert.That(tooltip, Does.Contain("QoL is not required."));
            Assert.That(tooltip.IndexOf("ExpandProfilePopup(__instance, body);", StringComparison.Ordinal), Is.LessThan(tooltip.IndexOf("TryResolveExact", StringComparison.Ordinal)));
            Assert.That(highlights, Does.Contain("Plugin.ShouldApplyCloneShield && Plugin.ShouldApplyEnhancedPlayerPanel && Plugin.CanUseLobbySafety(out _)"));
            Assert.That(highlights, Does.Contain("if (!Plugin.ShouldApplyCloneShield || !Plugin.CanUseLobbySafety(out _)) return fallback;"));
            Assert.That(evidence, Does.Contain("if (!Plugin.CanUseLobbySafety(out _))"));
            Assert.That(evidence, Does.Contain("return context;"));
            Assert.That(evidence, Does.Contain("Role = roleContext.RoleBySteamId.TryGetValue"));
        });
    }

    [Test]
    public void Turning_clone_shield_off_clears_incidents_and_human_chat_never_seeds_incidents()
    {
        string controller = Read("src", "CloneShieldController.cs");
        string actions = Read("src", "CloneIncidentActionController.cs");
        string highlights = Read("src", "Patches", "PlayerIdentityRoleHighlightController.cs");
        string commands = Read("src", "Patches", "BlueSageCommandPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("if (!Plugin.ShouldApplyCloneShield)"));
            Assert.That(controller, Does.Contain("ResetObservationState();"));
            Assert.That(controller, Does.Contain("CloneIncidentActionController.ResetLobbyState();"));
            Assert.That(actions, Does.Contain("internal static void ResetLobbyState()"));
            Assert.That(actions, Does.Not.Contain("ObserveIncoming"));
            Assert.That(commands, Does.Not.Contain("CloneIncidentActionController.ObserveIncoming"));
            Assert.That(commands, Does.Contain("Human-readable chat is presentation only"));
            Assert.That(highlights, Does.Contain("Plugin.ShouldApplyCloneShield && Plugin.ShouldApplyEnhancedPlayerPanel"));
        });
    }

    [Test]
    public void Mass_ping_authority_uses_rpc_sender_identity_and_accepts_a_uniquely_resolved_row_zero_host()
    {
        string plugin = Read("src", "Plugin.cs");
        string senderContext = Read("src", "Patches", "ChatSenderContextPatch.cs");
        int start = plugin.IndexOf("internal static bool IsLobbyMassPingAuthorized", StringComparison.Ordinal);
        int end = plugin.IndexOf("internal static bool TryGetRescueLobby", start, StringComparison.Ordinal);
        string method = plugin.Substring(start, end - start);
        Assert.Multiple(() =>
        {
            Assert.That(senderContext, Does.Not.Contain("ContainsLobbyPingToken"));
            Assert.That(senderContext, Does.Contain("CurrentSenderContext"));
            Assert.That(senderContext, Does.Contain("TryResolveUniqueIndex("));
            Assert.That(senderContext, Does.Contain("info.sender"));
            Assert.That(senderContext, Does.Contain("panel.PlayerIDs"));
            Assert.That(senderContext, Does.Contain("TryResolveRosterIndexFresh"));
            Assert.That(method, Does.Contain("TryResolveExact(senderSteamId"));
            Assert.That(method, Does.Not.Contain("senderIndex"));
            Assert.That(senderContext, Does.Not.Contain("GetLobbyMemberByIndex"));
        });
    }

    [Test]
    public void Host_lobby_safety_exposes_paired_native_bans_without_chat_dumping()
    {
        string menu = Read("src", "QolMenuWindow.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");
        string plugin = Read("src", "Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("if (isHost)\n        {\n            GUILayout.Space(12f);\n            DrawOfflineBanEntry();\n            GUILayout.Space(12f);\n            DrawNativeBanRecords();"));
            Assert.That(menu, Does.Contain("Ban a Steam ID that isn't in the lobby"));
            Assert.That(menu, Does.Contain("Confirm Preview"));
            Assert.That(menu, Does.Contain("The game's ban list •"));
            Assert.That(menu, Does.Contain("Copy Banned SteamID64"));
            Assert.That(menu, Does.Contain("Preview SID Unban"));
            Assert.That(menu, Does.Contain("Confirm SID Unban"));
            Assert.That(menu, Does.Contain("Click again to clear this selection."));
            Assert.That(menu, Does.Contain("No saved ban is selected."));
            Assert.That(menu, Does.Contain("Clear Selection"));
            Assert.That(menu, Does.Not.Contain("selected = records[start]"));
            Assert.That(menu, Does.Contain("Confirm Exact SID Ban"));
            Assert.That(menu, Does.Contain("Confirm Suspect Ban"));
            Assert.That(moderation, Does.Contain("if (!Plugin.CanManageHelpers())"));
            Assert.That(moderation, Does.Contain("only the lobby host can see the ban list"));
            Assert.That(moderation, Does.Contain("bans.BanServerPlayers.Count != bans.BanServerPlayerNicks.Count"));
            Assert.That(plugin, Does.Contain("PreviewSidUnbanFromQolMenu"));
            Assert.That(plugin, Does.Contain("ConfirmSidUnbanFromQolMenu"));
            Assert.That(plugin, Does.Contain("CopySavedBanSteamIdFromQolMenu"));
            Assert.That(plugin, Does.Contain("OpenOfflineSteamProfileFromQolMenu"));
            Assert.That(menu, Does.Contain("CopySavedBanSteamIdFromQolMenu(selected.SteamId)"));
            Assert.That(menu, Does.Contain("OpenOfflineSteamProfileFromQolMenu(_offlineBanSteamIdText)"));
        });
    }

    [Test]
    public void Moderation_confirm_buttons_are_disabled_until_the_exact_preview_is_ready()
    {
        string menu = Read("src", "QolMenuWindow.cs");
        string plugin = Read("src", "Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu.Split("GetModerationConfirmationReadiness(").Length - 1, Is.GreaterThanOrEqualTo(4));
            Assert.That(menu, Does.Contain("Preview Required"));
            Assert.That(menu, Does.Contain("GUI.enabled = confirmation.IsReady;"));
            Assert.That(menu, Does.Contain("confirmation.IsReady ? _enabledButtonStyle : _disabledButtonStyle"));
            Assert.That(plugin, Does.Contain("GetModerationConfirmationReadiness"));
            Assert.That(plugin, Does.Contain("ModerationProtocolPolicy.EvaluateConfirmation"));
        });
    }

    [Test]
    public void Partial_verified_roster_remains_usable_but_absence_sensitive_actions_wait()
    {
        string evidence = Read("src", "PlayerIdentityEvidenceController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(evidence, Does.Contain("!string.IsNullOrWhiteSpace(rosterError) && roster.Count == 0"));
            Assert.That(evidence, Does.Contain("Exact verified rows remain usable; Clone Shield and absence-sensitive actions wait for complete coverage."));
            Assert.That(evidence, Does.Contain("player-list rows"));
            Assert.That(evidence, Does.Contain("Steam lobby members"));
            Assert.That(evidence, Does.Contain("non-actionable"));
            Assert.That(evidence, Does.Contain("outside current Steam lobby membership"));
            Assert.That(evidence, Does.Contain("Profile access remains available; identity-sensitive actions stay locked"));
            Assert.That(evidence, Does.Contain("coverageComplete"));
        });
    }

    private static void AssertMethodGatePrecedes(string source, string methodToken, string protectedRead)
    {
        int method = source.IndexOf(methodToken, StringComparison.Ordinal);
        Assert.That(method, Is.GreaterThanOrEqualTo(0), "Missing method token " + methodToken);
        int gate = source.IndexOf("CanUseLobbySafety", method, StringComparison.Ordinal);
        int read = source.IndexOf(protectedRead, method, StringComparison.Ordinal);
        Assert.That(gate, Is.GreaterThan(method), "Missing access gate in " + methodToken);
        Assert.That(read, Is.GreaterThan(gate), protectedRead + " must occur after authorization in " + methodToken);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
