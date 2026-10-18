using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RescueSourceContractTests
{
    private static string Root => FindRoot();

    [Test]
    public void Rescue_receiver_is_always_ready_owner_or_helper_authenticated_and_bounded()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string patch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "OptInRescuePatch.cs"));
        string commandPatch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "BlueSageCommandPatch.cs"));

        Assert.That(plugin, Does.Not.Contain("EnableOwnerRescueRequests"));
        Assert.That(plugin, Does.Not.Contain("AllowTrustedHostRescue"));
        Assert.That(plugin, Does.Not.Contain("HandleRescueCommand"));
        Assert.That(patch, Does.Contain("SteamMatchmaking.GetLobbyOwner"));
        Assert.That(patch, Does.Contain("bluesage_rescue_helpers"));
        Assert.That(patch, Does.Contain("bluesage_rescue_longhaul"));
        Assert.That(patch, Does.Not.Contain("/rescue accept"));
        Assert.That(patch, Does.Contain("RescueProtocolPolicy.IsFresh"));
        Assert.That(patch, Does.Contain("SteamMatchmaking.GetLobbyMemberData"));
        Assert.That(patch, Does.Not.Contain("ShouldAllowTrustedHostRescue"));
        Assert.That(patch, Does.Contain("RescueProtocolPolicy.IsLocalTarget(request, localSteam, localPlayerId)"));
        Assert.That(patch, Does.Contain("SeenMemberDataNonces"));
        Assert.That(patch, Does.Contain("RescueProtocolPolicy.ShouldRememberNonce(applyResult)"));
        int applyIndex = patch.IndexOf(
            "ApplyMemberDataRequest(lobby, ownerSteam, senderSteam, request)",
            StringComparison.Ordinal);
        int rememberIndex = patch.IndexOf("RememberNonce(request.Nonce)", StringComparison.Ordinal);
        Assert.That(applyIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(rememberIndex, Is.GreaterThan(applyIndex));
        Assert.That(patch, Does.Contain("RescueFollowLeasePolicy.IsCurrent("));
        Assert.That(patch, Does.Contain("issuerIsCurrentMember"));
        Assert.That(patch, Does.Contain("issuerIsAuthorized"));
        Assert.That(patch, Does.Contain("longHaulIsEnabled"));
        Assert.That(patch, Does.Contain("IsAuthorizedMemberDataSender"));
        Assert.That(patch, Does.Contain("var authorizedSenders = new HashSet<string>(StringComparer.Ordinal) { ownerSteam };"));
        Assert.That(patch, Does.Contain("foreach (string senderSteam in authorizedSenders)"));
        Assert.That(patch, Does.Contain("IsCurrentLobbyMember(lobby, sender)"));
        Assert.That(patch, Does.Contain("ApplyLocalRescuePosition"));
        Assert.That(patch, Does.Contain("MainMovementController.SpeedY = 0f"));
        Assert.That(patch, Does.Contain("MainNetTransform?.ForceSync()"));
        Assert.That(patch, Does.Contain("CooldownSeconds"));
        Assert.That(patch, Does.Contain("MainMovementController"));
        Assert.That(patch, Does.Contain("ownerMatches != 1"));
        Assert.That(patch, Does.Not.Contain("PlayerSteamIDs.IndexOf(ownerSteam)"));
        Assert.That(patch, Does.Not.Contain("GiveOwnership"));
        Assert.That(commandPatch, Does.Not.Contain("OptInRescuePatch.HandleIncoming"));
        Assert.That(commandPatch, Does.Not.Contain("HostVisibilityProtocolPatch.HandleIncoming"));
        string visibility = File.ReadAllText(Path.Combine(Root, "src", "Patches", "HostVisibilityProtocolPatch.cs"));
        Assert.That(visibility, Does.Contain("Dictionary<Renderer, bool>"));
        Assert.That(visibility, Does.Contain("SteamMatchmaking.GetLobbyOwner"));
        Assert.That(visibility, Does.Contain("SteamMatchmaking.GetLobbyMemberData"));
        Assert.That(visibility, Does.Contain("panel.PlayerTransforms"));
        Assert.That(visibility, Does.Contain("HideCurrentVisuals"));
        Assert.That(visibility, Does.Contain("Dictionary<Renderer, bool> RendererStates"));
        Assert.That(visibility, Does.Contain("Dictionary<Canvas, bool> CanvasStates"));
        Assert.That(visibility, Does.Contain("Dictionary<TMP_Text, bool> TextStates"));
        Assert.That(visibility, Does.Contain("if (!RendererStates.ContainsKey(renderer))"));
        Assert.That(visibility, Does.Contain("AccessTools.Field(typeof(PlayerController), \"_canvases\")"));
        Assert.That(visibility, Does.Contain("AccessTools.Field(typeof(PlayerController), \"_playerCanvas\")"));
        Assert.That(visibility, Does.Contain("AccessTools.Field(typeof(PlayerController), \"_playerNameText\")"));
        Assert.That(visibility, Does.Contain("AccessTools.Field(typeof(TextBubbleController), \"_textBubbleCanvas\")"));
        Assert.That(visibility, Does.Contain("AccessTools.Field(typeof(TextBubbleController), \"_textBubble\")"));
        Assert.That(visibility, Does.Contain("AccessTools.Field(typeof(TextBubbleController), \"_nameText\")"));
        Assert.That(visibility, Does.Contain("foreach (Renderer renderer in _hostRoot.GetComponentsInChildren<Renderer>(true))"));
        Assert.That(visibility, Does.Contain("renderer.enabled = false;"));
        Assert.That(visibility, Does.Contain("canvas.enabled = false;"));
        Assert.That(visibility, Does.Contain("text.enabled = false;"));
        Assert.That(visibility, Does.Not.Contain("GetComponentsInChildren<Canvas>")
            .And.Not.Contain("_playerCanvas.SetActive")
            .And.Not.Contain("_textPanel.SetActive")
            .And.Not.Contain("_hostRoot.gameObject.SetActive")
            .And.Not.Contain("PlayerPanelController.I.gameObject.SetActive"));
        Assert.That(visibility, Does.Contain("MemberDataExpirySeconds = 8"));
        Assert.That(visibility, Does.Contain("RestoreAll"));
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
