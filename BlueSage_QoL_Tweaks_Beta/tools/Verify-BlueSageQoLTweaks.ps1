param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'

$projectName = 'BlueSage_QoL_Tweaks_Beta'
$version = '0.2.7'
$projectFile = Join-Path $ProjectRoot "$projectName.csproj"
$manifestPath = Join-Path $ProjectRoot 'manifest.json'
$readmePath = Join-Path $ProjectRoot 'README.md'
$changelogPath = Join-Path $ProjectRoot 'CHANGELOG.md'
$iconPath = Join-Path $ProjectRoot 'icon.png'
$dllPath = Join-Path $ProjectRoot "bin\Release\net46\$projectName.dll"
$distDir = Join-Path $ProjectRoot 'dist'
$stageRoot = Join-Path $distDir 'thunderstore'
$stageDir = Join-Path $stageRoot $projectName
$zipPath = Join-Path $distDir "$projectName-$version.zip"
$privacyModulePath = Join-Path $PSScriptRoot 'BlueSagePackagePrivacy.psm1'
$protectedIdentityDigest = '27486274A8AE0725D898841956872712E436CB5BB90875B9F6AF631CB5863737'

Import-Module $privacyModulePath -Force

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Copy-FileStrict {
    param([string]$Source, [string]$Destination)
    Assert-True (Test-Path -LiteralPath $Source) "Missing required file: $Source"
    $parent = Split-Path -Parent $Destination
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent | Out-Null
    }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

if (-not $SkipBuild) {
    if (-not $SkipTests) {
        dotnet test (Join-Path $ProjectRoot "$projectName.Tests\$projectName.Tests.csproj")
        Assert-True ($LASTEXITCODE -eq 0) 'dotnet test failed.'
    }
    # --disable-build-servers: this verifier is also launched from inside `dotnet test`
    # (PublicPackageDocumentationContractTests), which reads its redirected stdout to the end. A
    # build server started here (Roslyn VBCSCompiler / MSBuild node) inherits that pipe and holds
    # it open until its own idle timeout, so the test stalled ~5-10 min whenever this build had to
    # start a fresh server (intermittent; seen 2026-10-17). No build servers, no inherited pipe.
    dotnet build $projectFile -c Release --disable-build-servers
    Assert-True ($LASTEXITCODE -eq 0) 'dotnet build failed.'
}
elseif ($SkipTests) {
    throw '-SkipTests cannot be combined with -SkipBuild.'
}

Assert-True (Test-Path -LiteralPath $dllPath) "Missing build output: $dllPath"

$pluginSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Plugin.cs') -Raw
$reconnectPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ReconnectGuardPolicy.cs') -Raw
$commandPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\BlueSageCommandPatch.cs') -Raw
$focusPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\FocusAnywherePatch.cs') -Raw
$panelPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\EnhancedPlayerPanelPatch.cs') -Raw
$panelSurfaceControllerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\PlayerPanelSurfaceController.cs') -Raw
$panelSurfacePolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\PlayerPanelSurfacePolicy.cs') -Raw
$nativeIdCardControllerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\NativeIdCardController.cs') -Raw
$miniMapPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\MiniMapPlayerLabelPatch.cs') -Raw
$chatReadabilityPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatReadabilityPatch.cs') -Raw
$chatReadabilityLifecyclePatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatReadabilityLifecyclePatch.cs') -Raw
$chatReadabilityControllerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\ChatReadabilityController.cs') -Raw
$chatReadabilityStylePolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ChatReadabilityStylePolicy.cs') -Raw
$configMigrationPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ConfigMigrationPolicy.cs') -Raw
$sweepSettingsPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\SweepSettingsPolicy.cs') -Raw
$rescueReceiverCapabilitySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\RescueReceiverCapabilityPolicy.cs') -Raw
$chatAccessibilityScalePolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ChatAccessibilityScalePolicy.cs') -Raw
$chatFontSizePolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ChatFontSizePolicy.cs') -Raw
$chatWindowHeightPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ChatWindowHeightPolicy.cs') -Raw
$chatHistoryLimitPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ChatHistoryLimitPolicy.cs') -Raw
$chatHistoryLimitPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatHistoryLimitPatch.cs') -Raw
$chatInteractionSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatMessageInteractionController.cs') -Raw
$windowFitPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\WindowFitPolicy.cs') -Raw
$windowCoordinatorSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\BlueSageWindowCoordinator.cs') -Raw
$mentionNamePolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\MentionNamePolicy.cs') -Raw
$mentionHighlighterSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\MentionHighlighterPatch.cs') -Raw
$chatSenderContextSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatSenderContextPatch.cs') -Raw
$mentionPingControllerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\MentionPingController.cs') -Raw
$playerMentionProviderSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\PlayerMentionProvider.cs') -Raw
$chatCopyPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatCopyMessagePatch.cs') -Raw
$chatCopySanitizerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ChatCopySanitizer.cs') -Raw
$chatLinkFormatterSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ChatLinkFormatter.cs') -Raw
$sessionAuditSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\SessionAuditController.cs') -Raw
$sessionAuditPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\SessionAuditPolicy.cs') -Raw
$auditSnapshotPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\AuditSnapshotPolicy.cs') -Raw
$navigationCalloutSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\NavigationCalloutPolicy.cs') -Raw
$uiThemeSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\BlueSageUiTheme.cs') -Raw
$playerLookupPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\PlayerLookupPolicy.cs') -Raw
$typeaheadSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\CommandTypeaheadController.cs') -Raw
$playerLimitCompanionSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\PlayerLimitCompanionStatus.cs') -Raw
$easterEggTrackerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\EasterEggTracker.cs') -Raw
$qolMenuSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\QolMenuWindow.cs') -Raw
$supporterLedgerPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\SupporterLedgerPolicy.cs') -Raw
$supporterLedgerDataSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Data\SupporterLedger.g.cs') -Raw
$avatarStyleSlotsSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\AvatarStyleSlotsPatch.cs') -Raw
$avatarSlotStoreSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\AvatarSlotStore.cs') -Raw
$cloneShieldSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\CloneShieldController.cs') -Raw
$moderationRelaySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ModerationRelayPatch.cs') -Raw
$moderationControllerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\SteamIdModerationController.cs') -Raw
$banTargetPresencePolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\BanTargetPresencePolicy.cs') -Raw
$nativeBanAuditPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\NativeBanAuditPatch.cs') -Raw
$moderationProtocolSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ModerationProtocolPolicy.cs') -Raw
$moderationCapabilitySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\ModerationCapabilityPolicy.cs') -Raw
$identityPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\PlayerIdentityEvidencePolicy.cs') -Raw
$identityControllerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\PlayerIdentityEvidenceController.cs') -Raw
$identityTooltipSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\PlayerIdentityTooltipPatch.cs') -Raw
$unifiedHelperPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\UnifiedHelperPolicy.cs') -Raw
$cloneIncidentActionSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\CloneIncidentActionController.cs') -Raw
$cloneIncidentResolutionSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\CloneIncidentResolutionProtocolPolicy.cs') -Raw
$identityRoleHighlightSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\PlayerIdentityRoleHighlightController.cs') -Raw
$unlimitedConsumablesPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\UnlimitedConsumablesPolicy.cs') -Raw
$unlimitedConsumablesPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\UnlimitedConsumablesPatch.cs') -Raw
$publicSourceText = (Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src') -Recurse -Filter '*.cs' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$requiredReusableItems = @(
    'CottonCandyPink', 'LemonZestYellow', 'BlueBerrySplashBlue', 'GrapePopPurple', 'SourAppleGreen',
    'ColaTurbo', 'OrangeBlast', 'LimeShock', 'Boba1', 'Boba2', 'Boba3', 'IceCream', 'CottonCandy',
    'CottonGreen', 'CottonPurple', 'CottonLemon', 'CottonBlue', 'RainbowPopcorn', 'Apple', 'Water',
    'Chalk0', 'Chalk1', 'Chalk2', 'Chalk3', 'Chalk4'
)
foreach ($itemName in $requiredReusableItems) {
    Assert-True ($unlimitedConsumablesPolicySource.Contains('"' + $itemName + '"')) "Reusable Consumables allowlist is missing current game item: $itemName"
}
Assert-True ($pluginSource.Contains('EnableUnlimitedConsumables = Config.Bind("Consumables", "EnableUnlimitedConsumables", true')) 'Reusable Consumables must default On.'
Assert-True ($unlimitedConsumablesPatchSource.Contains('InventoryManager.DecreaseItemCount')) 'Reusable Consumables must patch only the known inventory decrement seam.'
Assert-True (-not $unlimitedConsumablesPatchSource.Contains('EconomyManager') -and -not $unlimitedConsumablesPatchSource.Contains('RemoveTicket') -and -not $unlimitedConsumablesPatchSource.Contains('FishingManager')) 'Reusable Consumables must not patch ticket, shop, or fishing economy paths.'
$reusableBaitPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ReusableBaitPatch.cs') -Raw
Assert-True ($reusableBaitPatchSource.Contains('ShouldApplyUnlimitedConsumables') -and -not $reusableBaitPatchSource.Contains('EconomyManager') -and -not $reusableBaitPatchSource.Contains('RemoveTicket') -and -not $reusableBaitPatchSource.Contains('AddFishTicket')) 'Reusable bait may only restore the owned bait count; never tickets, shop, or catch rewards (2026-10-17).'
Assert-True (-not $publicSourceText.Contains('WatchNetwork')) 'Unfinished cross-lobby Watch Network prototype must not ship in public source.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'tools\New-WatchNetworkOwnerKey.ps1'))) 'Owner signing-key tooling must not ship with the public project.'
Assert-True ($avatarStyleSlotsSource.Contains('internal const int NativeSlotCount = 3;') -and $avatarStyleSlotsSource.Contains('internal const int TotalSlotCount = 9;')) 'Avatar style bridge must expose three native plus six additive QoL slots.'
# Round 4 review R4 (2026-10-19): the old '.cfg' pin passed vacuously once slots moved to the crash-safe store.
Assert-True ($avatarStyleSlotsSource.Contains('com.bluesage.ontogether.qol-avatar-styles.store') -and $avatarStyleSlotsSource.Contains('AvatarSlotStore.Save(_storePath, _selectedSlot, _legacyConfigHash, SlotNames, SlotPayloads)')) 'Avatar slots 4-9 must persist through the BlueSage crash-safe store, never the game save or a BepInEx ConfigFile.'
Assert-True ($avatarStyleSlotsSource.Contains('SaveOnConfigSet = false') -and -not $avatarStyleSlotsSource.Contains('.Value = ')) 'The legacy qol-avatar-styles.cfg is read only (migration and rollback re-import); it is never written.'
Assert-True ($avatarSlotStoreSource.Contains('Flush(true)') -and $avatarSlotStoreSource.Contains('File.Replace(tempPath, mainPath, mainComplete ? mainBackup : null)') -and $avatarSlotStoreSource.Contains('AvatarSlotStoreFormat.ChooseFirstValid(')) 'The avatar slot store must flush to disk, swap atomically with main rotating into backup 1, and load the newest complete file.'
Assert-True ($avatarStyleSlotsSource.Contains('LegacyConfigReimportPolicy.Decide(') -and $avatarStyleSlotsSource.Contains('AvatarSlotStore.BackupBeforeReimport(')) 'A changed legacy .cfg must re-import only after the current store is copied aside.'
Assert-True ($pluginSource.Contains('EnableExtendedAvatarStyles = Config.Bind("Avatar Styles", "EnableExtendedAvatarStyles", true')) 'Validated avatar slots 4-9 must default on with a reversible config toggle.'
Assert-True (-not $publicSourceText.Contains('EXPERIMENTAL - Avatar Presets 4-9')) 'Validated avatar slots 4-9 must not retain stale experimental labeling.'
Assert-True ($avatarStyleSlotsSource.Contains('DisableAndRestoreVanilla') -and $avatarStyleSlotsSource.Contains('QoL preset data retained')) 'Disabling avatar slots 4-9 must restore vanilla UI/state without deleting QoL presets.'
Assert-True ($pluginSource.Contains('EnableChalkboardPersistence = Config.Bind("Chalkboards", "EnableChalkboardPersistence", true')) 'Validated chalkboard persistence must default on and retain compatibility stand-down.'
Assert-True (-not $avatarStyleSlotsSource.Contains('CustomizationDataIDsDict =') -and -not $avatarStyleSlotsSource.Contains('CustomizationDataIDs1Dict =') -and -not $avatarStyleSlotsSource.Contains('CustomizationDataIDs2Dict =')) 'Avatar style bridge must never assign vanilla slot payload fields directly.'
Assert-True (-not $avatarStyleSlotsSource.Contains('nativeTexts.Add(') -and -not $avatarStyleSlotsSource.Contains('nativeBgs.Add(')) 'Avatar style bridge must not expand vanilla indexed UI lists.'
foreach ($privateSurface in @(
    'BlueSage_QoL_Tweaks_BlueExclusive',
    'Private Experimental Lab',
    'Ghost Probe',
    'Host Broadcast',
    'Publish Metadata',
    'Clone Outfit',
    'Invisible Model'
)) {
    Assert-True (-not $publicSourceText.Contains($privateSurface)) "Public source contains a private companion surface: $privateSurface"
}
Assert-True ($pluginSource.Contains('[BepInPlugin(PluginGuid, PluginName, PluginVersion)]')) 'Missing BepInPlugin attribute.'
Assert-True ($pluginSource.Contains('[BepInProcess("OnTogether.exe")]')) 'Missing BepInProcess attribute.'
Assert-True ($pluginSource.Contains('_harmony.UnpatchSelf();')) 'Missing Harmony unpatch on destroy.'
Assert-True (-not $pluginSource.Contains('HiddenDiagnostics') -and -not $pluginSource.Contains('[BlueSageDiag]')) 'Round 4: /bluedevdebug hidden diagnostics must stay removed (menu audit copy replaces them).'
Assert-True ($pluginSource.Contains('0.2.7+20261025.round5h-rc7')) 'Working source identity must bind the October 23 round-5d 0.2.7 build (QoL version badge folded into 0.2.7).'
Assert-True ($pluginSource.Contains('EnableLocalAuditExports = Config.Bind("Local Audit", "EnableLocalAuditExports", false') -and $pluginSource.Contains('ConfigMigrationPolicy.LocalAuditOffV6Migration')) 'Round 5d: local chat and BepInEx audit exports are optional, off by default, and existing installs turn off once.'
Assert-True ($pluginSource.Contains('"auditlog"') -and $pluginSource.Contains('HandleAuditLogCommand')) 'Local audit status, opt-out, path, and snapshot command must be registered.'
Assert-True ($chatCopyPatchSource.IndexOf('SessionAuditController.RecordChat') -ge 0 -and $chatCopyPatchSource.IndexOf('SessionAuditController.RecordChat') -lt $chatCopyPatchSource.IndexOf('Plugin.ShouldApplyChatCopy')) 'Chat audit capture must remain independent of the click-to-copy toggle.'
Assert-True ($sessionAuditSource.Contains('internal static string AuditRoot => ModSuitePaths.Current.Logs;') -and $sessionAuditSource.Contains('Path.Combine(Paths.BepInExRootPath, "LogOutput.log")')) 'Round 5d: local audit lives in BepInEx/BlueSageQoLModSuite/logs and mirrors the running BepInEx log.'
Assert-True ($sessionAuditSource.Contains('MaxChatPartBytes = LocalFileBudgetPolicy.AuditChatPartBytes') -and $sessionAuditSource.Contains('MaxBepInExPartBytes = LocalFileBudgetPolicy.AuditMirrorPartBytes') -and $sessionAuditSource.Contains('MaxChatParts = LocalFileBudgetPolicy.AuditChatParts') -and $sessionAuditSource.Contains('MaxBepInExParts = LocalFileBudgetPolicy.AuditMirrorParts') -and $sessionAuditSource.Contains('ModSuitePaths.RunRetentionInBackground(_sessionDirectory)')) 'Round 5d: local audit parts come from LocalFileBudgetPolicy; older sessions are zipped by the shared retention pass.'
Assert-True ($sessionAuditSource.Contains('redacted-normal-client') -and $sessionAuditSource.Contains('Plugin.CanUseLobbySafety(out bool isHost)')) 'Structured exact-identity audit enrichment must stay host/Helper scoped.'
Assert-True ($sessionAuditPolicySource.Contains('displayNameRaw') -and $sessionAuditPolicySource.Contains('displayNamePlain') -and $sessionAuditPolicySource.Contains('messageRaw') -and $sessionAuditPolicySource.Contains('messagePlain') -and $sessionAuditPolicySource.Contains('captureStage') -and $sessionAuditPolicySource.Contains('observerAuthority') -and $sessionAuditPolicySource.Contains('senderRole')) 'Chat audit must preserve rendered raw rich-text evidence beside plain text without confusing observer authority with sender role.'
Assert-True (-not $sessionAuditSource.Contains('HttpClient') -and -not $sessionAuditSource.Contains('WebClient') -and -not $sessionAuditSource.Contains('WebRequest')) 'Public local audit must not contain an upload or network client.'
Assert-True ($playerLookupPolicySource.Contains('TimeSpan.FromMilliseconds(30)') -and $playerLookupPolicySource.Contains('budget.ElapsedMilliseconds >= 50') -and $playerLookupPolicySource.Contains('MaximumQueryCharacters = 96') -and $playerLookupPolicySource.Contains('MinimumPartialSteamIdDigits = 4')) 'Live player lookup regex and partial-ID matching must remain bounded per expression and total lookup.'
Assert-True ($sessionAuditSource.Contains("Local Audit is on but couldn't save files just now.") -and $sessionAuditSource.Contains("The snapshot couldn't be saved this time.") -and $sessionAuditSource.Contains('return WriteTroubleText;') -and $sessionAuditSource.Contains('return SnapshotFailedText;')) 'Audit status and snapshot must fail visibly (calmly) when local writers are unavailable.'
Assert-True ($sessionAuditSource.Contains('AuditSnapshotPolicy.TryCreate') -and $sessionAuditSource.Contains('bytes from the true BepInEx LogOutput.log')) 'Snapshot Now must create and report a true live BepInEx log snapshot.'
Assert-True ($auditSnapshotPolicySource.Contains('temporaryPath = finalPath + ".tmp";') -and$auditSnapshotPolicySource.Contains('FileShare.ReadWrite | FileShare.Delete') -and $auditSnapshotPolicySource.Contains('MaxRetainedSnapshots = LocalFileBudgetPolicy.AuditSnapshotsPerSession')) 'Audit snapshots must be atomic, live-log compatible, and bounded.'
Assert-True ($sessionAuditSource.Contains("Anything from while it was off wasn't saved.") -and $sessionAuditSource.Contains('ResumedText,') -and $sessionAuditSource.Contains('AdvanceBepInExSourceOffsetToEnd')) 'Audit opt-out must create an explicit gap and never backfill disabled-period BepInEx output.'
Assert-True ($nativeBanAuditPatchSource.Contains('DataManager.SaveBanData') -and $nativeBanAuditPatchSource.Contains('CapturePersistedNativeBanSave') -and $nativeBanAuditPatchSource.Contains('HarmonyFinalizer')) 'Every successful native BanData persistence sink must trigger immediate audit capture and clear failed pending context.'
Assert-True ($moderationControllerSource.Contains('DisplayNameRaw') -and $sessionAuditPolicySource.Contains('issuerSteamId64') -and $sessionAuditPolicySource.Contains('incidentId') -and $sessionAuditPolicySource.Contains('targetRole')) 'Moderation evidence must retain raw nickname, issuer, incident, and role metadata.'
Assert-True ($pluginSource.Contains('bare confirm is refused') -and $moderationControllerSource.Contains('issuer-local:')) 'Moderation confirmation and Helper-local incident attribution must remain fail-closed.'
Assert-True ($moderationControllerSource.Contains('SteamMatchmaking.SetLobbyMemberData') -and -not $moderationControllerSource.Contains('TrySendLobbyChatMessage(request)') -and $cloneIncidentActionSource.Contains('SteamMatchmaking.SetLobbyMemberData') -and -not $cloneIncidentActionSource.Contains('TrySendLobbyChatMessage(packet)')) 'Helper moderation and incident-resolution requests must use non-chat Steam member data regardless of Fomo or other chat-hook mods.'
Assert-True (-not $pluginSource.Contains('ArmReconnectJoinWatchdog')) 'Ordinary JoinByCode must not arm reconnect cleanup.'
Assert-True (-not $pluginSource.Contains('LobbyJoinWatchdogLoop')) 'The destructive ordinary-join watchdog must remain removed.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ReconnectJoinWatchdogPatch.cs'))) 'ReconnectJoinWatchdogPatch must not ship.'
Assert-True (-not $pluginSource.Contains('join watchdog timeout:')) 'Join timeout must not be a reconnect recovery origin.'
Assert-True ($pluginSource.Contains('LockedReconnectCleanupTimeoutSeconds = 10')) 'Reconnect cleanup/rejoin start budget must be 10 seconds.'
Assert-True ($pluginSource.Contains('LockedReconnectJoinValidationTimeoutSeconds = 45')) 'Large-lobby post-join validation must retain its 45-second budget.'
Assert-True (-not $pluginSource.Contains('bool joinIdle = multiplayerManager == null || !multiplayerManager.IsConnecting')) 'Vanilla IsConnecting must not block clean-menu readiness after a successful join.'
Assert-True ($pluginSource.Contains('blueSageJoinIdle')) 'Clean-menu readiness must use generation-owned BlueSage join state.'
Assert-True ($pluginSource.Contains('Resources.UnloadUnusedAssets();')) 'Missing asset sweep call.'
# Round 4 WS2 (Blue, 2026-10-19): the deep sweep diagnostics (full asset census + process/thread census) were removed.
Assert-True (-not $pluginSource.Contains('EvidenceCapture.Capture()') -and -not $pluginSource.Contains('EvidenceFormatter.Format(')) 'Deep sweep diagnostics must stay removed.'

