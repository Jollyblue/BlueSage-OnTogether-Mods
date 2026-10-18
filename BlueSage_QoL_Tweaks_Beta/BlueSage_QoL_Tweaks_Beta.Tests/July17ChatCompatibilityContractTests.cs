using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed partial class July17ChatCompatibilityContractTests
{
    private static readonly string QolRoot = FindProjectRoot("BlueSage_QoL_Tweaks_Beta.csproj");
    private static readonly string LowLevelRoot = Directory.GetParent(QolRoot)?.FullName
        ?? throw new DirectoryNotFoundException("LowLevel-RE root not found.");

    [Test]
    public void Qol_chat_copy_targets_the_new_player_message_signature()
    {
        string patch = File.ReadAllText(Path.Combine(QolRoot, "src", "Patches", "ChatCopyMessagePatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(
                patch,
                Does.Contain("[HarmonyPatch(\"AddMessageUI\", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]"),
                "The July 17 game build added the sendByPlayer argument.");
            Assert.That(
                patch,
                Does.Contain("bool sendByPlayer"),
                "The postfix should name the new argument so future audits preserve its moderation-card meaning.");
        });
    }

    [Test]
    public void Qol_startup_keeps_non_patch_controllers_alive_if_a_future_optional_patch_drifts()
    {
        string plugin = File.ReadAllText(Path.Combine(QolRoot, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ApplyHarmonyPatchesFailOpen();"));
            Assert.That(plugin, Does.Contain("private void ApplyHarmonyPatchesFailOpen()"));
            Assert.That(
                plugin,
                Does.Contain("Continuing with compatible controllers"),
                "A single future Harmony target drift must not disable Home, Insert, sweep, or the menu controllers.");
        });
    }

    [Test]
    public void PlayerLimitLift_chat_relay_invokes_player_messages_with_the_new_flag()
    {
        string relay = File.ReadAllText(Path.Combine(
            LowLevelRoot,
            "BlueSagePatched_PlayerLimitLift",
            "src",
            "Patches",
            "TextChannelManagerPatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(
                relay,
                Does.Contain("typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool)"),
                "Reflection must bind the exact July 17 AddMessageUI overload.");
            Assert.That(
                relay,
                Does.Contain("new object[] { senderName, message, isLocal, colorIndex, true }"),
                "The local-player relay path must preserve the new vanilla local-message flag.");
            Assert.That(
                relay,
                Does.Contain("EnsureChatRelayCompatible()"),
                "PlayerLimitLift must fail open to vanilla chat when a future game build changes either relay target.");
            Assert.That(
                relay,
                Does.Contain("return true;"),
                "Compatibility failure must return control to the game's native chat handler.");
        });
    }

    [Test]
    public void Public_chat_copy_preserves_plain_left_click_and_adds_modifier_left_fallback_on_the_native_button()
    {
        string projectRoot = QolRoot;
        string patch = File.ReadAllText(Path.Combine(projectRoot, "src", "Patches", "ChatCopyMessagePatch.cs"));
        string interactionPath = Path.Combine(projectRoot, "src", "Patches", "ChatMessageInteractionController.cs");
        string interaction = File.Exists(interactionPath)
            ? File.ReadAllText(interactionPath)
            : patch;
        string plugin = File.ReadAllText(Path.Combine(projectRoot, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(interaction, Does.Contain("PointerEventData.InputButton.Right"));
            Assert.That(interaction, Does.Contain("button.onClick.AddListener"));
            Assert.That(interaction, Does.Contain("HandleNativeButtonClick"));
            Assert.That(plugin, Does.Contain("Ctrl-left-click"));
            Assert.That(plugin, Does.Contain("Shift-left-click"));
            Assert.That(plugin, Does.Not.Contain("plain click copies"));
        });
    }

    // 2026-10-17: a companion mod's never-applied chat patches were removed as dead code (docs/DEAD_CODE_REGISTER.md).
    [TestCase("BlueSage_QoL_Tweaks_Beta")]
    public void Chat_link_visuals_follow_the_games_generic_tmp_text_contract(string projectName)
    {
        string projectRoot = projectName == "BlueSage_QoL_Tweaks_Beta"
            ? QolRoot
            : Path.Combine(LowLevelRoot, projectName);
        string patch = File.ReadAllText(Path.Combine(projectRoot, "src", "Patches", "ChatCopyMessagePatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(
                patch,
                Does.Contain("TMP_Text[] labels = messageRow.GetComponentsInChildren<TMP_Text>(true);"),
                "Build 24259292 declares the live chat prefab as TMP_Text, so visual decoration must accept every TMP text subtype.");
            Assert.That(
                patch,
                Does.Not.Contain("GetComponentsInChildren<TextMeshProUGUI>"),
                "A TextMeshProUGUI-only query silently misses the current live chat row while leaving click handling functional.");
        });
    }

    [TestCase("BlueSage_QoL_Tweaks_Beta")]
    public void Every_chat_row_decorator_targets_the_new_five_argument_signature(string projectName)
    {
        string projectRoot = projectName == "BlueSage_QoL_Tweaks_Beta"
            ? QolRoot
            : Path.Combine(LowLevelRoot, projectName);
        string signature = "typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool)";

        foreach (string fileName in new[]
                 {
                     "ChatReadabilityPatch.cs",
                     "ChatTimestampPatch.cs",
                     "MentionHighlighterPatch.cs",
                 })
        {
            string patch = File.ReadAllText(Path.Combine(projectRoot, "src", "Patches", fileName));
            Assert.That(
                patch,
                Does.Contain(signature),
                $"{projectName}/{fileName} must bind the July 17 AddMessageUI signature exactly.");
            Assert.That(
                patch,
                Does.Not.Contain("typeof(string), typeof(string), typeof(bool), typeof(int))]"),
                $"{projectName}/{fileName} must not retain the removed four-argument overload.");
        }
    }

    [Test]
    public void Qol_id_card_extensions_share_one_fresh_exact_player_controller()
    {
        string controller = File.ReadAllText(Path.Combine(QolRoot, "src", "NativeIdCardController.cs"));
        string dispatcher = File.ReadAllText(Path.Combine(QolRoot, "src", "Patches", "EnhancedPlayerPanelPatch.cs"));
        string panel = File.ReadAllText(Path.Combine(QolRoot, "src", "Patches", "PlayerPanelSurfaceController.cs"));
        string miniMap = File.ReadAllText(Path.Combine(QolRoot, "src", "Patches", "MiniMapPlayerLabelPatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(dispatcher, Does.Contain("PlayerPanelSurfaceController.RefreshRow"));
            Assert.That(panel, Does.Contain("NativeIdCardController.TryOpen"));
            Assert.That(miniMap, Does.Contain("NativeIdCardController.TryOpen"));
            Assert.That(dispatcher, Does.Not.Contain("ButtonOpenIdCheck"));
            Assert.That(panel, Does.Not.Contain("ButtonOpenIdCheck"));
            Assert.That(miniMap, Does.Not.Contain("ButtonOpenIdCheck"));
            Assert.That(dispatcher, Does.Not.Contain("TemporaryPhoto"));
            Assert.That(panel, Does.Not.Contain("TemporaryPhoto"));
            Assert.That(miniMap, Does.Not.Contain("TemporaryPhoto"));
            Assert.That(controller, Does.Contain("temporaryPhoto.CapturePhoto(target.PlayerController)"));
            Assert.That(controller, Does.Contain("NativeIdCardInvocationGuard.Invoke"));
            Assert.That(controller, Does.Contain("ReferenceEquals(panel.PlayerControllers[index], playerController)"));
            Assert.That(controller, Does.Not.Contain("ButtonOpenIdCheck("));
            Assert.That(controller, Does.Not.Contain("ButtonOpenIdCheck(playerName"));
            Assert.That(controller, Does.Not.Contain("ButtonOpenIdCheck(target.DisplayNameRaw"));
        });
    }

    [Test]
    public void Optional_harmony_patch_drift_isolated_to_one_patch_type()
    {
        string plugin = File.ReadAllText(Path.Combine(QolRoot, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ApplyHarmonyPatchTypeFailOpen"));
            Assert.That(plugin, Does.Contain("AccessTools.GetTypesFromAssembly"));
            Assert.That(plugin, Does.Contain("_harmony.CreateClassProcessor(patchType).Patch();"));
            Assert.That(plugin, Does.Not.Contain("_harmony.PatchAll();"));
            Assert.That(plugin, Does.Contain("patch type"));
        });
    }

    private static string FindProjectRoot(string projectFile)
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, projectFile)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException($"Could not find {projectFile}.");
    }
}
