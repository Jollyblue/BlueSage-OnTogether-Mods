using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerIdentitySurfaceContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Steam_profile_tooltip_uses_exact_row_id_and_fail_closed_language()
    {
        string patch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "PlayerIdentityTooltipPatch.cs"));
        string highlights = File.ReadAllText(Path.Combine(Root, "src", "Patches", "PlayerIdentityRoleHighlightController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("PlayerPanelButtonType.AddFriend"));
            Assert.That(patch, Does.Contain("_playerSteamId"));
            Assert.That(patch, Does.Contain("TryResolveExact"));
            Assert.That(patch, Does.Contain("Steam Profile • VERIFIED"));
            Assert.That(patch, Does.Contain("Profile opens, but moderation stays locked."));
            Assert.That(patch, Does.Contain("Compact(evidence.SteamPersona, 24)"));
            Assert.That(patch, Does.Contain("Verified live row #"));
            Assert.That(patch, Does.Contain("HOME > Lobby Safety to review or clear flags"));
            Assert.That(highlights, Does.Contain("PROTECTED VICTIM"));
            Assert.That(highlights, Does.Contain("SUSPECT — VERIFY"));
            Assert.That(patch, Does.Contain("BuildCompactRoleSummary"));
        });
    }

    [Test]
    public void Clone_incident_actions_expose_exact_ids_copy_preview_and_non_destructive_closure()
    {
        string controller = File.ReadAllText(Path.Combine(Root, "src", "CloneIncidentActionController.cs"));
        string ledger = File.ReadAllText(Path.Combine(Root, "src", "Core", "CloneIncidentLedger.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("GetOpenIncidents"));
            Assert.That(controller, Does.Contain("TryCopySteamId"));
            Assert.That(controller, Does.Contain("TryCopyBanPreviewCommand"));
            Assert.That(controller, Does.Contain("TryResolveForLobby"));
            Assert.That(controller, Does.Contain("TryDismissLocal"));
            Assert.That(controller, Does.Contain("TryDismissAllOpenLocal"));
            Assert.That(controller, Does.Contain("TryDismissOpenForVerifiedPlayer"));
            Assert.That(controller, Does.Contain("MarkNoLongerMatching"));
            Assert.That(controller, Does.Contain("RecordLifecycle"));
            Assert.That(controller, Does.Contain("GUIUtility.systemCopyBuffer"));
            Assert.That(ledger, Does.Contain("CloneIncidentState.Resolved"));
            Assert.That(ledger, Does.Contain("MaximumIncidentAgeSeconds"));
            Assert.That(controller, Does.Contain("CloneIncidentResolutionProtocolPolicy.BuildMemberDataRequest"));
            Assert.That(controller, Does.Contain("SteamMatchmaking.SetLobbyMemberData"));
            Assert.That(controller, Does.Contain("SetLobbyData"));
            Assert.That(controller, Does.Not.Contain("BuildResolutionMessage"));
            Assert.That(controller, Does.Not.Contain("TrySendLobbyChatMessage(packet)"));
        });
    }

    [Test]
    public void Clone_incident_resolution_uses_host_polled_member_data_and_rejects_legacy_chat_relay()
    {
        string relay = File.ReadAllText(Path.Combine(Root, "src", "Patches", "ModerationRelayPatch.cs"));
        string controller = File.ReadAllText(Path.Combine(Root, "src", "CloneIncidentActionController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(relay, Does.Contain("CloneIncidentResolutionProtocolPolicy.Prefix"));
            Assert.That(relay, Does.Contain("Rejected a legacy chat-shaped BlueSage moderation request"));
            Assert.That(relay, Does.Not.Contain("HandleServerResolutionRequest(text"));
            Assert.That(relay, Does.Contain("return false;"));
            Assert.That(controller, Does.Contain("SteamIdModerationController.IsAuthorizedCurrentIssuer"));
            Assert.That(controller, Does.Contain("SteamMatchmaking.SetLobbyData"));
            Assert.That(controller, Does.Not.Contain("BuildResolutionMessage"));
            Assert.That(controller, Does.Not.Contain("[Clone Shield] Incident"));
        });
    }

    [Test]
    public void Id_card_resolution_never_falls_back_to_a_cloneable_name()
    {
        string resolver = File.ReadAllText(Path.Combine(Root, "src", "NativeIdCardController.cs"));
        string policy = File.ReadAllText(Path.Combine(Root, "src", "Core", "NativeIdCardResolutionPolicy.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(resolver, Does.Contain("PlayerIdentityEvidenceController.TryResolveConfirmed"));
            Assert.That(resolver, Does.Contain("CurrentRowReferenceMatches"));
            Assert.That(resolver, Does.Contain("CurrentRowSteamId"));
            Assert.That(resolver, Does.Contain("CurrentRowPlayerId"));
            Assert.That(policy, Does.Contain("Names are presentation only"));
            Assert.That(resolver, Does.Not.Contain("playerName"));
            Assert.That(resolver, Does.Not.Contain("ReadIdInfoName"));
            Assert.That(resolver, Does.Not.Contain("DisplayName =="));
            Assert.That(policy, Does.Not.Contain("DisplayName"));
        });
    }

    [Test]
    public void Sidwho_read_only_lookup_and_typeahead_are_registered()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string typeahead = File.ReadAllText(Path.Combine(Root, "src", "CommandTypeaheadController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("\"sidwho\""));
            Assert.That(plugin, Does.Contain("HandleSidWhoCommand"));
            Assert.That(plugin, Does.Contain("/sidwho list"));
            Assert.That(plugin, Does.Contain("cloned names never authorize"));
            Assert.That(typeahead, Does.Contain("GetIdentityLookupSuggestions"));
            Assert.That(typeahead, Does.Contain("BuildIncidentSuggestions"));
            Assert.That(typeahead, Does.Contain("CloneIncidentActionController.GetOpenIncidents()"));
            Assert.That(typeahead, Does.Contain("Choice(command, incident.Id"));
            Assert.That(typeahead, Does.Not.Contain("Choice(command, incident.StableId"));
            Assert.That(typeahead, Does.Contain("dismiss-all"));
            Assert.That(typeahead, Does.Contain("clear-player"));
            Assert.That(typeahead, Does.Contain("clear-history"));
            Assert.That(typeahead, Does.Contain("Choice(command, \"resolve\""));
            Assert.That(typeahead, Does.Contain("Choice(command, \"dismiss\""));
        });
    }

    [Test]
    public void Verified_rows_and_native_ban_ui_fail_closed_on_runtime_misalignment()
    {
        string evidence = File.ReadAllText(Path.Combine(Root, "src", "PlayerIdentityEvidenceController.cs"));
        string moderation = File.ReadAllText(Path.Combine(Root, "src", "SteamIdModerationController.cs"));
        string menu = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(evidence, Does.Contain("var steamCounts = CountValues(steamIds)"));
            Assert.That(evidence, Does.Contain("var playerCounts = CountValues(playerIds)"));
            Assert.That(evidence, Does.Contain("liveSteamCheck="));
            Assert.That(evidence, Does.Contain("non-actionable"));
            Assert.That(evidence, Does.Contain("No hidden or guessed row is actionable"));
            Assert.That(moderation, Does.Contain("uniquePlayerId"));
            Assert.That(menu, Does.Contain("TryGetNativeBanRecords"));
            Assert.That(menu, Does.Contain("The game's ban list can't be read right now"));
            Assert.That(menu, Does.Contain("no unban is offered until it can"));
        });
    }

    [Test]
    public void Identity_resync_command_is_authorized_one_shot_and_never_a_polling_loop()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string controller = File.ReadAllText(Path.Combine(Root, "src", "IdentityResyncController.cs"));
        string evidence = File.ReadAllText(Path.Combine(Root, "src", "PlayerIdentityEvidenceController.cs"));
        string highlights = File.ReadAllText(Path.Combine(Root, "src", "Patches", "PlayerIdentityRoleHighlightController.cs"));
        int handlerStart = plugin.IndexOf("private BlueSageCommandResult HandleIdentityResyncCommand", StringComparison.Ordinal);
        int handlerEnd = handlerStart < 0
            ? -1
            : plugin.IndexOf("private BlueSageCommandResult", handlerStart + 1, StringComparison.Ordinal);
        string handler = handlerStart >= 0 && handlerEnd > handlerStart
            ? plugin.Substring(handlerStart, handlerEnd - handlerStart)
            : string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("\"idsync\""));
            Assert.That(plugin, Does.Contain("HandleIdentityResyncCommand"));
            Assert.That(handler, Does.Contain("IdentityResyncController.HandleCommand(context)"));
            Assert.That(handler, Does.Not.Contain("ForceRefreshIncompleteRosterRowsOnce"));
            Assert.That(evidence, Does.Contain("ForceRefreshIncompleteRosterRowsOnce"));
            Assert.That(evidence, Does.Contain("_snapshotDirty || snapshotExpired || !previous.CoverageComplete"));
            Assert.That(evidence, Does.Contain("snapshotAgeTicks > SnapshotLifetimeTicks"));
            Assert.That(controller, Does.Contain("Coordinator.TryBegin("));
            Assert.That(controller, Does.Contain("ForceRefreshIncompleteRosterRowsOnce"));
            Assert.That(controller, Does.Contain(".RefreshRows(targetedSteamIds)"));
            Assert.That(controller, Does.Not.Contain("PlayerRoleLabelController"));
            Assert.That(controller, Does.Not.Contain("PlayerIdentityRoleHighlightController.RefreshAll()"));
            Assert.That(highlights, Does.Contain("if (targets.Contains(steamId)) RefreshRow"));
            Assert.That(controller, Does.Not.Contain("StartCoroutine"));
            Assert.That(controller, Does.Not.Contain("InvokeRepeating"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
