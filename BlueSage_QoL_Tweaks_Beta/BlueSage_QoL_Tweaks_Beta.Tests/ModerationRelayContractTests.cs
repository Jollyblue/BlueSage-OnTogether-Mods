using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ModerationRelayContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Server_relay_consumes_and_rejects_legacy_chat_protocol_without_actions()
    {
        string patch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "ModerationRelayPatch.cs"));
        string controller = File.ReadAllText(Path.Combine(Root, "src", "SteamIdModerationController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("HandleRPCGenerated_0"));
            Assert.That(patch, Does.Contain("HarmonyPriority(Priority.First)"));
            Assert.That(patch, Does.Contain("if (!asServer)"));
            Assert.That(patch, Does.Contain("ModerationProtocolPolicy.Prefix"));
            Assert.That(patch, Does.Contain("recognizedBlueSagePacket = true"));
            Assert.That(patch, Does.Contain("return !recognizedBlueSagePacket"));
            Assert.That(patch, Does.Contain("Rejected a legacy chat-shaped BlueSage moderation request"));
            Assert.That(patch, Does.Not.Contain("HandleServerRequest(text"));
            Assert.That(patch, Does.Not.Contain("HandleServerResolutionRequest(text"));
            Assert.That(controller, Does.Contain("PollHostMemberDataRequests"));
            Assert.That(controller, Does.Contain("ModerationAuthorizationPolicy.IsAuthorizedIssuer"));
        });
    }

    [Test]
    public void Public_clone_warning_does_not_expose_client_local_incident_number()
    {
        string cloneShield = File.ReadAllText(Path.Combine(Root, "src", "CloneShieldController.cs"));

        Assert.That(cloneShield, Does.Contain("string message = \"[Clone Shield] \""));
        Assert.That(cloneShield, Does.Not.Contain("string message = \"[Clone Shield \" + incidentId"));
    }

    [Test]
    public void Only_host_writes_paired_native_ban_state_and_targets_exact_PlayerID()
    {
        string controller = File.ReadAllText(Path.Combine(Root, "src", "SteamIdModerationController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("NetworkManager.main.isHost"));
            Assert.That(controller, Does.Contain("BanServerPlayers"));
            Assert.That(controller, Does.Contain("BanServerPlayerNicks"));
            Assert.That(controller, Does.Contain("SaveBanData()"));
            Assert.That(controller, Does.Contain("BanRPC(panel.PlayerIDs[targetIndex], true)"));
            Assert.That(controller, Does.Contain("GetNumLobbyMembers"));
            Assert.That(controller, Does.Contain("CloneIncidentLedger.IsProtectedVictim"));
            Assert.That(controller, Does.Contain("if (string.IsNullOrWhiteSpace(name)) return \"Steam …\" + Suffix(steamId)"));
        });
    }

    [Test]
    public void Public_commands_and_aliases_are_registered()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        Assert.That(plugin, Does.Contain("\"sidlist\""));
        Assert.That(plugin, Does.Contain("\"sidban\""));
        Assert.That(plugin, Does.Contain("\"sidunban\""));
        Assert.That(plugin, Does.Contain("\"unbansid\""));
        Assert.That(plugin, Does.Contain("HandleSidBanCommand"));
        Assert.That(plugin, Does.Contain("HandleSidUnbanCommand"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
