using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityResyncAuthorizationContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Runtime_request_uses_non_chat_member_data_and_host_revalidates_every_authority_boundary()
    {
        string controller = Read("src", "IdentityResyncController.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("IdentityResyncRequestProtocol.MemberDataKey"));
            Assert.That(controller, Does.Contain("SteamMatchmaking.SetLobbyMemberData"));
            Assert.That(controller, Does.Contain("TryGetOwnedHostLobby"));
            Assert.That(controller, Does.Contain("TryGetFreshCurrentCapability"));
            Assert.That(controller, Does.Contain("TryResolveExactCached"));
            Assert.That(controller, Does.Contain("IsAuthorizedCurrentIssuer"));
            Assert.That(controller, Does.Contain("IsUniqueCurrentLobbyMember"));
            Assert.That(controller, Does.Contain("RememberNonce"));
            Assert.That(controller, Does.Not.Contain("TrySendLobbyChatMessage"));
            Assert.That(moderation, Does.Contain("TryGetFreshCurrentCapability"));
        });
    }

    [Test]
    public void Repair_writes_only_bluesage_caches_and_owned_surfaces()
    {
        string controller = Read("src", "IdentityResyncController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("ForceRefreshIncompleteRosterRowsOnce"));
            Assert.That(controller, Does.Contain("RefreshRows(targetedSteamIds)"));
            Assert.That(controller, Does.Contain("RefreshRowsFromAlreadyAvailableSnapshot"));
            Assert.That(controller, Does.Not.Contain("PlayerRoleLabelController"));
            Assert.That(controller, Does.Not.Contain(".PlayerSteamIDs.Clear"));
            Assert.That(controller, Does.Not.Contain(".PlayerIDs.Clear"));
            Assert.That(controller, Does.Not.Contain(".PlayerTransforms.Clear"));
            Assert.That(controller, Does.Not.Contain("SetLobbyOwner"));
            Assert.That(controller, Does.Not.Contain("BanData"));
            Assert.That(controller, Does.Not.Contain("PlayerName"));
            Assert.That(controller, Does.Not.Contain("SetName"));
        });
    }

    [Test]
    public void Quiet_or_throttled_helper_request_is_not_burned_before_a_repair_lease_begins()
    {
        string controller = Read("src", "IdentityResyncController.cs");
        int begin = controller.IndexOf(
            "Coordinator.TryBegin(",
            StringComparison.Ordinal);
        int remember = controller.IndexOf(
            "RememberNonce(requestNonce)",
            StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(begin, Is.GreaterThanOrEqualTo(0));
            Assert.That(remember, Is.GreaterThan(begin));
        });
    }

    [Test]
    public void Plugin_routes_all_idsync_operations_to_the_coordinator_and_legacy_aliases_stay_read_only()
    {
        string plugin = Read("src", "Plugin.cs");
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("IdentityResyncController.HandleCommand(context)"));
            Assert.That(plugin, Does.Contain("IdentityResyncController.Tick();"));
            Assert.That(plugin, Does.Not.Contain("ManualIdentityResyncCooldownTicks"));
            Assert.That(plugin, Does.Not.Contain("_lastManualIdentityResyncUtcTicks"));
        });
    }

    [Test]
    public void Autocomplete_exposes_the_four_explicit_operations_without_hiding_status_from_ordinary_users()
    {
        string plugin = Read("src", "Plugin.cs");
        string typeahead = Read(
            "src",
            "CommandTypeaheadController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(
                typeahead,
                Does.Contain("[\"idsync\"] = IdentityResyncChoices(\"idsync\")"));
            Assert.That(typeahead, Does.Contain("\"status\""));
            Assert.That(typeahead, Does.Contain("\"refresh\""));
            Assert.That(typeahead, Does.Contain("\"repair\""));
            Assert.That(typeahead, Does.Contain("\"request\""));
            Assert.That(
                typeahead,
                Does.Contain("SteamIdModerationController.GetLocalAccessState"));
            Assert.That(
                typeahead,
                Does.Contain("LobbySafetyAccessState.Host"));
            Assert.That(
                typeahead,
                Does.Contain("LobbySafetyAccessState.HelperReady"));
            Assert.That(
                plugin,
                Does.Not.Match(
                    @"command\.Matches\(""idsync""\).*CanUseLobbySafety"));
        });
    }

    [Test]
    public void Helper_safety_surface_exposes_bounded_status_and_only_role_appropriate_actions()
    {
        string menu = Read("src", "QolMenuWindow.cs");
        string help = Read("src", "Core", "IdentityResyncHelpPolicy.cs");
        Assert.Multiple(() =>
        {
            Assert.That(
                menu,
                Does.Contain("IdentityResyncController.StatusText()"));
            Assert.That(
                menu,
                Does.Contain("IdentityResyncController.ExecuteMenuAction(\"refresh\")"));
            Assert.That(
                menu,
                Does.Contain("IdentityResyncController.ExecuteMenuAction(action)"));
            Assert.That(
                menu,
                Does.Contain("IdentityResyncHelpPolicy.FormatActionResult"));
            Assert.That(
                menu,
                Does.Contain("help.RecheckExplanation + \" \" + help.ExpectedResult),\n                    _buttonStyle"));
            Assert.That(
                help,
                Does.Contain("Ask Host to Recheck Player List"));
            Assert.That(
                menu,
                Does.Contain("Player matching and safe host actions"));
            Assert.That(
                help,
                Does.Not.Contain("Recheck Exact Rows"));
            Assert.That(
                menu,
                Does.Contain("if (_showHostToolsTechnicalDetails)"));
        });
    }

    [Test]
    public void Runtime_generation_shape_observes_native_row_steam_and_purrnet_rebinding()
    {
        string controller = Read(
            "src",
            "IdentityResyncController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(
                controller,
                Does.Contain("panel.PlayerItemControllers"));
            Assert.That(
                controller,
                Does.Contain("panel.PlayerSteamIDs"));
            Assert.That(
                controller,
                Does.Contain("panel.PlayerIDs"));
            Assert.That(
                controller,
                Does.Contain("panel.PlayerTransforms"));
            Assert.That(
                controller,
                Does.Contain("SteamMatchmaking.GetLobbyMemberByIndex"));
            Assert.That(
                controller,
                Does.Contain("manager.playerModule.players"));
            Assert.That(
                controller,
                Does.Contain("manager.playerModule.IsPlayerConnected"));
            Assert.That(
                controller,
                Does.Contain("IdentityAuthorityFingerprintPolicy.Build"));
        });
    }

    [Test]
    public void Coverage_reads_authoritative_purrnet_players_and_only_excludes_rows_absent_from_both_authorities()
    {
        string evidence = Read(
            "src",
            "PlayerIdentityEvidenceController.cs");
        string policy = Read(
            "src",
            "Core",
            "PlayerIdentityEvidencePolicy.cs");
        Assert.Multiple(() =>
        {
            Assert.That(evidence, Does.Contain("TryReadCurrentPurrNetPlayers"));
            Assert.That(evidence, Does.Contain("manager.playerModule.IsValidPlayer"));
            Assert.That(evidence, Does.Contain("manager.playerModule.IsPlayerConnected"));
            Assert.That(evidence, Does.Contain("manager.isServer &&"));
            Assert.That(evidence, Does.Contain("currentPurrNetPlayer"));
            Assert.That(evidence, Does.Contain("ActiveNativeRowCount"));
            Assert.That(policy, Does.Contain("isPurrNetConnected == false"));
            Assert.That(evidence, Does.Not.Contain("playerModule.players.Remove"));
            Assert.That(evidence, Does.Not.Contain("panel.PlayerIDs.Remove"));
            Assert.That(evidence, Does.Not.Contain("panel.PlayerSteamIDs.Remove"));
        });
    }

    [Test]
    public void Runtime_generation_observer_is_low_frequency_and_does_not_stringify_purrnet_ids()
    {
        string controller = Read(
            "src",
            "IdentityResyncController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(
                controller,
                Does.Contain("RuntimePollIntervalMilliseconds = 500L"));
            Assert.That(
                controller,
                Does.Not.Contain("playerId?.ToString()"));
            Assert.That(
                controller,
                Does.Contain("playerId.GetHashCode()"));
        });
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(path).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