Assert-True ($pluginSource.Contains('PluginCompatibility.Evaluate')) 'Missing compatibility evaluation.'
Assert-True ($pluginSource.Contains('AllowManualSweepWhenAndrewSweepInstalled')) 'Missing manual sweep compatibility override config.'
Assert-True ($pluginSource.Contains('AutoDisableSweepWhenAndrewSweepInstalled = Config.Bind("Asset Sweep", "AutoDisableSweepWhenAndrewSweepInstalled", true')) 'AndrewLin Sweep auto-disable must default on.'
Assert-True ($pluginSource.Contains('requireManualPermission: true')) 'Manual sweep path must require compatibility permission.'
Assert-True ($pluginSource.Contains('Chainloader.PluginInfos.Keys')) 'Compatibility detection must use loaded BepInEx plugin IDs.'
Assert-True ($pluginSource.Contains('delayed post-load recheck')) 'Missing delayed compatibility re-check.'
Assert-True ($pluginSource.Contains('EnableLeaveNotifications = Config.Bind("Notifications", "EnableLeaveNotifications", true')) 'Leave notifications must default on for beta.'
Assert-True (-not $pluginSource.Contains('EnableInputFieldLimits = Config.Bind')) 'Longer text fields must not expose a normal on/off config toggle.'
Assert-True (-not $pluginSource.Contains('EnableSessionNameLimit = Config.Bind')) 'Session name limit must not expose a normal on/off config toggle.'
Assert-True (-not $pluginSource.Contains('EnableChatInputLimit = Config.Bind')) 'ChatLimitRemover must not expose a normal on/off config toggle.'
Assert-True ($pluginSource.Contains('MaxChatCharacters = Config.Bind("Text Limits", "MaxChatCharacters", publicDefaults.ChatTextLimit')) 'ChatLimitRemover max characters must default safe but be configurable.'
Assert-True ($pluginSource.Contains('MaxProfileCharacters = Config.Bind("Text Limits", "MaxProfileCharacters", publicDefaults.ProfileTextLimit')) 'Profile/text field max characters must use the qualified public default and remain configurable.'
Assert-True ($pluginSource.Contains('MaxSessionNameCharacters = Config.Bind("Text Limits", "MaxSessionNameCharacters", publicDefaults.RoomNameLimit')) 'Room/session-name max characters must use the qualified public default and remain configurable.'
Assert-True (-not $pluginSource.Contains('ReconnectGuardRetryIntervalSeconds = Config.Bind')) 'Reconnect retry interval must not be user-configurable.'
Assert-True (-not $pluginSource.Contains('ReconnectGuardMaxAttempts = Config.Bind')) 'Reconnect max attempts must not be user-configurable.'
Assert-True (-not $pluginSource.Contains('ReconnectGuardManageHostKick = Config.Bind')) 'Host-kick reconnect handling must not be user-configurable.'
Assert-True (-not $pluginSource.Contains('ReconnectAnnouncementMessage = Config.Bind')) 'Reconnect announcement message must not be user-configurable.'
Assert-True (-not $pluginSource.Contains('ReconnectAnnouncementDelaySeconds = Config.Bind')) 'Reconnect announcement delay must not be user-configurable.'
Assert-True (-not $pluginSource.Contains('HostHealthIntervalSeconds = Config.Bind')) 'Host Health interval must not be user-configurable.'
Assert-True (-not $pluginSource.Contains('HostHealthRefreshLobbyMetadata = Config.Bind')) 'Host Health metadata refresh must not be user-configurable.'
Assert-True (-not $pluginSource.Contains('HostHealthRequestPersonaRefresh = Config.Bind')) 'Host Health persona refresh must not be user-configurable.'
Assert-True ($pluginSource.Contains('LockedMaxChatCharacters => InputFieldLimitPolicy.ClampChatCharacters')) 'Chat input character max must clamp user config through the shared policy.'
Assert-True ($pluginSource.Contains('LockedMaxIdCardCharacters => InputFieldLimitPolicy.ClampProfileCharacters')) 'ID/profile character max must clamp user config through the shared policy.'
Assert-True ($pluginSource.Contains('LockedMaxSessionNameCharacters => InputFieldLimitPolicy.ClampSessionNameCharacters')) 'Room/session-name character max must clamp user config through the shared policy.'
Assert-True ($pluginSource.Contains('LockedReconnectRetryIntervalSeconds = 5')) 'Reconnect retry interval must be locked to 5 seconds.'
Assert-True ($pluginSource.Contains('LockedReconnectMaxAttempts = 3')) 'Reconnect max attempts must be locked to 3.'
Assert-True ($pluginSource.Contains('LockedHostHealthIntervalSeconds = 300')) 'Host Health interval must be locked to 5 minutes.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\InputFieldLimitPolicy.cs') -Raw).Contains('public const int DefaultMaxChatCharacters = 4000')) 'Chat input character max must default to 4000 (Blue, 2026-10-17).'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\InputFieldLimitPolicy.cs') -Raw).Contains('public const int DefaultMaxProfileCharacters = 4000')) 'Profile field character max must default to 4000.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\InputFieldLimitPolicy.cs') -Raw).Contains('public const int DefaultMaxSessionNameCharacters = 4000')) 'Session name character max must default to 4000.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\InputFieldLimitPolicy.cs') -Raw).Contains('public const int AbsoluteMaxChatCharacters = 4000')) 'Chat input character max must hard-cap at 4000.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\InputFieldLimitPolicy.cs') -Raw).Contains('public const int AbsoluteMaxProfileCharacters = 4000')) 'Profile field character max must hard-cap at 4000.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\InputFieldLimitPolicy.cs') -Raw).Contains('public const int AbsoluteMaxSessionNameCharacters = 4000')) 'Session name character max must hard-cap at 4000.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\SweepSettingsPolicy.cs') -Raw).Contains('public const int MinimumIntervalMinutes = 5')) 'Sweep interval minimum must be 5 minutes.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\SweepSettingsPolicy.cs') -Raw).Contains('public const int DefaultIntervalMinutes = 40')) 'Sweep interval default must be 40 minutes: clear of the game''s 30-minute cleanup, still one BlueSage sweep inside every 75 minutes (Blue, 2026-10-20, round 4 Stage G).'
Assert-True ($sweepSettingsPolicySource.Contains('MigrateOverlapAvoidanceInterval') -and $sweepSettingsPolicySource.Contains('configuredMinutes == TenMinuteSafetyMigrationInterval || configuredMinutes == LegacyFifteenMinuteDefault') -and $pluginSource.Contains('ConfigMigrationPolicy.SweepFortyMinuteOverlapV4Migration') -and $pluginSource.Contains('AutosaveBoundaryPolicy.ShouldDefer(autosaveTimerSeconds)')) 'Round 4 Stage G: the one-time 10/15 -> 40 sweep migration and the autosave-boundary deferral must stay wired (Marbles, 2026-10-20).'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\SweepSettingsPolicy.cs') -Raw).Contains('public const int MaximumIntervalMinutes = 180')) 'Sweep interval maximum must be 180 minutes.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatSendLimitPatch.cs') -Raw).Contains('[HarmonyPatch("OnEnterPressed")]')) 'ChatLimitRemover must patch the local OnEnterPressed send cap.'
Assert-True ((Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\ChatSendLimitPatch.cs') -Raw).Contains('value == 250')) 'ChatLimitRemover must replace the vanilla 250-character send cap.'
$inputFieldLimitPolicySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\InputFieldLimitPolicy.cs') -Raw
$playerIdInfoLimitPatchSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\PlayerIdInfoLimitPatch.cs') -Raw
Assert-True ($inputFieldLimitPolicySource.Contains('"FavoriteQuoteInputField"')) 'ID-card About Me/FavoriteQuote input field must use the profile text limit.'
Assert-True ($playerIdInfoLimitPatchSource.Contains('[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.GetPlayerIdInfo))]')) 'ID-card publish data must patch PlayerData.GetPlayerIdInfo.'
Assert-True ($playerIdInfoLimitPatchSource.Contains('GetMaxIdCardCharacters')) 'ID-card publish data must read the shared Name/ID limit.'
Assert-True ($playerIdInfoLimitPatchSource.Contains('Plugin.LockedMaxIdCardCharacters')) 'ID-card publish data must use the clamped runtime Name/ID limit.'
Assert-True ($playerIdInfoLimitPatchSource.Contains('VanillaSafeMaxCharacters = 1000')) 'ID-card publish data must retain the reviewed vanilla-safe 1000-character fallback.'
Assert-True ($playerIdInfoLimitPatchSource.Contains('value == VanillaSafeMaxCharacters')) 'ID-card publish data must replace the reviewed vanilla field caps.'
Assert-True ($playerIdInfoLimitPatchSource.Contains('HasExpectedCoverage = replacedVanillaIdCardCaps == 6')) 'ID-card publish data must prove all six reviewed field caps before extended-name use.'
Assert-True ($playerIdInfoLimitPatchSource.Contains('if (!HasExpectedCoverage)')) 'ID-card publish data must warn and fail safe if the six reviewed caps drift.'
Assert-True ($pluginSource.Contains('EnableReconnectAnnouncement = Config.Bind("Reconnect Guard", "EnableReconnectAnnouncement", true')) 'Reconnect announcement must default on.'
Assert-True ($pluginSource.Contains('LockedReconnectAnnouncementMessage = "I''m back after a connection hiccup (BlueSage QoL)."')) 'Reconnect announcement message must match the voice-pass (2026-10-23) community wording.'
Assert-True ($pluginSource.Contains('LockedReconnectAnnouncementDelaySeconds = 3')) 'Reconnect announcement delay must default to 3 seconds.'
Assert-True ($pluginSource.Contains('TrySendLobbyChatMessage')) 'Reconnect success announcement must attempt in-game chat send.'
Assert-True ($pluginSource.Contains('StartCoroutine(DelayedReconnectAnnouncement(message))')) 'Reconnect success announcement must be delayed before chat send.'
Assert-True ($pluginSource.Contains('case ReconnectGuardAction.Noop')) 'Reconnect guard must handle Noop decisions.'
Assert-True ($pluginSource.Contains('ReconnectGuardAction.StopManagedRetry')) 'Reconnect guard bridge must handle StopManagedRetry decisions explicitly.'
Assert-True ($pluginSource.Contains('RegisterBlueSageCommands();')) 'Plugin must register internal BlueSage commands.'
Assert-True ($pluginSource.Contains('"modhelper"') -and $pluginSource.Contains('"sidlist"') -and $pluginSource.Contains('"sidwho"') -and $pluginSource.Contains('"sidlookup"') -and $pluginSource.Contains('"sidban"') -and $pluginSource.Contains('"sidunban"') -and $pluginSource.Contains('"unbansid"')) 'Host/helper Steam-ID evidence, moderation commands, and aliases must be registered.'
Assert-True ($identityPolicySource.Contains('TryResolveExactRosterIndex') -and $identityPolicySource.Contains('IsVerifiedActiveRow') -and $identityPolicySource.Contains('IsUniqueCurrentLobbyMember')) 'Player identity evidence must verify each active row through unique native SteamID64, PlayerID, live transform, and current Steam membership.'
Assert-True ($identityControllerSource.Contains('TryResolveExact') -and $identityControllerSource.Contains('TryResolveLookup') -and $identityControllerSource.Contains('GetNumLobbyMembers') -and $identityControllerSource.Contains('UnresolvedRowState.LiveSteamCheck') -and $identityControllerSource.Contains('UnresolvedRowState.LeftStale')) 'Identity lookup must preserve the live-native/Steam-check split while reporting truly departed history separately.'
Assert-True ($identityControllerSource.Contains('roster.Count == 0') -and $identityControllerSource.Contains('Exact verified rows remain usable; Clone Shield and absence-sensitive actions wait for complete coverage.') -and $identityControllerSource.Contains('player-list rows') -and $identityControllerSource.Contains('Steam lobby members') -and $identityControllerSource.Contains('non-actionable.')) 'Partial verified rows must remain usable while count truth and complete-coverage gates stay explicit.'
Assert-True ($identityControllerSource.Contains('private sealed class RosterSnapshot') -and $identityControllerSource.Contains('LargeLobbyRefreshPolicy.RosterSnapshotLifetime.Ticks') -and $identityControllerSource.Contains('CaptureRosterSnapshot(cacheAllowed: false)') -and $identityControllerSource.Contains('CapturedFrame == Time.frameCount')) 'Display truth must use one bounded immutable snapshot per frame while moderation actions retain a fresh authoritative capture.'
Assert-True ($identityControllerSource.Contains('BuildRoleContext(lobby, lobbyMembers)') -and $identityControllerSource.Contains('RoleBySteamId.TryGetValue') -and -not $identityControllerSource.Contains('Role = BuildRole(lobby, steamId)') -and $moderationControllerSource.Contains('GetPublishedHelpers')) 'Roster roles must parse Helper and incident state once per snapshot instead of once per verified row.'
Assert-True (-not $panelPatchSource.Contains('PlayerIdentityRoleHighlightController.RefreshAll();') -and $panelPatchSource.Contains('PlayerPanelSurfaceController.RefreshRow(')) 'Player row initialization must route through the bounded row surface controller and never launch a full-panel identity refresh.'
Assert-True ($moderationControllerSource.Contains('MemberDataPollIntervalMilliseconds = 1000') -and $cloneIncidentActionSource.Contains('MemberDataPollIntervalMilliseconds = 1000')) 'Idle moderation member-data polling must stay bounded to once per second.'
Assert-True ($identityTooltipSource.Contains('PlayerPanelButtonType.AddFriend') -and $identityTooltipSource.Contains('Steam Profile • VERIFIED') -and $identityTooltipSource.Contains('Steam Profile • LIVE') -and $identityTooltipSource.Contains('Steam Profile • LIVE / STEAM CHECK') -and $identityTooltipSource.Contains('Steam Profile • LEFT / STALE') -and $identityTooltipSource.Contains('Steam Profile • CHECKING')) 'The native Steam-profile hover must distinguish live, live/Steam-check, stale-history, syncing, and authorized verified states without judging mod installation.'
Assert-True ($identityTooltipSource.IndexOf('ExpandProfilePopup(__instance, body);') -lt $identityTooltipSource.IndexOf('TryResolveExact') -and $identityTooltipSource.Contains('TextOverflowModes.Ellipsis') -and $identityTooltipSource.Contains('body.fontSize = _defaultBodyFontSize') -and $identityTooltipSource.Contains('header.color = _defaultHeaderColor')) 'Every BlueSage-authored profile status must use bounded popup geometry before exact identity resolution, while later native tooltips restore font and header color state.'
Assert-True ($pluginSource.Contains('This command is read-only') -and $pluginSource.Contains('cloned names never authorize or select a ban target')) '/sidwho must remain read-only and separate from ban authority.'
Assert-True ($pluginSource.Contains('LobbyHelpers = Config.Bind("Helpers", "LobbyHelpers"')) 'Helpers must use one host-owned exact-SteamID64 list.'
Assert-True ($pluginSource.Contains('HandleHelperCommand') -and $pluginSource.Contains('"lobbyhelper"') -and $pluginSource.Contains('"modhelper"')) 'The primary /helper command and compatibility aliases must share one handler.'
Assert-True ($unifiedHelperPolicySource.Contains('MergeForOwner') -and $pluginSource.Contains('GetUnifiedHelpersForOwner') -and $pluginSource.Contains('SaveUnifiedHelpers')) 'Legacy helper lists must migrate into the canonical host-owned Helper list.'
Assert-True ($moderationCapabilitySource.Contains('v3-memberdata') -and $moderationCapabilitySource.Contains('MaxLeaseAgeSeconds = 20')) 'Helper access must require the fresh transport-specific v3 member-data capability.'
Assert-True ($moderationProtocolSource.Contains('MemberDataKey') -and $moderationProtocolSource.Contains('BuildMemberData') -and $moderationProtocolSource.Contains('RequestExpirySeconds = 15')) 'Moderation requests must use versioned, short-lived Steam member data.'
Assert-True ($moderationRelaySource.Contains('HandleRPCGenerated_0') -and $moderationRelaySource.Contains('HarmonyPriority(Priority.First)')) 'Moderation relay must intercept the generated server RPC before native chat relay.'
Assert-True ($moderationRelaySource.Contains('Rejected a legacy chat-shaped BlueSage moderation request') -and -not $moderationRelaySource.Contains('HandleServerRequest(text') -and -not $moderationRelaySource.Contains('HandleServerResolutionRequest(text')) 'Legacy chat-shaped moderation and incident requests must be consumed without executing.'
Assert-True ($moderationRelaySource.Contains('recognizedBlueSagePacket = true') -and $moderationRelaySource.Contains('return !recognizedBlueSagePacket')) 'Recognized private moderation packets must fail closed instead of leaking into public chat.'
Assert-True ($moderationControllerSource.Contains('PollHostMemberDataRequests') -and $moderationControllerSource.Contains('GetLobbyMemberData') -and $moderationControllerSource.Contains('PlayerIdentityEvidenceController.TryResolveExact(senderSteamId') -and -not $moderationControllerSource.Contains('clientPlayerId')) 'Host moderation polling must bind Steam member-data requests to an exact verified current sender and ignore client-supplied identity.'
Assert-True ($moderationControllerSource.Contains('NetworkManager.main.isHost') -and $moderationControllerSource.Contains('SteamMatchmaking.GetLobbyOwner')) 'Only the current Steam/PurrNet host may receive and execute moderation requests.'
Assert-True ($moderationControllerSource.Contains('BanServerPlayers') -and $moderationControllerSource.Contains('BanServerPlayerNicks') -and $moderationControllerSource.Contains('SaveBanData()')) 'Native moderation must preserve the paired Steam ID/nickname lists and save through DataManager.'
Assert-True ($moderationControllerSource.Contains('if (string.IsNullOrWhiteSpace(name)) return "Steam …" + Suffix(steamId)')) 'Native ban nickname storage must retain a non-empty safe fallback label.'
Assert-True ($moderationControllerSource.Contains('BanRPC(panel.PlayerIDs[targetIndex], true)')) 'Native ban must target the exact revalidated PurrNet PlayerID.'
# Round 4 Stage B: the legacy (no connection table) presence rule moved into BanTargetPresencePolicy unchanged.
Assert-True ($moderationControllerSource.Contains('BanTargetPresencePolicy.ClassifyWithoutLiveness(targetIndex >= 0, panelMatches, uniquePlayerId, lobbyMatches)') -and $banTargetPresencePolicySource.Contains('OfflineBanPolicy.IsConfirmedAbsentFromCurrentLobby(steamLobbyMatches)') -and $pluginSource.Contains('OpenOfflineSteamProfileFromQolMenu') -and $pluginSource.Contains('CopySavedBanSteamIdFromQolMenu')) 'Off-roster profile/copy/ban actions must use dedicated host-only validation instead of live-row-only helpers.'
Assert-True ($moderationControllerSource.Contains('CloneIncidentLedger.IsProtectedVictim')) 'Clone Shield victims must remain protected from the quick-ban path.'
Assert-True ($cloneShieldSource.Contains('string message = "[Clone Shield] "') -and -not $cloneShieldSource.Contains('string message = "[Clone Shield " + incidentId')) 'Public Clone Shield warning must not publish a client-local incident number.'
Assert-True ($cloneIncidentResolutionSource.Contains('MemberDataKey') -and $cloneIncidentResolutionSource.Contains('BuildMemberDataRequest') -and $cloneIncidentResolutionSource.Contains('LobbyDataKey = "bluesage_clone_resolution"')) 'Clone incident resolution must use member-data requests and host-owned lobby propagation state.'
Assert-True ($cloneIncidentActionSource.Contains('PollHostMemberDataRequests') -and $cloneIncidentActionSource.Contains('GetLobbyMemberData') -and $cloneIncidentActionSource.Contains('PlayerIdentityEvidenceController.TryResolveExact(senderSteamId')) 'Clone incident resolution requests must be host-polled from a verified current helper Steam member.'
Assert-True ($cloneIncidentActionSource.Contains('TryResolveForLobby') -and $cloneIncidentActionSource.Contains('TryDismissLocal') -and $cloneIncidentActionSource.Contains('SetLobbyData')) 'Clone incident workflow must support deliberate lobby resolution, local dismissal, and peer propagation.'
Assert-True ($identityRoleHighlightSource.Contains('SuspectColor') -and $identityRoleHighlightSource.Contains('VictimColor') -and $identityRoleHighlightSource.Contains('RefreshAll')) 'Clone Shield must render distinct suspect/victim row states and refresh them after lifecycle changes.'
Assert-True ($pluginSource.Contains('GetSteamPersonaName()') -and $pluginSource.Contains('name reset to your current Steam persona name')) '/setname reset must restore the current Steam persona name transactionally.'
Assert-True ($pluginSource.Contains('Status Helper color: reset to #FFD45D')) '/statuscolor reset must restore the shipped status color.'
Assert-True ($pluginSource.Contains('SweepIntervalMinutes = Config.Bind("Asset Sweep", "SweepIntervalMinutes", SweepSettingsPolicy.DefaultIntervalMinutes')) 'Sweep interval must default to 10 minutes on fresh installs.'
Assert-True ($sweepSettingsPolicySource.Contains('configuredMinutes == 30 || configuredMinutes == 60') -and $sweepSettingsPolicySource.Contains('MigrateLegacyDefaultIntervalMinutes(configuredMinutes, legacyDefaultsMigrated)') -and $pluginSource.Contains('SweepSettingsPolicy.ResolveStartupIntervalMinutes(SweepIntervalMinutes.Value, freshInstall, sweepDefaultsMigrated, sweepTenMinuteSafetyMigrated, sweepOverlapAvoidanceMigrated)')) 'Known legacy Sweep defaults 30/60 must migrate once to 15 (then the Stage G overlap step moves 10/15 to 40 once).'
Assert-True ($sweepSettingsPolicySource.Contains('MigrateTenMinuteSafetyInterval') -and $sweepSettingsPolicySource.Contains('MigrateAutomaticCleanupOptIn') -and $pluginSource.Contains('SweepAutomaticOptInV3Migration')) 'Sweep updater migration must preserve the saved automatic-cleanup choice while retaining the 10-minute default.'
Assert-True ($pluginSource.Contains('EnableAutoSweep = Config.Bind("Asset Sweep", "EnableAutoSweep", publicDefaults.AutomaticCleanup') -and -not $pluginSource.Contains('MigrateTenMinuteSafetyInterval(migrated, sweepAutomaticOptInMigrated)') -and $pluginSource.Contains('MigrateAutomaticCleanupOptIn(EnableAutoSweep.Value, sweepAutomaticOptInMigrated)')) 'Automatic Cleanup must default on for fresh installs, preserve an existing explicit off choice, and preserve valid interval choices.'
Assert-True ($pluginSource.Contains('if (Input.GetKeyDown(ManualSweepKey.Value))') -and -not $pluginSource.Contains('if (EnableManualSweepHotkey.Value && Input.GetKeyDown') -and $pluginSource.Contains('HandleAssetSweepCommand')) 'F8 and bare sweep commands must remain manual cleanup controls independent of the automatic toggle and deprecated hotkey key.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'src\Patches\NativeSweepIntervalOverridePatch.cs')) -and -not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'src\Core\NativeSweepIntervalOverridePolicy.cs')) -and -not $pluginSource.Contains('ResolveNativeSweepDurationForRuntime')) 'BlueSage must not patch or replace the game native cleanup duration.'
Assert-True ($sweepSettingsPolicySource.Contains('return ClampIntervalMinutes(configuredMinutes);')) 'Sweep migration must preserve later valid custom values and persist the 5-180 clamp.'
Assert-True ($pluginSource.Contains('EnableHostHealthMonitor = Config.Bind("Host Health", "EnableHostHealthMonitor", true')) 'Host Health must default on for the beta pass.'
Assert-True ($pluginSource.Contains('EnableFocusAnywhere = Config.Bind("Focus Anywhere", "EnableFocusAnywhere", true')) 'Focus Anywhere must default on.'
Assert-True ($pluginSource.Contains('EnableEnhancedPlayerPanel = Config.Bind("Enhanced Player Panel", "EnableEnhancedPlayerPanel", true')) 'Enhanced Player Panel must default on.'
Assert-True ($pluginSource.Contains('EnablePingMentions = Config.Bind("Ping Mentions", "EnablePingMentions", true')) 'Ping Mentions must default on.'
Assert-True ($pluginSource.Contains('EnablePingSound = Config.Bind("Ping Mentions", "EnablePingSound", true')) 'Ping sound must default on.'
Assert-True ($pluginSource.Contains('PingSoundMode = Config.Bind("Ping Mentions", "PingSoundMode", "ticket"')) 'Ping sound mode must default to ticket and be configurable.'
Assert-True ($pluginSource.Contains('EnableChatReadability = Config.Bind("Chat Readability", "EnableChatReadability", true')) 'Chat Readability must default on.'
Assert-True ($pluginSource.Contains('ChatOutlineIntensity = Config.Bind("Chat Readability", "ChatOutlineIntensity", ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent')) 'Chat outline intensity must default through the shared style policy.'
Assert-True ($chatReadabilityStylePolicySource.Contains('DefaultOutlineIntensityPercent = 75')) 'Chat outline intensity policy must default to 75.'
Assert-True ($pluginSource.Contains('ChatUiScalePercent = Config.Bind("Chat Readability", "ChatUiScalePercent", ChatAccessibilityScalePolicy.DefaultScalePercent')) 'Chat UI scale must default through the shared accessibility scale policy.'
Assert-True ($chatAccessibilityScalePolicySource.Contains('DefaultScalePercent = 100')) 'Chat accessibility scale policy must default to 100 percent.'
Assert-True ($chatAccessibilityScalePolicySource.Contains('MinScalePercent = 75')) 'Chat accessibility scale policy must allow a conservative 75 percent minimum.'
Assert-True ($chatAccessibilityScalePolicySource.Contains('MaxScalePercent = 200')) 'Chat accessibility scale policy must allow up to 200 percent for accessibility.'
Assert-True ($pluginSource.Contains('ChatFontSizePercent = Config.Bind("Chat Readability", "ChatFontSizePercent", ChatFontSizePolicy.DefaultFontSizePercent')) 'Chat font size must default through the shared font-size policy.'
Assert-True ($pluginSource.Contains('LockedChatFontSizePercent => ChatFontSizePolicy.ClampFontSizePercent')) 'Chat font size must expose a clamped runtime value.'
Assert-True ($chatFontSizePolicySource.Contains('DefaultFontSizePercent = 100')) 'Chat font size policy must default to vanilla 100 percent.'
Assert-True ($chatFontSizePolicySource.Contains('EnabledFontSizePercent = 125')) 'Chat font size policy must make on/toggle use 125 percent.'
Assert-True ($chatFontSizePolicySource.Contains('MinFontSizePercent = 75')) 'Chat font size policy must allow a conservative 75 percent minimum.'
Assert-True ($chatFontSizePolicySource.Contains('MaxFontSizePercent = 200')) 'Chat font size policy must allow up to 200 percent.'
Assert-True ($pluginSource.Contains('ChatWindowHeightPercent = Config.Bind("Chat Readability", "ChatWindowHeightPercent", ChatWindowHeightPolicy.DefaultHeightPercent')) 'Chat window size must default through the shared size policy.'
Assert-True ($pluginSource.Contains('LockedChatWindowHeightPercent => ChatWindowHeightPolicy.ClampHeightPercent')) 'Chat window size must expose a clamped runtime value.'
Assert-True ($chatWindowHeightPolicySource.Contains('DefaultHeightPercent = 100')) 'Chat window size policy must default to 100 percent.'
Assert-True ($chatWindowHeightPolicySource.Contains('MinHeightPercent = 100')) 'Chat window size policy must keep vanilla size as the minimum.'
Assert-True ($chatWindowHeightPolicySource.Contains('MaxHeightPercent = 200')) 'Chat window size policy must cap larger chat panels at 200 percent.'
Assert-True ($pluginSource.Contains('ChatHistoryRows = Config.Bind')) 'Chat history rows must expose the bounded player-facing config.'
Assert-True ($pluginSource.Contains('LockedChatHistoryRows => ChatHistoryLimitPolicy.ClampRows')) 'Chat history rows status must expose the clamped runtime value.'
Assert-True ($chatHistoryLimitPolicySource.Contains('DefaultRows = 150')) 'Chat history rows policy must use the balanced 150-row default.'
Assert-True ($chatHistoryLimitPolicySource.Contains('MinRows = 25')) 'Chat history rows policy must enforce the 25-row minimum.'
Assert-True ($chatHistoryLimitPolicySource.Contains('MaxRows = 250')) 'Chat history rows policy must enforce the 250-row maximum.'
Assert-True ($chatHistoryLimitPolicySource.Contains('ApplyConfiguredRows')) 'Chat history rows policy must raise the native getter result through one helper.'
Assert-True ($chatHistoryLimitPatchSource.Contains('[HarmonyPatch(typeof(GameSettings))]')) 'Chat history limit patch must target GameSettings.'
Assert-True ($chatHistoryLimitPatchSource.Contains('[HarmonyPatch(nameof(GameSettings.GlobalMessageLimitCount), MethodType.Getter)]')) 'Chat history limit patch must target the global message limit getter.'
Assert-True ($chatHistoryLimitPatchSource.Contains('[HarmonyPatch(nameof(GameSettings.LocalMessageLimitCount), MethodType.Getter)]')) 'Chat history limit patch must target the local message limit getter.'
Assert-True ($chatHistoryLimitPatchSource.Contains('ChatHistoryLimitPolicy.ApplyConfiguredRows')) 'Chat history limit patch must apply the unified retained-row helper to both channels.'
Assert-True ($chatHistoryLimitPolicySource.Contains('Math.Max(originalLimit, ClampRows(configuredRows))')) 'Chat history limit policy must only raise, not lower, native/other-mod row limits.'
Assert-True (-not $chatHistoryLimitPatchSource.Contains('TextMeshProUGUI')) 'Chat history retention must not alter TMP text components.'
Assert-True (-not $chatHistoryLimitPatchSource.Contains('UiOutline')) 'Chat history retention must not own readability outlines.'
Assert-True (-not $chatHistoryLimitPatchSource.Contains('transform.localScale')) 'Chat history retention must not own chat scaling.'
Assert-True (-not $chatHistoryLimitPatchSource.Contains('SetSizeWithCurrentAnchors')) 'Chat history retention must not own chat height.'
Assert-True (-not $chatHistoryLimitPatchSource.Contains('SendMessageAsync')) 'Chat history retention must not touch chat networking.'
Assert-True ($pluginSource.Contains('ChatOutlineColor = Config.Bind("Chat Readability", "ChatOutlineColor", "000000"')) 'Normal chat outline color must default to black.'
Assert-True ($pluginSource.Contains('BlackChatOutlineColor = Config.Bind("Chat Readability", "BlackChatOutlineColor", "C0C0C0"')) 'Black chat outline color must default to light grey.'
Assert-True ($pluginSource.Contains('BlackNamesOutline = Config.Bind("Chat Readability", "BlackNamesOutline", true')) 'BlackNamesOutline must default on so true-black chat/name rows keep a readable light outline.'
Assert-True ($pluginSource.Contains('LockedChatOutlineIntensityPercent')) 'Chat outline intensity must expose a clamped runtime value.'
Assert-True ($pluginSource.Contains('LockedChatOutlineColorHex')) 'Normal outline color must expose a normalized runtime value.'
Assert-True ($pluginSource.Contains('LockedBlackChatOutlineColorHex')) 'Black outline color must expose a normalized runtime value.'
Assert-True ($pluginSource.Contains('ShouldApplyBlackNamesOutline')) 'BlackNamesOutline must expose a guarded apply helper.'
Assert-True ($pluginSource.Contains('ShouldApplyChatReadability')) 'Chat Readability must expose a guarded apply helper.'
Assert-True (-not $pluginSource.Contains('EnableSplitChat')) 'SplitChat config must be removed from the public release.'
Assert-True (-not $pluginSource.Contains('ShouldApplySplitChat')) 'SplitChat helper must be removed from the public release.'
Assert-True (-not $pluginSource.Contains('StartSplitChatController')) 'Public release must not start the split chat controller.'
Assert-True ($pluginSource.Contains('"chatreadability"')) 'Chat Readability must register /chatreadability.'
Assert-True ($pluginSource.Contains('new[] { "chatread" }')) 'Chat Readability must register /chatread alias.'
Assert-True ($pluginSource.Contains('"chatoutline"')) 'Chat Readability must register /chatoutline.'
Assert-True ($pluginSource.Contains('new[] { "outline" }')) 'Chat Readability must register /outline alias.'
Assert-True ($pluginSource.Contains('"chatscale"')) 'Chat Readability must register /chatscale.'
Assert-True ($pluginSource.Contains('new[] { "chatresize", "cr" }')) 'Chat Readability must register /chatresize and /cr aliases.'
Assert-True ($pluginSource.Contains('"chatfont"')) 'Chat Readability must register /chatfont.'
Assert-True ($pluginSource.Contains('new[] { "chatfontsize", "cf" }')) 'Chat Readability must register /chatfontsize and /cf aliases.'
Assert-True ($pluginSource.Contains('"chatheight"')) 'Chat Readability must register /chatheight.'
Assert-True ($pluginSource.Contains('new[] { "chatstretch", "chath" }')) 'Chat Readability must register /chatstretch and /chath aliases.'
Assert-True ($pluginSource.Contains('"chatvanilla"')) 'Chat Readability must register /chatvanilla.'
Assert-True ($pluginSource.Contains('new[] { "chatreset", "chatdefault" }')) 'Chat Readability must register /chatreset and /chatdefault aliases.'
Assert-True ($typeaheadSource.Contains('Choice("chatoutline", "75", "default outline")')) 'Typeahead must suggest the default 75 chat outline value.'
Assert-True ($typeaheadSource.Contains('Choice("outline", "75", "default outline")')) 'Typeahead must suggest the default 75 outline alias value.'
Assert-True ($typeaheadSource.Contains('Choice("chatscale", "125", "larger whole chat UI")')) 'Typeahead must suggest whole chat UI scale values.'
Assert-True ($typeaheadSource.Contains('ChatFontChoices("chatfont")')) 'Typeahead must suggest chat font-size values.'
Assert-True ($typeaheadSource.Contains('ChatHeightChoices("chatheight")')) 'Typeahead must suggest chat window size values.'
Assert-True ($typeaheadSource.Contains('Choice(command, "180", "very tall chat window")')) 'Typeahead must include a very tall chat window suggestion.'
Assert-True ($typeaheadSource.Contains('ChatHistoryChoices')) 'Typeahead must expose bounded retained-row choices and reset/status.'
Assert-True ($typeaheadSource.Contains('["chathistory"]')) 'Typeahead must expose the /chathistory alias.'
Assert-True ($pluginSource.Contains('"chatoutlinecolor"')) 'Chat Readability must register /chatoutlinecolor.'
Assert-True ($pluginSource.Contains('"blackoutlinecolor"')) 'Chat Readability must register /blackoutlinecolor.'
Assert-True ($pluginSource.Contains('EnableStyleUi = Config.Bind("Style Helper", "EnableStyleUi", true')) 'Style UI must default on.'
Assert-True ($pluginSource.Contains('EnableMiniMapPlayerLabels = Config.Bind("MiniMap", "EnableMiniMapPlayerLabels", true')) 'MiniMap dot-to-ID must default on while labels stay guarded against crowded lobbies.'
Assert-True ($pluginSource.Contains('EnableSpoons = Config.Bind("Spoons", "EnableSpoons", false')) 'Spoons must default off so the optional tag is opt-in.'
Assert-True ($pluginSource.Contains('ApplyOneTimeConfigMigration')) 'Plugin must run the one-time config migration.'
Assert-True (-not $pluginSource.Contains('ConfigMigrationTarget =') -and $pluginSource.Contains('ConfigMigrationPolicy.RecordMigration')) 'Plugin must append stable migration stages instead of overwriting the ledger with a fixed or plugin-version-derived marker.'
Assert-True ($configMigrationPolicySource.Contains('LegacyVersionDerivedV2Marker = "0.2.0-final-defaults-v2"') -and $configMigrationPolicySource.Contains('CurrentMigrationLedger')) 'Stable migration ledger must recognize the shipped v2 marker without rerunning completed defaults.'
Assert-True ($configMigrationPolicySource.Contains('HashSet<string>') -and $configMigrationPolicySource.Contains('seen.Add(completedMigration)') -and $configMigrationPolicySource.Contains('completed.Add(normalizedMigration)')) 'Migration ledger updates must preserve and deduplicate existing unknown or future stages before appending a missing current stage.'
Assert-True ($pluginSource.Contains('ConfigMigrationPolicy.MigrateOldDefaultInt')) 'Plugin must migrate old default-shaped numeric values safely.'
Assert-True ($pluginSource.Contains('ConfigMigrationPolicy.MigrateOldDefaultString(PingSoundMode.Value, "click", "ticket", finalDefaultsMigrated)')) 'Plugin must migrate untouched old ping sound defaults to ticket.'
Assert-True ($pluginSource.Contains('ConfigMigrationPolicy.MigrateOldDefaultInt(ChatOutlineIntensity.Value, 50, ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent, finalDefaultsMigrated)')) 'Plugin must migrate the 0.1.1 outline default up to 75.'
Assert-True ($pluginSource.Contains('ConfigMigrationPolicy.MigrateOldDefaultInt(migrated, 60, ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent, finalDefaultsMigrated)')) 'Plugin must migrate the short-lived 60 outline default up to 75.'
Assert-True ($pluginSource.Contains('ConfigMigrationVersion.Value = migrationLedger')) 'Plugin must persist the monotonic release-default migration ledger.'
Assert-True ($rescueReceiverCapabilitySource.Contains('MemberDataKey = "bluesage_qol_rescue_receiver_v1"') -and $rescueReceiverCapabilitySource.Contains('PublishIntervalSeconds = 5') -and $rescueReceiverCapabilitySource.Contains('MaximumAgeSeconds = 15')) 'Public rescue receiver capability must use the reviewed stable member-data key and bounded heartbeat.'
Assert-True ($pluginSource.Contains('StartRescueReceiverCapabilityPublisher();') -and $pluginSource.Contains('RescueReceiverCapabilityLoop()') -and $pluginSource.Contains('Patches.OptInRescuePatch.IsReceiverAvailable')) 'Public rescue receiver capability must publish only from the bounded coroutine while the receiver is active.'
Assert-True ($pluginSource.Contains('ConfigMigrationPolicy.ShouldDisableDefaultSpoons')) 'Plugin must disable prior enabled Spoons once during the 0.1.2 migration.'
Assert-True ($pluginSource.Contains('EnableWelcomeMessage = Config.Bind("Notifications", "EnableWelcomeMessage", true')) 'Welcome message must default on for lobby navigation guidance.'
Assert-True ($pluginSource.Contains('TryGetCurrentLobbyKeyForWelcomeMessage')) 'Welcome message must key off lobby state instead of only game launch.'
Assert-True ($navigationCalloutSource.Contains('/qol status shows what is on')) 'Welcome message must mention /qol status.'
Assert-True ($pluginSource.Contains('NavigationCalloutPolicy.BuildPrimaryWelcomeMessage(PluginVersion, QolMenuKey.Value.ToString(), StyleMenuKey.Value.ToString())')) 'Welcome message must use the centralized navigation callout policy with the real configured hotkeys.'
Assert-True ($navigationCalloutSource.Contains('"HOME"') -and $navigationCalloutSource.Contains('"INSERT"') -and $navigationCalloutSource.Contains('Highlight("SLASH COMMANDS"')) 'Local help must default to and highlight the three reviewed navigation keywords.'
Assert-True ($pluginSource.Contains('Quick status: /brb, /afk, /back')) 'Welcome message must mention quick status commands.'
Assert-True ($navigationCalloutSource.Contains('typing /, then press Tab or click a suggestion')) 'Welcome message must mention slash-command typeahead.'
Assert-True ($pluginSource.Contains('_welcomeVoiceStateLogKey') -and $pluginSource.Contains('if (!string.Equals(_welcomeVoiceStateLogKey, stateLogKey')) 'Welcome voice-state logging must be bounded to lobby/state transitions.'
Assert-True ($pluginSource.Contains('BlueSageCommandRegistry.TryParse(text, out string commandName, out string commandArguments)') -and $pluginSource.Contains('(arguments redacted).') -and -not $pluginSource.Contains('command handled locally: {text}')) 'Handled-command diagnostics must never log raw slash-command arguments.'
Assert-True ($typeaheadSource.Contains('BuildIncidentSuggestions') -and $typeaheadSource.Contains('Choice(command, incident.Id') -and -not $typeaheadSource.Contains('Choice(command, incident.StableId')) 'Incident typeahead must use the local C### command ID and expose lifecycle choices.'
Assert-True ($pluginSource.Contains('UiThemePreset = Config.Bind("Style Helper", "UiThemePreset", publicDefaults.Theme')) 'Midnight Dock must be the public default theme while saved choices remain preserved.'
Assert-True ($pluginSource.Contains('ApplySpoonVisibilityFromMenu')) 'Plugin must let the menus immediately apply Spoons visibility changes.'
Assert-True (-not $pluginSource.Contains('MoodPreset')) 'Plugin must not bind legacy MoodPreset config.'
Assert-True (-not $pluginSource.Contains('CustomMood')) 'Plugin must not bind legacy CustomMood config.'
Assert-True (-not $pluginSource.Contains('HandleMoodCommand')) 'Plugin must not ship a hidden /mood command handler.'
Assert-True (-not $pluginSource.Contains('"mood"')) 'Plugin must not register the old /mood command.'
Assert-True ($pluginSource.Contains('"qolmenu"')) 'Plugin must register /qolmenu command.'
Assert-True ($pluginSource.Contains('new[] { "qm" }')) 'Plugin must register /qm alias.'
Assert-True ($pluginSource.Contains('Input.GetKeyDown(QolMenuKey.Value)')) 'Plugin must toggle QoL Menu with the configurable QolMenuKey hotkey.'
Assert-True ($pluginSource.Contains('QolMenuKey = Config.Bind("Hotkeys", "QolMenuKey", KeyCode.Home,')) 'QolMenuKey must default to Home.'
Assert-True ($pluginSource.Contains('ToggleQolMenuFromHotkey')) 'Plugin must route the QoL Menu hotkey through a guarded helper.'
Assert-True ($pluginSource.Contains('"boop"')) 'Plugin must register a harmless hidden boop easter egg.'
Assert-True ($pluginSource.Contains('"sagewave"')) 'Plugin must register a harmless hidden Sage wave easter egg.'
Assert-True ($pluginSource.Contains('HandleBoopCommand')) 'Plugin must implement boop easter egg command handling.'
Assert-True ($pluginSource.Contains('HandleSageWaveCommand')) 'Plugin must implement Sage wave easter egg command handling.'
Assert-True ($easterEggTrackerSource.Contains('PlayerPrefs')) 'Easter egg tracker must persist only via PlayerPrefs.'
Assert-True ($easterEggTrackerSource.Contains('BlueSageQoL.EasterEggs.Total')) 'Easter egg tracker must use a scoped local PlayerPrefs key.'
Assert-True ($pluginSource.Contains('Lower it if very long chat lines wrap oddly')) 'Text-limit config must warn users that expanded values may be unstable.'
Assert-True ($pluginSource.Contains('ShouldApplyMiniMapPlayerLabels')) 'Plugin must preserve the legacy MiniMap config key for dot-to-ID compatibility.'
Assert-True ($pluginSource.Contains('"minimaplabels"')) 'Plugin must register /minimaplabels command.'
Assert-True ($pluginSource.Contains('new[] { "mml" }')) 'Plugin must register /mml alias.'
Assert-True ($pluginSource.Contains('StartCoroutine(HostHealthLoop())')) 'Host Health loop must be started by the plugin.'
Assert-True ($pluginSource.Contains('RunHostHealthCheck("manual command")')) 'Host Health manual command must run a real check.'
Assert-True (-not $pluginSource.Contains('Hidden BlueSage clue')) 'Hidden clue command must not ship a friendly public description string.'
Assert-True ($pluginSource.Contains('isHidden: true')) 'Hidden BlueSage commands must be marked hidden from suggestions.'
Assert-True ($pluginSource.Contains('CapturePlayerLimitCompanionStatus')) 'Host Health must probe optional BlueSage PlayerLimitLift companion state.'
Assert-True ($pluginSource.Contains('PluginCompatibility.BlueSagePlayerLimitLiftGuid')) 'PlayerLimit companion probe must use the loaded-plugin GUID, not file scanning.'
Assert-True ($playerLimitCompanionSource.Contains('PlayerLimit=loaded')) 'Host Health diagnostics must include PlayerLimit loaded-state text.'
Assert-True ($pluginSource.Contains('bluesage_playerlimit_cap')) 'Host Health heartbeat must include PlayerLimit cap metadata when available.'
Assert-True ($playerLimitCompanionSource.Contains('FromLoadedValues')) 'PlayerLimit companion status must have a loaded-values factory.'
Assert-True ($playerLimitCompanionSource.Contains('ToDiagnosticText')) 'PlayerLimit companion status must format a diagnostic summary.'
Assert-True ($pluginSource.Contains('SteamMatchmaking.SetLobbyData')) 'Host Health H1 must refresh lobby metadata.'
Assert-True ($pluginSource.Contains('SteamFriends.RequestUserInformation')) 'Host Health H4 must request persona refresh.'
Assert-True ($pluginSource.Contains('HandleOutgoingChatCommand')) 'Plugin must expose outgoing chat command handling.'
Assert-True ($pluginSource.Contains('"bsqol"')) 'Plugin must register /bsqol command.'
Assert-True ($pluginSource.Contains('new[] { "qol", "guide", "faq" }')) 'Plugin must register /qol alias.'
Assert-True ($pluginSource.Contains('new[] { "sweep", "as" }')) 'Plugin must register /sweep and /as aliases.'
Assert-True ($pluginSource.Contains('new[] { "hh" }')) 'Plugin must register /hh alias.'
Assert-True ($pluginSource.Contains('new[] { "ln" }')) 'Plugin must register /ln alias.'
Assert-True ($pluginSource.Contains('new[] { "tt" }')) 'Plugin must register /tt alias.'
Assert-True ($pluginSource.Contains('HandleTimestampCommand')) 'Plugin must use a combined timestamp command handler.'
Assert-True ($pluginSource.Contains('EnableNotificationTimestamps.Value = EnableChatTimestamps.Value')) 'Timestamp command must keep notification timestamps in sync with chat timestamps.'
Assert-True ($pluginSource.Contains('new[] { "rg" }')) 'Plugin must register /rg alias.'
Assert-True ($pluginSource.Contains('new[] { "rm" }')) 'Plugin must register /rm alias.'
Assert-True ($pluginSource.Contains('new[] { "fa" }')) 'Plugin must register /fa alias.'
Assert-True ($pluginSource.Contains('new[] { "sty" }')) 'Plugin must register /sty alias.'
Assert-True ($pluginSource.Contains('new[] { "sui" }')) 'Plugin must register /styleui alias.'
Assert-True ($pluginSource.Contains('"setname"')) 'Plugin must register /setname command.'
Assert-True ($pluginSource.Contains('"status"')) 'Plugin must register /status command.'
Assert-True ($pluginSource.Contains('"clearstatus"')) 'Plugin must register /clearstatus command.'
Assert-True ($pluginSource.Contains('"statuscolor"')) 'Plugin must register /statuscolor command.'
Assert-True ($pluginSource.Contains('"setpingcolor"')) 'Plugin must register /setpingcolor command.'
Assert-True ($pluginSource.Contains('"pingsound"')) 'Plugin must register /pingsound command.'
Assert-True ($pluginSource.Contains('HandlePingSoundCommand')) 'Plugin must implement ping sound mode command handling.'
Assert-True ($pluginSource.Contains('HandleStyleCommand')) 'Plugin must implement Style Helper command handling.'
Assert-True ($pluginSource.Contains('HandleStyleUiCommand')) 'Plugin must implement Style UI command handling.'
Assert-True ($pluginSource.Contains('TryApplyStyledNameFromStyleUi')) 'Style UI must provide a direct Apply To Name path.'
Assert-True ($pluginSource.Contains('HandleSetPingColorCommand')) 'Plugin must implement ping color command handling.'
Assert-True ($pluginSource.Contains('HandleSetNameCommand')) 'Plugin must implement Status Helper /setname handling.'
Assert-True ($pluginSource.Contains('HandleStatusCommand')) 'Plugin must implement Status Helper /status handling.'
Assert-True ($pluginSource.Contains('ApplyStatusNameToPlayerData')) 'Plugin must apply Status Helper changes through live player data.'
Assert-True ($pluginSource.Contains('MainCustomizationController.UpdatePlayerInfo')) 'Status Helper must refresh current customization/player info.'
Assert-True ($pluginSource.Contains('GUIUtility.systemCopyBuffer')) 'Style Helper must copy generated text to the local clipboard.'
Assert-True (-not $pluginSource.Contains('PlayerData.txt')) 'Style Helper must not edit PlayerData.txt directly.'
Assert-True (-not $pluginSource.Contains('File.Write')) 'QoL plugin must not write save/profile files directly.'
Assert-True ($pluginSource.Contains('HandleToggleCommand')) 'Plugin must implement runtime toggle commands.'
Assert-True ($commandPatchSource.Contains('[HarmonyPatch("SendMessageAsync", typeof(byte[]), typeof(byte[]), typeof(bool), typeof(Vector3), typeof(string), typeof(RPCInfo))]')) 'BlueSage command patch must target exact current SendMessageAsync signature.'
Assert-True ($commandPatchSource.Contains('HandleOutgoingChatCommand(__0)')) 'BlueSage command patch must use Harmony argument index for outgoing message bytes.'
Assert-True ($commandPatchSource.Contains('IsLocalCommandInvocation(__5)')) 'BlueSage command patch must guard against non-local/default RPC invocations.'
Assert-True ($focusPatchSource.Contains('IsReadyForFreeFocus')) 'Focus Anywhere must patch free focus readiness.'
Assert-True ($focusPatchSource.Contains('IsReadyForAreaFocus')) 'Focus Anywhere must patch area focus readiness.'
Assert-True ($focusPatchSource.Contains('PlayerFocusController')) 'Focus Anywhere must patch PlayerFocusController.IsFocusable.'
Assert-True ($focusPatchSource.Contains('get_IsLake')) 'Focus Anywhere must patch lake-state focus blocker.'
Assert-True ($focusPatchSource.Contains('Plugin.ShouldApplyFocusAnywhere')) 'Focus Anywhere patches must honor BlueSage toggle.'
Assert-True ($panelPatchSource.Contains('PlayerItemController')) 'Enhanced Player Panel must patch PlayerItemController.Init.'
Assert-True (-not $panelPatchSource.Contains('PlayerIdentityEvidenceController.InvalidateRosterCache();') -and $panelPatchSource.Contains('PlayerPanelSurfaceController.RefreshRow(')) 'Enhanced Player Panel Init patch must stay a thin dispatcher into the bounded row surface controller without invalidating the global identity cache per row.'
Assert-True (-not $panelPatchSource.Contains('Group_Buttons') -and -not $panelPatchSource.Contains('RowHasPomoSurface') -and -not $panelPatchSource.Contains('ButtonOpenIdCheck')) 'Enhanced Player Panel Init patch must not discover, classify, or invoke native controls directly.'
Assert-True ($panelSurfaceControllerSource.Contains('AccessTools.Field(typeof(PlayerItemController), "_reportButton")') -and $panelSurfaceControllerSource.Contains('reportButton.transform.parent')) 'Enhanced Player Panel must anchor its independent lane from the serialized report-button parent.'
Assert-True ($panelSurfaceControllerSource.Contains('BlueSage_ActionLane') -and $panelSurfaceControllerSource.Contains('BlueSagePlayerPanelSurfaceMarker') -and $panelSurfaceControllerSource.Contains('layoutElement.ignoreLayout = true')) 'Enhanced Player Panel must own one ignore-layout action lane with explicit lifecycle marking.'
Assert-True ($panelSurfaceControllerSource.Contains('Button_BlueSageMention') -and $panelSurfaceControllerSource.Contains('Button_BlueSageNativeId')) 'Enhanced Player Panel must provide mention and guarded native-ID actions without replacing game-owned controls.'
Assert-True ($panelSurfaceControllerSource.Contains('without interrupting native UI')) 'Enhanced Player Panel must log-and-skip instead of breaking native UI.'
Assert-True ($panelSurfaceControllerSource.Contains('button.onClick.RemoveAllListeners();') -and $panelSurfaceControllerSource.Contains('OwnedLanesByRowInstanceId') -and $panelSurfaceControllerSource.Contains('DestroyOwnedLane')) 'Enhanced Player Panel must rebind recycled rows and destroy only marker-owned lanes.'
# Round 4 R9: the Interlocked lane/button counters fed only the removed hidden diagnostics and stay removed.
Assert-True (-not $panelSurfaceControllerSource.Contains('System.Threading.Interlocked') -and $panelSurfaceControllerSource.Contains('PruneInactivePooledRows') -and $panelSurfaceControllerSource.Contains('internal static void ClearAll()')) 'Enhanced Player Panel must prune pooled/teardown-owned state without the removed diagnostic counters.'
Assert-True (-not $panelSurfaceControllerSource.Contains('HierarchyDiagnostic')) 'Round 4: the hidden Enhanced Player Panel hierarchy diagnostic must stay removed.'
Assert-True ($panelSurfacePolicySource.Contains('PreserveNativeControls = true') -and $panelSurfacePolicySource.Contains('Pomo and voice are game-owned state')) 'Enhanced Player Panel must preserve every native Pomo/voice/control state.'
Assert-True (-not $panelPatchSource.Contains('anchoredPosition += new Vector2(-135f, 0f)')) 'Enhanced Player Panel must not shove the native button group into Pomodoro/PomoSync row space.'
Assert-True ($panelSurfaceControllerSource.Contains('NativeIdCardController.TryOpen') -and $nativeIdCardControllerSource.Contains('PlayerIdentityEvidenceController.TryResolveConfirmed') -and $nativeIdCardControllerSource.Contains('NativeIdCardResolutionPolicy.Evaluate')) 'Enhanced Player Panel must resolve each native ID action through fresh exact row identity evidence.'
Assert-True ($nativeIdCardControllerSource.Contains('temporaryPhoto.CapturePhoto(target.PlayerController)') -and -not $nativeIdCardControllerSource.Contains('ButtonOpenIdCheck(') -and $nativeIdCardControllerSource.Contains('NativeIdCardInvocationGuard.Invoke')) 'The shared native ID controller must invoke the exact-controller photo/card API and retain no ambiguous index fallback.'
Assert-True (-not $nativeIdCardControllerSource.Contains('ReadIdInfoName') -and -not $nativeIdCardControllerSource.Contains('DisplayName ==')) 'Native ID actions must fail closed instead of falling back to cloneable display names.'
Assert-True ($panelPatchSource.Contains('Plugin.ShouldApplyEnhancedPlayerPanel')) 'Enhanced Player Panel patch must honor BlueSage toggle.'
Assert-True ($miniMapPatchSource.Contains('[HarmonyPatch(typeof(MapManager), "UpdatePosition")]')) 'MiniMap ID Click must patch the live MapManager.UpdatePosition method.'
Assert-True ($miniMapPatchSource.Contains('_playerIcons')) 'MiniMap ID Click must use the live MapManager _playerIcons field.'
Assert-True ($miniMapPatchSource.Contains('Plugin.ShouldApplyMiniMapPlayerLabels')) 'MiniMap ID Click must honor the guarded legacy toggle.'
Assert-True ($miniMapPatchSource.Contains('panel.IDInfos')) 'MiniMap ID Click must use player-panel ID info instead of guessing names.'
Assert-True ($miniMapPatchSource.Contains('MiniMapIdCardClickHandler')) 'MiniMap ID Click must include a local click handler for ID-card lookup.'
Assert-True ($miniMapPatchSource.Contains('IPointerClickHandler')) 'MiniMap ID-card clicks must use local UI pointer handling.'
Assert-True ($miniMapPatchSource.Contains('OpenIdCardFromMiniMap')) 'MiniMap ID-card clicks must resolve through a guarded helper.'
Assert-True ($miniMapPatchSource.Contains('NativeIdCardController.TryOpen') -and -not $miniMapPatchSource.Contains('ButtonOpenIdCheck')) 'MiniMap ID-card clicks must use the shared guarded native-ID controller rather than invoking the game API directly.'
Assert-True ($miniMapPatchSource.Contains('RefreshIntervalSeconds = 2f')) 'MiniMap ID Click must stay throttled to protect FPS in large lobbies.'
Assert-True (-not $miniMapPatchSource.Contains('BlueSageMiniMapNameLabel')) 'MiniMap ID Click must not ship floating label objects.'
Assert-True (-not $miniMapPatchSource.Contains('GetOrCreateLabel')) 'MiniMap ID Click must not create floating labels.'
Assert-True (-not $miniMapPatchSource.Contains('SetAllLabelsVisible')) 'MiniMap ID Click must not manage floating label visibility.'
Assert-True (-not $miniMapPatchSource.Contains('MaxLabelsWithoutZoom')) 'MiniMap ID Click must not keep old crowded-label logic.'
Assert-True (-not $miniMapPatchSource.Contains('IsZoomedEnoughForCrowdedLabels')) 'MiniMap ID Click must not keep old zoom-label logic.'
Assert-True (-not $miniMapPatchSource.Contains('TextMeshProUGUI')) 'MiniMap ID Click must not allocate TMP label UI.'
Assert-True (-not $miniMapPatchSource.Contains('enableWordWrapping')) 'MiniMap ID Click must avoid obsolete TMP word-wrap APIs.'
Assert-True (-not $miniMapPatchSource.Contains('SendMessageAsync')) 'MiniMap ID Click must stay local-only and avoid chat/network sends.'
Assert-True (-not $miniMapPatchSource.Contains('ServerRpc')) 'MiniMap ID Click must not use server RPCs.'
Assert-True ($pluginSource.Contains('EnableChatCopy = Config.Bind("Chat Copy", "EnableChatCopy", true')) 'Chat Copy must default on.'
Assert-True ($pluginSource.Contains('ShouldApplyChatCopy')) 'Chat Copy must expose a compatibility-guarded apply helper.'
Assert-True ($pluginSource.Contains('PluginCompatibility.LegacyCopyChatGuid')) 'Chat Copy must detect the legacy CopyChat mod.'
Assert-True ($pluginSource.Contains('"copychat"')) 'Chat Copy must register /copychat.'
Assert-True ($pluginSource.Contains('new[] { "ccopy" }')) 'Chat Copy must register /ccopy alias.'
Assert-True ($chatCopyPatchSource.Contains('[HarmonyPatch("AddMessageUI", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]')) 'Chat Copy must patch the live TextChannelManager.AddMessageUI signature.'
Assert-True ($chatCopyPatchSource.Contains('ChatCopySanitizer.ToClipboardText(sourceText)')) 'Chat Copy must copy only the source message/notification text after sanitizing.'
Assert-True ($chatCopyPatchSource.Contains('[HarmonyPatch("AddNotification", typeof(string))]')) 'Chat Copy must attach to the verified local notification lifecycle used by BlueSage command responses.'
Assert-True ($chatCopyPatchSource.Contains('AttachToCreatedRow') -and $chatCopyPatchSource.Contains('ChatRowReconciliationPolicy.Resolve')) 'Chat and local notifications must bind copy/link actions to the one exact row created by each native render call.'
Assert-True ($chatInteractionSource.Contains('GUIUtility.systemCopyBuffer = _payload')) 'Chat Copy must write to the local Unity clipboard.'
Assert-True ($chatInteractionSource.Contains('GUIUtility.systemCopyBuffer = _safeUrl')) 'Chat Link warning must copy the URL before opening.'
Assert-True ($chatInteractionSource.Contains('LinkConfirmationSeconds = 10f') -and $chatInteractionSource.Contains('Time.unscaledTime + LinkConfirmationSeconds')) 'Chat Link opening must require a short confirmation window.'
Assert-True ($chatInteractionSource.Contains('Be careful with random links')) 'Chat Link opening must warn players before opening URLs.'
Assert-True ($chatCopyPatchSource.Contains('ChatLinkFormatter.TryColorizeUrls')) 'Chat Link feature must recolor detected links.'
Assert-True ($chatLinkFormatterSource.Contains('LinkColor = "70D6FF"') -and $chatLinkFormatterSource.Contains('<u>')) 'Chat Link formatter must use hyperlink-style rich text.'
Assert-True ($chatInteractionSource.Contains('IPointerClickHandler')) 'Chat Copy must use local UI pointer handling.'
Assert-True ($chatInteractionSource.Contains('PlayUIClick')) 'Chat Copy must give a small native-style click cue.'
Assert-True (-not $chatInteractionSource.Contains('raycastTarget = false')) 'Chat Copy must not disable parent UI raycasts like the legacy implementation.'
Assert-True (-not $chatCopyPatchSource.Contains('color = Color.green') -and -not $chatInteractionSource.Contains('color = Color.green')) 'Chat Copy must not recolor all chat messages like the legacy implementation.'
Assert-True ($chatCopySanitizerSource.Contains('Regex("<[^>]+>"')) 'Chat Copy sanitizer must strip rich text tags.'
Assert-True ($chatReadabilityPatchSource.Contains('[HarmonyPatch("AddMessageUI", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]')) 'Chat Readability must patch the live TextChannelManager.AddMessageUI signature.'
Assert-True ($chatReadabilityPatchSource.Contains('Plugin.ShouldApplyChatReadability')) 'Chat Readability patch must honor the toggle.'
Assert-True ($chatReadabilityPatchSource.Contains('Plugin.LockedChatOutlineIntensityPercent')) 'Chat Readability patch must use the configured outline intensity.'
Assert-True ($chatReadabilityPatchSource.Contains('Plugin.LockedChatUiScalePercent')) 'Chat Readability patch must use the configured chat UI scale.'
Assert-True ($chatReadabilityPatchSource.Contains('Plugin.LockedChatFontSizePercent')) 'Chat Readability patch must use the configured chat font size.'
Assert-True ($chatReadabilityPatchSource.Contains('Plugin.LockedChatWindowHeightPercent')) 'Chat Readability patch must use the configured chat window size.'
Assert-True ($chatReadabilityPatchSource.Contains('ChatReadabilityStylePolicy.NormalizeOutlineIntensity')) 'Chat Readability patch must normalize outline intensity through the shared policy.'
Assert-True ($chatReadabilityPatchSource.Contains('ChatAccessibilityScalePolicy.NormalizeScale')) 'Chat Readability patch must normalize chat UI scale through the shared policy.'
Assert-True ($chatReadabilityPatchSource.Contains('ChatFontSizePolicy.NormalizeFontSize')) 'Chat Readability patch must normalize chat font size through the shared policy.'
Assert-True ($chatReadabilityPatchSource.Contains('ChatWindowHeightPolicy.NormalizeHeight')) 'Chat Readability patch must normalize chat window size through the shared policy.'
Assert-True ($chatReadabilityPatchSource.Contains('ApplyChatAccessibilityScale')) 'Chat Readability patch must apply chat UI scaling at the same local chat roots as readability.'
Assert-True ($chatReadabilityPatchSource.Contains('RestoreChatAccessibilityScale')) 'Chat Readability patch must restore original chat UI scale on reset/unload.'
Assert-True ($chatReadabilityPatchSource.Contains('OriginalSurfaceScales')) 'Chat Readability patch must remember original chat panel scales.'
Assert-True ($chatReadabilityPatchSource.Contains('ApplyChatFontSize')) 'Chat Readability patch must apply chat font size separately from UI scaling.'
Assert-True ($chatReadabilityPatchSource.Contains('RestoreChatFontSize')) 'Chat Readability patch must restore chat font size on reset/unload.'
Assert-True ($chatReadabilityPatchSource.Contains('OriginalFontSizes')) 'Chat Readability patch must remember original chat font sizes.'
Assert-True ($chatReadabilityPatchSource.Contains('ApplyChatWindowHeight')) 'Chat Readability patch must apply chat window size at the chat panel surface.'
Assert-True ($chatReadabilityPatchSource.Contains('RestoreChatWindowHeight')) 'Chat Readability patch must restore chat window size on reset/unload.'
Assert-True ($chatReadabilityPatchSource.Contains('OriginalSurfaceWindowSizes')) 'Chat Readability patch must remember original chat panel sizes.'
Assert-True ($chatReadabilityPatchSource.Contains('OriginalWidth')) 'Chat Readability patch must remember original chat panel width.'
Assert-True ($chatReadabilityPatchSource.Contains('RectTransform.Axis.Vertical')) 'Chat Readability patch must resize chat panel height in the stable vertical build.'
Assert-True (-not $chatReadabilityPatchSource.Contains('RectTransform.Axis.Horizontal')) 'Chat Readability patch must not promise rectangular width resizing until native wrap width is proven stable.'
Assert-True (-not $chatReadabilityPatchSource.Contains('ApplyChatContentWidth')) 'Chat Readability patch must not keep dead width-expansion helpers.'
Assert-True (-not $chatReadabilityPatchSource.Contains('OriginalSurfaceHeights')) 'Chat Readability patch must not use the old vertical-only restore dictionary.'
Assert-True ($chatReadabilityPatchSource.Contains('GetOutlineColorForText')) 'Chat Readability patch must choose outline color per rendered text.'
Assert-True ($chatReadabilityPatchSource.Contains('ChatReadabilityStylePolicy.ContainsTrueBlackRichTextColorTag(text.text)')) 'Chat Readability must detect exact true-black rich-text tags in rendered TMP chat text.'
Assert-True ($chatReadabilityStylePolicySource.Contains('ContainsTrueBlackRichTextColorTag')) 'Chat Readability style policy must expose exact true-black rich-text detection.'
Assert-True ($chatReadabilityStylePolicySource.Contains('string.Equals(hex.Substring(0, 6), "000000", StringComparison.OrdinalIgnoreCase)')) 'Chat Readability style policy must detect exact #000000 rich-text color tags.'
Assert-True ($chatReadabilityStylePolicySource.Contains('string.Equals(name, "black", StringComparison.OrdinalIgnoreCase)')) 'Chat Readability style policy must detect named black rich-text color tags.'
Assert-True ($chatReadabilityPatchSource.Contains('Plugin.ShouldApplyBlackNamesOutline')) 'Chat Readability patch must honor BlackNamesOutline.'
Assert-True (-not $chatReadabilityPatchSource.Contains('ChatReadabilityStylePolicy.IsTrueBlackColorToken')) 'Chat Readability must not parse inline rich-text color tokens.'
Assert-True (-not $chatReadabilityPatchSource.Contains('Contains("<color=#000")')) 'Chat Readability patch must not treat all #000-prefixed colors as black.'
Assert-True ($chatReadabilityPatchSource.Contains('LightOutlineColor')) 'Chat Readability patch must support a light outline for true black text.'
Assert-True ($chatReadabilityPatchSource.Contains('outline.useGraphicAlpha = false')) 'Chat Readability UI outline must apply configured outline alpha directly.'
Assert-True ($chatReadabilityPatchSource.Contains('shadow.useGraphicAlpha = false')) 'Chat Readability UI shadow must apply configured shadow alpha directly.'
Assert-True ($chatReadabilityPatchSource.Contains('GetComponentsInChildren<TextMeshProUGUI>(true)')) 'Chat Readability must target rendered TMP chat text.'
Assert-True ($chatReadabilityPatchSource.Contains('RefreshVisibleChat')) 'Chat Readability toggles must refresh already-visible chat rows.'
Assert-True ($chatReadabilityPatchSource.Contains('RequestRefreshSoon')) 'Chat Readability must support queued refreshes from UI redraw hooks.'
Assert-True ($chatReadabilityPatchSource.Contains('RefreshRequestVersion')) 'Chat Readability controller must be able to observe explicit refresh requests.'
Assert-True ($chatReadabilityPatchSource.Contains('RefreshChatContents(ui)')) 'Chat Readability must provide an explicit topology/config reconciliation path.'
Assert-True ($chatReadabilityPatchSource.Contains('RefreshContent(local)')) 'Chat Readability must anchor on the local chat content transform.'
Assert-True ($chatReadabilityPatchSource.Contains('RefreshContent(global)')) 'Chat Readability must anchor on the global chat content transform.'
Assert-True ($chatReadabilityPatchSource.Contains('RefreshBackdropTopology(local, global)')) 'Chat Readability must reconcile the current scroll-view/backdrop topology.'
Assert-True ($chatReadabilityPatchSource.Contains('AddUniqueSurface')) 'Chat Readability must avoid duplicate chat surfaces.'
Assert-True (-not $chatReadabilityPatchSource.Contains('EnsureChatScrollbackFriendly')) 'Chat Readability must not take ownership of vanilla chat scrolling.'
Assert-True (-not $chatReadabilityPatchSource.Contains('GetComponentsInChildren<ScrollRect>(true)')) 'Chat Readability must not scan and mutate ScrollRect components.'
Assert-True (-not $chatReadabilityPatchSource.Contains('scrollRect.movementType')) 'Chat Readability must not override ScrollRect movement mode.'
Assert-True (-not $chatReadabilityPatchSource.Contains('scrollRect.scrollSensitivity')) 'Chat Readability must not override vanilla or scroll-mod sensitivity.'
Assert-True (-not $chatReadabilityPatchSource.Contains('mask.showMaskGraphic')) 'Chat Readability must not rewrite viewport mask graphics during refresh.'
Assert-True ($chatReadabilityPatchSource.Contains('ClampScaledChatSurfaceToScreen')) 'Chat UI scale must nudge scaled chat surfaces back on-screen.'
Assert-True ($chatReadabilityPatchSource.Contains('TryCalculateScreenClampDelta')) 'Chat UI scale must use the shared screen-clamp policy.'
Assert-True ($chatReadabilityPatchSource.Contains('ScreenClampMargin')) 'Chat UI scale must leave a screen margin for the drag handle.'
Assert-True ($chatReadabilityPatchSource.Contains('gameObject.AddComponent<UiOutline>()')) 'Chat Readability must add local UI outlines.'
Assert-True ($chatReadabilityPatchSource.Contains('gameObject.AddComponent<UiShadow>()')) 'Chat Readability must add local UI shadows.'
Assert-True ($chatReadabilityPatchSource.Contains('ApplyTmpMaterialOutline')) 'Chat Readability must apply TMP material outline, which is the visible chat outline renderer players expect.'
Assert-True ($chatReadabilityPatchSource.Contains('ShaderUtilities.ID_OutlineWidth')) 'Chat Readability must set TMP outline width.'
Assert-True ($chatReadabilityPatchSource.Contains('ShaderUtilities.ID_OutlineColor')) 'Chat Readability must set TMP outline color.'
Assert-True ($chatReadabilityPatchSource.Contains('new Material(material)')) 'Chat Readability must clone the TMP material before changing outline state.'
Assert-True ($chatReadabilityPatchSource.Contains('DestroyOwnedMaterial')) 'Chat Readability must clean up owned TMP outline materials when restoring readability.'
Assert-True (-not $chatReadabilityPatchSource.Contains('text.color =')) 'Chat Readability must not overwrite message text color.'
Assert-True ($chatReadabilityPatchSource.Contains('RestoreReadability')) 'Chat Readability must restore BlueSage-added text effects when disabled.'
Assert-True ($chatReadabilityPatchSource.Contains('SetMaterialDirty')) 'Chat Readability must mark TMP materials dirty after outline changes.'
Assert-True ($chatReadabilityPatchSource.Contains('SetVerticesDirty')) 'Chat Readability must mark TMP geometry dirty after outline changes.'
Assert-True ($chatReadabilityLifecyclePatchSource.Contains('[HarmonyPatch(typeof(UIManager), "Awake")]')) 'Chat Readability must refresh after UIManager creates chat surfaces.'
Assert-True ($chatReadabilityLifecyclePatchSource.Contains('[HarmonyPatch("SetMessageFontOutline", typeof(bool))]')) 'Chat Readability must refresh after the game rewrites message font-outline state.'
Assert-True ($chatReadabilityLifecyclePatchSource.Contains('AccessTools.Method(typeof(UIManager), "SetMessageFontOutline", new[] { typeof(bool) }) != null')) 'Optional font-outline redraw hook must skip safely if the game method changes.'
Assert-True ($chatReadabilityLifecyclePatchSource.Contains('[HarmonyPatch(typeof(TextChannelManager), "ResetTextMaterials")]')) 'Chat Readability must refresh after the game toggles chat text content to reset materials.'
Assert-True ($chatReadabilityLifecyclePatchSource.Contains('ChatReadabilityPatch.RequestRefreshSoon();')) 'Chat Readability lifecycle hooks must queue a refresh instead of doing heavy work.'
Assert-True ($pluginSource.Contains('StartChatReadabilityController();')) 'Plugin must start the lightweight Chat Readability refresh controller.'
Assert-True ($pluginSource.Contains('_chatReadabilityControllerObject')) 'Plugin must keep a destroyable Chat Readability controller object.'
Assert-True (-not $chatReadabilityControllerSource.Contains('ActiveRefreshIntervalSeconds')) 'Chat Readability must not restore periodic active polling.'
Assert-True (-not $chatReadabilityControllerSource.Contains('IdleRefreshIntervalSeconds')) 'Chat Readability must not restore periodic idle polling.'
Assert-True ($chatReadabilityControllerSource.Contains('RefreshRequestVersion')) 'Chat Readability controller must react to explicit refresh requests.'
Assert-True ($chatReadabilityControllerSource.Contains('LockedChatUiScalePercent')) 'Chat Readability controller must watch chat UI scale even if outline/backdrop toggles are off.'
Assert-True ($chatReadabilityControllerSource.Contains('LockedChatWindowHeightPercent')) 'Chat Readability controller must watch chat window size even if outline/backdrop toggles are off.'
Assert-True ($chatReadabilityControllerSource.Contains('if (!stateChanged)')) 'Chat Readability controller must skip unchanged frames.'
Assert-True ($chatReadabilityControllerSource.Contains('ChatReadabilityPatch.RefreshVisibleChat();')) 'Chat Readability controller must reconcile after actual UI/config/display changes.'
Assert-True ($chatReadabilityControllerSource.Contains('Screen.width')) 'Chat Readability controller must notice display/window-size changes.'
Assert-True ($chatReadabilityControllerSource.Contains('IsMessagePanelHidden')) 'Chat Readability controller must notice chat panel state changes.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'src\SplitChatController.cs'))) 'SplitChat controller source must not ship in the public release.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'src\Patches\SplitChatPanelPatch.cs'))) 'SplitChat patch source must not ship in the public release.'
Assert-True ($chatReadabilityPatchSource.Contains('RestorePersistentBackdrop')) 'Chat Backdrop must restore saved backdrop colors when disabled.'
Assert-True ($chatReadabilityPatchSource.Contains('OriginalBackdropColors')) 'Chat Backdrop must remember original colors before darkening.'
Assert-True (-not $chatReadabilityPatchSource.Contains('image.raycastTarget = false')) 'Chat Backdrop must not disable vanilla raycast targets needed for chat scrolling.'
Assert-True (-not $chatReadabilityPatchSource.Contains('.raycastTarget = false')) 'Chat Backdrop must not disable any vanilla raycast targets needed for chat scrolling.'
Assert-True (-not $chatReadabilityPatchSource.Contains('text.text =')) 'Chat Readability must not rewrite chat text.'
Assert-True (-not $chatReadabilityPatchSource.Contains('SendMessageAsync')) 'Chat Readability must not send network/chat messages.'
Assert-True ($mentionNamePolicySource.Contains('TrailingStatusGroups')) 'Mention names must strip trailing status/Spoons groups.'
Assert-True ($mentionNamePolicySource.Contains('\([^\(\)]+\)')) 'Mention names must strip trailing parenthesized status groups like (Homelab).'
Assert-True ($mentionNamePolicySource.Contains('ToMentionDisplayLabel')) 'Mention names must keep a full display label for ping rendering.'
Assert-True ($mentionNamePolicySource.Contains('MentionReferenceRegex')) 'Mention names must expose a regex that consumes typed status/Spoons suffixes.'
Assert-True ($mentionHighlighterSource.Contains('MentionNamePolicy.MentionReferenceRegex')) 'Ping mention highlighter must consume typed status/Spoons suffixes as part of the mention.'
Assert-True ($mentionHighlighterSource.Contains('MentionNamePolicy.ToMentionDisplayLabel')) 'Ping mention highlighter must render the full display label once.'
Assert-True ($mentionPingControllerSource.Contains('MentionNamePolicy.ToMentionInsertionToken(match)')) 'Ping mention autocomplete must insert a parser-safe canonical token.'
Assert-True ($playerMentionProviderSource.Contains('.Select(MentionNamePolicy.ToMentionDisplayLabel)')) 'Mention provider must expose full display labels for suggestions.'
Assert-True ($reconnectPolicySource.Contains('suppressing overlapping reconnect pressure')) 'Reconnect guard policy must suppress overlapping reconnect pressure.'
Assert-True ($reconnectPolicySource.Contains('ReconnectGuardAction.Noop')) 'Reconnect guard policy must return Noop for handled no-work decisions.'
Assert-True ($reconnectPolicySource.Contains('not auto reconnecting')) 'Reconnect guard policy must suppress host-kick style reconnects by default.'

