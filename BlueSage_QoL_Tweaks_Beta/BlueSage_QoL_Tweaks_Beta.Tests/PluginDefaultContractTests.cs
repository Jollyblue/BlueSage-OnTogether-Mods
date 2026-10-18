using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PluginDefaultContractTests
{
    [Test]
    public void Readme_documents_opt_in_ten_minute_blue_sage_cleanup_without_changing_native_timing()
    {
        string readmeSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "README.md"));

        Assert.That(readmeSource, Does.Contain("Automatic Cleanup starts on and runs every 15 minutes"));
        Assert.That(readmeSource, Does.Contain("turn it off or choose 5–180 minutes"));
        Assert.That(readmeSource, Does.Contain("leaves the game's own cleanup alone"));
        Assert.That(readmeSource, Does.Not.Contain("fallback only when the game does not own cleanup"));
        Assert.That(readmeSource, Does.Not.Contain("migrate once"));
        Assert.That(readmeSource, Does.Not.Contain("`SweepIntervalMinutes`: `30`"));
    }

    [Test]
    public void PluginConfigDefaults_MatchBetaCommunityContract()
    {
        string pluginSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));
        string navigationSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Core", "NavigationCalloutPolicy.cs"));

        Assert.That(pluginSource, Does.Contain("public const string PluginVersion = \"0.2.5\""));
        Assert.That(pluginSource, Does.Contain("public const string PluginDisplayVersion = \"0.2.5\""));
        Assert.That(pluginSource, Does.Contain("public const string PluginReleaseDate = \"2026-10-18\""));
        Assert.That(pluginSource, Does.Not.Contain("ConfigMigrationTarget = PluginVersion +"));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationPolicy.RecordMigration"));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationVersion"));
        Assert.That(pluginSource, Does.Contain("ApplyOneTimeConfigMigration"));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationPolicy.MigrateOldDefaultInt"));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationPolicy.ShouldDisableDefaultSpoons"));
        Assert.That(pluginSource, Does.Contain("EnableLeaveNotifications = Config.Bind(\"Notifications\", \"EnableLeaveNotifications\", true"));
        Assert.That(pluginSource, Does.Not.Contain("EnableInputFieldLimits = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("EnableSessionNameLimit = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("EnableChatInputLimit = Config.Bind"));
        Assert.That(pluginSource, Does.Contain("MaxChatCharacters = Config.Bind(\"Text Limits\", \"MaxChatCharacters\", publicDefaults.ChatTextLimit"));
        Assert.That(pluginSource, Does.Contain("MaxProfileCharacters = Config.Bind(\"Text Limits\", \"MaxProfileCharacters\", publicDefaults.ProfileTextLimit"));
        Assert.That(pluginSource, Does.Contain("MaxSessionNameCharacters = Config.Bind(\"Text Limits\", \"MaxSessionNameCharacters\", publicDefaults.RoomNameLimit"));
        Assert.That(pluginSource, Does.Not.Contain("ReconnectGuardRetryIntervalSeconds = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("ReconnectGuardMaxAttempts = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("ReconnectGuardManageHostKick = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("ReconnectAnnouncementMessage = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("ReconnectAnnouncementDelaySeconds = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("HostHealthIntervalSeconds = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("HostHealthRefreshLobbyMetadata = Config.Bind"));
        Assert.That(pluginSource, Does.Not.Contain("HostHealthRequestPersonaRefresh = Config.Bind"));
        Assert.That(pluginSource, Does.Contain("LockedMaxChatCharacters => InputFieldLimitPolicy.ClampChatCharacters"));
        Assert.That(pluginSource, Does.Contain("LockedReconnectRetryIntervalSeconds = 5"));
        Assert.That(pluginSource, Does.Contain("LockedReconnectMaxAttempts = 3"));
        Assert.That(pluginSource, Does.Contain("LockedReconnectPostConnectValidationSeconds = 8"));
        Assert.That(pluginSource, Does.Contain("ReconnectRuntimeHealthPolicy.Evaluate"));
        Assert.That(pluginSource, Does.Contain("TryCaptureReconnectLobbyCode"));
        Assert.That(pluginSource, Does.Contain("_lastReconnectLobbyCode"));
        Assert.That(pluginSource, Does.Contain("StartCleanMenuReconnect"));
        Assert.That(pluginSource, Does.Contain("IsCleanMenuReadyForReconnect"));
        Assert.That(pluginSource, Does.Contain("JoinByCode(targetLobbyCode, false)"));
        Assert.That(pluginSource, Does.Not.Contain("JoinByCode(_lastReconnectLobbyCode, true)"));
        Assert.That(pluginSource, Does.Contain("LockedHostHealthIntervalSeconds = 300"));
        Assert.That(pluginSource, Does.Contain("AutoDisableSweepWhenAndrewSweepInstalled = Config.Bind(\"Asset Sweep\", \"AutoDisableSweepWhenAndrewSweepInstalled\", true"));
        Assert.That(pluginSource, Does.Contain("SweepIntervalMinutes = Config.Bind(\"Asset Sweep\", \"SweepIntervalMinutes\", SweepSettingsPolicy.DefaultIntervalMinutes"));
        Assert.That(pluginSource, Does.Contain("Used only when BlueSage Automatic Cleanup is explicitly enabled"));
        Assert.That(pluginSource, Does.Contain("SweepSettingsPolicy.ResolveStartupIntervalMinutes(SweepIntervalMinutes.Value, freshInstall, sweepDefaultsMigrated, sweepTenMinuteSafetyMigrated)"));
        Assert.That(pluginSource, Does.Contain("MigrateAutomaticCleanupOptIn(EnableAutoSweep.Value, sweepAutomaticOptInMigrated)"));
        Assert.That(File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Core", "SweepSettingsPolicy.cs")), Does.Contain("MigrateTenMinuteSafetyInterval(migrated, tenMinuteSafetyMigrated)"));
        Assert.That(pluginSource, Does.Not.Contain("MigrateTenMinuteSafetyInterval(migrated, sweepAutomaticOptInMigrated)"));
        Assert.That(pluginSource, Does.Contain("EnableReconnectGuard = Config.Bind(\"Reconnect Guard\", \"EnableReconnectGuard\", true"));
        Assert.That(pluginSource, Does.Contain("EnableHiddenDiagnostics = Config.Bind(\"Developer\", \"EnableHiddenDiagnostics\", false"));
        Assert.That(pluginSource, Does.Contain("ShouldEmitHiddenDiagnostics =>"));
        Assert.That(pluginSource, Does.Contain("EnableHiddenDiagnostics != null &&"));
        Assert.That(pluginSource, Does.Contain("EnableHiddenDiagnostics.Value;"));
        Assert.That(pluginSource, Does.Not.Contain("Locked on through 0.2.x"));
        Assert.That(pluginSource, Does.Not.Contain("/bluedevdebug off cannot disable this release"));
        Assert.That(pluginSource, Does.Contain("StartCoroutine(DelayedReconnectAnnouncement(message))"));
        Assert.That(pluginSource, Does.Contain("LockedHostHealthShowNotifications = false"));
        Assert.That(pluginSource, Does.Contain("EnableHostHealthMonitor = Config.Bind(\"Host Health\", \"EnableHostHealthMonitor\", true"));
        Assert.That(pluginSource, Does.Contain("EnableFocusAnywhere = Config.Bind(\"Focus Anywhere\", \"EnableFocusAnywhere\", true"));
        Assert.That(pluginSource, Does.Contain("EnableEnhancedPlayerPanel = Config.Bind(\"Enhanced Player Panel\", \"EnableEnhancedPlayerPanel\", true"));
        Assert.That(pluginSource, Does.Contain("StartCoroutine(HostHealthLoop())"));
        Assert.That(pluginSource, Does.Contain("RunHostHealthCheck(\"manual command\")"));
        Assert.That(pluginSource, Does.Contain("SteamMatchmaking.SetLobbyData"));
        Assert.That(pluginSource, Does.Contain("SteamFriends.RequestUserInformation"));
        Assert.That(pluginSource, Does.Contain("RegisterBlueSageCommands();"));
        Assert.That(pluginSource, Does.Contain("Input.GetKeyDown(StyleMenuKey.Value)"));
        Assert.That(pluginSource, Does.Contain("StyleMenuKey = Config.Bind(\"Hotkeys\", \"StyleMenuKey\", KeyCode.Insert,"));
        Assert.That(pluginSource, Does.Contain("ToggleStyleUiFromHotkey();"));
        Assert.That(pluginSource, Does.Contain("Style Helper toggled via {StyleMenuKey.Value} key."));
        Assert.That(pluginSource, Does.Contain("\"bsqol\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"qol\", \"guide\", \"faq\" }"));
        Assert.That(pluginSource, Does.Contain("new[] { \"sweep\", \"as\" }"));
        Assert.That(pluginSource, Does.Contain("new[] { \"hh\" }"));
        Assert.That(pluginSource, Does.Contain("new[] { \"ln\" }"));
        Assert.That(pluginSource, Does.Contain("new[] { \"tt\" }"));
        Assert.That(pluginSource, Does.Contain("HandleTimestampCommand"));
        Assert.That(pluginSource, Does.Contain("EnableNotificationTimestamps.Value = EnableChatTimestamps.Value"));
        Assert.That(pluginSource, Does.Contain("SyncNotificationTimestampsWithChatTimestamps(\"startup config normalization\")"));
        Assert.That(pluginSource, Does.Contain("new[] { \"rg\" }"));
        Assert.That(pluginSource, Does.Contain("new[] { \"rm\" }"));
        Assert.That(pluginSource, Does.Contain("new[] { \"fa\" }"));
        Assert.That(pluginSource, Does.Contain("new[] { \"sty\" }"));
        Assert.That(pluginSource, Does.Contain("\"styleui\""));
        Assert.That(pluginSource, Does.Contain("\"setpingcolor\""));
        Assert.That(pluginSource, Does.Contain("\"pingsound\""));
        Assert.That(pluginSource, Does.Contain("HandleStyleCommand"));
        Assert.That(pluginSource, Does.Contain("HandleStyleUiCommand"));
        Assert.That(pluginSource, Does.Contain("TryApplyStyledNameFromStyleUi"));
        Assert.That(pluginSource, Does.Contain("HandleSetPingColorCommand"));
        Assert.That(pluginSource, Does.Contain("GUIUtility.systemCopyBuffer"));
        Assert.That(pluginSource, Does.Contain("EnablePingMentions = Config.Bind(\"Ping Mentions\", \"EnablePingMentions\", true"));
        Assert.That(pluginSource, Does.Contain("EnablePingSound = Config.Bind(\"Ping Mentions\", \"EnablePingSound\", true"));
        Assert.That(pluginSource, Does.Contain("PingSoundMode = Config.Bind(\"Ping Mentions\", \"PingSoundMode\", \"ticket\""));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationPolicy.MigrateOldDefaultString(PingSoundMode.Value, \"click\", \"ticket\", finalDefaultsMigrated)"));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationPolicy.MigrateOldDefaultInt(ChatOutlineIntensity.Value, 50, ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent, finalDefaultsMigrated)"));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationPolicy.MigrateOldDefaultInt(migrated, 60, ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent, finalDefaultsMigrated)"));
        Assert.That(pluginSource, Does.Contain("ConfigMigrationVersion.Value = migrationLedger"));
        Assert.That(pluginSource, Does.Contain("HandlePingSoundCommand"));
        Assert.That(pluginSource, Does.Contain("EnableChatReadability = Config.Bind(\"Chat Readability\", \"EnableChatReadability\", true"));
        Assert.That(pluginSource, Does.Contain("ChatWindowHeightPercent = Config.Bind(\"Chat Readability\", \"ChatWindowHeightPercent\", ChatWindowHeightPolicy.DefaultHeightPercent"));
        Assert.That(pluginSource, Does.Contain("LockedChatWindowHeightPercent => ChatWindowHeightPolicy.ClampHeightPercent"));
        Assert.That(pluginSource, Does.Contain("LockedChatHistoryRows => ChatHistoryLimitPolicy.ClampRows"));
        Assert.That(pluginSource, Does.Contain("ChatHistoryRows = Config.Bind"));
        Assert.That(pluginSource, Does.Contain("internal static ConfigEntry<int> ChatHistoryRows"));
        Assert.That(pluginSource, Does.Contain("ShouldApplyChatReadability"));
        Assert.That(pluginSource, Does.Not.Contain("EnableSplitChat"));
        Assert.That(pluginSource, Does.Not.Contain("ShouldApplySplitChat"));
        Assert.That(pluginSource, Does.Not.Contain("StartSplitChatController"));
        Assert.That(File.Exists(Path.Combine(FindProjectRoot(), "src", "SplitChatController.cs")), Is.False);
        Assert.That(File.Exists(Path.Combine(FindProjectRoot(), "src", "Patches", "SplitChatPanelPatch.cs")), Is.False);
        Assert.That(pluginSource, Does.Contain("\"chatreadability\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"chatread\" }"));
        Assert.That(pluginSource, Does.Contain("\"chatheight\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"chatstretch\", \"chath\" }"));
        Assert.That(pluginSource, Does.Contain("\"chathistory\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"chathistory\", \"chatr\" }"));
        Assert.That(pluginSource, Does.Contain("EnableChatCopy = Config.Bind(\"Chat Copy\", \"EnableChatCopy\", true"));
        Assert.That(pluginSource, Does.Contain("EnableChatUrlLinks = Config.Bind(\"Chat Copy\", \"EnableChatUrlLinks\", true"));
        Assert.That(pluginSource, Does.Contain("ShouldApplyChatCopy"));
        Assert.That(pluginSource, Does.Contain("ShouldApplyChatUrlLinks"));
        Assert.That(pluginSource, Does.Contain("\"copychat\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"ccopy\" }"));
        Assert.That(pluginSource, Does.Contain("\"chatlinks\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"clinks\" }"));
        Assert.That(pluginSource, Does.Contain("EnableStyleUi = Config.Bind(\"Style Helper\", \"EnableStyleUi\", true"));
        Assert.That(pluginSource, Does.Contain("EnableWelcomeMessage = Config.Bind(\"Notifications\", \"EnableWelcomeMessage\", true"));
        Assert.That(pluginSource, Does.Contain("UiThemePreset = Config.Bind(\"Style Helper\", \"UiThemePreset\", publicDefaults.Theme"));
        Assert.That(pluginSource, Does.Contain("StartWelcomeMessageLoop"));
        Assert.That(pluginSource, Does.Contain("WelcomeMessageLoop"));
        Assert.That(pluginSource, Does.Contain("TryGetCurrentLobbyKeyForWelcomeMessage"));
        Assert.That(navigationSource, Does.Contain("/qol status shows what is on"));
        Assert.That(navigationSource, Does.Contain("INSERT"));
        Assert.That(pluginSource, Does.Contain("Quick status: /brb, /afk, /back"));
        Assert.That(navigationSource, Does.Contain("SLASH COMMANDS"));
        Assert.That(pluginSource, Does.Contain("Lower it if very long chat lines wrap oddly"));
        Assert.That(pluginSource, Does.Contain("HandleToggleCommand"));
        Assert.That(pluginSource, Does.Contain("HandleOutgoingChatCommand"));
        Assert.That(pluginSource, Does.Contain("HandleHiddenDiagnosticsCommand"));
        Assert.That(pluginSource, Does.Contain("BuildBlueSageClueCommandName"));
        Assert.That(pluginSource, Does.Contain("HandleBlueSageClueCommand"));
        Assert.That(pluginSource, Does.Contain("found a Clue"));
        Assert.That(pluginSource, Does.Contain("@jollyblue"));
        Assert.That(pluginSource, Does.Contain("CaptureHostHealthInput(out CSteamID? lobbyId, out string[] playerSteamIds)"));
        Assert.That(pluginSource, Does.Contain("TryGetSteamLobbyCounts(lobbyId, out int? steamLobbyMembers, out int? steamLobbyLimit)"));
        Assert.That(pluginSource, Does.Contain("SteamMatchmaking.GetNumLobbyMembers(lobbyId.Value)"));
        Assert.That(pluginSource, Does.Contain("SteamMatchmaking.GetLobbyMemberLimit(lobbyId.Value)"));
        Assert.That(pluginSource, Does.Contain("steamLobbyMembers={FormatNullableCount(steamLobbyMembers)}"));
        Assert.That(pluginSource, Does.Contain("steamLobbyLimit={FormatNullableCount(steamLobbyLimit)}"));

        Assert.That(pluginSource, Does.Not.Contain("/diag"));

        string readmeSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "README.md"));
        Assert.That(readmeSource, Does.Not.Contain("/diag"));
        Assert.That(readmeSource, Does.Not.Contain(string.Concat("blue", "sclues")));

        string commandPatchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "BlueSageCommandPatch.cs"));
        Assert.That(commandPatchSource, Does.Contain("[HarmonyPatch(\"SendMessageAsync\", typeof(byte[]), typeof(byte[]), typeof(bool), typeof(Vector3), typeof(string), typeof(RPCInfo))]"));
        Assert.That(commandPatchSource, Does.Contain("HandleOutgoingChatCommand(__0)"));
        Assert.That(commandPatchSource, Does.Contain("IsLocalCommandInvocation(__5)"));

        string sendLimitPatchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "ChatSendLimitPatch.cs"));
        Assert.That(sendLimitPatchSource, Does.Contain("[HarmonyPatch(\"OnEnterPressed\")]"));
        Assert.That(sendLimitPatchSource, Does.Contain("GetMaxChatSendCharacters"));
        Assert.That(sendLimitPatchSource, Does.Contain("Plugin.LockedMaxChatCharacters"));
        Assert.That(sendLimitPatchSource, Does.Contain("value == 250"));

        string playerIdInfoLimitPatchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "PlayerIdInfoLimitPatch.cs"));
        Assert.That(playerIdInfoLimitPatchSource, Does.Contain("[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.GetPlayerIdInfo))]"));
        Assert.That(playerIdInfoLimitPatchSource, Does.Contain("GetMaxIdCardCharacters"));
        Assert.That(playerIdInfoLimitPatchSource, Does.Contain("Plugin.LockedMaxIdCardCharacters"));
        Assert.That(playerIdInfoLimitPatchSource, Does.Contain("VanillaSafeMaxCharacters = 1000"));
        Assert.That(playerIdInfoLimitPatchSource, Does.Contain("value == VanillaSafeMaxCharacters"));
        Assert.That(playerIdInfoLimitPatchSource, Does.Contain("HasExpectedCoverage = replacedVanillaIdCardCaps == 6"));

        string copyChatPatchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "ChatCopyMessagePatch.cs"));
        string chatInteractionSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "ChatMessageInteractionController.cs"));
        Assert.That(copyChatPatchSource, Does.Contain("[HarmonyPatch(\"AddMessageUI\", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]"));
        Assert.That(copyChatPatchSource, Does.Contain("[HarmonyPatch(\"AddNotification\", typeof(string))]"));
        Assert.That(copyChatPatchSource, Does.Contain("AttachToCreatedRow"));
        Assert.That(copyChatPatchSource, Does.Contain("ChatCopySanitizer.ToClipboardText(sourceText)"));
        Assert.That(copyChatPatchSource, Does.Contain("ChatUrlDetector.TryGetFirstUrl(payload"));
        Assert.That(copyChatPatchSource, Does.Contain("ChatLinkFormatter.TryColorizeUrls"));
        Assert.That(chatInteractionSource, Does.Contain("Application.OpenURL(_safeUrl)"));
        Assert.That(chatInteractionSource, Does.Contain("Input.GetKey(KeyCode.LeftShift)"));
        Assert.That(chatInteractionSource, Does.Contain("LinkConfirmationSeconds"));
        Assert.That(chatInteractionSource, Does.Contain("Be careful with random links"));
        Assert.That(pluginSource, Does.Contain("FormatBlueSageNotice"));
        Assert.That(pluginSource, Does.Contain("FormatBlueSageWarning"));
        Assert.That(pluginSource, Does.Contain("FormatBlueSageAction"));
        Assert.That(pluginSource, Does.Contain("<color=#FFD700><b>{cleanAction}</b></color>"));
        Assert.That(chatInteractionSource, Does.Contain("\"Copied\""));
        Assert.That(chatInteractionSource, Does.Contain("\"Link copied\""));
        Assert.That(chatInteractionSource, Does.Contain("\"Opening chat link\""));
        Assert.That(chatInteractionSource, Does.Contain("GUIUtility.systemCopyBuffer = _payload"));
        Assert.That(chatInteractionSource, Does.Contain("GUIUtility.systemCopyBuffer = _safeUrl"));
        Assert.That(chatInteractionSource, Does.Contain("IPointerClickHandler"));
        Assert.That(chatInteractionSource, Does.Not.Contain("raycastTarget = false"));
        Assert.That(chatInteractionSource, Does.Not.Contain("color = Color.green"));

        string chatReadabilityPatchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "ChatReadabilityPatch.cs"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("[HarmonyPatch(\"AddMessageUI\", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("Plugin.ShouldApplyChatReadability"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("GetComponentsInChildren<TextMeshProUGUI>(true)"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RefreshVisibleChat"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("ApplyChatWindowHeight"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RestoreChatWindowHeight"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("OriginalSurfaceWindowSizes"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("OriginalWidth"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RectTransform.Axis.Vertical"));
        Assert.That(chatReadabilityPatchSource, Does.Not.Contain("RectTransform.Axis.Horizontal"));
        Assert.That(chatReadabilityPatchSource, Does.Not.Contain("ApplyChatContentWidth"));
        Assert.That(chatReadabilityPatchSource, Does.Not.Contain("OriginalSurfaceHeights"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("Plugin.LockedChatWindowHeightPercent"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("ChatWindowHeightPolicy.NormalizeHeight"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RequestRefreshSoon"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RefreshRequestVersion"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RefreshChatContents(ui)"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RefreshNewestRows()"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("ResolveChatScrollView"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("AddUniqueSurface"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("OwnedBackdropOverlays"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("PruneDestroyedState"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("RestoreReadability"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("gameObject.AddComponent<UiOutline>()"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("gameObject.AddComponent<UiShadow>()"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("OutlineColor = new Color(0f, 0f, 0f, 0.95f)"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("ApplyTmpMaterialOutline"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("ShaderUtilities.ID_OutlineWidth"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("ShaderUtilities.ID_OutlineColor"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("new Material(material)"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("DestroyOwnedMaterial"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("SetMaterialDirty"));
        Assert.That(chatReadabilityPatchSource, Does.Contain("SetVerticesDirty"));
        Assert.That(chatReadabilityPatchSource, Does.Not.Contain("text.text ="));
        Assert.That(chatReadabilityPatchSource, Does.Not.Contain("text.color ="));
        Assert.That(chatReadabilityPatchSource, Does.Not.Contain("SendMessageAsync"));

        string chatHistoryLimitPatchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "ChatHistoryLimitPatch.cs"));
        Assert.That(chatHistoryLimitPatchSource, Does.Contain("[HarmonyPatch(typeof(GameSettings))]"));
        Assert.That(chatHistoryLimitPatchSource, Does.Contain("[HarmonyPatch(nameof(GameSettings.GlobalMessageLimitCount), MethodType.Getter)]"));
        Assert.That(chatHistoryLimitPatchSource, Does.Contain("[HarmonyPatch(nameof(GameSettings.LocalMessageLimitCount), MethodType.Getter)]"));
        Assert.That(chatHistoryLimitPatchSource, Does.Contain("ChatHistoryLimitPolicy.ApplyConfiguredRows"));
        string chatHistoryLimitPolicySource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Core", "ChatHistoryLimitPolicy.cs"));
        Assert.That(chatHistoryLimitPolicySource, Does.Contain("Math.Max(originalLimit, ClampRows(configuredRows))"));
        Assert.That(chatHistoryLimitPolicySource, Does.Contain("DefaultRows = 150"));
        Assert.That(chatHistoryLimitPolicySource, Does.Contain("MinRows = 25"));
        Assert.That(chatHistoryLimitPolicySource, Does.Contain("MaxRows = 250"));

        string chatReadabilityLifecyclePatchSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "ChatReadabilityLifecyclePatch.cs"));
        Assert.That(chatReadabilityLifecyclePatchSource, Does.Contain("[HarmonyPatch(typeof(UIManager), \"Awake\")]"));
        Assert.That(chatReadabilityLifecyclePatchSource, Does.Contain("[HarmonyPatch(\"SetMessageFontOutline\", typeof(bool))]"));
        Assert.That(chatReadabilityLifecyclePatchSource, Does.Contain("AccessTools.Method(typeof(UIManager), \"SetMessageFontOutline\", new[] { typeof(bool) }) != null"));
        Assert.That(chatReadabilityLifecyclePatchSource, Does.Contain("[HarmonyPatch(typeof(TextChannelManager), \"ResetTextMaterials\")]"));
        Assert.That(chatReadabilityLifecyclePatchSource, Does.Contain("ChatReadabilityPatch.RequestRefreshSoon();"));

        string chatReadabilityControllerSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "ChatReadabilityController.cs"));
        Assert.That(pluginSource, Does.Contain("_chatReadabilityControllerObject"));
        Assert.That(pluginSource, Does.Contain("StartChatReadabilityController();"));
        Assert.That(pluginSource, Does.Contain("Destroy(_chatReadabilityControllerObject);"));
        Assert.That(chatReadabilityControllerSource, Does.Not.Contain("ActiveRefreshIntervalSeconds"));
        Assert.That(chatReadabilityControllerSource, Does.Not.Contain("IdleRefreshIntervalSeconds"));
        Assert.That(chatReadabilityControllerSource, Does.Contain("RefreshRequestVersion"));
        Assert.That(chatReadabilityControllerSource, Does.Contain("ChatReadabilityPatch.RefreshVisibleChat();"));
        Assert.That(chatReadabilityControllerSource, Does.Contain("Screen.width"));
        Assert.That(chatReadabilityControllerSource, Does.Contain("IsMessagePanelHidden"));
        Assert.That(chatReadabilityControllerSource, Does.Contain("LockedChatWindowHeightPercent"));

        string qolMenuSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));
        string styleHelperSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "StyleHelperWindow.cs"));
        Assert.That(qolMenuSource, Does.Contain("Getting started"));
        Assert.That(qolMenuSource, Does.Contain("Run QoL Status"));
        Assert.That(qolMenuSource, Does.Contain("Run Host Health"));
        Assert.That(qolMenuSource, Does.Not.Contain("Run Host Health\", \"Runs one quiet lobby-health check"));
        Assert.That(qolMenuSource, Does.Contain("Use /styleui for name, status, and Spoons identity edits."));
        Assert.That(qolMenuSource, Does.Not.Contain("DrawTextSetting(\"Status base name\""));
        Assert.That(qolMenuSource, Does.Not.Contain("DrawTextSetting(\"Status message\""));
        Assert.That(qolMenuSource, Does.Not.Contain("DrawTextSetting(\"Status color\""));
        Assert.That(qolMenuSource, Does.Not.Contain("DrawTextSetting(\"Status brackets\""));
        Assert.That(qolMenuSource, Does.Not.Contain("DrawTextSetting(\"Spoons 0-5\""));
        Assert.That(qolMenuSource, Does.Contain("Reset to BlueSage Harbor"));
        Assert.That(qolMenuSource, Does.Not.Contain("Undo Theme"));
        Assert.That(qolMenuSource, Does.Not.Contain("Revert Theme"));
        Assert.That(qolMenuSource, Does.Contain("Click a feature's On/Off button"));
        Assert.That(qolMenuSource, Does.Contain("Performance Overlay"));
        Assert.That(qolMenuSource, Does.Contain("DrawToggle(\"Performance Overlay\", Plugin.EnablePerformanceOverlay"));
        Assert.That(qolMenuSource, Does.Not.Contain("SplitChat"));
        Assert.That(qolMenuSource, Does.Not.Contain("Each row has Undo"));
        Assert.That(qolMenuSource, Does.Contain("Every editable row has Save and Undo"));
        Assert.That(qolMenuSource, Does.Contain("Chat window height"));
        Assert.That(qolMenuSource, Does.Contain("SaveChatWindowHeight"));
        Assert.That(qolMenuSource, Does.Contain("ChatWindowHeight="));
        Assert.That(qolMenuSource, Does.Not.Contain("Chat history rows"));
        Assert.That(qolMenuSource, Does.Contain("SaveChatHistoryRows"));
        Assert.That(qolMenuSource, Does.Contain("ChatRows="));
        Assert.That(styleHelperSource, Does.Contain("UndoLastAction"));
        Assert.That(styleHelperSource, Does.Contain("Name Styler"));
        Assert.That(styleHelperSource, Does.Contain("Status Styler"));
        Assert.That(styleHelperSource, Does.Not.Contain("Rewind"));
        Assert.That(styleHelperSource, Does.Not.Contain("new GUIContent(\"Revert\""));
        Assert.That(styleHelperSource, Does.Contain("Detect Current"));
        Assert.That(styleHelperSource, Does.Contain("Clear Draft"));
        Assert.That(styleHelperSource, Does.Not.Contain("Reset Current"));
        Assert.That(styleHelperSource, Does.Not.Contain("Clear the current tab's editor text"));
        Assert.That(styleHelperSource, Does.Contain("DrawSpoonsControls"));
        Assert.That(styleHelperSource, Does.Contain("MinWindowWidth = 920f"));
        Assert.That(styleHelperSource, Does.Contain("_statusColor"));
        Assert.That(styleHelperSource, Does.Contain("DrawStatusColorPicker"));
        Assert.That(styleHelperSource, Does.Contain("CleanColor(_statusColor)"));
        Assert.That(styleHelperSource, Does.Not.Contain("_colorA = Plugin.StatusColor?.Value"));
        Assert.That(styleHelperSource, Does.Not.Contain("DrawColorPicker(\"Status color\", 0)"));
        Assert.That(styleHelperSource, Does.Contain("DrawPresetColorLibrary("));
        Assert.That(styleHelperSource, Does.Contain("Color library: tap Color A/B/C first"));
        Assert.That(styleHelperSource, Does.Contain("Status color library: tap a swatch"));
        Assert.That(styleHelperSource, Does.Not.Contain("index += 6"));
        Assert.That(styleHelperSource, Does.Contain("State: "));
        Assert.That(styleHelperSource, Does.Not.Contain("STATE: \" + _stateLabel"));
        Assert.That(qolMenuSource, Does.Contain("Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl)"));
        Assert.That(File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Core", "HeaderActionLayoutPolicy.cs")), Does.Contain("ThunderstoreUrl = \"https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/\""));
        Assert.That(styleHelperSource, Does.Contain("https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/"));
        Assert.That(qolMenuSource, Does.Not.Contain("https://thunderstore.io/c/on-together/p/BlueSage/BlueSage_QoL_Tweaks_Beta/"));
        Assert.That(styleHelperSource, Does.Not.Contain("https://thunderstore.io/c/on-together/p/BlueSage/BlueSage_QoL_Tweaks_Beta/"));

        string typeaheadSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "CommandTypeaheadController.cs"));
        Assert.That(typeaheadSource, Does.Contain("PopupBackground"));
        Assert.That(typeaheadSource, Does.Contain("[\"chatheight\"]"));
        Assert.That(typeaheadSource, Does.Contain("ChatHeightChoices(\"chatheight\")"));
        Assert.That(typeaheadSource, Does.Contain("Choice(command, \"180\", \"very tall chat window\")"));
        Assert.That(typeaheadSource, Does.Contain("[\"chathistory\"]"));
        Assert.That(typeaheadSource, Does.Contain("ChatHistoryChoices"));
        Assert.That(typeaheadSource, Does.Not.Contain("background = MakeSolidTexture(theme.PreviewBackground)"));
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
