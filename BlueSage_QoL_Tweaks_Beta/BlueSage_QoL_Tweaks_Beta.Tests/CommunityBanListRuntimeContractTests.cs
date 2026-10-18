using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CommunityBanListRuntimeContractTests
{
    [Test]
    public void RuntimeSyncIsOffByDefaultAnyClientOptInLocalAdditiveBoundedAndRollbackSafe()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string controller = File.ReadAllText(Path.Combine(root, "src", "CommunityBanListController.cs"));
        string sourceClient = File.ReadAllText(Path.Combine(root, "src", "CommunityBanSourceClient.cs"));
        string adapter = File.ReadAllText(Path.Combine(root, "src", "NativeBanStoreAdapter.cs"));
        string policy = File.ReadAllText(Path.Combine(root, "src", "Core", "CommunityBanSyncPolicy.cs"));
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        string migration = File.ReadAllText(Path.Combine(root, "src", "Core", "ConfigMigrationPolicy.cs"));
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        int migrationCall = plugin.IndexOf("ApplyOneTimeConfigMigration();", StringComparison.Ordinal);
        int maintenanceCall = plugin.IndexOf("StartCommunityBanListMaintenance();", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("EnableCommunityBanListSync"));
            Assert.That(plugin, Does.Contain("publicDefaults.CommunitySafetyList"));
            Assert.That(plugin, Does.Contain("CommunityBanAnyClientOptInV1Migration"));
            Assert.That(migration, Does.Contain("community-ban-any-client-opt-in-v1"));
            Assert.That(migrationCall, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                maintenanceCall,
                Is.GreaterThan(migrationCall),
                "The legacy default-on value must be reset before any network fetch or local BanData maintenance can start.");
            Assert.That(plugin, Does.Contain("HandleCommunityBanCommand"));
            Assert.That(controller, Does.Not.Contain("CanManageHelpers()"));
            Assert.That(sourceClient, Does.Contain("raw.githubusercontent.com/andrewlimforfun/ot-community-banlist/master/BanData.txt"));
            Assert.That(controller, Does.Contain("MaxPayloadBytes"));
            Assert.That(controller, Does.Contain("CommunityBanListMergePolicy.TryBuildAdditions"));
            Assert.That(controller, Does.Contain("CommunityBanSourceClient"));
            Assert.That(controller, Does.Contain("NativeBanStoreAdapter"));
            Assert.That(adapter, Does.Contain("SaveBanData()"));
            Assert.That(
                CountOccurrences(adapter, "SaveBanData()"),
                Is.GreaterThanOrEqualTo(2),
                "A failed native persistence attempt must retry persistence after rolling the in-memory pairs back.");
            Assert.That(adapter, Does.Contain("RemoveAt"));
            Assert.That(adapter, Does.Not.Contain(".Clear()"));
            Assert.That(sourceClient, Does.Not.Contain("BanRPC").And.Not.Contain("Kick"));
            Assert.That(policy, Does.Contain("BuildStatusText"));
            Assert.That(policy, Does.Contain("BuildAttestationJson"));
            Assert.That(menu, Does.Contain("On for fresh installs"));
            Assert.That(menu, Does.Contain("preserves every existing row"));
            Assert.That(readme, Does.Contain("Community Safety and the Performance Overlay start on"));
            Assert.That(readme, Does.Not.Contain("verified current host only"));
            Assert.That(readme, Does.Not.Contain("Verified hosts may enable the default-on additive community BanData"));
        });
    }

    private static int CountOccurrences(string value, string fragment)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
