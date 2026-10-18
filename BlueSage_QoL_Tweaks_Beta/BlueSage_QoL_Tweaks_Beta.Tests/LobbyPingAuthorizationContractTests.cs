using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class LobbyPingAuthorizationContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Lobby_mass_ping_uses_authoritative_identity_and_host_gated_commands()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string patch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "MentionHighlighterPatch.cs"));
        string senderContext = File.ReadAllText(Path.Combine(Root, "src", "Patches", "ChatSenderContextPatch.cs"));
        string typeahead = File.ReadAllText(Path.Combine(Root, "src", "CommandTypeaheadController.cs"));
        Assert.That(patch, Does.Contain("Plugin.IsLobbyMassPingAuthorized(ChatSenderContextPatch.CurrentSenderSteamId)"));
        Assert.That(patch, Does.Contain("return match.Value"));
        Assert.That(senderContext, Does.Contain("SendMessageAsync_Original_0"));
        Assert.That(senderContext, Does.Contain("RPCInfo info"));
        Assert.That(senderContext, Does.Not.Contain("ContainsLobbyPingToken"));
        Assert.That(senderContext, Does.Contain("CurrentSenderContext"));
        Assert.That(senderContext, Does.Contain("InputFieldLimitPolicy.AbsoluteMaxChatCharacters * sizeof(char)"));
        Assert.That(senderContext, Does.Contain("AuthoritativeChatSenderPolicy.TryResolveUniqueIndex("));
        Assert.That(senderContext, Does.Contain("info.sender"));
        Assert.That(senderContext, Does.Contain("panel.PlayerIDs"));
        Assert.That(senderContext, Does.Contain("PlayerIdentityEvidenceController.TryResolveRosterIndexFresh"));
        Assert.That(senderContext, Does.Contain("[HarmonyFinalizer"));
        Assert.That(senderContext, Does.Contain("if (!_captureFailureLogged)"));
        Assert.That(plugin, Does.Not.Contain("senderIndex <= 0"));
        Assert.That(plugin, Does.Contain("this function is host only. You are not the current lobby host"));
        Assert.That(plugin, Does.Contain("SteamFriends.GetFriendPersonaName"));
        Assert.That(plugin, Does.Contain("SteamIdModerationController.IsAuthorizedCurrentIssuer"));
        Assert.That(plugin, Does.Contain("UnifiedHelperPolicy.MergeForOwner"));
        Assert.That(typeahead, Does.Contain("Choice(\"helper\", \"reset\""));
    }
}