$readmeText = Get-Content -LiteralPath $readmePath -Raw
Assert-True ($supporterLedgerDataSource.Contains('"smorescubed"') -and $supporterLedgerDataSource.Contains('"<333"') -and $qolMenuSource.Contains('SupporterLedgerPolicy.RollUp(SupporterLedgerData.Transactions)')) 'Supporter credits must aggregate repeat receipts under the approved public display name and retain each distinct public message.'
Assert-True ($supporterLedgerDataSource.Contains('"Anon"') -and $supporterLedgerDataSource.Contains('"Akayra"') -and $supporterLedgerDataSource.Contains('"Ashcan"') -and $supporterLedgerPolicySource.Contains('SanitizeForDisplay')) 'Supporter aggregation must retain anonymous and named transactions using sanitized display name, received total, date, and message only.'
Assert-True ($supporterLedgerDataSource.Contains('new SupporterTransaction("2026-07-25 15:31 UTC", "Neonicalle"') -and $supporterLedgerDataSource.Contains('"15.00"') -and $supporterLedgerDataSource.Contains('new SupporterTransaction("2026-07-25 19:32 UTC", "Bochi"') -and $supporterLedgerDataSource.Contains('"50.00"') -and $supporterLedgerPolicySource.Contains('Date not recorded')) 'Neonicalle and Bochi must keep their exact UTC timestamps and individual amounts while genuinely empty dates retain the honest fallback.'
Assert-True ($pluginSource.Contains('private const string DefaultSpoonLabel = "sp"')) 'Spoons tag must keep the ASCII default label.'
Assert-True ($pluginSource.Contains('isHidden: true')) 'Internal BlueSage commands must stay hidden from normal command browsing.'
Assert-True ($typeaheadSource.Contains('MaxBrowserSuggestions')) 'Typeahead must allow a scrollable full command browser.'
Assert-True ($typeaheadSource.Contains('GUILayout.BeginScrollView')) 'Typeahead popup must be scrollable.'
Assert-True (-not $typeaheadSource.Contains('["mood"]')) 'Typeahead must not advertise the removed mood command.'
Assert-True ((Get-Item -LiteralPath $readmePath).Length -le 40000) 'README must remain readable without becoming a wall of text for casual players (cap raised again 2026-10-18: completeness and plain language come first).'
Assert-True ($readmeText.Contains('## Quick Start') -and $readmeText.Contains('Press `Home`') -and $readmeText.Contains('Press `Insert`')) 'README must lead consumers through an immediate first-use path.'
Assert-True ($readmeText.Contains('## Your first lobby') -and $readmeText.Contains('Start with the defaults') -and $readmeText.Contains('one setting at a time')) 'README must give casual players a defaults-first first-lobby path.'
Assert-True ($readmeText.Contains('## Friendly FAQ') -and $readmeText.Contains('`/chatvanilla`') -and $readmeText.Contains('never grants host or moderation powers')) 'README must answer everyday chat, compatibility-marker, and support questions without private implementation detail.'
Assert-True ($readmeText.Contains('Automatic Cleanup starts on and runs every 40 minutes') -and $readmeText.Contains('turn it off or choose 5–180 minutes') -and $readmeText.Contains("leaves the game's own cleanup alone") -and -not $readmeText.Contains('migrate once')) 'README must explain the user-controlled BlueSage cleanup default and untouched native timing without exposing migration mechanics.'
Assert-True ($readmeText.Contains('Community Safety and the Performance Overlay start on') -and $readmeText.Contains('Spoons start off')) 'README must state the qualified clean-install toggle defaults.'
Assert-True ($readmeText.Contains('names, status, and Spoons tag') -and $readmeText.Contains('first open after someone joins')) 'README must explain the user-visible row-layout fix.'
Assert-True ($readmeText.Contains('## Compatibility') -and $readmeText.Contains('PlayerLimitLift') -and $readmeText.Contains('host-only')) 'README must preserve simple PlayerLimitLift host-only guidance.'
Assert-True ($readmeText.Contains('Windows is tested') -and $readmeText.Contains('macOS and Linux can load the mod, but still need real-world player testing')) 'README must retain the plain-language platform-proof boundary.'
Assert-True ($readmeText.Contains('## Privacy') -and $readmeText.Contains('Visible labels never grant authority')) 'README must retain its consumer privacy boundary.'
Assert-True ($readmeText.Contains('## Troubleshooting and Support') -and $readmeText.Contains('roll back in your mod manager')) 'README must retain recovery and support paths.'
Assert-True ($readmeText.Contains('Made by Blue + Sage = BlueSage with superpowered neurodivergence ♥') -and -not $readmeText.Contains('Neonicalle') -and -not $readmeText.Contains('Bochi') -and -not $readmeText.Contains('partner in crime') -and -not $readmeText.Contains('candidate') -and -not $readmeText.Contains('runtime promotion') -and -not $readmeText.Contains('PurrNet')) 'README must keep the concise BlueSage credit without exposing named supporters or internal release and networking chronology.'
Assert-True (-not $readmeText.Contains('7BF6BA50FAC5') -and -not $readmeText.Contains('BlueExclusive') -and -not $readmeText.Contains('SteamID64') -and -not $readmeText.Contains('native PlayerID')) 'README must not include private candidate, companion, or identity implementation details.'

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
Assert-True ($manifest.name -eq $projectName) "Manifest name must be $projectName."
Assert-True ($manifest.version_number -eq $version) "Manifest version must be $version."
Assert-True ($manifest.description.Length -le 250) 'Manifest description must be 250 chars or fewer.'
Assert-True ($manifest.dependencies -contains 'BepInEx-BepInExPack-5.4.2305') 'Manifest must depend on current BepInEx-BepInExPack-5.4.2305.'

