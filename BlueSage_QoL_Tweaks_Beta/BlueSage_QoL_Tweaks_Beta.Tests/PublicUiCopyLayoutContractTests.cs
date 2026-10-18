using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicUiCopyLayoutContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void First_lobby_guidance_is_short_local_and_points_to_the_two_main_surfaces()
    {
        string source = Read("src", "Plugin.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("NavigationCalloutPolicy.BuildPrimaryWelcomeMessage(PluginVersion, QolMenuKey.Value.ToString(), StyleMenuKey.Value.ToString())"));
            Assert.That(source, Does.Contain("NavigationCalloutPolicy.BuildCommandBrowserHint()"));
            Assert.That(source, Does.Contain("Green Steam-profile buttons verify player accounts"));
            Assert.That(source, Does.Contain("VoiceWelcomePolicy.Build"));
            Assert.That(source, Does.Contain("Pomodoro/focus timer can earn tickets and XP"));
            Assert.That(Slice(source, "private bool ShowWelcomeMessageForLobby()", "private void RefreshCompatibility(string source)"), Does.Not.Contain("/sidwho"));
            Assert.That(source, Does.Not.Contain("AddGlobalChatMessage(\"BlueSage QoL v"));
        });
    }

    [Test]
    public void Main_menu_footer_and_supporter_roster_preserve_wrapped_copy_without_overtaking_main()
    {
        string source = Read("src", "QolMenuWindow.cs");
        string ledger = Read("src", "Data", "SupporterLedger.g.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("private const float FooterHeight = 120f;"));
            Assert.That(source, Does.Contain("private static int GetThanksGridColumnCount(float availableWidth)"));
            Assert.That(source, Does.Contain("if (availableWidth >= 1400f) return 4;"));
            Assert.That(source, Does.Contain("if (availableWidth >= 1020f) return 3;"));
            Assert.That(source, Does.Contain("if (availableWidth >= 680f) return 2;"));
            Assert.That(source, Does.Contain("DrawSupporterDirectory"));
            Assert.That(source, Does.Contain("GUILayout.Height(34f)"));
            Assert.That(source, Does.Contain("DrawSelectedSupporterMessages"));
            Assert.That(source, Does.Contain("SupporterLedgerPolicy.GetPublicMessages(supporter)"));
            Assert.That(ledger, Does.Contain("\"Lily1chan\""));
            Assert.That(ledger, Does.Contain("one of the lovelist ive been a part of!"));
            Assert.That(source, Does.Not.Contain("SupporterThanksCards"));
            Assert.That(source, Does.Not.Contain("GUILayout.MinHeight(190f)"));
            Assert.That(source, Does.Not.Contain("GUILayout.Height(132f)"));
            Assert.That(source, Does.Contain("richText = false, fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true"));
            Assert.That(source, Does.Contain("_footerTextStyle"));
            Assert.That(source, Does.Contain("BlueSageHelpFrame.DrawStateAndHelp"));
            Assert.That(source, Does.Not.Contain("GUILayout.Label(stateLine, _footerTextStyle, GUILayout.Height(32f))"));
        });
    }

    [Test]
    public void Style_helper_quick_actions_use_neutral_and_enabled_colors_semantically()
    {
        string source = Read("src", "QolMenuWindow.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("bool styleHelperOpen = Plugin.IsStyleHelperVisible;"));
            Assert.That(source, Does.Contain("styleHelperOpen ? \"Close Style Helper\" : \"Open Style Helper\""));
            Assert.That(source, Does.Contain("styleHelperOpen ? _enabledButtonStyle : _buttonStyle"));
            Assert.That(source, Does.Not.Contain("_calloutButtonStyle"));
            Assert.That(source, Does.Not.Contain("Open / Close Style Helper"));
        });
    }

    [Test]
    public void Supported_public_fly_uses_normal_enabled_state_not_experimental_state()
    {
        string source = Read("src", "QolMenuWindow.cs");
        string movement = Slice(source, "private void DrawSelfMovementTools()", "private void DrawSelfMovementSetting(");
        Assert.Multiple(() =>
        {
            Assert.That(movement, Does.Contain("Plugin.ShouldApplyNoclipFly ? _enabledButtonStyle : _buttonStyle"));
            Assert.That(movement, Does.Not.Contain("_experimentalButtonStyle"));
        });
    }

    [Test]
    public void Optional_lobby_joke_uses_the_established_do_not_press_gag_without_hiding_its_harmless_one_shot_behavior()
    {
        string menu = Read("src", "QolMenuWindow.cs");
        string plugin = Read("src", "Plugin.cs");
        string joke = Slice(menu, "private void DrawDoNotPressButton()", "private void DrawFooter()");
        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("Optional lobby joke"));
            Assert.That(menu, Does.Contain("Sends one playful lobby chat line the first time. It never changes moderation or game state."));
            Assert.That(joke, Does.Contain("new GUIContent(\"DO NOT PRESS\", \"Extremely dangerous. Definitely not a harmless one-time easter egg. Probably.\")"));
            Assert.That(joke, Does.Contain("Repeat presses stay local and send nothing."));
            Assert.That(joke, Does.Contain("_buttonStyle"));
            Assert.That(joke, Does.Not.Contain("_experimentalButtonStyle"));
            Assert.That(joke, Does.Contain("TrySendDoNotPressLobbyMessage"));
            Assert.That(plugin, Does.Contain("TrySendDoNotPressLobbyMessage"));
        });
    }

    [Test]
    public void Supported_self_movement_has_menu_and_local_slash_command_parity()
    {
        string source = Read("src", "Plugin.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("\"selfmove\""));
            Assert.That(source, Does.Contain("HandleSelfMovementCommand"));
            Assert.That(source, Does.Contain("SaveSelfPositionFromQolMenu()"));
            Assert.That(source, Does.Contain("TeleportSelfToSavedFromQolMenu()"));
            Assert.That(source, Does.Contain("ToggleNoclipFlyFromQolMenu()"));
            Assert.That(source, Does.Contain("SaveSelfMovementMultiplierFromQolMenu(action, value)"));
            Assert.That(source, Does.Contain("ResetSelfMovementFromQolMenu()"));
        });
    }

    [Test]
    public void Navigation_keywords_use_one_restrained_rich_text_callout_system()
    {
        string source = Read("src", "QolMenuWindow.cs");
        string policy = Read("src", "Core", "NavigationCalloutPolicy.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("BuildMenuNavigationHint(_theme.NavigationKeywordHex, Plugin.QolMenuKey.Value.ToString(), Plugin.StyleMenuKey.Value.ToString())"));
            Assert.That(source, Does.Contain("richText = true"));
            Assert.That(policy, Does.Contain("HOME"));
            Assert.That(policy, Does.Contain("INSERT"));
            Assert.That(policy, Does.Contain("SLASH COMMANDS"));
            Assert.That(policy, Does.Contain("<b><color=#"));
        });
    }

    [Test]
    public void Casual_moderation_copy_requires_profile_verification_and_avoids_backend_jargon()
    {
        string source = Read("src", "QolMenuWindow.cs");
        string safety = Slice(source, "private void DrawLobbySafetyTab()", "private void DrawSelfMovementTools()");
        string technicalResync = Slice(
            source,
            "private void DrawIdentityResyncControls(",
            "private void DrawCloneIncidentReview()");
        safety = safety.Replace(technicalResync, string.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(safety, Does.Contain("Clone Shield is an advisory clue"));
            Assert.That(safety, Does.Contain("after checking the Steam profile"));
            Assert.That(safety, Does.Contain("Check My Access"));
            Assert.That(safety, Does.Contain("Copy SteamID64"));
            Assert.That(safety, Does.Contain("Open Steam Profile"));
            Assert.That(safety, Does.Contain("Preview SID Ban"));
            Assert.That(safety, Does.Not.Contain("PurrNet"));
            Assert.That(safety, Does.Not.Contain("authoritative"));
            Assert.That(safety, Does.Not.Contain("authenticated"));
        });
    }

    [Test]
    public void Slash_command_popup_is_screen_fitted_wrapped_and_explicitly_local_until_enter()
    {
        string source = Read("src", "CommandTypeaheadController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("Mathf.Clamp(Screen.width - 24f, 300f, 560f)"));
            Assert.That(source, Does.Contain("Nothing sends until you press Enter."));
            Assert.That(source, Does.Contain("GUILayout.Height(36f)"));
            Assert.That(source, Does.Contain("wordWrap = true"));
            Assert.That(source, Does.Contain("clipping = TextClipping.Overflow"));
            Assert.That(source, Does.Not.Contain("BeginScrollView(_scrollPosition, true"));
        });
    }

    [Test]
    public void Steam_profile_tooltip_stays_compact_and_fails_closed()
    {
        string source = Read("src", "Patches", "PlayerIdentityTooltipPatch.cs");
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("Steam Profile • VERIFIED"));
            Assert.That(source, Does.Contain("Steam Profile • LEFT / STALE"));
            Assert.That(source, Does.Contain("Steam Profile • CHECKING"));
            Assert.That(source, Does.Contain("Steam Profile • LIVE"));
            Assert.That(source, Does.Contain("SteamID64:"));
            Assert.That(source, Does.Contain("Click profile"));
            Assert.That(source, Does.Contain("Profile opens, but moderation stays locked."));
            Assert.That(source, Does.Contain("private static string Compact"));
            Assert.That(source, Does.Contain("ExpandProfilePopup"));
            Assert.That(source, Does.Contain("ExpandProfilePopup(__instance, body);"));
            Assert.That(source.IndexOf("ExpandProfilePopup(__instance, body);", StringComparison.Ordinal), Is.LessThan(source.IndexOf("TryResolveExact", StringComparison.Ordinal)));
            Assert.That(source, Does.Not.Contain("body.overflowMode = TextOverflowModes.Overflow"));
            Assert.That(source, Does.Contain("Mathf.Max(panel.sizeDelta.y, 156f)"));
            Assert.That(source, Does.Contain("body.textWrappingMode = TextWrappingModes.Normal"));
            Assert.That(source, Does.Contain("evidence.SteamPersona"));
            Assert.That(source, Does.Contain("_defaultBodyFontSize"));
            Assert.That(source, Does.Contain("_defaultHeaderColor"));
            Assert.That(source, Does.Contain("body.fontSize = _defaultBodyFontSize"));
            Assert.That(source, Does.Contain("header.color = _defaultHeaderColor"));
        });
    }

    [Test]
    public void Every_steam_profile_popup_state_is_expanded_before_identity_resolution()
    {
        string source = Read("src", "Patches", "PlayerIdentityTooltipPatch.cs");
        string postfix = Slice(source, "[HarmonyPostfix]", "private static void ExpandProfilePopup");
        int expandIndex = postfix.IndexOf("ExpandProfilePopup(__instance, body);", StringComparison.Ordinal);
        int resolutionIndex = postfix.IndexOf("TryResolveExactCached", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(expandIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(resolutionIndex, Is.GreaterThan(expandIndex));
            Assert.That(postfix.Split("ExpandProfilePopup(__instance, body);", StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(postfix.Substring(resolutionIndex), Does.Not.Contain("ExpandProfilePopup(__instance, body);"));
        });
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Start marker missing: " + startMarker);
        Assert.That(end, Is.GreaterThan(start), "End marker missing: " + endMarker);
        return source.Substring(start, end - start);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
