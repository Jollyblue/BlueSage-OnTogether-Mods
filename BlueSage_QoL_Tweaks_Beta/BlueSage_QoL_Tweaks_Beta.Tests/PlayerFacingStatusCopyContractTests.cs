using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerFacingStatusCopyContractTests
{
    [Test]
    public void Qol_status_routes_compact_copy_to_chat_and_detailed_copy_to_log()
    {
        string source = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("QolStatusTextPolicy.BuildPlayerSummary"));
            Assert.That(source, Does.Contain("BuildDetailedQolStatusMessage"));
            Assert.That(source, Does.Contain("QoL detailed status: "));
            Assert.That(source, Does.Not.Contain("HandledWithMessages(BuildDetailedQolStatusMessage())"));
        });
    }

    [Test]
    public void Community_commands_use_friendly_copy_and_reserve_attestation_path_for_attestation()
    {
        string source = ReadPluginSource();
        int handlerStart = source.IndexOf("private BlueSageCommandResult HandleCommunityBanCommand", StringComparison.Ordinal);
        int handlerEnd = source.IndexOf("private BlueSageCommandResult HandleAuditLogCommand", handlerStart, StringComparison.Ordinal);
        string handler = source.Substring(handlerStart, handlerEnd - handlerStart);

        Assert.Multiple(() =>
        {
            Assert.That(handler, Does.Contain("CommunityBanListController.FriendlyStatusText()"));
            Assert.That(handler, Does.Contain("action == \"attestation\""));
            Assert.That(handler, Does.Not.Contain("action == \"status\" || action == \"?\" || action == \"attestation\""));
            Assert.That(handler, Does.Not.Contain("CommunityBanListController.StatusText(),"));
            Assert.That(handler, Does.Contain("Attestation: local BlueSageQoLModSuite/trackers/community-ban-attestation.json"));
        });
    }

    [Test]
    public void Community_refresh_notification_is_friendly_while_detailed_state_is_logged()
    {
        string source = ReadPluginSource();
        int refreshStart = source.IndexOf("private IEnumerator RunCommunityBanManualRefresh", StringComparison.Ordinal);
        int refreshEnd = source.IndexOf("private BlueSageCommandResult HandleCommunityBanCommand", refreshStart, StringComparison.Ordinal);
        string refresh = source.Substring(refreshStart, refreshEnd - refreshStart);

        Assert.Multiple(() =>
        {
            Assert.That(refresh, Does.Contain("CommunityBanListController.FriendlyStatusText()"));
            Assert.That(refresh, Does.Contain("CommunityBanListController.StatusText()"));
            Assert.That(refresh, Does.Contain("Log?.LogInfo"));
        });
    }

    private static string ReadPluginSource()
    {
        string root = FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
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
