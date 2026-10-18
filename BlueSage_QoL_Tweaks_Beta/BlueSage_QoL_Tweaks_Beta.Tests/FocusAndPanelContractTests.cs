using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class FocusAndPanelContractTests
{
    [Test]
    public void PluginRegistersFocusAnywhereToggle()
    {
        string pluginSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));

        Assert.That(pluginSource, Does.Contain("EnableFocusAnywhere = Config.Bind(\"Focus Anywhere\", \"EnableFocusAnywhere\", true"));
        Assert.That(pluginSource, Does.Contain("\"focusanywhere\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"fa\" }"));
        Assert.That(pluginSource, Does.Contain("ShouldApplyFocusAnywhere"));
    }

    [Test]
    public void FocusAnywherePatchKeepsOldBehaviorBehindBlueSageToggle()
    {
        string patchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "FocusAnywherePatch.cs"));

        Assert.That(patchSource, Does.Contain("IsReadyForFreeFocus"));
        Assert.That(patchSource, Does.Contain("IsReadyForAreaFocus"));
        Assert.That(patchSource, Does.Contain("PlayerFocusController"));
        Assert.That(patchSource, Does.Contain("IsFocusable"));
        Assert.That(patchSource, Does.Contain("get_IsLake"));
        Assert.That(patchSource, Does.Contain("Plugin.ShouldApplyFocusAnywhere"));
        Assert.That(patchSource, Does.Contain("__result = true"));
        Assert.That(patchSource, Does.Contain("__result = false"));
    }

    [Test]
    public void EnhancedPlayerPanelPatchUsesCurrentPlayerPanelIdentifiers()
    {
        string patchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "EnhancedPlayerPanelPatch.cs"));
        string controllerSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "PlayerPanelSurfaceController.cs"));
        string resolverSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "NativeIdCardController.cs"));
        string rosterShapePolicySource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Core", "NativeRosterShapePolicy.cs"));

        Assert.That(patchSource, Does.Contain("PlayerItemController"));
        Assert.That(patchSource, Does.Contain("\"Init\""));
        Assert.That(patchSource, Does.Contain("PlayerPanelSurfaceController.RefreshRow"));
        Assert.That(patchSource, Does.Contain("Plugin.ShouldApplyEnhancedPlayerPanel"));
        Assert.That(patchSource, Does.Not.Contain("Group_Buttons"));
        Assert.That(patchSource, Does.Not.Contain("RowHasPomoSurface"));
        Assert.That(patchSource, Does.Not.Contain("ButtonOpenIdCheck"));

        Assert.That(controllerSource, Does.Contain("Button_BlueSageMention"));
        Assert.That(controllerSource, Does.Contain("Button_BlueSageNativeId"));
        Assert.That(controllerSource, Does.Contain("without interrupting native UI"));
        Assert.That(controllerSource, Does.Contain("BlueSageEnhancedPanelButtonMarker"));
        Assert.That(controllerSource, Does.Contain("BlueSagePlayerPanelSurfaceMarker"));
        Assert.That(controllerSource, Does.Contain("button.onClick.RemoveAllListeners();"));
        Assert.That(controllerSource, Does.Contain("NativeIdCardController.TryOpen"));
        Assert.That(controllerSource, Does.Contain("NativeIdCardController.TryResolveCurrentRow"));
        Assert.That(controllerSource, Does.Contain("MentionNamePolicy.ToMentionInsertionToken"));
        Assert.That(controllerSource, Does.Not.Contain("playerName"));
        Assert.That(controllerSource, Does.Not.Contain("RowHasPomoSurface"));

        Assert.That(resolverSource, Does.Contain("PlayerIdentityEvidenceController.TryResolveConfirmed"));
        Assert.That(resolverSource, Does.Contain("PlayerItemControllers"));
        Assert.That(resolverSource, Does.Contain("NativeRosterShapePolicy.HasAlignedActivePrefix"));
        Assert.That(rosterShapePolicySource, Does.Contain("pooledRowCount >= steamIdCount"));
        Assert.That(rosterShapePolicySource, Does.Not.Contain("pooledRowCount == steamIdCount"));
        Assert.That(resolverSource, Does.Contain("NativeIdCardResolutionPolicy.Evaluate"));
        Assert.That(resolverSource, Does.Contain("temporaryPhoto.CapturePhoto(target.PlayerController)"));
        Assert.That(resolverSource, Does.Not.Contain("ButtonOpenIdCheck("));
        Assert.That(resolverSource, Does.Contain("NativeIdCardInvocationGuard.Invoke"));
        Assert.That(resolverSource, Does.Not.Contain("ReadIdInfoName"));
        Assert.That(resolverSource, Does.Not.Contain("DisplayName =="));
    }

    [Test]
    public void MiniMapIdClickPatchIsLocalGuardedDefaultOnAndLabelFree()
    {
        string pluginSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));
        string patchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "MiniMapPlayerLabelPatch.cs"));

        Assert.That(pluginSource, Does.Contain("EnableMiniMapPlayerLabels = Config.Bind(\"MiniMap\", \"EnableMiniMapPlayerLabels\", true"));
        Assert.That(pluginSource, Does.Contain("ShouldApplyMiniMapPlayerLabels"));
        Assert.That(pluginSource, Does.Contain("\"minimaplabels\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"mml\" }"));
        Assert.That(pluginSource, Does.Contain("MiniMap ID Click"));
        Assert.That(pluginSource, Does.Contain("Floating name labels were removed"));
        Assert.That(patchSource, Does.Contain("[HarmonyPatch(typeof(MapManager), \"UpdatePosition\")]"));
        Assert.That(patchSource, Does.Contain("_playerIcons"));
        Assert.That(patchSource, Does.Contain("Plugin.ShouldApplyMiniMapPlayerLabels"));
        Assert.That(patchSource, Does.Contain("panel.IDInfos"));
        Assert.That(patchSource, Does.Contain("panel.PlayerSteamIDs"));
        Assert.That(patchSource, Does.Contain("panel.PlayerIDs"));
        Assert.That(patchSource, Does.Contain("panel.PlayerTransforms"));
        Assert.That(patchSource, Does.Contain("MiniMapIdCardClickHandler"));
        Assert.That(patchSource, Does.Contain("IPointerClickHandler"));
        Assert.That(patchSource, Does.Contain("NativeIdCardController.TryOpen"));
        Assert.That(patchSource, Does.Contain("OpenIdCardFromMiniMap"));
        Assert.That(patchSource, Does.Not.Contain("ButtonOpenIdCheck"));
        Assert.That(patchSource, Does.Not.Contain("BlueSageMiniMapNameLabel"));
        Assert.That(patchSource, Does.Not.Contain("TextMeshProUGUI"));
        Assert.That(patchSource, Does.Not.Contain("GetOrCreateLabel"));
        Assert.That(patchSource, Does.Not.Contain("enableWordWrapping"));
        Assert.That(patchSource, Does.Not.Contain("SendMessageAsync"));
        Assert.That(patchSource, Does.Not.Contain("ServerRpc"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find BlueSage_QoL_Tweaks_Beta project root.");
    }
}