$changelogText = Get-Content -LiteralPath $changelogPath -Raw
Assert-True ((Get-Item -LiteralPath $changelogPath).Length -le 120000) 'CHANGELOG must keep the restored published history (cap raised 2026-10-18 for the full-depth restore).'
Assert-True ($changelogText -match '## 0\.2\.3 — 2026-07-23' -and $changelogText -match '## 0\.1\.0 — 2026-07-03' -and $changelogText -match '## 0\.0\.1 — 2026-06-18') 'CHANGELOG must preserve representative published-version history.'
Assert-True ($changelogText.StartsWith("# Changelog`n`nEvery previous public release is kept below so the complete BlueSage history stays available on Thunderstore.`n`n## 0.2.7 — 2026-10-25")) 'CHANGELOG must lead with the full-history notice and October 17, 2026 0.2.5 release notes.'
Assert-True ($changelogText.Contains('## 0.2.4 — 2026-07-30')) 'CHANGELOG must preserve the July 30, 2026 0.2.4 release history.'
Assert-True ($changelogText.Contains('role-label colors and visibility') -and $changelogText.Contains('pooled-row cleanup')) 'CHANGELOG must retain the readable role-label and row-lifecycle boundaries.'
Assert-True ($changelogText.Contains('names, status, and Spoons tag')) 'CHANGELOG must describe the player-row readability fix.'
Assert-True ($changelogText.Contains('Updated QoL and PLL with love as Blue + Sage = BlueSage ♥') -and -not $changelogText.Contains('Neonicalle') -and -not $changelogText.Contains('Bochi')) 'CHANGELOG must keep the concise BlueSage release credit without naming supporters.'
Assert-True ($changelogText.Contains('Focus Pulse')) 'CHANGELOG must describe the player-facing native Statistics summary.'
Assert-True (-not $changelogText.Contains('failed UAT') -and -not $changelogText.Contains('7BF6BA50FAC5') -and -not $changelogText.Contains('BlueExclusive') -and -not $changelogText.Contains('CreatorPresent') -and -not $changelogText.Contains('heartbeat') -and -not $changelogText.Contains('publication')) 'CHANGELOG must not include internal candidate, UAT, companion, or protocol chronology.'
$styleHelperSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\StyleHelperWindow.cs') -Raw
Assert-True ($pluginSource.Contains('PluginReleaseDate = "2026-10-25"')) 'QoL plugin must identify the October 25, 2026 release date.'
Assert-True ($qolMenuSource.Contains('Updated {Plugin.PluginReleaseDate}')) 'QoL Menu must render the repaired UAT date from the canonical plugin release identity.'
$mentionHighlighterSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Patches\MentionHighlighterPatch.cs') -Raw
$mentionPingSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\MentionPingController.cs') -Raw
Assert-True ($styleHelperSource.Contains('Application.OpenURL("https://discord.gg/JujMEwtN3q")')) 'Style Helper must include a Discord header button.'
Assert-True ($qolMenuSource.Contains('Application.OpenURL("https://discord.gg/JujMEwtN3q")')) 'QoL Menu must include a Discord header button.'
Assert-True ($styleHelperSource.Contains('Application.OpenURL("https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/")')) 'Style Helper must include the correct Blues Thunderstore header button.'
Assert-True ($qolMenuSource.Contains('Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl)') -and (Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\Core\HeaderActionLayoutPolicy.cs') -Raw).Contains('ThunderstoreUrl = "https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/"')) 'QoL Menu must include the correct Blues Thunderstore header button.'
Assert-True (-not $styleHelperSource.Contains('https://thunderstore.io/c/on-together/p/BlueSage/BlueSage_QoL_Tweaks_Beta/')) 'Style Helper must not use the old BlueSage Thunderstore owner path.'
Assert-True (-not $qolMenuSource.Contains('https://thunderstore.io/c/on-together/p/BlueSage/BlueSage_QoL_Tweaks_Beta/')) 'QoL Menu must not use the old BlueSage Thunderstore owner path.'
Assert-True ($styleHelperSource.Contains('Application.OpenURL("https://ko-fi.com/Q5Q1JRPW")')) 'Style Helper must include a Ko-fi header button.'
Assert-True ($qolMenuSource.Contains('Application.OpenURL("https://ko-fi.com/Q5Q1JRPW")')) 'QoL Menu must include a Ko-fi header button.'
Assert-True ($qolMenuSource.Contains('Credits / Other')) 'QoL Menu must replace the old Beta tab with Credits / Other.'
Assert-True ($qolMenuSource.Contains('DrawTabButton(4, isHost ? "Host Tools" : "Safety"') -and $qolMenuSource.Contains('DrawLobbySafetyTab()')) 'QoL Menu must expose the friendly Host Tools or Safety surface as a dedicated fifth top-level tab for verified hosts and assigned Helpers.'
Assert-True ($qolMenuSource.Contains('ShouldShowLobbySafetyTab(out LobbySafetyAccessState accessState') -and $qolMenuSource.Contains('if (_tab == 4 && !showLobbySafetyTab)') -and $qolMenuSource.Contains('HelperWaitingForHost')) 'Helper / Safety must stay hidden for ordinary players while showing a fail-closed waiting state for a listed Helper on a mismatched host build.'
Assert-True ($qolMenuSource.Contains('DrawLocalAuditActions(showIdentityNote: false)') -and $qolMenuSource.Contains('Snapshot Now') -and $qolMenuSource.Contains('Copy Audit Folder')) 'Normal Chat and Readability controls must match the public audit status, snapshot, and path commands.'
Assert-True ($qolMenuSource.Contains('Copy SteamID64') -and $qolMenuSource.Contains('Open Steam Profile')) 'Lobby Safety must provide exact copy and native profile actions for selected verified rows.'
Assert-True ($qolMenuSource.Contains('Click again to clear this player selection.') -and $qolMenuSource.Contains('Click again to clear this selection.') -and $qolMenuSource.Contains('No saved ban is selected.') -and -not $qolMenuSource.Contains('selected = records[start]')) 'Lobby Safety selections must be explicitly clearable and must never silently auto-target the first native ban.'
Assert-True ($qolMenuSource.Contains('ClearLobbySafetyTransientState();') -and $qolMenuSource.Contains('_selectedBannedSteamId = string.Empty;') -and $qolMenuSource.Contains('_nativeBanPage = 0;')) 'Lobby Safety access loss must disarm both live-player and native-ban selections and reset pages.'
Assert-True ($chatReadabilityControllerSource.Contains('DestroyedStatePruneIntervalSeconds = 5f') -and $chatReadabilityControllerSource.Contains('Time.unscaledTime >= _nextDestroyedStatePruneAt') -and $chatReadabilityPatchSource.Contains('DestroyOwnedMaterial(state?.AppliedFontMaterial)') -and $chatReadabilityPatchSource.Contains('OriginalSurfaceScales.Remove(id)') -and $chatReadabilityPatchSource.Contains('OriginalSurfaceWindowSizes.Remove(id)') -and -not $chatReadabilityPatchSource.Contains('GetDiagnosticStateSummary')) 'Destroyed chat rows must release owned TMP materials and retained surface state while readability remains active (round 4 R9: the diagnostic summary stays removed).'
Assert-True ($qolMenuSource.Contains('bool isProtectedRole = isSelfRow || isHostRow || isHelperRow || isProtectedVictim;') -and $qolMenuSource.Contains('GUI.enabled = !isProtectedRole &&') -and $qolMenuSource.Contains('moderationSurface.CanArmHostRemove')) 'Lobby Safety must block protected roles and require the live host moderation surface before a ban preview can be clicked.'
Assert-True ($pluginSource.Contains('SteamIdModerationController.IsAuthorizedCurrentIssuer(target.SteamId)') -and $pluginSource.Contains('No preview or pending action was created.')) 'Slash moderation must reject the host and assigned Helpers before creating a pending ban preview.'
Assert-True ($qolMenuSource.Contains('Resolve for Lobby') -and $qolMenuSource.Contains('Dismiss Locally')) 'Lobby Safety must expose explicit incident completion and local dismissal actions.'
Assert-True ($qolMenuSource.Contains('const int pageSize = 8') -and $qolMenuSource.Contains('◀ Previous') -and $qolMenuSource.Contains('Next ▶')) 'Lobby Safety must page large verified rosters outside vanilla chat.'
Assert-True ($qolMenuSource.Contains('DrawNativeBanRecords()') -and $qolMenuSource.Contains('Copy Banned SteamID64') -and $qolMenuSource.Contains('Preview SID Unban') -and $qolMenuSource.Contains('Confirm SID Unban')) 'Host Lobby Safety must expose paired native ban records with copy and exact preview/confirm unban.'
Assert-True ($qolMenuSource.Contains('Confirm Exact SID Ban') -and $qolMenuSource.Contains('Confirm Suspect Ban') -and $pluginSource.Contains('ConfirmSidUnbanFromQolMenu')) 'Lobby Safety menu moderation must retain the same exact-target preview/confirm flow as slash commands.'
Assert-True (-not $qolMenuSource.Contains('menu_desk_turtle')) 'QoL Menu must retire the broken turtle easter egg button.'
Assert-True (-not $qolMenuSource.Contains('menu_duck_debug')) 'QoL Menu must retire the broken duck easter egg button.'
Assert-True (-not $qolMenuSource.Contains('menu_wordle_goblin')) 'QoL Menu must retire the broken Wordle easter egg button.'
Assert-True ($qolMenuSource.Contains('menu_do_not_press')) 'QoL Menu must preserve the stable discovery key for its one optional lobby joke.'
Assert-True ($qolMenuSource.Contains('Optional lobby joke') -and $qolMenuSource.Contains('Sends one playful lobby chat line the first time. It never changes moderation or game state.')) 'QoL Menu must describe the optional one-time lobby joke truthfully.'
Assert-True ($qolMenuSource.Contains('TrySendDoNotPressLobbyMessage')) 'Optional lobby joke must call the guarded one-time lobby-chat path.'
Assert-True ($qolMenuSource.Contains('new GUIContent("DO NOT PRESS", "Extremely dangerous. Definitely not a harmless one-time easter egg. Probably.")')) 'Optional lobby joke must preserve the established fake-danger DO NOT PRESS presentation.'
Assert-True ($qolMenuSource.Contains('Repeat presses stay local and send nothing.')) 'Optional lobby joke must keep repeat presses local after the first discovery.'
Assert-True (-not $qolMenuSource.Contains('Join Blue Discord')) 'QoL Menu body must not duplicate the header Discord button.'
Assert-True (-not $qolMenuSource.Contains('if (!string.IsNullOrWhiteSpace(GUI.tooltip))')) 'QoL Menu footer must not conditionally change layout between IMGUI passes.'
Assert-True (-not $styleHelperSource.Contains('if (!string.IsNullOrWhiteSpace(GUI.tooltip))')) 'Style Helper footer must not conditionally change layout between IMGUI passes.'
Assert-True ($qolMenuSource.Contains('EasterEggTracker.Discover')) 'Optional lobby joke must use the shared discovery tracker.'
Assert-True ($qolMenuSource.Contains('Share finds in Blue''s Discord')) 'QoL Menu must invite players to share easter egg finds in Discord.'
Assert-True ($styleHelperSource.Contains('Detect Current')) 'Style Helper must include Detect Current.'
Assert-True ($styleHelperSource.Contains('Clear Draft')) 'Style Helper must label local-only clearing as Clear Draft.'
Assert-True (-not $styleHelperSource.Contains('Clear the current tab''s editor text')) 'Style Helper must not use ambiguous old Clear tooltip wording.'
Assert-True ($styleHelperSource.Contains('UndoLastAction')) 'Style Helper must include Undo support for apply/clear/save/load/edit actions.'
Assert-True (-not $styleHelperSource.Contains('Rewind')) 'Style Helper must not expose Rewind wording.'
Assert-True ($styleHelperSource.Contains('DrawSpoonsControls')) 'Style Helper must own Spoons identity editing.'
Assert-True ($styleHelperSource.Contains('SaveSpoonsFromStyleUi')) 'Style Helper must save Spoons from the identity/status surface.'
Assert-True ($styleHelperSource.Contains('MinWindowWidth = 920f')) 'Style Helper must default/min-size wide enough for readability.'
Assert-True ($styleHelperSource.Contains('BeginScrollView(_contentScroll')) 'Style Helper must scroll its content area.'
Assert-True ($styleHelperSource.Contains('_statusColor')) 'Style Helper must keep Status Styler color separate from Name Styler Color A.'
Assert-True ($styleHelperSource.Contains('DrawStatusColorPicker')) 'Style Helper must draw a dedicated Status Styler color picker.'
Assert-True ($styleHelperSource.Contains('CleanColor(_statusColor)')) 'Style Helper must apply Status Styler using the dedicated status color draft.'
Assert-True (-not $styleHelperSource.Contains('_colorA = Plugin.StatusColor?.Value')) 'Detect Current must not overwrite Name Styler Color A with the status color.'
Assert-True (-not $styleHelperSource.Contains('DrawColorPicker("Status color", 0)')) 'Status Styler must not reuse the Name Styler Color A picker.'
Assert-True ($styleHelperSource.Contains('DrawPresetColorLibrary(')) 'Style Helper must render Name and Status swatches through one shared color-library layout.'
Assert-True ($styleHelperSource.Contains('Status color library: tap a swatch')) 'Status Styler must label its color library clearly.'
Assert-True (-not $styleHelperSource.Contains('index += 6')) 'Status Styler color library must not use a fixed 6-column grid that diverges from Name Styler.'
Assert-True ($styleHelperSource.Contains('State: ')) 'Style Helper must show compact state text without duplicate shouting.'
Assert-True (-not $styleHelperSource.Contains('STATE: " + _stateLabel')) 'Style Helper must not duplicate tall state labels in the preview and footer.'
Assert-True ($qolMenuSource.Contains('BeginScrollView(_contentScroll')) 'QoL Menu must scroll its content area.'
Assert-True ($styleHelperSource.Contains('0.99f')) 'Style Helper must use near-opaque backgrounds.'
Assert-True ($qolMenuSource.Contains('0.99f')) 'QoL Menu must use near-opaque backgrounds.'
Assert-True ($qolMenuSource.Contains('MinWindowWidth = 920f')) 'QoL Menu must default/min-size wide enough for readability.'
Assert-True ($qolMenuSource.Contains('Click a feature''s On/Off button')) 'QoL Menu must include visible user guidance.'
Assert-True ($qolMenuSource.Contains('BuildTextSettingTooltip')) 'QoL Menu must include editable-field tooltips.'
Assert-True ($qolMenuSource.Contains('Use /styleui for name, status, and Spoons identity edits.')) 'QoL Menu Customize tab must point identity edits to Style Helper.'
Assert-True (-not $qolMenuSource.Contains('DrawTextSetting("Status base name"')) 'QoL Menu must not duplicate Style Helper status/name editing.'
Assert-True (-not $qolMenuSource.Contains('DrawTextSetting("Status message"')) 'QoL Menu must not duplicate Style Helper status editing.'
Assert-True (-not $qolMenuSource.Contains('DrawTextSetting("Status color"')) 'QoL Menu must not duplicate Style Helper status color editing.'
Assert-True (-not $qolMenuSource.Contains('DrawTextSetting("Spoons 0-5"')) 'QoL Menu must not duplicate Style Helper Spoons editing.'
Assert-True ($qolMenuSource.Contains('Reset to BlueSage Harbor')) 'QoL Menu must include a deterministic default-theme reset button.'
Assert-True (-not $uiThemeSource.Contains('public Color CalloutAccent') -and $uiThemeSource.Contains('NavigationKeywordHex')) 'Shared themes must keep navigation emphasis restrained instead of adding another button-state accent.'
Assert-True ($uiThemeSource.Contains('public Color NeutralButtonAccent') -and $uiThemeSource.Contains('public Color NeutralButtonText') -and $uiThemeSource.Contains('public static GUIStyle CreateNeutralButtonStyle') -and $uiThemeSource.Contains('palette.NeutralButtonAccent, palette.FieldBorder, palette.NeutralButtonText')) 'Shared themes must own neutral button fill, border, and text instead of inheriting ambiguous Unity skin colors.'
Assert-True ($qolMenuSource.Contains('_buttonStyle = BlueSageUiTheme.CreateNeutralButtonStyle(GUI.skin.button, _theme, 13);') -and $styleHelperSource.Contains('_buttonStyle = BlueSageUiTheme.CreateNeutralButtonStyle(GUI.skin.button, _theme, 14);')) 'QoL Menu and Style Helper must both use the owned neutral button style.'
Assert-True ($uiThemeSource.Contains('public Color ActiveStateText') -and $uiThemeSource.Contains('public Color EnabledButtonText') -and $uiThemeSource.Contains('public Color DisabledButtonText') -and $uiThemeSource.Contains('public Color ExperimentalButtonText')) 'Shared themes must expose explicit text colors for every semantic button state.'
Assert-True ($qolMenuSource.Contains('_theme.ActiveStateText, 13)') -and $qolMenuSource.Contains('_theme.EnabledButtonText, 13)') -and $qolMenuSource.Contains('_theme.DisabledButtonText, 13)') -and $qolMenuSource.Contains('_theme.ExperimentalButtonText, 13)') -and $styleHelperSource.Contains('_theme.ActiveStateText, 14)')) 'Public menu surfaces must build semantic states from the owned state-text palette.'
Assert-True ($uiThemeSource.Contains('bool lightText = RelativeLuminance(text) >= 0.50f;') -and $uiThemeSource.Contains('Color hoverFill = Color.Lerp(fill, lightText ? Color.black : Color.white') -and $uiThemeSource.Contains('Color pressedFill = Color.Lerp(fill, lightText ? Color.black : Color.white') -and $uiThemeSource.Contains('ApplyState(style.active, pressedFill, border, text);') -and $uiThemeSource.Contains('ApplyState(style.onActive, pressedFill, border, text);')) 'Semantic button hover and pressed fills must move away from the text luminance polarity for readable contrast.'
Assert-True ($uiThemeSource.Contains('public static Color BestTextColor(Color background, BlueSageUiThemePalette palette)') -and $uiThemeSource.Contains('ContrastRatio(dark, background) >= ContrastRatio(Color.white, background)')) 'Shared themes must choose swatch text dynamically from measured contrast.'
Assert-True ($styleHelperSource.Contains('GUI.contentColor = BlueSageUiTheme.BestTextColor(swatch, _theme);') -and $styleHelperSource.Contains('GUI.contentColor = oldContent;')) 'Style Helper swatches must apply the dynamic text color and restore the prior GUI content color.'
Assert-True ($qolMenuSource.Contains('styleHelperOpen ? _enabledButtonStyle : _buttonStyle') -and -not $qolMenuSource.Contains('_calloutButtonStyle')) 'Style Helper actions must stay neutral while closed and use enabled styling only while open.'
Assert-True ($qolMenuSource.Contains('BuildMenuNavigationHint(_theme.NavigationKeywordHex, Plugin.QolMenuKey.Value.ToString(), Plugin.StyleMenuKey.Value.ToString())')) 'QoL Menu help must use the theme-aware navigation keyword callout with the real configured hotkeys.'
Assert-True (-not $publicSourceText.Contains('!bsrescue:') -and -not $publicSourceText.Contains('!bsvisible:')) 'Legacy chat-shaped rescue/visibility control prefixes must not ship.'
Assert-True (-not $pluginSource.Contains('AllowTrustedHostRescue') -and -not $publicSourceText.Contains('ShouldAllowTrustedHostRescue')) 'The authenticated compatibility rescue receiver must stay always ready without a user-facing opt-in gate.'
Assert-True ($pluginSource.Contains('"selfmove"') -and $pluginSource.Contains('HandleSelfMovementCommand')) 'Supported self movement must have local slash-command parity with the menu.'
Assert-True (-not $qolMenuSource.Contains('Undo Theme')) 'QoL Menu must not expose the removed theme undo button.'
Assert-True ($qolMenuSource.Contains('Run QoL Status')) 'QoL Menu Main must include a user-facing QoL status button.'
Assert-True ($typeaheadSource.Contains('PopupBackground')) 'Typeahead popup must use a dedicated opaque background.'
Assert-True (-not $typeaheadSource.Contains('background = MakeSolidTexture(theme.PreviewBackground)')) 'Typeahead popup must not reuse the preview background.'
Assert-True ($mentionHighlighterSource.Contains('[PING]')) 'Mention highlighter must add visible ping marker behavior.'
Assert-True ($mentionHighlighterSource.Contains('"lobby"')) 'Mention highlighter must support @lobby.'
Assert-True ($mentionHighlighterSource.Contains('Plugin.IsLobbyMassPingAuthorized(ChatSenderContextPatch.CurrentSenderSteamId)')) '@lobby must consume the sender identity captured from the authoritative chat RPC.'
Assert-True ($chatSenderContextSource.Contains('SendMessageAsync_Original_0') -and $chatSenderContextSource.Contains('RPCInfo info')) 'Chat sender capture must wrap the current chat method that still carries RPCInfo.sender.'
Assert-True ($chatSenderContextSource.Contains('MaximumChatPayloadBytes') -and $chatSenderContextSource.IndexOf('__0.Length > MaximumChatPayloadBytes') -lt $chatSenderContextSource.IndexOf('TryCaptureAuthoritativeSender(info') -and -not $chatSenderContextSource.Contains('ContainsLobbyPingToken')) 'Chat sender capture must bound every player-message payload before authoritative roster work without special-casing @lobby.'
Assert-True ($chatSenderContextSource.Contains('InputFieldLimitPolicy.AbsoluteMaxChatCharacters * sizeof(char)')) '@lobby raw-token gating must honor the complete supported 4,000-character UTF-16 chat surface.'
Assert-True ($chatSenderContextSource.Contains('AuthoritativeChatSenderPolicy.TryResolveUniqueIndex(') -and $chatSenderContextSource.Contains('info.sender') -and $chatSenderContextSource.Contains('panel.PlayerIDs') -and $chatSenderContextSource.Contains('PlayerIdentityEvidenceController.TryResolveRosterIndexFresh')) '@lobby sender capture must resolve one unique aligned current roster row before authorization.'
Assert-True ($chatSenderContextSource.Contains('[HarmonyFinalizer')) '@lobby sender context must restore its prior scoped identity even when the game chat method throws.'
Assert-True ($chatSenderContextSource.Contains('if (!_captureFailureLogged)')) '@lobby sender-capture diagnostics must remain one-shot instead of spamming chat-heavy sessions.'
Assert-True (-not $pluginSource.Contains('senderIndex <= 0')) '@lobby must not confuse a valid row-zero host with the game sender-index fallback.'
Assert-True ($pluginSource.Contains('LobbyHelpers = Config.Bind("Helpers", "LobbyHelpers"')) '@lobby and moderation Helpers must share the canonical stable SteamID list.'
Assert-True ($pluginSource.Contains('HandleHelperCommand')) 'Host-only unified Helper commands must exist.'
Assert-True ($mentionPingSource.Contains('PlayTaskComplete')) 'Mention ping controller must support multiple sound modes.'
Assert-True ($mentionPingSource.Contains('string replacement = "@" + cleanMatch + " ";')) 'Mention autocomplete must insert plain @Name text, not hidden rich-text tags.'
Assert-True ($panelSurfaceControllerSource.Contains('MentionNamePolicy.ToMentionInsertionToken(') -and $panelSurfaceControllerSource.Contains('string mention = "@" + cleanName + " ";')) 'Player-list @ button must insert the same parser-safe mention token as autocomplete.'
Assert-True ($mentionHighlighterSource.Contains('string displayToken = string.IsNullOrWhiteSpace(cleanMatched)')) 'Mention highlighter must render matched plain names in ping color.'
Assert-True ($styleHelperSource.Contains('DrawResizeGrip')) 'Style Helper must expose a resize grip for smaller monitors.'
Assert-True ($qolMenuSource.Contains('DrawResizeGrip')) 'QoL Menu must expose a resize grip for smaller monitors.'
Assert-True ($uiThemeSource.Contains('HideFlags.HideAndDontSave') -and $uiThemeSource.Contains('RuntimeTextureGeneration') -and $uiThemeSource.Contains('ReleaseRuntimeTextures')) 'Runtime BlueSage UI textures must survive unused-asset sweeps and release on plugin unload.'
Assert-True ($qolMenuSource.Contains('BlueSageUiTheme.AreRuntimeTexturesAlive()') -and $styleHelperSource.Contains('BlueSageUiTheme.AreRuntimeTexturesAlive()')) 'Menu style caches must rebuild if a runtime texture is ever invalidated.'
Assert-True ($windowFitPolicySource.Contains('DefaultWindowWidth = 1390f')) 'Window fit policy must use the reviewed QoL comfort width.'
Assert-True ($windowFitPolicySource.Contains('DefaultWindowHeight = 1210f')) 'Window fit policy must use the reviewed QoL comfort height.'
Assert-True ($windowFitPolicySource.Contains('MenuDisplayScalePolicy.GetComfortSize(surface)')) 'Window fit policy must select the reviewed per-surface comfort size.'
Assert-True ($windowFitPolicySource.Contains('MinimumUsableWidth = 560f')) 'Window fit policy must allow small-display fallback width.'
Assert-True ($windowFitPolicySource.Contains('MinimumUsableHeight = 560f')) 'Window fit policy must preserve a readable content viewport when the display permits.'
Assert-True ($qolMenuSource.Contains('BlueSageWindowCoordinator.ResolveForFrame')) 'QoL Menu must use the shared smart-fit coordinator.'
Assert-True ($styleHelperSource.Contains('BlueSageWindowCoordinator.ResolveForFrame')) 'Style Helper must use the shared smart-fit coordinator.'
Assert-True ($windowFitPolicySource.Contains('FitInitial')) 'Window fit policy must support deterministic initial sizing.'
Assert-True ($windowFitPolicySource.Contains('FitForOpen')) 'Window fit policy must distinguish first open from later user-sized opens.'
Assert-True ($windowCoordinatorSource.Contains('WindowFitPolicy.FitToScreen') -and $windowCoordinatorSource.Contains('WindowFitPolicy.FitForOpen') -and -not $windowCoordinatorSource.Contains('SharedWindowLayoutPolicy.CreatePair')) 'Each public window must preserve independent saved geometry and screen fitting without forced paired-window framing.'
Assert-True ($qolMenuSource.Contains('BlueSageWindowCoordinator.ResolveForOpen') -and $qolMenuSource.Contains('private bool _hasOpened;')) 'QoL Menu must use the reviewed first-open fit while preserving later user sizing.'
Assert-True ($styleHelperSource.Contains('BlueSageWindowCoordinator.ResolveForOpen') -and $styleHelperSource.Contains('private bool _hasOpened;')) 'Style Helper must use the reviewed first-open fit while preserving later user sizing.'
Assert-True (-not $qolMenuSource.Contains('expandToMaximum: !_hasManualResize')) 'QoL Menu must not vary its layout branch through session-only maximum expansion.'
Assert-True (-not $styleHelperSource.Contains('expandToMaximum: !_hasManualResize')) 'Style Helper must not vary its layout branch through session-only maximum expansion.'
Assert-True ($qolMenuSource.Contains('Tip: hover')) 'QoL Menu must make hover/footer guidance obvious.'
Assert-True ($styleHelperSource.Contains('The highlighted slot is the one the shared color library will change.')) 'Style Helper must make color-library guidance obvious.'

Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Image]::FromFile($iconPath)
try {
    Assert-True ($icon.Width -eq 256 -and $icon.Height -eq 256) 'icon.png must be 256x256.'
}
finally {
    $icon.Dispose()
}

if ($SkipPackage) {
    [pscustomobject]@{
        Project = $projectName
        Version = $version
        BuildAndTests = $(if ($SkipBuild) { 'skipped by caller' } elseif ($SkipTests) { 'build passed; tests skipped by caller' } else { 'passed' })
        StaticReleaseContract = 'passed'
        PackagePrivacy = 'not run'
        Package = 'not modified'
        ReleaseDllSha256 = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash
        VerifiedAt = (Get-Date).ToString('s')
    } | Format-List
    return
}

if (Test-Path -LiteralPath $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $stageDir | Out-Null
Copy-FileStrict $manifestPath (Join-Path $stageDir 'manifest.json')
Copy-FileStrict $readmePath (Join-Path $stageDir 'README.md')
Copy-FileStrict $changelogPath (Join-Path $stageDir 'CHANGELOG.md')
Copy-FileStrict $iconPath (Join-Path $stageDir 'icon.png')
Copy-FileStrict $dllPath (Join-Path $stageDir "BepInEx\plugins\$projectName\$projectName.dll")

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $fixedTimestamp = [DateTimeOffset]::new(2026, 8, 6, 0, 0, 0, [TimeSpan]::Zero)
    $files = Get-ChildItem -LiteralPath $stageDir -Recurse -File |
        Sort-Object { [System.IO.Path]::GetRelativePath($stageDir, $_.FullName).Replace('\', '/') }
    foreach ($file in $files) {
        $entryName = [System.IO.Path]::GetRelativePath($stageDir, $file.FullName).Replace('\', '/')
        $entry = $zip.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $fixedTimestamp
        $input = [System.IO.File]::OpenRead($file.FullName)
        $output = $entry.Open()
        try {
            $input.CopyTo($output)
        }
        finally {
            $output.Dispose()
            $input.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
}

$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = $zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
    $fileEntries = @($zip.Entries | Where-Object { -not $_.FullName.EndsWith('/') -and -not $_.FullName.EndsWith('\\') })
    $required = @(
        'manifest.json',
        'README.md',
        'CHANGELOG.md',
        'icon.png',
        "BepInEx/plugins/$projectName/$projectName.dll"
    )

    foreach ($entry in $required) {
        Assert-True ($entries -contains $entry) "Zip missing required entry: $entry"
    }

    Assert-True ($fileEntries.Count -eq $required.Count) "Zip must contain exactly the five intended payload files; found $($fileEntries.Count)."
    Assert-True (-not ($fileEntries | Where-Object { $required -notcontains $_.FullName.Replace('\\', '/') })) 'Zip contains an unexpected payload file.'

    Assert-True (-not ($entries | Where-Object { $_ -like "$projectName/*" })) 'Zip has a top-level wrapper directory.'
    Assert-True (-not ($entries -contains 'CREDITS_AND_SOURCES.md')) 'Separate credits/source file must not ship in Thunderstore zip.'
    Assert-True (-not ($entries -contains 'BETA_TEST_GUIDE.md')) 'Internal beta test guide must not ship in Thunderstore zip.'
    Assert-True (-not ($entries -contains 'BETA_UAT_CHECKLIST.md')) 'Internal UAT checklist must not ship in Thunderstore zip.'
    Assert-True (-not ($entries -contains 'FINAL_BETA_AUDIT.md')) 'Internal final audit must not ship in Thunderstore zip.'
    Assert-True (-not ($entries | Where-Object { $_ -eq 'docs/community-metadata-probe.json' })) 'Generated probe evidence must not ship in Thunderstore zip.'

    $sourceByEntry = @{
        'manifest.json' = $manifestPath
        'README.md' = $readmePath
        'CHANGELOG.md' = $changelogPath
        'icon.png' = $iconPath
        "BepInEx/plugins/$projectName/$projectName.dll" = $dllPath
    }
    foreach ($entryName in $required) {
        $archiveEntry = $zip.Entries |
            Where-Object { $_.FullName.Replace('\', '/') -eq $entryName } |
            Select-Object -First 1
        Assert-True ($null -ne $archiveEntry) "Zip parity check could not open required entry: $entryName"

        $entryStream = $archiveEntry.Open()
        try {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $archivedHash = ([BitConverter]::ToString($sha.ComputeHash($entryStream))).Replace('-', '')
            }
            finally {
                $sha.Dispose()
            }
        }
        finally {
            $entryStream.Dispose()
        }

        $sourceHash = (Get-FileHash -LiteralPath $sourceByEntry[$entryName] -Algorithm SHA256).Hash
        Assert-True ($archivedHash -eq $sourceHash) "Archived payload does not match its verified source: $entryName"
    }

    foreach ($markdownEntry in $zip.Entries | Where-Object { $_.FullName -match '\.md$' }) {
        $reader = New-Object System.IO.StreamReader($markdownEntry.Open())
        try {
            $markdownText = $reader.ReadToEnd()
            Assert-True (-not ($markdownText -match '[A-Z]:\\')) "Packaged markdown contains a local Windows path: $($markdownEntry.FullName)"
            Assert-True (-not ($markdownText -match 'Program Files')) "Packaged markdown contains a local install path: $($markdownEntry.FullName)"
        }
        finally {
            $reader.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
}

$privacyScan = Test-BlueSageZipPrivacy -ZipPath $zipPath -ForbiddenDigests @($protectedIdentityDigest)
Assert-True ($privacyScan.Passed) "Package privacy check failed; findings=$($privacyScan.FindingCount); scanErrors=$($privacyScan.ScanErrorCount)"

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
[pscustomobject]@{
    Project = $projectName
    Version = $version
    ZipPath = $zipPath
    Sha256 = $hash.Hash
    PackagePrivacy = 'passed'
    VerifiedAt = (Get-Date).ToString('s')
} | Format-List
