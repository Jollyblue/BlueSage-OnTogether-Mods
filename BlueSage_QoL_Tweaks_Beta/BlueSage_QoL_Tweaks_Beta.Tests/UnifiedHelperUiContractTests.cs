using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class UnifiedHelperUiContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void One_host_scoped_helper_list_drives_ping_and_moderation_authorization()
    {
        string plugin = Read("src", "Plugin.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("\"Host-managed helpers: add, remove, list, or reset\""));
            Assert.That(plugin, Does.Contain("new[] { \"lobbyhelper\", \"lobbymod\", \"modhelper\", \"moderationhelper\" }"));
            Assert.That(plugin, Does.Contain("UnifiedHelperPolicy.MergeForOwner"));
            Assert.That(plugin, Does.Contain("SteamIdModerationController.IsAuthorizedCurrentIssuer"));
            Assert.That(moderation, Does.Contain("HelpersKey = \"bluesage_helpers\""));
            Assert.That(moderation, Does.Contain("LegacyHelpersKey = \"bluesage_mod_helpers\""));
        });
    }

    [Test]
    public void Public_host_mirrors_helpers_to_the_legacy_023_rescue_receiver_key()
    {
        string moderation = Read("src", "SteamIdModerationController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(moderation, Does.Contain("RescueHelpersKey = \"bluesage_rescue_helpers\""));
            Assert.That(moderation, Does.Contain("SteamMatchmaking.SetLobbyData(lobby, RescueHelpersKey, value)"));
            Assert.That(moderation, Does.Contain("SteamMatchmaking.SetLobbyData(lobby, RescueHelpersKey, string.Empty)"));
        });
    }

    [Test]
    public void Lobby_safety_menu_uses_exact_selected_ids_and_paged_direct_actions()
    {
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("DrawTabButton(4, isHost ? \"Host Tools\" : \"Safety\""));
            Assert.That(menu, Does.Contain("HELPER DETECTED  •  Waiting for compatible host capability"));
            Assert.That(menu, Does.Contain("Protected player details and actions remain locked"));
            Assert.That(menu, Does.Contain("const int pageSize = 8;"));
            Assert.That(menu, Does.Contain("SteamID64: \" + selectedRow.SteamId"));
            Assert.That(menu, Does.Contain("Copy SteamID64"));
            Assert.That(menu, Does.Contain("Open Steam Profile"));
            Assert.That(menu, Does.Contain("Add Helper"));
            Assert.That(menu, Does.Contain("Preview SID Ban"));
            Assert.That(menu, Does.Contain("Resolve for Lobby"));
            Assert.That(menu, Does.Contain("Dismiss Locally"));
            Assert.That(menu, Does.Contain("Clear Closed History"));
        });
    }

    [Test]
    public void Host_and_helpers_are_blocked_before_a_ban_preview_is_created()
    {
        string menu = Read("src", "QolMenuWindow.cs");
        string plugin = Read("src", "Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("bool isProtectedRole = isSelfRow || isHostRow || isHelperRow || isProtectedVictim;"));
            Assert.That(menu, Does.Contain("GUI.enabled = !isProtectedRole &&"));
            Assert.That(menu, Does.Contain("moderationSurface.CanArmHostRemove"));
            Assert.That(menu, Does.Contain("Protected Self").And.Contain("Protected Host").And.Contain("Protected Helper"));

            int handlerStart = plugin.IndexOf("private BlueSageCommandResult HandleSidBanCommand", StringComparison.Ordinal);
            int handlerEnd = plugin.IndexOf("private BlueSageCommandResult HandleSidUnbanCommand", handlerStart, StringComparison.Ordinal);
            int protectedRoleGate = plugin.IndexOf("SteamIdModerationController.IsAuthorizedCurrentIssuer(target.SteamId)", handlerStart, StringComparison.Ordinal);
            int pendingAction = plugin.IndexOf("_pendingModerationAction = BuildPendingModeration(\"ban\"", protectedRoleGate, StringComparison.Ordinal);
            Assert.That(handlerStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(protectedRoleGate, Is.GreaterThan(handlerStart));
            Assert.That(protectedRoleGate, Is.LessThan(handlerEnd));
            Assert.That(pendingAction, Is.GreaterThan(protectedRoleGate), "Host/Helper protection must run before creating a pending ban preview.");
            Assert.That(pendingAction, Is.LessThan(handlerEnd));
            Assert.That(plugin, Does.Contain("No preview or pending action was created."));
        });
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
