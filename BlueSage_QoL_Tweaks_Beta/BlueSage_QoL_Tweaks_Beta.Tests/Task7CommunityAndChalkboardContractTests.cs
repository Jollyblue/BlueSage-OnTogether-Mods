using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class Task7CommunityAndChalkboardContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Community_ban_refresh_is_opt_in_local_additive_and_never_a_live_moderation_action()
    {
        string plugin = Read("src", "Plugin.cs");
        string controller = Read("src", "CommunityBanListController.cs");
        string sourceClient = ReadOptional("src", "CommunityBanSourceClient.cs");
        string adapter = ReadOptional("src", "NativeBanStoreAdapter.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("HandleCommunityBanCommand"));
            Assert.That(plugin, Does.Contain("CommunityBanListController.StatusText()"));
            Assert.That(plugin, Does.Not.Contain("&& isVerifiedHost"));
            Assert.That(controller, Does.Contain("CommunityBanSourceClient"));
            Assert.That(controller, Does.Contain("NativeBanStoreAdapter"));
            Assert.That(controller, Does.Not.Contain("CanManageHelpers()"));
            Assert.That(sourceClient, Does.Not.Contain("BanRPC").And.Not.Contain("Kick").And.Not.Contain("CanManageHelpers"));
            Assert.That(adapter, Does.Contain("SaveBanData()"));
            Assert.That(adapter, Does.Contain("Rollback"));
            Assert.That(adapter, Does.Not.Contain("BanRPC").And.Not.Contain("Kick").And.Not.Contain("Helper"));
        });
    }

    [Test]
    public void Community_ban_status_and_attestation_are_bounded_and_identity_free()
    {
        string policy = ReadOptional("src", "Core", "CommunityBanSyncPolicy.cs");
        string controller = Read("src", "CommunityBanListController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Contain("LastAttemptUtc"));
            Assert.That(policy, Does.Contain("LastSuccessUtc"));
            Assert.That(policy, Does.Contain("HttpResult"));
            Assert.That(policy, Does.Contain("SourceRevision"));
            Assert.That(policy, Does.Contain("SourceHash"));
            Assert.That(policy, Does.Contain("AcceptedCount"));
            Assert.That(policy, Does.Contain("NextAttemptUtc"));
            Assert.That(policy, Does.Contain("BoundedError"));
            Assert.That(policy, Does.Contain("BuildAttestationJson"));
            Assert.That(controller, Does.Contain("WriteCurrentAttestation"));
            Assert.That(policy, Does.Not.Contain("SteamID").And.Not.Contain("Nickname").And.Not.Contain("PlayerName"));
        });
    }

    [Test]
    public void Chalkboard_commands_expose_exact_local_and_authorized_actions_with_truthful_results()
    {
        string plugin = Read("src", "Plugin.cs");
        string typeahead = Read("src", "CommandTypeaheadController.cs");
        string menu = Read("src", "QolMenuWindow.cs");
        string controller = Read("src", "ChalkboardPersistenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ChalkboardPersistenceController.View("));
            Assert.That(plugin, Does.Contain("ChalkboardPersistenceController.Delete("));
            Assert.That(plugin, Does.Contain("ChalkboardPersistenceController.RefreshMap()"));
            Assert.That(typeahead, Does.Contain("Choice(command, \"view\""));
            Assert.That(typeahead, Does.Contain("Choice(command, \"delete\""));
            Assert.That(typeahead, Does.Contain("Choice(command, \"refresh\""));
            Assert.That(menu, Does.Contain("View, save, list, delete, and refresh stay on this computer"));
            Assert.That(menu, Does.Contain("Use /chalk boards for 0 Stage, 1 Classroom A, and 2 Classroom B"));
            Assert.That(menu, Does.Not.Contain("fail-closed rollback"));
            Assert.That(controller, Does.Contain("authority="));
            Assert.That(controller, Does.Contain("compatibleRecipients="));
            Assert.That(controller, Does.Contain("snapshot="));
            Assert.That(controller, Does.Contain("rollback="));
            Assert.That(controller, Does.Contain("vanillaObserver="));
            Assert.That(controller, Does.Contain("no authoritative drawing-owner identity"));
        });
    }

    [Test]
    public void Destructive_chalk_actions_require_fresh_exact_catalog_and_lock_after_unresolved_rollback()
    {
        string catalog = ReadOptional("src", "Core", "ChalkboardCatalogPolicy.cs");
        string command = ReadOptional("src", "Core", "ChalkboardCommandPolicy.cs");
        string controller = Read("src", "ChalkboardPersistenceController.cs");
        string transport = ReadOptional("src", "ChalkboardSnapshotTransportController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(catalog, Does.Contain("River/Stage"));
            Assert.That(catalog, Does.Contain("Classroom board A (face A)"));
            Assert.That(catalog, Does.Contain("Classroom board B (face B)"));
            Assert.That(catalog, Does.Contain("MaxCatalogIndex = 2"));
            Assert.That(command, Does.Contain("FreshListSeconds"));
            Assert.That(command, Does.Contain("TryAuthorizeDestructiveAction"));
            Assert.That(controller, Does.Contain("_lastListedBoardSetSignature"));
            Assert.That(controller, Does.Contain("_rollbackUnresolved"));
            Assert.That(controller, Does.Contain("TryAuthorizeDestructiveAction"));
            Assert.That(transport, Does.Contain("MarkRollbackUnresolved"));
            Assert.That(transport, Does.Contain("DestructiveActionsBlocked"));
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string ReadOptional(params string[] parts)
    {
        string path = Path.Combine(new[] { Root }.Concat(parts).ToArray());
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
