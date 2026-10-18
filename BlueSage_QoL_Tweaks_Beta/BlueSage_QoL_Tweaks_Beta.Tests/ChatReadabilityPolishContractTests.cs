using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatReadabilityPolishContractTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Test]
    public void Chat_readability_has_soft_default_intensity_and_player_menu_control()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string typeahead = File.ReadAllText(Path.Combine(ProjectRoot, "src", "CommandTypeaheadController.cs"));
        string policy = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Core", "ChatReadabilityStylePolicy.cs"));

        Assert.That(policy, Does.Contain("DefaultOutlineIntensityPercent = 75"));
        Assert.That(policy, Does.Contain("NormalizeHexColor"));
        Assert.That(plugin, Does.Contain("ChatOutlineIntensity = Config.Bind(\"Chat Readability\", \"ChatOutlineIntensity\", ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent"));
        Assert.That(plugin, Does.Contain("ChatOutlineColor = Config.Bind(\"Chat Readability\", \"ChatOutlineColor\", \"000000\""));
        Assert.That(plugin, Does.Contain("BlackChatOutlineColor = Config.Bind(\"Chat Readability\", \"BlackChatOutlineColor\", \"C0C0C0\""));
        Assert.That(plugin, Does.Contain("BlackNamesOutline = Config.Bind(\"Chat Readability\", \"BlackNamesOutline\", true"));
        Assert.That(plugin, Does.Contain("ChatUiScalePercent = Config.Bind(\"Chat Readability\", \"ChatUiScalePercent\", ChatAccessibilityScalePolicy.DefaultScalePercent"));
        Assert.That(plugin, Does.Contain("LockedChatUiScalePercent => ChatAccessibilityScalePolicy.ClampScalePercent"));
        Assert.That(plugin, Does.Contain("ChatFontSizePercent = Config.Bind(\"Chat Readability\", \"ChatFontSizePercent\", ChatFontSizePolicy.DefaultFontSizePercent"));
        Assert.That(plugin, Does.Contain("LockedChatFontSizePercent => ChatFontSizePolicy.ClampFontSizePercent"));
        Assert.That(plugin, Does.Contain("ChatWindowHeightPercent = Config.Bind(\"Chat Readability\", \"ChatWindowHeightPercent\", ChatWindowHeightPolicy.DefaultHeightPercent"));
        Assert.That(plugin, Does.Contain("LockedChatWindowHeightPercent => ChatWindowHeightPolicy.ClampHeightPercent"));
        Assert.That(plugin, Does.Contain("LockedChatHistoryRows => ChatHistoryLimitPolicy.ClampRows"));
        Assert.That(plugin, Does.Contain("ChatHistoryRows = Config.Bind"));
        Assert.That(plugin, Does.Contain("\"chathistory\""));
        Assert.That(plugin, Does.Contain("HandleChatHistoryCommand"));
        Assert.That(plugin, Does.Contain("ShouldApplyBlackNamesOutline"));
        Assert.That(plugin, Does.Contain("LockedChatOutlineIntensityPercent => ChatReadabilityStylePolicy.ClampOutlineIntensityPercent"));
        Assert.That(plugin, Does.Contain("LockedChatOutlineColorHex"));
        Assert.That(plugin, Does.Contain("LockedBlackChatOutlineColorHex"));
        Assert.That(plugin, Does.Contain("\"chatoutline\""));
        Assert.That(plugin, Does.Contain("\"chatoutlinecolor\""));
        Assert.That(plugin, Does.Contain("\"blackoutlinecolor\""));
        Assert.That(plugin, Does.Contain("\"chatscale\""));
        Assert.That(plugin, Does.Contain("\"chatresize\""));
        Assert.That(plugin, Does.Contain("\"chatfont\""));
        Assert.That(plugin, Does.Contain("\"chatfontsize\""));
        Assert.That(plugin, Does.Contain("\"chatheight\""));
        Assert.That(plugin, Does.Contain("\"chatvanilla\""));
        Assert.That(plugin, Does.Contain("HandleChatVanillaCommand"));
        Assert.That(menu, Does.Contain("Chat outline intensity"));
        Assert.That(menu, Does.Contain("Whole chat UI scale"));
        Assert.That(menu, Does.Contain("Chat font size"));
        Assert.That(menu, Does.Contain("Chat window height"));
        Assert.That(menu, Does.Contain("Chat font size grows message text only"));
        Assert.That(menu, Does.Contain("Whole chat UI scale grows the panel/input/backdrop too"));
        Assert.That(menu, Does.Contain("Native Global/Local tabs stay game-owned"));
        Assert.That(menu, Does.Contain("Reset Chat UI to Vanilla"));
        Assert.That(menu, Does.Not.Contain("Chat history rows"));
        Assert.That(menu, Does.Contain("Normal outline color"));
        Assert.That(menu, Does.Contain("Black text outline color"));
        Assert.That(menu, Does.Contain("BlackNamesOutline"));
        Assert.That(menu, Does.Contain("SaveOutlineIntensity"));
        Assert.That(menu, Does.Contain("SaveChatUiScale"));
        Assert.That(menu, Does.Contain("SaveChatFontSize"));
        Assert.That(menu, Does.Contain("SaveChatWindowHeight"));
        Assert.That(menu, Does.Contain("SaveChatHistoryRows"));
        Assert.That(menu, Does.Contain("ChatOutline="));
        Assert.That(menu, Does.Contain("ChatUIScale="));
        Assert.That(menu, Does.Contain("ChatFont="));
        Assert.That(menu, Does.Contain("ChatWindowHeight="));
        Assert.That(menu, Does.Contain("ChatRows="));
        Assert.That(menu, Does.Contain("OutlineColors=#"));
        Assert.That(menu, Does.Contain("BlackNamesOutline="));
        Assert.That(typeahead, Does.Contain("[\"chatoutline\"]"));
        Assert.That(typeahead, Does.Contain("Choice(\"chatoutline\", \"75\", \"default outline\")"));
        Assert.That(typeahead, Does.Contain("Choice(\"outline\", \"75\", \"default outline\")"));
        Assert.That(typeahead, Does.Contain("Choice(\"chatoutline\", \"50\", \"lighter outline\")"));
        Assert.That(typeahead, Does.Contain("Choice(\"outline\", \"50\", \"lighter outline\")"));
        Assert.That(typeahead, Does.Contain("Choice(\"chatoutline\", \"100\""));
        Assert.That(typeahead, Does.Contain("[\"chatoutlinecolor\"]"));
        Assert.That(typeahead, Does.Contain("[\"blackoutlinecolor\"]"));
        Assert.That(typeahead, Does.Contain("[\"chatscale\"]"));
        Assert.That(typeahead, Does.Contain("Choice(\"chatscale\", \"125\", \"larger whole chat UI\")"));
        Assert.That(typeahead, Does.Contain("[\"chatfont\"]"));
        Assert.That(typeahead, Does.Contain("ChatFontChoices(\"chatfont\")"));
        Assert.That(typeahead, Does.Contain("Choice(command, \"125\", \"larger message text\")"));
        Assert.That(typeahead, Does.Contain("[\"chatresize\"]"));
        Assert.That(typeahead, Does.Contain("[\"chatheight\"]"));
        Assert.That(typeahead, Does.Contain("ChatHeightChoices(\"chatheight\")"));
        Assert.That(typeahead, Does.Contain("Choice(command, \"on\", \"moderate taller chat window\")"));
        Assert.That(typeahead, Does.Contain("Choice(command, \"off\", \"vanilla chat window\")"));
        Assert.That(typeahead, Does.Contain("Choice(command, \"toggle\", \"switch taller window on or off\")"));
        Assert.That(typeahead, Does.Contain("[\"chathistory\"]"));
        Assert.That(typeahead, Does.Contain("ChatHistoryChoices"));
    }

    [Test]
    public void Chat_readability_uses_tmp_material_outline_and_preserves_text_content()
    {
        string patch = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatReadabilityPatch.cs"));

        Assert.That(patch, Does.Contain("GetOutlineColorForText"));
        Assert.That(patch, Does.Contain("Plugin.ShouldApplyBlackNamesOutline"));
        Assert.That(patch, Does.Contain("Plugin.LockedChatOutlineColorHex"));
        Assert.That(patch, Does.Contain("Plugin.LockedBlackChatOutlineColorHex"));
        Assert.That(patch, Does.Contain("ChatReadabilityStylePolicy.ShouldUseLightOutline"));
        Assert.That(patch, Does.Contain("ChatReadabilityStylePolicy.ContainsTrueBlackRichTextColorTag(text.text)"));
        Assert.That(patch, Does.Contain("LightOutlineColor"));
        Assert.That(patch, Does.Contain("Plugin.LockedChatOutlineIntensityPercent"));
        Assert.That(patch, Does.Contain("Plugin.LockedChatUiScalePercent"));
        Assert.That(patch, Does.Contain("Plugin.LockedChatFontSizePercent"));
        Assert.That(patch, Does.Contain("Plugin.LockedChatWindowHeightPercent"));
        Assert.That(patch, Does.Contain("ApplyChatAccessibilityScale"));
        Assert.That(patch, Does.Contain("ApplyChatFontSize"));
        Assert.That(patch, Does.Contain("ApplyChatWindowHeight"));
        Assert.That(patch, Does.Contain("RestoreChatAccessibilityScale"));
        Assert.That(patch, Does.Contain("RestoreChatFontSize"));
        Assert.That(patch, Does.Contain("RestoreChatWindowHeight"));
        Assert.That(patch, Does.Contain("OriginalSurfaceScales"));
        Assert.That(patch, Does.Contain("OriginalFontSizes"));
        Assert.That(patch, Does.Contain("OriginalSurfaceWindowSizes"));
        Assert.That(patch, Does.Contain("OriginalWidth"));
        Assert.That(patch, Does.Contain("RectTransform.Axis.Vertical"));
        Assert.That(patch, Does.Not.Contain("RectTransform.Axis.Horizontal"));
        Assert.That(patch, Does.Not.Contain("ApplyChatContentWidth"));
        Assert.That(patch, Does.Contain("MinChatWindowBaselineSize"));
        Assert.That(patch, Does.Contain("AddChatWindowSurfaces"));
        Assert.That(patch, Does.Contain("ResolveChatScrollView"));
        Assert.That(patch, Does.Contain("Global/Local tabs"));
        Assert.That(patch, Does.Not.Contain("image.raycastTarget = false"));
        Assert.That(patch, Does.Contain("IsValidChatWindowBaseline"));
        Assert.That(patch, Does.Contain("waiting for visible layout"));
        Assert.That(patch, Does.Not.Contain("OriginalSurfaceHeights"));
        Assert.That(patch, Does.Contain("ChatAccessibilityScalePolicy.NormalizeScale"));
        Assert.That(patch, Does.Contain("ChatFontSizePolicy.NormalizeFontSize"));
        Assert.That(patch, Does.Contain("ChatWindowHeightPolicy.NormalizeHeight"));
        Assert.That(patch, Does.Contain("ApplyTmpMaterialOutline"));
        Assert.That(patch, Does.Contain("MaxTmpOutlineWidth"));
        Assert.That(patch, Does.Contain("ShaderUtilities.ID_OutlineWidth"));
        Assert.That(patch, Does.Contain("ShaderUtilities.ID_OutlineColor"));
        Assert.That(patch, Does.Contain("new Material(material)"));
        Assert.That(patch, Does.Contain("state.AppliedFontMaterial"));
        Assert.That(patch, Does.Contain("text.UpdateMeshPadding()"));
        Assert.That(patch, Does.Contain("DestroyOwnedMaterial"));
        Assert.That(patch, Does.Not.Contain("TextContainsTrueBlack"));
        Assert.That(patch, Does.Not.Contain("Contains(\"<color=#000\")"));
        Assert.That(patch, Does.Not.Contain("Contains(\"<#000\")"));
        Assert.That(patch, Does.Contain("outline.useGraphicAlpha = false"));
        Assert.That(patch, Does.Contain("shadow.useGraphicAlpha = false"));
        Assert.That(patch, Does.Not.Contain("text.text ="));
        Assert.That(patch, Does.Not.Contain("text.color ="));
    }

    [Test]
    public void Chat_history_retention_does_not_touch_readability_overlay_or_networking()
    {
        string patch = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatHistoryLimitPatch.cs"));

        Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(GameSettings))]"));
        Assert.That(patch, Does.Contain("GlobalMessageLimitCount"));
        Assert.That(patch, Does.Contain("LocalMessageLimitCount"));
        Assert.That(patch, Does.Contain("ChatHistoryLimitPolicy.ApplyConfiguredRows"));
        Assert.That(patch, Does.Not.Contain("TextMeshProUGUI"));
        Assert.That(patch, Does.Not.Contain("UiOutline"));
        Assert.That(patch, Does.Not.Contain("UiShadow"));
        Assert.That(patch, Does.Not.Contain("transform.localScale"));
        Assert.That(patch, Does.Not.Contain("SetSizeWithCurrentAnchors"));
        Assert.That(patch, Does.Not.Contain("SendMessageAsync"));
    }

    [Test]
    public void Chat_readability_does_not_take_over_vanilla_scroll_input()
    {
        string patch = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatReadabilityPatch.cs"));

        Assert.That(patch, Does.Not.Contain("EnsureChatScrollbackFriendly"));
        Assert.That(patch, Does.Not.Contain("GetComponentsInChildren<ScrollRect>(true)"));
        Assert.That(patch, Does.Not.Contain("scrollRect.vertical ="));
        Assert.That(patch, Does.Not.Contain("scrollRect.movementType"));
        Assert.That(patch, Does.Not.Contain("scrollRect.scrollSensitivity"));
        Assert.That(patch, Does.Not.Contain("verticalScrollbar.gameObject.SetActive"));
        Assert.That(patch, Does.Not.Contain("mask.showMaskGraphic"));
    }

    [Test]
    public void Chat_backdrop_preserves_vanilla_raycast_targets_needed_for_scrolling()
    {
        string patch = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatReadabilityPatch.cs"));

        Assert.That(patch, Does.Contain("ApplyPersistentBackdrop"));
        Assert.That(patch, Does.Contain("RestorePersistentBackdrop"));
        Assert.That(patch, Does.Contain("OriginalBackdropColors"));
        Assert.That(patch, Does.Contain("BlueSage Chat Backdrop"));
        Assert.That(patch, Does.Contain("anchorMin = Vector2.zero"));
        Assert.That(patch, Does.Contain("anchorMax = Vector2.one"));
        Assert.That(patch, Does.Contain("blocksRaycasts = false"));
        Assert.That(patch, Does.Contain("OwnedBackdropOverlays"));
        Assert.That(patch, Does.Not.Contain("raycastTarget = false"));
        Assert.That(patch, Does.Not.Contain(".raycastTarget = false"));
    }

    [Test]
    public void Chat_readability_is_event_driven_and_does_not_redirty_unchanged_rows()
    {
        string patch = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatReadabilityPatch.cs"));
        string controller = File.ReadAllText(Path.Combine(ProjectRoot, "src", "ChatReadabilityController.cs"));

        Assert.That(patch, Does.Contain("RefreshNewestRows();"));
        Assert.That(patch, Does.Contain("content.GetChild(content.childCount - 1)"));
        Assert.That(patch, Does.Contain("state.LastAppliedSignature == signature"));
        Assert.That(patch, Does.Contain("PruneDestroyedState"));
        Assert.That(patch, Does.Not.Contain("RequestRefreshSoon();\n        RefreshVisibleChat();"));
        Assert.That(controller, Does.Not.Contain("ActiveRefreshIntervalSeconds"));
        Assert.That(controller, Does.Not.Contain("IdleRefreshIntervalSeconds"));
        Assert.That(controller, Does.Not.Contain("_nextRefreshAt"));
        Assert.That(controller, Does.Contain("if (!stateChanged)"));
    }

    [Test]
    public void Destroyed_chat_rows_release_owned_material_and_surface_state_while_readability_stays_active()
    {
        string patch = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatReadabilityPatch.cs"));
        string controller = File.ReadAllText(Path.Combine(ProjectRoot, "src", "ChatReadabilityController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("DestroyedStatePruneIntervalSeconds = 5f"));
            Assert.That(controller, Does.Contain("Time.unscaledTime >= _nextDestroyedStatePruneAt"));
            Assert.That(controller, Does.Contain("ChatReadabilityPatch.PruneDestroyedState();"));
            Assert.That(patch, Does.Contain("DestroyOwnedMaterial(state?.AppliedFontMaterial)"));
            Assert.That(patch, Does.Contain("OriginalSurfaceScales.Remove(id)"));
            Assert.That(patch, Does.Contain("OriginalSurfaceWindowSizes.Remove(id)"));
            Assert.That(patch, Does.Contain("GetDiagnosticStateSummary"));
        });
    }

    [Test]
    public void Disabled_chat_readability_prunes_destroyed_state_on_the_same_bounded_cadence()
    {
        string controller = File.ReadAllText(Path.Combine(ProjectRoot, "src", "ChatReadabilityController.cs"));
        string idleBranch = ExtractIdleBranch(controller);
        int cadenceGuard = idleBranch.IndexOf("if (Time.unscaledTime >= _nextDestroyedStatePruneAt)", StringComparison.Ordinal);
        int cadenceAdvance = idleBranch.IndexOf("_nextDestroyedStatePruneAt = Time.unscaledTime + DestroyedStatePruneIntervalSeconds;", StringComparison.Ordinal);
        int pruneCall = idleBranch.IndexOf("ChatReadabilityPatch.PruneDestroyedState();", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(cadenceGuard, Is.GreaterThanOrEqualTo(0));
            Assert.That(cadenceAdvance, Is.GreaterThan(cadenceGuard));
            Assert.That(pruneCall, Is.GreaterThan(cadenceAdvance));
            Assert.That(idleBranch.Split("ChatReadabilityPatch.PruneDestroyedState();").Length - 1, Is.EqualTo(1));
            Assert.That(HasBoundedIdlePrune(idleBranch), Is.True);
        });
    }

    [Test]
    public void Disabled_branch_extraction_does_not_borrow_the_enabled_path_cadence_guard()
    {
        const string unboundedBaseline = "if (!shouldWatch)\n{\n    ChatReadabilityPatch.PruneDestroyedState();\n    return;\n}\n\nif (Time.unscaledTime >= _nextDestroyedStatePruneAt)\n{\n    ChatReadabilityPatch.PruneDestroyedState();\n}";
        const string missingAdvanceMutant = "if (!shouldWatch)\n{\n    if (Time.unscaledTime >= _nextDestroyedStatePruneAt)\n    {\n        ChatReadabilityPatch.PruneDestroyedState();\n    }\n    return;\n}";
        string idleBranch = ExtractIdleBranch(unboundedBaseline);

        Assert.Multiple(() =>
        {
            Assert.That(idleBranch, Does.Not.Contain("Time.unscaledTime >= _nextDestroyedStatePruneAt"));
            Assert.That(idleBranch.Split("ChatReadabilityPatch.PruneDestroyedState();").Length - 1, Is.EqualTo(1));
            Assert.That(HasBoundedIdlePrune(idleBranch), Is.False);
            Assert.That(HasBoundedIdlePrune(ExtractIdleBranch(missingAdvanceMutant)), Is.False);
        });
    }

    private static bool HasBoundedIdlePrune(string idleBranch)
    {
        int guard = idleBranch.IndexOf("if (Time.unscaledTime >= _nextDestroyedStatePruneAt)", StringComparison.Ordinal);
        int advance = idleBranch.IndexOf("_nextDestroyedStatePruneAt = Time.unscaledTime + DestroyedStatePruneIntervalSeconds;", StringComparison.Ordinal);
        int prune = idleBranch.IndexOf("ChatReadabilityPatch.PruneDestroyedState();", StringComparison.Ordinal);
        return guard >= 0 && advance > guard && prune > advance;
    }

    private static string ExtractIdleBranch(string controller)
    {
        int branchStart = controller.IndexOf("if (!shouldWatch)", StringComparison.Ordinal);
        Assert.That(branchStart, Is.GreaterThanOrEqualTo(0));
        int openBrace = controller.IndexOf('{', branchStart);
        Assert.That(openBrace, Is.GreaterThan(branchStart));

        int depth = 0;
        for (int index = openBrace; index < controller.Length; index++)
        {
            if (controller[index] == '{') depth++;
            if (controller[index] != '}') continue;
            depth--;
            if (depth == 0)
            {
                return controller.Substring(branchStart, index - branchStart + 1);
            }
        }

        Assert.Fail("Could not find the closing brace for the disabled readability branch.");
        return string.Empty;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
