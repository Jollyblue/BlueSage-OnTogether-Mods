using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BlueSage.QoLTweaks.Contracts;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlueSage.QoLTweaks;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("OnTogether.exe")]
[BepInProcess("On-Together")]
public sealed partial class Plugin : BaseUnityPlugin
{
    internal enum HotkeyBindTarget
    {
        QolMenu,
        StyleMenu,
        NoclipFly,
        ManualSweep
    }

    // Excludes Mouse*/Joystick* buttons: this feature is specifically for keyboard-layout
    // preference, and allowing mouse buttons would let the Rebind button's own click register as
    // a capturable key.
    private static readonly KeyCode[] CapturableKeyCodes = Enum.GetValues(typeof(KeyCode))
        .Cast<KeyCode>()
        .Where(code => !code.ToString().StartsWith("Mouse", StringComparison.Ordinal) &&
                       !code.ToString().StartsWith("Joystick", StringComparison.Ordinal))
        .ToArray();

    private const string DefaultSpoonLabel = "sp";

    public const string PluginGuid = "com.bluesage.ontogether.qoltweaks.beta";
    public const string PluginName = "BlueSage QoL Tweaks - Beta";
    public const string PluginVersion = "0.2.7";
    public const string PluginDisplayVersion = "0.2.7";
    public const string PluginReleaseDate = "2026-10-25";
#if MODDERBRANCH_UAT
    internal const string PluginBuildIdentity = "0.2.7+20261025.round5h-rc7-uat";
#else
    internal const string PluginBuildIdentity = "0.2.7+20261025.round5h-rc7";
#endif
    private const string StyleStateMigrationTarget = PluginVersion + "-legacy-style-repair-v1";

    private readonly Harmony _harmony = new Harmony(PluginGuid);
    private readonly SweepOperationGate _sweepOperationGate = new SweepOperationGate();
    private readonly SweepSchedulePolicy _sweepSchedule = new SweepSchedulePolicy();
    private Coroutine _sweepCoroutine;
    private Coroutine _activeSweepOperationCoroutine;
    private bool _ownsSweepRunInBackgroundOverride;
    private bool _runInBackgroundBeforeSweepOverride;
    private Coroutine _compatibilityRefreshCoroutine;
    private Coroutine _hostHealthCoroutine;
    private Coroutine _communityBanListCoroutine;
    private Coroutine _managedReconnectCoroutine;
    private Coroutine _replacementLobbyDiscoveryCoroutine;
    private Coroutine _cleanMenuReconnectCoroutine;
    private Coroutine _reconnectAnnouncementCoroutine;
    private Coroutine _welcomeMessageCoroutine;
    private Coroutine _rescueReceiverCapabilityCoroutine;
    private CSteamID _rescueReceiverCapabilityLobby = CSteamID.Nil;
    private PluginCompatibilityResult _compatibility;
    private BlueSageCommandRegistry _commandRegistry;
    private float _lastLobbyVisibleAt = -1f;
    private float _lastManagedReconnectAttemptAt = -1f;
    private int _managedReconnectAttempt;
    private bool _managedReconnectActive;
    private bool _managedReconnectSuccessAnnounced;
    private int _reconnectReadySampleCount;
    private float _reconnectQuorumReadySince = -1f;
    private string _lastReconnectLobbyCode = string.Empty;
    private string _lastReconnectLobbyOwnerSteamId = string.Empty;
    private string _lastReconnectLobbyNameFingerprint = string.Empty;
    private bool _replacementLobbyDiscoveryAttempted;
    private bool _lastLobbyRejoinInProgress;
    private bool _reconnectMenuFallbackInProgress;
    private bool _cleanMenuReconnectActive;
    private int _cleanMenuReconnectGeneration;
    private string _cleanMenuReconnectPhase = "idle";
    private string _reconnectBudgetLobbyCode = string.Empty;
    private int _reconnectBudgetGenerationsUsed;
    private float _reconnectBudgetIdleSince = -1f;
    private float _hostHealthStartedAt = -1f;
    private string _lastHostHealthLogKey;
    private GameObject _mentionPingControllerObject;
    private GameObject _commandTypeaheadControllerObject;
    private GameObject _chatReadabilityControllerObject;
    private GameObject _cloneShieldControllerObject;
    private GameObject _playerRenderSaverControllerObject;
    private GameObject _performanceOverlayControllerObject;
    private GameObject _sessionAuditControllerObject;
    private GameObject _sessionRunMarkerControllerObject;
    private StyleHelperWindow _styleHelperWindow;
    private QolMenuWindow _qolMenuWindow;
    private ConfigEntry<KeyCode> _pendingRebindEntry;
    private string _pendingRebindLabel;
    private HotkeyBindTarget _pendingRebindTarget;
    private int _pendingRebindStartFrame;
    private float? _pendingRebindActiveSince;
    private float _pendingRebindRequestedAt;
    private string _lastAnnouncedRebindRejection;
    private readonly List<string> _pendingHotkeyRepairNotices = new List<string>();
    private readonly List<ToastEntry> _toasts = new List<ToastEntry>();
    private GUIStyle _toastStyle;
    private string _welcomeOverlayOfferedVisitKey = string.Empty;
    private bool _welcomeOverlayVisible;
    private bool _otherQolModNoticeShown;
    private GUIStyle _welcomeOverlayBoxStyle;
    private GUIStyle _welcomeOverlayBodyStyle;
    private GUIStyle _welcomeOverlayTitleStyle;
    private const int WelcomeOverlayWindowId = 47062;
    private const int ToastWindowId = 47063;
    private static float ToastScale => ToastPolicy.ResolveScale(ToastScalePercent?.Value ?? 0, Screen.height);
    private static float ToastWidth => ToastPolicy.ComputeWidth(Screen.width, ToastScale);
    internal static float LockedToastHoldSeconds => ToastPolicy.ClampHoldSeconds(ToastHoldSeconds?.Value ?? ToastPolicy.DefaultHoldSeconds);
    private const string SteamIdMismatchAlertText =
        "BlueSage found a player whose Steam ID doesn't match Steam's own record. Their ID card and Helper tools stay locked; check Host Tools before trusting that name.";
    private const float ToastMinHeight = 36f;
    private const float ToastSpacing = 6f;
    private const float WelcomeOverlayTitleHeight = 26f;
    private const float WelcomeOverlayButtonHeight = 28f;
    // Must equal the buttons DrawWelcomeOverlayWindowContents draws (pinned by a contract test).
    private const int WelcomeOverlayButtonCount = 4;
    private GUI.WindowFunction _toastWindowFunction;
    private GUI.WindowFunction _welcomeOverlayWindowFunction;
    private const int GuideOverlayWindowId = 47064;
    private bool _guideOverlayVisible;
    private Vector2 _guideOverlayScroll;
    private GUI.WindowFunction _guideOverlayWindowFunction;
    private GUIStyle _guideOverlayHeadingStyle;
    // Rebuilt only when a hotkey changes, not on every IMGUI event.
    private IReadOnlyList<GuideSection> _guideSections;
    private string _guideSectionsKey;
    private string _welcomeOverlayBody = string.Empty;
    private float _welcomeOverlayBodyHeight;
    private BlueSageReportWindow _blueSageReportWindow;
    private bool _welcomeMessageShownForLobby;
    private bool _welcomePrimaryShownForLobby;
    private bool _welcomeContextShownForLobby;
    private string _welcomeVoiceStateLogKey = string.Empty;
    private string _welcomeLobbyKey = string.Empty;
    // Voice pass (2026-10-23) NOISE 1/2: quiet reconnect returns and a once-per-session Tip line.
    private bool _welcomeTipShownThisSession;
    private static string _quietTrackedVisitKey = string.Empty;
    private static bool _quietTrackedVisitIsReconnectReturn;
    private static bool _quietTrackedVisitDecided;
    private static string _lastVisitLobbyId = string.Empty;
    private static string _reconnectReturnTargetLobbyId = string.Empty;
    private static string _reconnectReturnLobbyIdBeforeDrop = string.Empty;
    private static string _reconnectReturnNotedVisitKey = string.Empty;
    private string _lastStatusBeforePreset = string.Empty;
    private PendingModerationAction _pendingModerationAction;
    private float _nextModerationCapabilityPublishAt;
    private bool _hadLobbySafetyAccess;

    private sealed class DisplayNameDraft
    {
        public string BaseName;
        public string StatusMessage;
        public string StatusColor;
        public string StatusBrackets;
        public bool SpoonsEnabled;
        public int SpoonCount;
        public string SpoonLabel;
        public string SpoonsUpdatedUtc;

    }

    private sealed class PendingModerationAction
    {
        public string Action;
        public string TargetSteamId;
        public string IncidentId;
        public string LobbyCode;
        public bool TargetWasOffline;
        public long ExpiresUnixSeconds;
    }

    internal static ManualLogSource Log { get; private set; }
    internal static Plugin Instance { get; private set; }

    internal static int LockedMaxIdCardCharacters => InputFieldLimitPolicy.ClampProfileCharacters(MaxProfileCharacters?.Value ?? InputFieldLimitPolicy.DefaultMaxProfileCharacters);
    internal static int LockedMaxSessionNameCharacters => InputFieldLimitPolicy.ClampSessionNameCharacters(MaxSessionNameCharacters?.Value ?? InputFieldLimitPolicy.DefaultMaxSessionNameCharacters);
    internal static int LockedMaxChatCharacters => InputFieldLimitPolicy.ClampChatCharacters(MaxChatCharacters?.Value ?? InputFieldLimitPolicy.DefaultMaxChatCharacters);
    internal static int LockedChatOutlineIntensityPercent => ChatReadabilityStylePolicy.ClampOutlineIntensityPercent(ChatOutlineIntensity?.Value ?? ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent);
    internal static int LockedChatUiScalePercent => ChatAccessibilityScalePolicy.ClampScalePercent(ChatUiScalePercent?.Value ?? ChatAccessibilityScalePolicy.DefaultScalePercent);
    internal static MenuDisplayScaleResult LockedMenuDisplayScale => MenuDisplayScalePolicy.Resolve(MenuUiScalePercent?.Value ?? MenuDisplayScalePolicy.AutoScalePercent, Screen.dpi);
    internal static int LockedChatFontSizePercent => ChatFontSizePolicy.ClampFontSizePercent(ChatFontSizePercent?.Value ?? ChatFontSizePolicy.DefaultFontSizePercent);
    internal static int LockedChatWindowHeightPercent => ChatWindowHeightPolicy.ClampHeightPercent(ChatWindowHeightPercent?.Value ?? ChatWindowHeightPolicy.DefaultHeightPercent);
    internal static int LockedChatHistoryRows => ChatHistoryLimitPolicy.ClampRows(ChatHistoryRows?.Value ?? ChatHistoryLimitPolicy.DefaultRows);
    internal static float LockedLocalChatRangeMeters => LocalChatRangePolicy.ClampMeters(LocalChatRangeMeters?.Value ?? LocalChatRangePolicy.DefaultMeters);
    internal static float LocalChatRangeSquaredMeters => LocalChatRangePolicy.ResolveSquaredMeters(LockedLocalChatRangeMeters);
    internal static int LockedPlayerRenderSaverRadiusMeters => PlayerRenderSaverPolicy.ClampRadiusMeters(PlayerRenderSaverRadiusMeters?.Value ?? PlayerRenderSaverPolicy.DefaultRadiusMeters);
    internal static int LockedPlayerRenderSaverMaxVisiblePlayers => PlayerRenderSaverPolicy.ClampMaxVisiblePlayers(PlayerRenderSaverMaxVisiblePlayers?.Value ?? PlayerRenderSaverPolicy.DefaultMaxVisiblePlayers);
    internal static float LockedSelfSpeedMultiplier => SelfMovementPolicy.ClampSpeed(SelfSpeedMultiplier?.Value ?? SelfMovementPolicy.DefaultMultiplier);
    internal static float LockedSelfJumpMultiplier => SelfMovementPolicy.ClampJump(SelfJumpMultiplier?.Value ?? SelfMovementPolicy.DefaultMultiplier);
    internal static float LockedSelfGravityMultiplier => SelfMovementPolicy.ClampGravity(SelfGravityMultiplier?.Value ?? SelfMovementPolicy.DefaultMultiplier);
    internal static string LockedChatOutlineColorHex => ChatReadabilityStylePolicy.NormalizeHexColor(ChatOutlineColor?.Value, "000000");
    internal static string LockedBlackChatOutlineColorHex => ChatReadabilityStylePolicy.NormalizeHexColor(BlackChatOutlineColor?.Value, "C0C0C0");
    internal const int LockedHostHealthIntervalSeconds = 300;
    internal const bool LockedHostHealthRefreshLobbyMetadata = true;
    internal const bool LockedHostHealthRequestPersonaRefresh = true;
    internal const bool LockedHostHealthShowNotifications = false;
    internal const int LockedHostHealthWarnAfterHours = 12;
    internal const bool LockedReconnectRequireVisibleLobby = true;
    internal const bool LockedReconnectManageHostKick = false;
    internal const bool LockedReconnectUseLobbyGrace = true;
    internal const int LockedReconnectLobbyGraceSeconds = 20;
    internal const int LockedReconnectRetryIntervalSeconds = 5;
    internal const int LockedReconnectMaxAttempts = 3;
    internal const int LockedReconnectPostConnectValidationSeconds = 8;
    internal const int LockedReconnectReadyStabilitySeconds = 2;
    internal const int LockedReconnectCleanupTimeoutSeconds = 10;
    internal const int LockedReconnectJoinValidationTimeoutSeconds = 45;
    // Round 5b (2026-10-23): guarded rejoins per saved lobby; a playable reconnect or a different lobby resets it.
    internal const int LockedReconnectMaxGenerationsPerLobby = 5;
    internal const int LockedReconnectBudgetIdleResetSeconds = 120;
    // The log readout tool counts "Steam dropped your lobby"; keep that phrase in the host notice.
    internal const string ReconnectBudgetSpentNotice = "BlueSage tried to reconnect 5 times, so it's resting now. You can rejoin from the menu when the host is back.";
    internal const string HostLobbyLostNotice = "Steam dropped your lobby, so new players can't join and host tools are locked. Host again when you're ready.";
    internal const bool LockedReconnectShowNotifications = true;
    internal const string LockedReconnectAnnouncementMessage = "I'm back after a connection hiccup (BlueSage QoL).";
    // Voice pass (2026-10-23): what Reconnect Guard tells the player, in order of the journey.
    internal const string ReconnectStartingNotice = "Connection dropped. BlueSage is getting you back in…";
    internal const string ReconnectJoiningNotice = "Reconnecting to your lobby now.";
    internal const string ReconnectIncompleteNotice = "That reconnect didn't finish. BlueSage is stepping back to the menu.";
    internal const string ReconnectStoppedNotice = "Reconnect stopped. You can rejoin from the menu whenever you're ready.";
    internal const string ReconnectLobbyNotFoundNotice = "BlueSage couldn't find your lobby again, so you're back at the menu. You can rejoin from there.";
    internal const string ReconnectSearchingNotice = "Looking for your host's new lobby…";
    internal const string ReconnectFoundNewLobbyNotice = "Found your host's new lobby. Reconnecting.";
    internal const int LockedReconnectAnnouncementDelaySeconds = 3;

    internal static ConfigEntry<bool> EnableAutoSweep { get; private set; }
    internal static ConfigEntry<bool> AutoDisableSweepWhenAndrewSweepInstalled { get; private set; }
    internal static ConfigEntry<bool> AllowManualSweepWhenAndrewSweepInstalled { get; private set; }
    internal static ConfigEntry<int> SweepIntervalMinutes { get; private set; }
    internal static ConfigEntry<bool> NotifyOnSweep { get; private set; }
    internal static ConfigEntry<bool> ForceManagedGcCollect { get; private set; }
    internal static ConfigEntry<bool> EnableManualSweepHotkey { get; private set; }
    internal static ConfigEntry<KeyCode> ManualSweepKey { get; private set; }
    internal static ConfigEntry<KeyCode> QolMenuKey { get; private set; }
    internal static ConfigEntry<KeyCode> StyleMenuKey { get; private set; }
    internal static ConfigEntry<KeyCode> NoclipFlyKey { get; private set; }
    internal static ConfigEntry<bool> EnableToastNotifications { get; private set; }
    internal static ConfigEntry<bool> EnableHostAnnouncements { get; private set; }
    internal static ConfigEntry<string> HostMotd { get; private set; }
    internal static ConfigEntry<bool> EnableWelcomeOverlay { get; private set; }
    internal static ConfigEntry<bool> EnableVivoxPositionRateGuard { get; private set; }
#if MODDERBRANCH_UAT
    internal static ConfigEntry<bool> EnableRosterAndNetworkGuards { get; private set; }
    internal static ConfigEntry<bool> EnableBetaUatObservationOnly { get; private set; }
#endif

    internal static ConfigEntry<bool> EnableChatTimestamps { get; private set; }
    internal static ConfigEntry<bool> EnableNotificationTimestamps { get; private set; }
    internal static ConfigEntry<bool> Use24HourTime { get; private set; }
    internal static ConfigEntry<int> TimestampSizePercent { get; private set; }
    internal static ConfigEntry<string> TimestampColor { get; private set; }
    internal static ConfigEntry<bool> AutoDisableChatTweaksWhenSimpleQoLInstalled { get; private set; }
    internal static ConfigEntry<bool> EnableChatReadability { get; private set; }
    internal static ConfigEntry<bool> EnablePersistentChatBackdrop { get; private set; }
    internal static ConfigEntry<int> ChatOutlineIntensity { get; private set; }
    internal static ConfigEntry<int> ChatUiScalePercent { get; private set; }
    internal static ConfigEntry<int> MenuUiScalePercent { get; private set; }
    internal static ConfigEntry<int> ChatFontSizePercent { get; private set; }
    internal static ConfigEntry<int> ChatWindowHeightPercent { get; private set; }
    internal static ConfigEntry<int> ChatHistoryRows { get; private set; }
    internal static ConfigEntry<float> LocalChatRangeMeters { get; private set; }
    internal static ConfigEntry<string> ChatOutlineColor { get; private set; }
    internal static ConfigEntry<string> BlackChatOutlineColor { get; private set; }
    internal static ConfigEntry<bool> BlackNamesOutline { get; private set; }
    internal static ConfigEntry<bool> EnableBetterMove { get; private set; }
    internal static ConfigEntry<bool> EnableUnlimitedConsumables { get; private set; }
    internal static ConfigEntry<bool> EnableNoclipFly { get; private set; }
    internal static ConfigEntry<int> FlyPrecisionPercent { get; private set; }
    internal static ConfigEntry<string> FlyRotateButton { get; private set; }
    internal static ConfigEntry<bool> EnableExtendedZoom { get; private set; }
    internal static ConfigEntry<bool> EnableFishingAid { get; private set; }
    internal static ConfigEntry<float> FishingAidReactionSeconds { get; private set; }
    internal static ConfigEntry<float> FishingAidClicksPerSecond { get; private set; }
    internal static ConfigEntry<float> FishingAidReleaseSeconds { get; private set; }
    internal static ConfigEntry<string> SelfSavedPosition { get; private set; }
    internal static ConfigEntry<float> SelfSpeedMultiplier { get; private set; }
    internal static ConfigEntry<float> SelfJumpMultiplier { get; private set; }
    internal static ConfigEntry<float> SelfGravityMultiplier { get; private set; }

    internal static ConfigEntry<string> StatusBaseName { get; private set; }
    internal static ConfigEntry<string> StatusMessage { get; private set; }
    internal static ConfigEntry<string> StatusColor { get; private set; }
    internal static ConfigEntry<string> StatusBrackets { get; private set; }
    internal static ConfigEntry<bool> EnableSpoons { get; private set; }
    internal static ConfigEntry<int> SpoonCount { get; private set; }
    internal static ConfigEntry<string> SpoonLabel { get; private set; }
    internal static ConfigEntry<string> SpoonsUpdatedUtc { get; private set; }
    internal static ConfigEntry<string> StatusBaseNameLegacyBackup { get; private set; }
    internal static ConfigEntry<string> StyleStateMigrationVersion { get; private set; }

    internal static ConfigEntry<bool> EnableLeaveNotifications { get; private set; }
    internal static ConfigEntry<bool> EnableWelcomeMessage { get; private set; }
    internal static ConfigEntry<float> ToastHoldSeconds { get; private set; }
    internal static ConfigEntry<int> ToastScalePercent { get; private set; }

    internal static ConfigEntry<bool> EnableReconnectGuard { get; private set; }
    internal static ConfigEntry<int> ReconnectRejoinHoldSeconds { get; private set; }
    internal static ConfigEntry<bool> EnableReconnectAnnouncement { get; private set; }

    internal static ConfigEntry<bool> EnableHostHealthMonitor { get; private set; }
    internal static ConfigEntry<bool> EnableCommunityBanListSync { get; private set; }
    internal static ConfigEntry<bool> EnableFocusAnywhere { get; private set; }
    internal static ConfigEntry<bool> EnableEnhancedPlayerPanel { get; private set; }
    internal static ConfigEntry<bool> EnableCloneShield { get; private set; }
    internal static ConfigEntry<bool> EnablePlayerRenderSaver { get; private set; }
    internal static ConfigEntry<bool> EnablePerformanceOverlay { get; private set; }
    internal static ConfigEntry<float> PerformanceOverlayRefreshSeconds { get; private set; }
    internal static ConfigEntry<int> PerformanceOverlayFontSize { get; private set; }
    internal static ConfigEntry<float> PerformanceOverlayOffsetX { get; private set; }
    internal static ConfigEntry<float> PerformanceOverlayOffsetY { get; private set; }
    internal static ConfigEntry<float> PerformanceOverlayWidth { get; private set; }
    internal static ConfigEntry<int> PlayerRenderSaverRadiusMeters { get; private set; }
    internal static ConfigEntry<int> PlayerRenderSaverMaxVisiblePlayers { get; private set; }
    internal static ConfigEntry<bool> EnableLocalAuditExports { get; private set; }
    internal static ConfigEntry<bool> EnablePingMentions { get; private set; }
    internal static ConfigEntry<bool> EnablePingSound { get; private set; }
    internal static ConfigEntry<string> PingHighlightColor { get; private set; }
    internal static ConfigEntry<string> PingMentionColor { get; private set; }
    internal static ConfigEntry<string> PingSoundMode { get; private set; }
    internal static ConfigEntry<string> LobbyPingDelegates { get; private set; }
    internal static ConfigEntry<string> LobbyPingDelegateOwnerSteamId { get; private set; }
    internal static ConfigEntry<string> LobbyModerationDelegates { get; private set; }
    internal static ConfigEntry<string> LobbyModerationDelegateOwnerSteamId { get; private set; }
    internal static ConfigEntry<string> LobbyHelpers { get; private set; }
    internal static ConfigEntry<string> LobbyHelperOwnerSteamId { get; private set; }
    internal static int LockedLobbyHelperCount => LobbyPingAuthorizationPolicy.ParseDelegates(LobbyHelpers?.Value).Length;
    internal static int LockedLobbyPingDelegateCount => LockedLobbyHelperCount;
    internal static int LockedLobbyModerationDelegateCount => LockedLobbyHelperCount;
    internal static ConfigEntry<bool> EnableChatCopy { get; private set; }
    internal static ConfigEntry<bool> EnableChatUrlLinks { get; private set; }
    internal static ConfigEntry<bool> EnableStyleUi { get; private set; }
    internal static ConfigEntry<bool> EnableExtendedAvatarStyles { get; private set; }
    internal static ConfigEntry<bool> OutfitSlotsHintShown { get; private set; }
    internal static ConfigEntry<bool> EnableChalkboardPersistence { get; private set; }
    internal static ConfigEntry<bool> EnableMiniMapPlayerLabels { get; private set; }
    internal static ConfigEntry<bool> EnableMiniMapFriendColors { get; private set; }
    internal static ConfigEntry<string> UiThemePreset { get; private set; }
    internal static ConfigEntry<int> MaxChatCharacters { get; private set; }
    internal static ConfigEntry<int> MaxProfileCharacters { get; private set; }
    internal static ConfigEntry<int> MaxSessionNameCharacters { get; private set; }
    internal static ConfigEntry<string> ConfigMigrationVersion { get; private set; }

    private static NativeAssetSweepOwnershipState? _nativeAssetSweepOwnership;
    internal static NativeAssetSweepOwnershipState NativeAssetSweepOwnership =>
        _nativeAssetSweepOwnership ??
        (_nativeAssetSweepOwnership = DetectNativeAssetSweepOwnership()).Value;
    internal static bool NativeAssetSweepDetected =>
        NativeAssetSweepOwnership != NativeAssetSweepOwnershipState.BlueSage;
    internal static bool NativeAssetSweepOwnershipVerified =>
        NativeAssetSweepOwnership == NativeAssetSweepOwnershipState.NativeGame;
    internal static bool AutoSweepAllowed =>
        Instance != null &&
        Instance._compatibility != null &&
        NativeAssetSweepPolicy.AllowBlueSageAutoSweep(
            NativeAssetSweepDetected,
            Instance._compatibility.AutoSweepAllowed,
            EnableAutoSweep?.Value == true);
    internal static bool ManualSweepAllowed =>
        Instance != null &&
        Instance._compatibility != null &&
        NativeAssetSweepPolicy.AllowBlueSageManualSweep(
            NativeAssetSweepDetected,
            Instance._compatibility.ManualSweepAllowed);
    internal static bool HostCleanupLaneHealthy =>
        NativeAssetSweepOwnershipVerified || AutoSweepAllowed;
    internal static bool HostReconnectLaneHealthy =>
        Instance != null &&
        EnableReconnectGuard?.Value == true &&
        !Instance._managedReconnectActive &&
        !Instance._cleanMenuReconnectActive;
    internal static bool ChatTweaksAllowed => Instance != null && Instance._compatibility.ChatTweaksAllowed;
    internal static bool HostHealthRepairsAllowed => Instance != null && Instance._compatibility != null && Instance._compatibility.HostHealthRepairsAllowed;
    internal static bool ShouldApplyChatTimestamps => ChatTweaksAllowed && EnableChatTimestamps.Value;
    internal static bool ShouldApplyNotificationTimestamps => ChatTweaksAllowed && EnableNotificationTimestamps.Value;
    internal static bool ShouldApplyLeaveNotifications => ChatTweaksAllowed && EnableLeaveNotifications.Value;
    internal static bool ShouldApplyChatReadability => EnableChatReadability != null && EnableChatReadability.Value;
    internal static bool ShouldApplyPersistentChatBackdrop => EnablePersistentChatBackdrop != null && EnablePersistentChatBackdrop.Value;
    internal static bool ShouldApplyBlackNamesOutline => BlackNamesOutline != null && BlackNamesOutline.Value;
    internal static bool ShouldApplyBetterMove => EnableBetterMove != null && EnableBetterMove.Value;
    internal static bool ShouldApplyUnlimitedConsumables => EnableUnlimitedConsumables != null && EnableUnlimitedConsumables.Value;
    internal static bool ShouldApplyNoclipFly => EnableNoclipFly != null && EnableNoclipFly.Value;
    internal static bool ShouldApplySelfMovementTuning =>
        Math.Abs(LockedSelfSpeedMultiplier - SelfMovementPolicy.DefaultMultiplier) > 0.001f ||
        Math.Abs(LockedSelfJumpMultiplier - SelfMovementPolicy.DefaultMultiplier) > 0.001f ||
        Math.Abs(LockedSelfGravityMultiplier - SelfMovementPolicy.DefaultMultiplier) > 0.001f;
    internal static bool ShouldApplySpoons => EnableSpoons != null && EnableSpoons.Value;
    internal static bool ShouldApplyFocusAnywhere => EnableFocusAnywhere != null && EnableFocusAnywhere.Value;
    internal static bool ShouldApplyEnhancedPlayerPanel => EnableEnhancedPlayerPanel != null && EnableEnhancedPlayerPanel.Value;
    internal static bool ShouldApplyCloneShield => EnableCloneShield != null && EnableCloneShield.Value;
    internal static bool ShouldApplyPlayerRenderSaver => !PlayerRenderSaverPolicy.Retired && EnablePlayerRenderSaver != null && EnablePlayerRenderSaver.Value;
    internal static bool ShouldApplyChatCopy => EnableChatCopy != null && EnableChatCopy.Value && !IsPluginLoaded(PluginCompatibility.LegacyCopyChatGuid);
    internal static bool ShouldApplyChatUrlLinks => EnableChatUrlLinks != null && EnableChatUrlLinks.Value;
    internal static bool ShouldApplyMiniMapPlayerLabels => EnableMiniMapPlayerLabels != null && EnableMiniMapPlayerLabels.Value;
    internal static bool ShouldApplyMiniMapFriendColors => EnableMiniMapFriendColors != null && EnableMiniMapFriendColors.Value;

    private static NativeAssetSweepOwnershipState DetectNativeAssetSweepOwnership()
    {
        bool timerPresent = false;
        bool durationPresent = false;
        bool unloadCallPresent = false;

        try
        {
            Type managerType =
                AccessTools.TypeByName("MainSceneManager") ??
                AccessTools.TypeByName("AssetLoadingManager");
            Type settingsType = AccessTools.TypeByName("SettingsData");
            if (managerType == null || settingsType == null)
            {
                return NativeAssetSweepPolicy.ResolveOwnership(
                    timerPresent,
                    durationPresent,
                    unloadCallPresent,
                    inspectionFailed: true);
            }

            timerPresent = AccessTools.Field(managerType, "_sweepTimer") != null;
            durationPresent = AccessTools.Field(settingsType, "SweepDuration") != null;

            MethodInfo updateMethod = AccessTools.Method(managerType, "Update", Type.EmptyTypes);
            if (updateMethod == null)
            {
                return NativeAssetSweepPolicy.ResolveOwnership(
                    timerPresent,
                    durationPresent,
                    unloadCallPresent,
                    inspectionFailed: true);
            }

            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(updateMethod))
            {
                if (!(instruction.operand is MethodInfo calledMethod))
                {
                    continue;
                }

                if (string.Equals(calledMethod.DeclaringType?.FullName, "UnityEngine.Resources", StringComparison.Ordinal) &&
                    string.Equals(calledMethod.Name, "UnloadUnusedAssets", StringComparison.Ordinal) &&
                    calledMethod.GetParameters().Length == 0)
                {
                    unloadCallPresent = true;
                    break;
                }
            }

            return NativeAssetSweepPolicy.ResolveOwnership(
                timerPresent,
                durationPresent,
                unloadCallPresent,
                inspectionFailed: false);
        }
        catch
        {
            return NativeAssetSweepPolicy.ResolveOwnership(
                timerPresent,
                durationPresent,
                unloadCallPresent,
                inspectionFailed: true);
        }
    }

    public static bool IsPerformanceOverlayActive
    {
        get
        {
#if MODDERBRANCH_UAT
            bool observationOnly = EnableBetaUatObservationOnly?.Value == true;
#else
            const bool observationOnly = false;
#endif
            return PerformanceOverlayOwnershipPolicy.IsActive(
                pluginReady: Instance != null,
                pluginEnabled: Instance != null && Instance.enabled,
                observationOnly: observationOnly,
                controllerStarted: Instance?._performanceOverlayControllerObject != null,
                overlayEnabled: EnablePerformanceOverlay?.Value == true);
        }
    }

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        // Round 5 Stage S: our own warnings and errors are counted for the session summary line.
        Log.LogEvent += SessionSummaryController.OnLogEvent;
        // BepInEx writes the config file on first save, so no file now means a fresh install.
        _configFileExistedAtStartup = File.Exists(Config.ConfigFilePath);
#if MODDERBRANCH_UAT
        if (!ValidateModderbranchUatRuntimeFailClosed())
        {
            enabled = false;
            Log.LogError(
                "BlueSage QoL modderbranch UAT build refused an unknown game runtime. " +
                "No config migration, Harmony patch, controller, sweep, or lobby behavior was started.");
            return;
        }
#endif
        BindConfig();
#if MODDERBRANCH_UAT
        if (EnableBetaUatObservationOnly?.Value == true)
        {
            Log.LogInfo(
                $"{PluginName} {PluginVersion} loaded in beta UAT observation-only mode. " +
                "No config migration, Harmony patch, controller, poller, or lobby write was started.");
            return;
        }
#endif
        ApplyOneTimeConfigMigration();
        InitializeModSuiteFolder();
        ApplyStyleStateMigration();
        SyncNotificationTimestampsWithChatTimestamps("startup config normalization");
        RegisterBlueSageCommands();
        RefreshCompatibility("initial BepInEx loaded-plugin scan");
        WorkRelaxRuntimeController.TryInitialize();
        if (NativeAssetSweepOwnershipVerified)
        {
            Log.LogInfo("The native game cleanup timer is present and remains untouched. BlueSage Automatic Cleanup is a separate user-controlled loop; explicit manual cleanup remains available when compatibility allows it.");
        }
        else if (NativeAssetSweepDetected)
        {
            Log.LogWarning("Native asset-cleanup inspection is incomplete. BlueSage will not alter native behavior; its separate automatic loop remains user-controlled and explicit manual cleanup remains available when compatibility allows it.");
        }
        ApplyHarmonyPatchesFailOpen();
        StartRescueReceiverCapabilityPublisher();
        RestartSweepLoop();
        RestartHostHealthLoop();
        StartCommunityBanListMaintenance();
        StartMentionPingController();
        StartCommandTypeaheadController();
        StartChatReadabilityController();
        StartSessionAuditController();
        StartSessionRunMarkerController();
        StartOptionalLocalBridges();
        StartCloneShieldController();
        StartPlayerRenderSaverController();
        StartPerformanceOverlayController();
        StartStyleHelperWindow();
        _compatibilityRefreshCoroutine = StartCoroutine(RefreshCompatibilityAfterLoadSettles());
        StartWelcomeMessageLoop();
        Log.LogInfo($"{PluginName} {PluginVersion} loaded. Build={PluginBuildIdentity}, NativeAssetSweep={NativeAssetSweepDetected}, AutoSweep={AutoSweepAllowed}, ChatTweaksAllowed={ChatTweaksAllowed}.");
    }

    // Round 5d: one BlueSageQoLModSuite folder. Runs before any tracker, journal or audit writer starts.
    internal const string FolderTidiedNotice = "BlueSage tidied its files into one folder: BepInEx/BlueSageQoLModSuite.";
    internal const string FolderTidyRetryNotice = "BlueSage couldn't tidy its files this time. Nothing was changed; it will try again next time you start the game.";

    private void InitializeModSuiteFolder()
    {
        try
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            ModSuiteMigrationResult result = ModSuitePaths.InitializeAtStartup(message => Log.LogWarning(message));
            Log.LogInfo("[BlueSageFiles] " + result.Summary + "; folder=BepInEx/" + ModSuiteLayoutPolicy.RootFolderName + (result.LayoutReady ? string.Empty : " (old places in use this session)") + "; ms=" + timer.ElapsedMilliseconds + ".");
            string notice = result.MovedOldFiles ? FolderTidiedNotice : result.LayoutReady ? null : FolderTidyRetryNotice;
            if (notice != null)
            {
                StartCoroutine(ShowToastAfterLoad(notice));
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning("[BlueSageFiles] folder check skipped safely: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // A toast queued during loading would fade before the player sees the menu.
    private IEnumerator ShowToastAfterLoad(string notice)
    {
        yield return new WaitForSecondsRealtime(20f);
        ShowToast(notice);
    }

#if MODDERBRANCH_UAT
    private bool ValidateModderbranchUatRuntimeFailClosed()
    {
        const string expectedBuildId = "24414155";
        const string expectedAssemblyHash = "29D381FB8CD1C9048EB8340CB2E97967602E15B8165D5BF10266508F0F6A42C9";
        string actualBuildId = ReadSteamBuildIdForUatGate();
        string assemblyPath = Path.Combine(
            Paths.GameRootPath,
            "OnTogether_Data",
            "Managed",
            "Assembly-CSharp.dll");
        string actualAssemblyHash = ReadSha256ForUatGate(assemblyPath);
        bool accepted =
            string.Equals(actualBuildId, expectedBuildId, StringComparison.Ordinal) &&
            string.Equals(actualAssemblyHash, expectedAssemblyHash, StringComparison.OrdinalIgnoreCase);
        if (!accepted)
        {
            Log.LogError(
                $"Modderbranch UAT runtime mismatch: build={actualBuildId}, " +
                $"Assembly-CSharp={actualAssemblyHash}. Expected build={expectedBuildId}, " +
                $"Assembly-CSharp={expectedAssemblyHash}.");
        }
        return accepted;
    }

    private static string ReadSteamBuildIdForUatGate()
    {
        try
        {
            DirectoryInfo directory = new DirectoryInfo(Paths.GameRootPath);
            for (int depth = 0; directory != null && depth < 6; depth++, directory = directory.Parent)
            {
                string manifest = Path.Combine(directory.FullName, "appmanifest_3707400.acf");
                if (!File.Exists(manifest))
                {
                    continue;
                }

                Match match = Regex.Match(
                    File.ReadAllText(manifest),
                    "\\\"buildid\\\"\\s+\\\"(?<id>\\d+)\\\"");
                return match.Success ? match.Groups["id"].Value : "?";
            }
        }
        catch
        {
        }
        return "?";
    }

    private static string ReadSha256ForUatGate(string path)
    {
        try
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
        catch
        {
            return "?";
        }
    }
#endif

    private void ApplyHarmonyPatchesFailOpen()
    {
        foreach (Type patchType in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
        {
            bool hasHarmonyPatch = patchType.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0;
            if (!hasHarmonyPatch)
            {
                foreach (System.Reflection.MethodInfo method in patchType.GetMethods(AccessTools.all))
                {
                    if (method.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                    {
                        hasHarmonyPatch = true;
                        break;
                    }
                }
            }

            if (hasHarmonyPatch)
            {
                ApplyHarmonyPatchTypeFailOpen(patchType);
            }
        }
    }

    private void ApplyHarmonyPatchTypeFailOpen(Type patchType)
    {
        try
        {
            _harmony.CreateClassProcessor(patchType).Patch();
        }
        catch (Exception ex)
        {
            Log.LogError(
                $"Harmony patch type '{patchType.FullName}' skipped after compatibility failure: {ex.GetType().Name}: {ex.Message}. " +
                "Continuing with compatible controllers; Home, Insert, sweep, and menu startup remain available.");
        }
    }

    private void Update()
    {
#if MODDERBRANCH_UAT
        if (EnableBetaUatObservationOnly?.Value == true)
        {
            return;
        }
#endif
        if (PluginShutdownController.IsShuttingDown)
        {
            return;
        }
        SteamRuntimeFacade.CaptureLive();

        bool textInputFocused = IsAnyTextInputFocused();
        if (_pendingRebindEntry != null)
        {
            float now = Time.unscaledTime;
            _pendingRebindActiveSince = HotkeyInputGatePolicy.AdvanceCaptureClock(_pendingRebindActiveSince, textInputFocused, now);
            if (HotkeyInputGatePolicy.IsRebindCaptureExpired(_pendingRebindActiveSince, _pendingRebindRequestedAt, now))
            {
                string expiredLabel = _pendingRebindLabel;
                ClearPendingRebind();
                AddLocalNotification($"Rebind timed out for {expiredLabel}; nothing changed. Try again from the Hotkeys menu or /rebind.");
                ShowToast($"Rebind timed out for {expiredLabel}.");
                Log?.LogInfo($"Hotkey rebind timed out for {expiredLabel}.");
            }
            else if (!HotkeyInputGatePolicy.ShouldEvaluateRebindCapture(textInputFocused))
            {
                // Keys typed into chat or another text box are not rebind candidates.
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                string cancelledLabel = _pendingRebindLabel;
                ClearPendingRebind();
                AddLocalNotification($"Rebind cancelled for {cancelledLabel}.");
                ShowToast($"Rebind cancelled for {cancelledLabel}.");
                Log?.LogInfo($"Hotkey rebind cancelled for {cancelledLabel}.");
            }
            else if (Input.anyKeyDown && Time.frameCount > _pendingRebindStartFrame)
            {
                foreach (KeyCode candidate in CapturableKeyCodes)
                {
                    if (candidate == KeyCode.Escape)
                    {
                        continue;
                    }

                    if (Input.GetKeyDown(candidate))
                    {
                        string candidateName = candidate.ToString();
                        IEnumerable<string> otherKeys = GetOtherHotkeyNames(_pendingRebindTarget);
                        if (!HotkeyRebindPolicy.TryValidateCandidate(candidateName, otherKeys, out string rejectionReason))
                        {
                            if (HotkeyInputGatePolicy.ShouldAnnounceRejection(candidateName, _lastAnnouncedRebindRejection))
                            {
                                _lastAnnouncedRebindRejection = candidateName;
                                AddLocalNotification($"Rebind: {rejectionReason}");
                                ShowToast($"Rebind: {rejectionReason}");
                            }

                            Log?.LogInfo($"Hotkey rebind rejected for {_pendingRebindLabel}: {rejectionReason}");
                        }
                        else
                        {
                            _pendingRebindEntry.Value = candidate;
                            Config.Save();
                            AddLocalNotification($"{_pendingRebindLabel} is now bound to {candidate}.");
                            ShowToast($"{_pendingRebindLabel} is now bound to {candidate}.");
                            Log?.LogInfo($"Hotkey {_pendingRebindLabel} rebound to {candidate} via in-game capture.");
                            ClearPendingRebind();
                        }

                        break;
                    }
                }
            }
        }
        else if (HotkeyInputGatePolicy.ShouldDispatchHotkeys(textInputFocused))
        {
            if (Input.GetKeyDown(QolMenuKey.Value))
            {
                ToggleQolMenuFromHotkey();
            }

            if (Input.GetKeyDown(StyleMenuKey.Value))
            {
                ToggleStyleUiFromHotkey();
            }

            if (Input.GetKeyDown(NoclipFlyKey.Value))
            {
                ToggleNoclipFlyFromHotkey();
            }

            if (Input.GetKeyDown(ManualSweepKey.Value))
            {
                RunSweep("manual hotkey", requireManualPermission: true);
            }
        }

        LobbyAnnouncementController.Poll(Time.unscaledTime);
        if (PlayerIdentityEvidenceController.ConsumeSteamIdMismatchAlert())
        {
            AddLocalNotificationWithToast(SteamIdMismatchAlertText);
        }

        if (Time.unscaledTime >= _nextModerationCapabilityPublishAt)
        {
            _nextModerationCapabilityPublishAt = Time.unscaledTime + 5f;
            SteamIdModerationController.PublishHostCapability();
            SteamIdModerationController.RefreshLobbyDataIfNotMember();
            bool hasLobbySafetyAccess = CanUseLobbySafety(out _);
            bool accessChanged = _hadLobbySafetyAccess != hasLobbySafetyAccess;
            if (_hadLobbySafetyAccess && !hasLobbySafetyAccess)
            {
                _pendingModerationAction = null;
            }
            _hadLobbySafetyAccess = hasLobbySafetyAccess;
            if (accessChanged)
            {
                PlayerIdentityEvidenceController.InvalidateRosterCache();
                Patches.PlayerIdentityRoleHighlightController.RefreshAll();
                // Round 5h: being made (or no longer being) a Helper is worth a quiet note, not silence.
                if (hasLobbySafetyAccess &&
                    SteamIdModerationController.GetLocalAccessState(out _, out _) == LobbySafetyAccessState.HelperReady)
                {
                    // No "off" toast: leaving a lobby also lands here, and a note at the main menu would be noise.
                    ShowToast("Helper tools are on for you in this lobby.");
                }
            }
        }

        SteamIdModerationController.PollHostMemberDataRequests();
        SteamIdModerationController.TickPendingKicks();
        CloneIncidentActionController.PollHostMemberDataRequests();
        ChalkboardPersistenceController.PollHostMemberDataRequests();
        IdentityResyncController.Tick();
        // Round 4 Stage G: Work / Relax initializes from here (never from a game hook, so the one-time ledger load
        // lands on an ordinary frame) and writes on a later frame than the one that changed it, off-thread.
        WorkRelaxRuntimeController.TryInitializeIfDue(DateTime.UtcNow.Ticks);
        WorkRelaxRuntimeController.FlushIfDue(DateTime.UtcNow.Ticks);
        // Round 5 Stage S: one summary line every 6 hours (cheap when not due).
        SessionSummaryController.Tick(DateTime.UtcNow.Ticks);
    }

    private void ToggleQolMenuFromHotkey()
    {
        ToggleQolMenuWindow($"{QolMenuKey.Value} key");
    }

    // One path for the hotkey and the overlay's QoL-Menu button (review 2026-10-18), so a fix to one
    // can't miss the other.
    private void ToggleQolMenuWindow(string via)
    {
        if (_qolMenuWindow == null)
        {
            StartQolMenuWindow();
        }

        if (_qolMenuWindow == null)
        {
            Log?.LogWarning($"QoL menu requested via {via}, but the QoL menu window could not start.");
            return;
        }

        _qolMenuWindow.ToggleVisible();
        // Voice pass (2026-10-23): no toast and no chat line; the menu itself appearing or closing is the answer.
        Log?.LogInfo($"QoL menu toggled via {via}.");
    }

    private void ToggleStyleUiFromHotkey()
    {
        string result = ToggleStyleUiFromQolMenu();
        ShowToast(result);
        Log?.LogInfo($"Style Helper toggled via {StyleMenuKey.Value} key. " + result);
    }

    private void ToggleNoclipFlyFromHotkey()
    {
        string result = ToggleNoclipFlyFromQolMenu();
        AddLocalNotification(result);
        ShowToast(result);
        Log?.LogInfo($"Self noclip/fly toggled via {NoclipFlyKey.Value} key. " + result);
    }

    // Round 3b (Blue, 2026-10-18): the Performance Overlay's QoL-Menu and Styler-Menu buttons open the
    // same windows as the hotkeys, with the same toast, for players who prefer clicking.
    internal void ToggleQolMenuFromOverlayButton()
    {
        ToggleQolMenuWindow("the overlay's QoL Menu button");
    }

    internal void ToggleStyleUiFromOverlayButton()
    {
        string result = ToggleStyleUiFromQolMenu();
        ShowToast(result);
        Log?.LogInfo("Style Helper toggled via the overlay's Style Helper button. " + result);
    }

    internal void ShowToast(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || EnableToastNotifications == null || !EnableToastNotifications.Value)
        {
            return;
        }

        ShowToastUnchecked(text, pinned: false);
    }

    // Host announcements have their own toggle, so they bypass the general Toast Pop-ups switch.
    private void ShowToastUnchecked(string text, bool pinned)
    {
        if (string.IsNullOrWhiteSpace(text) || ToastPolicy.IsDuplicateOfVisible(text, _toasts, Time.unscaledTime, LockedToastHoldSeconds))
        {
            return;
        }

        _toasts.Add(new ToastEntry(text, Time.unscaledTime, pinned));
        while (_toasts.Count > ToastPolicy.MaxVisible)
        {
            _toasts.RemoveAt(ToastPolicy.IndexToEvict(_toasts));
        }
    }

    private void OnGUI()
    {
#if MODDERBRANCH_UAT
        if (EnableBetaUatObservationOnly?.Value == true)
        {
            return;
        }
#endif
        if (PluginShutdownController.IsShuttingDown)
        {
            return;
        }

        // Toasts and the welcome overlay are GUI.Window calls raised to the front every frame:
        // Unity always draws windows above plain GUI.Box calls, and the QoL menu is itself a window
        // (47058), so boxes drew behind it no matter what GUI.depth said. GUI.depth stays low for
        // this whole OnGUI (restoring it before return would discard it) so these windows also sort
        // above other scripts' windows.
        GUI.depth = -200;
        DrawToasts();
        DrawWelcomeOverlay();
        DrawGuideOverlay();
    }

    private void DrawToasts()
    {
        if (_toasts.Count == 0)
        {
            return;
        }

        float now = Time.unscaledTime;
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            if (ToastPolicy.IsExpired(now - _toasts[i].ShownAt, LockedToastHoldSeconds))
            {
                _toasts.RemoveAt(i);
            }
        }

        if (_toasts.Count == 0)
        {
            return;
        }

        // Rebuilt when the toast size changes (Auto follows the resolution; Customize can override).
        float scale = ToastScale;
        int fontSize = Mathf.RoundToInt(14f * scale);
        if (_toastStyle == null || _toastStyle.fontSize != fontSize)
        {
            _toastStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = fontSize,
                padding = new RectOffset(Mathf.RoundToInt(10f * scale), Mathf.RoundToInt(10f * scale), Mathf.RoundToInt(8f * scale), Mathf.RoundToInt(8f * scale)),
                normal = { textColor = Color.white }
            };
        }

        float totalHeight = 0f;
        for (int i = 0; i < _toasts.Count; i++)
        {
            totalHeight += Mathf.Max(ToastMinHeight * ToastScale, _toastStyle.CalcHeight(new GUIContent(_toasts[i].Text), ToastWidth)) + ToastSpacing;
        }

        // Round 5g: the window is created on every IMGUI event. Round 5d created it on Repaint only (for click-through)
        // and nothing was ever painted in the game: Unity builds its window list in the Layout pass.
        var stackRect = new Rect((Screen.width - ToastWidth) / 2f, 24f, ToastWidth, totalHeight);
        GUI.Window(ToastWindowId, stackRect, _toastWindowFunction ??= DrawToastWindowContents, GUIContent.none, GUIStyle.none);
        GUI.BringWindowToFront(ToastWindowId);
    }

    // Window callbacks run after DrawToasts returns, so each toast's fade is computed here.
    private void DrawToastWindowContents(int windowId)
    {
        float now = Time.unscaledTime;
        float y = 0f;
        Color previousColor = GUI.color;
        for (int i = 0; i < _toasts.Count; i++)
        {
            float alpha = ToastPolicy.ComputeAlpha(now - _toasts[i].ShownAt, LockedToastHoldSeconds);
            float height = Mathf.Max(ToastMinHeight * ToastScale, _toastStyle.CalcHeight(new GUIContent(_toasts[i].Text), ToastWidth));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(new Rect(0f, y, ToastWidth, height), _toasts[i].Text, _toastStyle);
            y += height + ToastSpacing;
        }

        GUI.color = previousColor;
    }

    private void DrawWelcomeOverlay()
    {
        if (!_welcomeOverlayVisible)
        {
            return;
        }

        // Escape is read here, in the plain OnGUI pass, because keyboard events only reach a window
        // callback when that window has keyboard focus. Enter is deliberately NOT a dismiss key: it
        // opens chat, and the player would lose the pop-up for this visit without reading it.
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Event.current.Use();
            DismissWelcomeOverlay();
            return;
        }

        if (_welcomeOverlayBoxStyle == null)
        {
            _welcomeOverlayBoxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 16, 16) };
            // Window styles draw onNormal while focused; the box style has no onNormal background,
            // so without this a focused overlay would turn transparent.
            _welcomeOverlayBoxStyle.onNormal.background = _welcomeOverlayBoxStyle.normal.background;
            _welcomeOverlayBodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
            _welcomeOverlayTitleStyle = new GUIStyle(_welcomeOverlayBodyStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        const float width = 440f;
        const float innerWidth = width - 32f; // minus window padding (16 left + 16 right)

        _welcomeOverlayBody = WelcomeOverlayPolicy.BuildBody(
            PluginVersion, QolMenuKey.Value.ToString(), StyleMenuKey.Value.ToString(),
            NoclipFlyKey.Value.ToString(), ManualSweepKey.Value.ToString());
        _welcomeOverlayBodyHeight = _welcomeOverlayBodyStyle.CalcHeight(new GUIContent(_welcomeOverlayBody), innerWidth);
        float height = WelcomeOverlayPolicy.WindowHeight(
            _welcomeOverlayBoxStyle.padding.vertical,
            WelcomeOverlayTitleHeight,
            _welcomeOverlayBodyHeight,
            WelcomeOverlayButtonCount,
            WelcomeOverlayButtonHeight,
            _welcomeOverlayBodyStyle.margin.vertical,
            GUI.skin.button.margin.vertical);

        var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        BlueSageWindowHoverScope.RegisterWindow(WelcomeOverlayWindowId, rect, 30);
        GUI.Window(WelcomeOverlayWindowId, rect, _welcomeOverlayWindowFunction ??= DrawWelcomeOverlayWindowContents, string.Empty, _welcomeOverlayBoxStyle);
        GUI.BringWindowToFront(WelcomeOverlayWindowId);
    }

    private void DrawWelcomeOverlayWindowContents(int windowId)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("Welcome to BlueSage QoL", _welcomeOverlayTitleStyle, GUILayout.Height(WelcomeOverlayTitleHeight));
        GUILayout.Space(6f);
        GUILayout.Label(_welcomeOverlayBody, _welcomeOverlayBodyStyle, GUILayout.Height(_welcomeOverlayBodyHeight));
        GUILayout.Space(10f);
        if (GUILayout.Button("Open the full guide", GUILayout.Height(WelcomeOverlayButtonHeight)))
        {
            DismissWelcomeOverlay();
            ShowGuideOverlayNow();
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("Read the full README on Thunderstore", GUILayout.Height(WelcomeOverlayButtonHeight)))
        {
            Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl);
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("Got it", GUILayout.Height(WelcomeOverlayButtonHeight)))
        {
            DismissWelcomeOverlay();
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("Don't show this again", GUILayout.Height(WelcomeOverlayButtonHeight)))
        {
            TurnOffWelcomeOverlay();
        }

        GUILayout.EndVertical();
    }

    // Round 3b (Blue, 2026-10-18): the opt-out for the every-join pop-up. It flips the same Welcome
    // Overlay toggle the menu shows, so Toggles or /welcome bring it back.
    internal void TurnOffWelcomeOverlay()
    {
        DismissWelcomeOverlay();
        if (EnableWelcomeOverlay != null)
        {
            EnableWelcomeOverlay.Value = false;
            Config.Save();
        }

        AddLocalNotificationWithToast("Welcome Overlay off. Turn it back on in Toggles, or type /welcome any time.");
    }

    // "Got it" and Escape close the pop-up for this visit only; nothing is saved (it shows on every join).
    internal void DismissWelcomeOverlay()
    {
        _welcomeOverlayVisible = false;
    }

    internal string ShowWelcomeOverlayNow()
    {
        _welcomeOverlayVisible = true;
        return "Welcome overlay opened.";
    }

    // /bsqol guide pop-up (UAT F3FAB760BEB5, 2026-10-17): the welcome overlay's look, scrollable,
    // with the whole stack in cozy words from GuideOverlayPolicy. Esc or Close dismisses it.
    internal string ShowGuideOverlayNow()
    {
        _guideOverlayVisible = true;
        _guideOverlayScroll = Vector2.zero;
        return "BlueSage guide opened. Close it with Esc or the Close button.";
    }

    private void DrawGuideOverlay()
    {
        if (!_guideOverlayVisible)
        {
            return;
        }

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            Event.current.Use();
            _guideOverlayVisible = false;
            return;
        }

        if (_welcomeOverlayBoxStyle == null)
        {
            _welcomeOverlayBoxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 16, 16) };
            _welcomeOverlayBoxStyle.onNormal.background = _welcomeOverlayBoxStyle.normal.background;
            _welcomeOverlayBodyStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };
            _welcomeOverlayTitleStyle = new GUIStyle(_welcomeOverlayBodyStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        if (_guideOverlayHeadingStyle == null)
        {
            _guideOverlayHeadingStyle = new GUIStyle(_welcomeOverlayBodyStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
        }

        float width = Mathf.Min(520f, Screen.width - 32f);
        float height = Mathf.Min(620f, Screen.height - 48f);
        var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        BlueSageWindowHoverScope.RegisterWindow(GuideOverlayWindowId, rect, 30);
        GUI.Window(GuideOverlayWindowId, rect, _guideOverlayWindowFunction ??= DrawGuideOverlayWindowContents, string.Empty, _welcomeOverlayBoxStyle);
        GUI.BringWindowToFront(GuideOverlayWindowId);
    }

    private void DrawGuideOverlayWindowContents(int windowId)
    {
        GUILayout.BeginVertical();
        GUILayout.Label("BlueSage QoL guide", _welcomeOverlayTitleStyle, GUILayout.Height(WelcomeOverlayTitleHeight));
        GUILayout.Space(6f);
        _guideOverlayScroll = GUILayout.BeginScrollView(_guideOverlayScroll, GUILayout.ExpandHeight(true));
        string sectionsKey = QolMenuKey.Value + "|" + StyleMenuKey.Value + "|" + NoclipFlyKey.Value + "|" + ManualSweepKey.Value;
        if (_guideSections == null || !string.Equals(_guideSectionsKey, sectionsKey, StringComparison.Ordinal))
        {
            _guideSectionsKey = sectionsKey;
            _guideSections = GuideOverlayPolicy.BuildSections(
                PluginVersion, QolMenuKey.Value.ToString(), StyleMenuKey.Value.ToString(),
                NoclipFlyKey.Value.ToString(), ManualSweepKey.Value.ToString());
        }

        foreach (GuideSection section in _guideSections)
        {
            GUILayout.Label(section.Heading, _guideOverlayHeadingStyle);
            GUILayout.Label(section.Body, _welcomeOverlayBodyStyle);
            GUILayout.Space(8f);
        }
        GUILayout.EndScrollView();
        GUILayout.Space(8f);
        if (GUILayout.Button("Read the full README on Thunderstore", GUILayout.Height(WelcomeOverlayButtonHeight)))
        {
            Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl);
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("Close", GUILayout.Height(WelcomeOverlayButtonHeight)))
        {
            _guideOverlayVisible = false;
        }

        GUILayout.EndVertical();
    }

    private BlueSageCommandResult HandleMotdCommand(BlueSageCommandContext context)
    {
        string text = string.Join(" ", context.Tokens).Trim();
        if (text.Length == 0)
        {
            string current = LobbyMotdPolicy.Effective(HostMotd?.Value);
            bool isDefault = LobbyAnnouncementPolicy.Sanitize(HostMotd?.Value).Length == 0;
            return BlueSageCommandResult.HandledWithMessages(
                "Welcome message: '" + current + "'" + (isDefault ? " (default)" : string.Empty) + ". Change it with /motd <text>" + (isDefault ? "." : ", or /motd clear for the default."));
        }

        HostMotd.Value = string.Equals(text, "clear", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : LobbyAnnouncementPolicy.Sanitize(text);
        Config.Save();
        SteamIdModerationController.PublishHostCapability();
        string effective = LobbyMotdPolicy.Effective(HostMotd.Value);
        ShowToast("Welcome message saved: '" + effective + "'.");
        return BlueSageCommandResult.HandledWithMessages("Welcome message saved: '" + effective + "'. QoL players see it when they join your lobby.");
    }

    private BlueSageCommandResult HandleWelcomeOverlayCommand(BlueSageCommandContext context)
    {
        return BlueSageCommandResult.HandledWithMessages(ShowWelcomeOverlayNow());
    }

    internal bool IsAwaitingHotkeyRebind => _pendingRebindEntry != null;

    internal string PendingRebindLabel => _pendingRebindLabel;

    internal string BeginRebindHotkey(HotkeyBindTarget target)
    {
        ConfigEntry<KeyCode> entry = ResolveHotkeyEntry(target);
        string label = ResolveHotkeyLabel(target);
        if (entry == null)
        {
            return "Rebind: unknown hotkey target.";
        }

        _pendingRebindEntry = entry;
        _pendingRebindLabel = label;
        _pendingRebindTarget = target;
        _pendingRebindStartFrame = Time.frameCount;
        _pendingRebindRequestedAt = Time.unscaledTime;
        _pendingRebindActiveSince = null;
        _lastAnnouncedRebindRejection = null;
        return $"Press any key to bind {label} (Esc cancels; times out after {HotkeyInputGatePolicy.RebindCaptureTimeoutSeconds:0} seconds).";
    }

    private void ClearPendingRebind()
    {
        _pendingRebindEntry = null;
        _pendingRebindLabel = null;
        _pendingRebindActiveSince = null;
        _lastAnnouncedRebindRejection = null;
    }

    // True while any text box owns the keyboard: a BlueSage IMGUI field (reported by the window
    // drawing it), the game's chat box, or any uGUI/TMP field the game's EventSystem has selected
    // (lobby codes, names in game menus). Hotkeys and rebind capture stand down while it is true.
    private static bool IsAnyTextInputFocused()
    {
        if (ImGuiTextFocusTracker.IsEditing)
        {
            return true;
        }

        try
        {
            TMPro.TMP_InputField chat = MonoSingleton<UIManager>.I?.MessageInput;
            if (chat != null && chat.isFocused)
            {
                return true;
            }

            GameObject selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (selected == null)
            {
                return false;
            }

            TMPro.TMP_InputField tmpField = selected.GetComponent<TMPro.TMP_InputField>();
            if (tmpField != null && tmpField.isFocused)
            {
                return true;
            }

            UnityEngine.UI.InputField legacyField = selected.GetComponent<UnityEngine.UI.InputField>();
            return legacyField != null && legacyField.isFocused;
        }
        catch
        {
            return false;
        }
    }

    internal string CancelRebindHotkey()
    {
        if (_pendingRebindEntry == null)
        {
            return "No rebind in progress.";
        }

        string label = _pendingRebindLabel;
        ClearPendingRebind();
        return $"Cancelled rebinding {label}.";
    }

    private static ConfigEntry<KeyCode> ResolveHotkeyEntry(HotkeyBindTarget target)
    {
        switch (target)
        {
            case HotkeyBindTarget.QolMenu:
                return QolMenuKey;
            case HotkeyBindTarget.StyleMenu:
                return StyleMenuKey;
            case HotkeyBindTarget.NoclipFly:
                return NoclipFlyKey;
            case HotkeyBindTarget.ManualSweep:
                return ManualSweepKey;
            default:
                return null;
        }
    }

    private static string ResolveHotkeyLabel(HotkeyBindTarget target)
    {
        switch (target)
        {
            case HotkeyBindTarget.QolMenu:
                return "QoL Menu";
            case HotkeyBindTarget.StyleMenu:
                return "Style Helper";
            case HotkeyBindTarget.NoclipFly:
                return "Noclip/Fly";
            case HotkeyBindTarget.ManualSweep:
                return "Run Cleanup";
            default:
                return "Unknown";
        }
    }

    private static IEnumerable<string> GetOtherHotkeyNames(HotkeyBindTarget excluding)
    {
        foreach (HotkeyBindTarget candidate in (HotkeyBindTarget[])Enum.GetValues(typeof(HotkeyBindTarget)))
        {
            if (candidate == excluding)
            {
                continue;
            }

            ConfigEntry<KeyCode> entry = ResolveHotkeyEntry(candidate);
            if (entry != null)
            {
                yield return entry.Value.ToString();
            }
        }
    }

    private static bool TryParseHotkeyTargetToken(string token, out HotkeyBindTarget target)
    {
        switch (token)
        {
            case "qolmenu":
            case "menu":
                target = HotkeyBindTarget.QolMenu;
                return true;
            case "stylemenu":
            case "style":
                target = HotkeyBindTarget.StyleMenu;
                return true;
            case "fly":
            case "noclip":
                target = HotkeyBindTarget.NoclipFly;
                return true;
            case "sweep":
                target = HotkeyBindTarget.ManualSweep;
                return true;
            default:
                target = default;
                return false;
        }
    }

    private static string BuildHotkeyStatusMessage()
    {
        return "Your keys: QoL menu " + QolMenuKey.Value +
               ", Style Helper " + StyleMenuKey.Value +
               ", Fly " + NoclipFlyKey.Value +
               ", Cleanup " + ManualSweepKey.Value +
               ". Change one with /rebind <name> (names: qolmenu, stylemenu, fly, sweep for Cleanup).";
    }

    private BlueSageCommandResult HandleRebindCommand(BlueSageCommandContext context)
    {
        if (context.Tokens.Count == 0)
        {
            return BlueSageCommandResult.HandledWithMessages(BuildHotkeyStatusMessage());
        }

        string targetToken = context.Tokens[0].Trim().ToLowerInvariant();
        if (string.Equals(targetToken, "reset", StringComparison.OrdinalIgnoreCase))
        {
            return BlueSageCommandResult.HandledWithMessages(ResetAllHotkeysToDefaults());
        }

        if (!TryParseHotkeyTargetToken(targetToken, out HotkeyBindTarget target))
        {
            return BlueSageCommandResult.HandledWithMessages("Rebind: use qolmenu, stylemenu, fly, sweep (Cleanup), or reset.");
        }

        if (context.Tokens.Count == 1)
        {
            return BlueSageCommandResult.HandledWithMessages(BeginRebindHotkey(target));
        }

        string keyToken = context.Tokens[1].Trim();
        if (string.Equals(keyToken, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return BlueSageCommandResult.HandledWithMessages(CancelRebindHotkey());
        }

        if (string.Equals(keyToken, "reset", StringComparison.OrdinalIgnoreCase))
        {
            return BlueSageCommandResult.HandledWithMessages(ResetHotkeyToDefault(target));
        }

        if (!Enum.TryParse(keyToken, ignoreCase: true, out KeyCode parsedKey) || !CapturableKeyCodes.Contains(parsedKey))
        {
            return BlueSageCommandResult.HandledWithMessages($"Rebind: \"{keyToken}\" isn't a recognized keyboard key name.");
        }

        string label = ResolveHotkeyLabel(target);
        if (!HotkeyRebindPolicy.TryValidateCandidate(parsedKey.ToString(), GetOtherHotkeyNames(target), out string rejectionReason))
        {
            return BlueSageCommandResult.HandledWithMessages($"Rebind: {rejectionReason}");
        }

        ConfigEntry<KeyCode> entry = ResolveHotkeyEntry(target);
        entry.Value = parsedKey;
        Config.Save();
        return BlueSageCommandResult.HandledWithMessages($"{label} is now bound to {parsedKey}.");
    }

    private static KeyCode ResolveHotkeyDefault(HotkeyBindTarget target)
    {
        switch (target)
        {
            case HotkeyBindTarget.QolMenu:
                return KeyCode.Home;
            case HotkeyBindTarget.StyleMenu:
                return KeyCode.Insert;
            case HotkeyBindTarget.NoclipFly:
                return KeyCode.End;
            case HotkeyBindTarget.ManualSweep:
                return KeyCode.F8;
            default:
                return KeyCode.None;
        }
    }

    // The one loop over hotkey targets; reset checks and startup repair both build on it.
    private static IEnumerable<KeyValuePair<HotkeyBindTarget, HotkeyBinding>> EnumerateHotkeyBindings(HotkeyBindTarget? excluding = null)
    {
        foreach (HotkeyBindTarget target in (HotkeyBindTarget[])Enum.GetValues(typeof(HotkeyBindTarget)))
        {
            ConfigEntry<KeyCode> entry = ResolveHotkeyEntry(target);
            if (target == excluding || entry == null)
            {
                continue;
            }

            yield return new KeyValuePair<HotkeyBindTarget, HotkeyBinding>(
                target,
                new HotkeyBinding(ResolveHotkeyLabel(target), entry.Value.ToString(), ResolveHotkeyDefault(target).ToString()));
        }
    }

    private static HotkeyBinding[] BuildHotkeyBindings(HotkeyBindTarget? excluding = null)
    {
        return EnumerateHotkeyBindings(excluding).Select(pair => pair.Value).ToArray();
    }

    // Saved hotkeys never went through the rebind policy (older versions had no allowlist, and
    // Noclip/Fly arrived later defaulting to End), so they are validated once on load. Notices wait
    // for the first lobby because chat does not exist yet at load time.
    private void ApplyStartupHotkeyRepair()
    {
        List<KeyValuePair<HotkeyBindTarget, HotkeyBinding>> pairs = EnumerateHotkeyBindings().ToList();
        List<HotkeyBindTarget> targets = pairs.Select(pair => pair.Key).ToList();
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(pairs.Select(pair => pair.Value).ToList());
        if (!result.Changed)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            if (Enum.TryParse(result.FinalKeys[i], ignoreCase: true, out KeyCode repaired))
            {
                ResolveHotkeyEntry(targets[i]).Value = repaired;
            }
        }

        Config.Save();
        foreach (string message in result.Messages)
        {
            Log?.LogWarning("Hotkey repair: " + message);
            _pendingHotkeyRepairNotices.Add("BlueSage hotkeys: " + message);
        }
    }

    private void FlushPendingHotkeyRepairNotices()
    {
        while (_pendingHotkeyRepairNotices.Count > 0 && AddLocalNotification(_pendingHotkeyRepairNotices[0]))
        {
            _pendingHotkeyRepairNotices.RemoveAt(0);
        }
    }

    private string ResetHotkeyToDefault(HotkeyBindTarget target)
    {
        ConfigEntry<KeyCode> entry = ResolveHotkeyEntry(target);
        string label = ResolveHotkeyLabel(target);
        if (entry == null)
        {
            return "Rebind: unknown hotkey target.";
        }

        KeyCode defaultKey = ResolveHotkeyDefault(target);
        string holder = HotkeyConfigRepairPolicy.FindHolder(defaultKey.ToString(), BuildHotkeyBindings(excluding: target));
        if (holder != null)
        {
            return $"Can't reset {label} to {defaultKey}: {holder} already uses {defaultKey}. Rebind {holder} first, or pick another key with /rebind.";
        }

        entry.Value = defaultKey;
        Config.Save();
        return $"{label} reset to its default, {defaultKey}.";
    }

    internal string ResetAllHotkeysToDefaults()
    {
        foreach (HotkeyBindTarget target in (HotkeyBindTarget[])Enum.GetValues(typeof(HotkeyBindTarget)))
        {
            ConfigEntry<KeyCode> entry = ResolveHotkeyEntry(target);
            if (entry != null)
            {
                entry.Value = ResolveHotkeyDefault(target);
            }
        }

        Config.Save();
        return "All BlueSage keys are back to their defaults: QoL menu Home, Style Helper Insert, Fly End, Cleanup F8.";
    }

    private void OnApplicationQuit()
    {
#if MODDERBRANCH_UAT
        if (EnableBetaUatObservationOnly?.Value == true) return;
#endif
        BeginPluginShutdown("application-quit");
    }

    private void OnDestroy()
    {
#if MODDERBRANCH_UAT
        if (EnableBetaUatObservationOnly?.Value == true) return;
#endif
        BeginPluginShutdown("destroy");
    }

    private void BeginPluginShutdown(string trigger)
    {
        PluginShutdownController.Begin(
            trigger,
            RunSteamShutdownCleanup,
            RunLocalShutdownCleanup);
    }

    private void RunSteamShutdownCleanup(SteamRuntimeSnapshot snapshot)
    {
        ClearRescueReceiverCapability(snapshot);
        SteamIdModerationController.ClearHostCapabilityForShutdown(snapshot);
        Patches.PlayerRoleLabelController.ClearPublishedStateForShutdown(snapshot);
        ClearHostHealthPresenceForShutdown(snapshot);
    }

    private static void ClearHostHealthPresenceForShutdown(SteamRuntimeSnapshot snapshot)
    {
        if (snapshot == null ||
            !snapshot.SteamWasReady ||
            !snapshot.LocalWasOwner ||
            snapshot.Lobby == CSteamID.Nil)
        {
            return;
        }

        try
        {
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, "bluesage_qol_heartbeat", string.Empty);
            SteamMatchmaking.SetLobbyData(snapshot.Lobby, "heartbeat", string.Empty);
        }
        catch
        {
            // Cached-lobby cleanup is best-effort and never probes runtime managers.
        }
    }

    private void RunLocalShutdownCleanup()
    {
        // Round 5 Stage S: the session summary line goes out before anything else is torn down.
        SessionSummaryController.OnShutdown("quit");
        SessionRunMarkerController.MarkCleanClose();
        if (_rescueReceiverCapabilityCoroutine != null)
        {
            StopCoroutine(_rescueReceiverCapabilityCoroutine);
            _rescueReceiverCapabilityCoroutine = null;
        }
        if (_sweepCoroutine != null)
        {
            StopCoroutine(_sweepCoroutine);
            _sweepCoroutine = null;
        }
        _sweepSchedule.Reset();
        if (_activeSweepOperationCoroutine != null)
        {
            StopCoroutine(_activeSweepOperationCoroutine);
            _activeSweepOperationCoroutine = null;
            _sweepOperationGate.Complete(_sweepOperationGate.ActiveSequence);
        }
        ReleaseSweepBackgroundExecution();

        if (_compatibilityRefreshCoroutine != null)
        {
            StopCoroutine(_compatibilityRefreshCoroutine);
            _compatibilityRefreshCoroutine = null;
        }

        if (_managedReconnectCoroutine != null)
        {
            StopCoroutine(_managedReconnectCoroutine);
            _managedReconnectCoroutine = null;
        }

        if (_replacementLobbyDiscoveryCoroutine != null)
        {
            StopCoroutine(_replacementLobbyDiscoveryCoroutine);
            _replacementLobbyDiscoveryCoroutine = null;
        }

        if (_cleanMenuReconnectCoroutine != null)
        {
            StopCoroutine(_cleanMenuReconnectCoroutine);
            _cleanMenuReconnectCoroutine = null;
        }

        _reconnectMenuFallbackInProgress = false;
        _cleanMenuReconnectActive = false;

        if (_hostHealthCoroutine != null)
        {
            StopCoroutine(_hostHealthCoroutine);
            _hostHealthCoroutine = null;
        }

        if (_communityBanListCoroutine != null)
        {
            StopCoroutine(_communityBanListCoroutine);
            _communityBanListCoroutine = null;
        }

        if (_reconnectAnnouncementCoroutine != null)
        {
            StopCoroutine(_reconnectAnnouncementCoroutine);
            _reconnectAnnouncementCoroutine = null;
        }

        if (_welcomeMessageCoroutine != null)
        {
            StopCoroutine(_welcomeMessageCoroutine);
            _welcomeMessageCoroutine = null;
        }

        if (_mentionPingControllerObject != null)
        {
            Destroy(_mentionPingControllerObject);
            _mentionPingControllerObject = null;
        }

        if (_commandTypeaheadControllerObject != null)
        {
            Destroy(_commandTypeaheadControllerObject);
            _commandTypeaheadControllerObject = null;
        }

        if (_chatReadabilityControllerObject != null)
        {
            Destroy(_chatReadabilityControllerObject);
            _chatReadabilityControllerObject = null;
        }

        if (_cloneShieldControllerObject != null)
        {
            Destroy(_cloneShieldControllerObject);
            _cloneShieldControllerObject = null;
        }

        if (_playerRenderSaverControllerObject != null)
        {
            Destroy(_playerRenderSaverControllerObject);
            _playerRenderSaverControllerObject = null;
        }

        if (_performanceOverlayControllerObject != null)
        {
            Destroy(_performanceOverlayControllerObject);
            _performanceOverlayControllerObject = null;
        }

        if (_sessionAuditControllerObject != null)
        {
            Destroy(_sessionAuditControllerObject);
            _sessionAuditControllerObject = null;
        }

        StopOptionalLocalBridges();

        if (_sessionRunMarkerControllerObject != null)
        {
            Destroy(_sessionRunMarkerControllerObject);
            _sessionRunMarkerControllerObject = null;
        }

        PlayerRenderSaverController.RestoreAll();
        Patches.SelfMovementPatch.CleanupRuntimeState();
        if (EnableNoclipFly != null && EnableNoclipFly.Value)
        {
            EnableNoclipFly.Value = false;
            Config.Save();
        }
        Patches.HostVisibilityProtocolPatch.RestoreAll();
        Patches.PlayerRoleLabelController.RestoreAll();
        Patches.PlayerIdentityRoleHighlightController.ClearAll();
        Patches.PlayerPanelSurfaceController.ClearAll();
        IdentityResyncController.Reset();
        Patches.MiniMapPlayerLabelPatch.ClearAll();
        if (_styleHelperWindow != null)
        {
            Destroy(_styleHelperWindow.gameObject);
            _styleHelperWindow = null;
        }

        if (_qolMenuWindow != null)
        {
            Destroy(_qolMenuWindow.gameObject);
            _qolMenuWindow = null;
        }

        BlueSageUiTheme.ReleaseRuntimeTextures();
        NativeManagerCache.Clear();

        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RestoreChatAccessibilityScale();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RestoreChatFontSize();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RestoreChatWindowHeight();
        _harmony.UnpatchSelf();
        Log?.LogInfo($"{PluginName} unloaded and Harmony patches removed.");
        Instance = null;
    }

    private void BindConfig()
    {
        PublicFirstRunDefaults publicDefaults = PublicFirstRunDefaults.Current;
        EnableAutoSweep = Config.Bind("Asset Sweep", "EnableAutoSweep", publicDefaults.AutomaticCleanup, "Run BlueSage Automatic Cleanup on its own timer (40 minutes by default, never right before or after the game's autosave). Existing saved off choices remain off; manual cleanup remains available.");
        AutoDisableSweepWhenAndrewSweepInstalled = Config.Bind("Asset Sweep", "AutoDisableSweepWhenAndrewSweepInstalled", true, "Disable BlueSage auto sweep if AndrewLin Sweep is installed. On by default to avoid double sweeping.");
        AllowManualSweepWhenAndrewSweepInstalled = Config.Bind("Asset Sweep", "AllowManualSweepWhenAndrewSweepInstalled", publicDefaults.ManualCleanupWithOverlappingSweepMod, "Keep BlueSage manual cleanup separate when another cleanup mod is installed. Off by default avoids two cleanup tools acting at once.");
        SweepIntervalMinutes = Config.Bind("Asset Sweep", "SweepIntervalMinutes", SweepSettingsPolicy.DefaultIntervalMinutes, "Minutes between BlueSage automatic cleanups. Safe range 5 to 180. Used only when BlueSage Automatic Cleanup is explicitly enabled; the game native interval is never changed.");
        NotifyOnSweep = Config.Bind("Asset Sweep", "NotifyOnSweep", false, "Show an in-game notification when BlueSage runs an automatic cleanup. Off by default since 0.2.7 (a toast every 40 minutes is noise in a cozy session); a cleanup you trigger yourself always confirms.");
        ForceManagedGcCollect = Config.Bind("Asset Sweep", "ForceManagedGcCollect", false, "Also run GC.Collect after a BlueSage-coordinated cleanup. Off by default for smoother play.");
        EnableManualSweepHotkey = Config.Bind("Asset Sweep", "EnableManualSweepHotkey", true, "Deprecated compatibility key retained so existing configs still load. ManualSweepKey always remains active; menu and slash-command cleanup remain available.");
        ManualSweepKey = Config.Bind("Asset Sweep", "ManualSweepKey", KeyCode.F8, "Hotkey for Run Cleanup (one memory cleanup now).");
        QolMenuKey = Config.Bind("Hotkeys", "QolMenuKey", KeyCode.Home, "Hotkey to open or close the BlueSage QoL menu. Set to None to disable this hotkey; /qolmenu always remains available.");
        StyleMenuKey = Config.Bind("Hotkeys", "StyleMenuKey", KeyCode.Insert, "Hotkey to toggle Style Helper. Set to None to disable this hotkey.");
        NoclipFlyKey = Config.Bind("Hotkeys", "NoclipFlyKey", KeyCode.End, "Hotkey to toggle self-only noclip/fly mode without opening the QoL menu. Set to None to disable this hotkey.");
        ApplyStartupHotkeyRepair();
        EnableToastNotifications = Config.Bind("Notifications", "EnableToastNotifications", true, "Shows a brief on-screen toast alongside the usual chat notification for hotkey toggles and rebinds, so it's visible even in a busy, fast-scrolling chat.");
        HostMotd = Config.Bind("Host", "HostMotd", string.Empty, "Short welcome QoL players see when they join a lobby you host (toast + chat). Leave empty for \"Welcome to my lobby!\". Set it in-game with /motd or the QoL menu.");
        ToastHoldSeconds = Config.Bind("Notifications", "ToastHoldSeconds", ToastPolicy.DefaultHoldSeconds, "Seconds a toast stays fully visible before it fades (2-15). Raise it if toasts disappear before you finish reading.");
        ToastScalePercent = Config.Bind("Notifications", "ToastScalePercent", 0, "Toast size: 0 = Auto (grows with your screen resolution from 1080p, never smaller than normal), or 75-200 percent.");
        EnableHostAnnouncements = Config.Bind("Notifications", "EnableHostAnnouncements", true, "Shows heads-up toasts the lobby host or an assigned Helper sends with /announce (restarts, breaks). Separate from Toast Pop-ups; the chat line always appears.");
        EnableWelcomeOverlay = Config.Bind("Notifications", "EnableWelcomeOverlay", true, "Shows a dismissible welcome overlay every time you join a lobby, with a quick how-to and your current hotkeys. Its Don't show this again button turns this off. Chat welcome message is a separate toggle.");
        EnableVivoxPositionRateGuard = Config.Bind("Performance Safety", "EnableVivoxPositionRateGuard", true, "Coalesce the game's per-frame Vivox 3D-position requests to the Unity-recommended 4 Hz. Disable only for compatibility testing.");
#if MODDERBRANCH_UAT
        EnableRosterAndNetworkGuards = Config.Bind("Compatibility Safety", "EnableRosterAndNetworkGuards", true, "Allow BlueSage roster reconciliation and PurrNet null-reflection guards. Turn off for vanilla-first beta UAT.");
        EnableBetaUatObservationOnly = Config.Bind("Compatibility Safety", "EnableBetaUatObservationOnly", true, "Load and link the exact beta UAT build without starting migrations, patches, controllers, pollers, or lobby writes.");
#endif

        EnableChatTimestamps = Config.Bind("Chat", "EnableChatTimestamps", true, "Add local timestamps before player names.");
        EnableNotificationTimestamps = Config.Bind("Chat", "EnableNotificationTimestamps", true, "Add local timestamps before system notifications.");
        Use24HourTime = Config.Bind("Chat", "Use24HourTime", false, "Use 24-hour timestamps when timestamp features are enabled. Off uses 12-hour time with AM/PM.");
        TimestampSizePercent = Config.Bind("Chat", "TimestampSizePercent", 60, "Timestamp text size percentage. Clamped from 40 to 100.");
        TimestampColor = Config.Bind("Chat", "TimestampColor", "F5EDE1", "Timestamp color as a six-character hex code.");
        AutoDisableChatTweaksWhenSimpleQoLInstalled = Config.Bind("Chat", "AutoDisableChatTweaksWhenSimpleQoLInstalled", true, "Disable BlueSage timestamps and leave notices if Simple_QOL/ChatTweaks-style mods are installed.");
        EnableChatReadability = Config.Bind("Chat Readability", "EnableChatReadability", true, "Add local outline and shadow to chat text so white or colored messages stay readable on bright backgrounds.");
        EnablePersistentChatBackdrop = Config.Bind("Chat Readability", "EnablePersistentChatBackdrop", true, "Keep the local chat panel/backdrop easier to read when the game exposes it. Local UI only; does not rewrite messages.");
        ChatOutlineIntensity = Config.Bind("Chat Readability", "ChatOutlineIntensity", ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent, "Chat outline strength from 25 to 100. Default 75 keeps chat readable with a stronger outline; 100 is the crispest outline.");
        ChatUiScalePercent = Config.Bind("Chat Readability", "ChatUiScalePercent", ChatAccessibilityScalePolicy.DefaultScalePercent, "Local chat UI accessibility scale from 75 to 200 percent. Default 100 keeps vanilla sizing; larger values enlarge the chat panel and visible message text.");
        MenuUiScalePercent = Config.Bind("Accessibility", "MenuUiScalePercent", publicDefaults.MenuScalePercent, "BlueSage menu and tooltip scale. Default 0 (Auto) picks a display-aware scale; 75-200 sets it by hand. Chat sizing remains separate.");
        ChatFontSizePercent = Config.Bind("Chat Readability", "ChatFontSizePercent", ChatFontSizePolicy.DefaultFontSizePercent, "Local chat message font size from 75 to 200 percent. Default 100 keeps vanilla text size; larger values enlarge message text without scaling the whole chat shell.");
        ChatWindowHeightPercent = Config.Bind("Chat Readability", "ChatWindowHeightPercent", ChatWindowHeightPolicy.DefaultHeightPercent, "Local chat window height from 100 to 200 percent. Default 100 keeps vanilla size; larger values add vertical chat space without scaling text.");
        ChatHistoryRows = Config.Bind("Chat Readability", "ChatHistoryRows", ChatHistoryLimitPolicy.DefaultRows, "Retained chat rows from 25 to 250. Default 150 balances useful scrollback with large-lobby UI cost; BlueSage never lowers a higher vanilla or other-mod limit.");
        LocalChatRangeMeters = Config.Bind("Chat Readability", "LocalChatRangeMeters", LocalChatRangePolicy.DefaultMeters, "How far away a local chat line can be and still show in your own chat window, from 5 to 25 meters. Default 15; 5 is the game's own range. This only changes what you see; global chat has no range.");
        ChatOutlineColor = Config.Bind("Chat Readability", "ChatOutlineColor", "000000", "Six-character hex color for the normal local chat outline path. Default 000000.");
        BlackChatOutlineColor = Config.Bind("Chat Readability", "BlackChatOutlineColor", "C0C0C0", "Six-character hex color for true-black styled chat/name outline path. Default C0C0C0 light grey.");
        BlackNamesOutline = Config.Bind("Chat Readability", "BlackNamesOutline", true, "Use the black-text outline color for true-black chat/name rows. On by default so black styled names and messages stay readable; turn off to force the normal outline color.");
        EnableBetterMove = Config.Bind("BetterMove", "EnableBetterMove", true, "Hold Shift to run or Ctrl to slow-walk. This changes only your character and is safe to turn off.");
        EnableUnlimitedConsumables = Config.Bind("Consumables", "EnableUnlimitedConsumables", true, "Once you own a canteen item, chalk color, or fishing bait, using it no longer lowers its count. Clothing, rods, and bobbers stay vanilla. Turn this off to restore normal item use immediately.");
        EnableNoclipFly = Config.Bind("Self Movement", "EnableNoclipFly", false, "Self-only noclip/fly mode. Always reset off when BlueSage loads or unloads. Controls: WASD, Space/E up, Ctrl/Q down, Shift boost, Alt precision, middle mouse rotates.");
        FlyPrecisionPercent = Config.Bind("Self Movement", "FlyPrecisionPercent", FlyCameraPolicy.DefaultPrecisionPercent, "Fly speed while holding Alt, as a percent of normal fly speed (5-50). Lower is finer for careful positioning.");
        FlyRotateButton = Config.Bind("Self Movement", "FlyRotateButton", FlyCameraPolicy.DefaultRotateButton, "Mouse button that rotates the camera while flying: Mouse2 (middle, default), Mouse3, Mouse4, or Mouse1 (right, same as normal play).");
        EnableExtendedZoom = Config.Bind("Self Movement", "EnableExtendedZoom", true, "Scroll zoom reaches much closer and farther than normal (0.2x-4x the game's range) everywhere except desk mode. Turning it off snaps the camera back into the normal range.");
        EnableFishingAid = Config.Bind("Accessibility", "EnableFishingAid", FishingAidPolicy.DefaultEnabled, "Accessibility fishing aid for sore or unsteady hands. Off by default. Take out your rod with the fishing key (H) near water and stand still: it finds the water, casts, answers bites, plays the reel mini-game, and recasts until you put the rod away. A cast by hand sets your preferred spot.");
        FishingAidReactionSeconds = Config.Bind("Accessibility", "FishingAidReactionSeconds", FishingAidPolicy.DefaultReactionSeconds, "Seconds the fishing aid waits before answering a bite (0.1-1.5).");
        FishingAidClicksPerSecond = Config.Bind("Accessibility", "FishingAidClicksPerSecond", FishingAidPolicy.DefaultClicksPerSecond, "Clicks per second in the reel mini-game's last round (3-15).");
        FishingAidReleaseSeconds = Config.Bind("Accessibility", "FishingAidReleaseSeconds", FishingAidPolicy.DefaultReleaseSeconds, "Seconds a catch stays on screen before the fishing aid presses Release/Recycle so it can cast again sooner (0.2-5).");
        EnableFishingAid.SettingChanged += (sender, args) => Patches.FishingAidRuntime.Reset();
        EnableExtendedZoom.SettingChanged += (sender, args) =>
        {
            if (!EnableExtendedZoom.Value)
            {
                Patches.SelfMovementPatch.RestoreVanillaCamera();
            }
        };
        EnableNoclipFly.SettingChanged += (sender, args) =>
        {
            if (!EnableNoclipFly.Value)
            {
                Patches.SelfMovementPatch.ReleaseFlyRotateCursor();
            }
        };
        SelfSavedPosition = Config.Bind("Self Movement", "SavedPosition", string.Empty, "Local saved self-teleport position in x,y,z format.");
        SelfSpeedMultiplier = Config.Bind("Self Movement", "SpeedMultiplier", SelfMovementPolicy.DefaultMultiplier, "Self-only movement speed multiplier. 1 is vanilla; range 0.25 to 5.");
        SelfJumpMultiplier = Config.Bind("Self Movement", "JumpMultiplier", SelfMovementPolicy.DefaultMultiplier, "Self-only jump-height multiplier. 1 is vanilla; range 0.25 to 5.");
        SelfGravityMultiplier = Config.Bind("Self Movement", "GravityMultiplier", SelfMovementPolicy.DefaultMultiplier, "Self-only gravity/fall multiplier. 1 is vanilla; range 0.1 to 5.");
        if (EnableNoclipFly.Value)
        {
            EnableNoclipFly.Value = false;
            Config.Save();
        }

        StatusBaseName = Config.Bind("Status Helper", "StatusBaseName", string.Empty, "Base display name used by BlueSage /setname and /status.");
        StatusMessage = Config.Bind("Status Helper", "StatusMessage", string.Empty, "Current BlueSage status text appended to your display name.");
        StatusColor = Config.Bind("Status Helper", "StatusColor", "FFD45D", "Six-character hex color for BlueSage status text.");
        StatusBrackets = Config.Bind("Status Helper", "StatusBrackets", "()", "Two characters used around BlueSage status text.");
        EnableSpoons = Config.Bind("Spoons", "EnableSpoons", false, "Optional compact social-battery tag for your BlueSage display name. Use /spoons on, Save Spoons, or Show Tag if you want it visible.");
        SpoonCount = Config.Bind("Spoons", "SpoonCount", 5, "Social battery from 0 to 5. Use /spoons 3 or Style Helper > Status.");
        SpoonLabel = Config.Bind("Spoons", "SpoonLabel", DefaultSpoonLabel, "Short custom label used after 0/5. Use /setmood label; default is sp.");
        SpoonsUpdatedUtc = Config.Bind("Spoons", "SpoonsUpdatedUtc", string.Empty, "UTC timestamp for the last spoon update.");
        EnableStyleUi = Config.Bind("Style Helper", "EnableStyleUi", true, "Enable the BlueSage /styleui preset and clipboard helper window.");
        EnableExtendedAvatarStyles = Config.Bind("Avatar Styles", "EnableExtendedAvatarStyles", true, "Adds six extra outfit slots (4 to 9) below the game's three in the avatar screen's style list. They start on. Styles 1 to 3 stay the game's own and never change; if you turn this off, your extra outfits wait for you.");
        OutfitSlotsHintShown = Config.Bind("Avatar Styles", "OutfitSlotsHintShown", false, "Set to true after BlueSage showed its one-time note about the extra outfit slots. Set it back to false to see the note again.");
        EnableChalkboardPersistence = Config.Bind("Chalkboards", "EnableChalkboardPersistence", true, "Save chalkboards locally. Only the host can restore a saved board for the lobby. Automatically stands down when Chalky is installed.");
        MaxChatCharacters = Config.Bind("Text Limits", "MaxChatCharacters", publicDefaults.ChatTextLimit, "Default 4000 (the maximum). Lower it if very long chat lines wrap oddly with rich-text tags or after a game update.");
        MaxProfileCharacters = Config.Bind("Text Limits", "MaxProfileCharacters", publicDefaults.ProfileTextLimit, "Default 4000 for names, ID cards, and styled profile text. Lower it if another mod or future game update needs a shorter limit.");
        MaxSessionNameCharacters = Config.Bind("Text Limits", "MaxSessionNameCharacters", publicDefaults.RoomNameLimit, "Default 4000 for room/session names. Long room names can still look visually messy in the game's own UI.");

        EnableLeaveNotifications = Config.Bind("Notifications", "EnableLeaveNotifications", true, "Show a local notification when a player leaves. Auto-disabled when overlapping chat mods are detected.");
        EnableWelcomeMessage = Config.Bind("Notifications", "EnableWelcomeMessage", true, "Show BlueSage QoL navigation tips once each time you join a lobby.");

        EnableReconnectGuard = Config.Bind("Reconnect Guard", "EnableReconnectGuard", true, "Try one guarded rejoin after the game reports a real disconnect, then stop safely if the lobby is not playable.");
        ReconnectRejoinHoldSeconds = Config.Bind("Reconnect Guard", "ReconnectRejoinHoldSeconds", ReconnectRejoinHoldPolicy.DefaultHoldSeconds, "Seconds Reconnect Guard waits on the clean menu before rejoining, so the host can clear your old connection first (an instant rejoin can break the lobby player count). Safe range 10 to 120; adjustable in the QoL menu.");
        // Turning Reconnect Guard off in the menu must stop a pending reconnect immediately, the same
        // way /rg off does; otherwise a countdown already running would still auto-join.
        EnableReconnectGuard.SettingChanged += (_, __) =>
        {
            if (!EnableReconnectGuard.Value)
            {
                StopManagedReconnect("Reconnect Guard turned off");
            }
        };
        EnableReconnectAnnouncement = Config.Bind("Reconnect Guard", "EnableReconnectAnnouncement", true, "Send a small in-game chat message after BlueSage auto reconnect succeeds.");

        EnableHostHealthMonitor = Config.Bind("Host Health", "EnableHostHealthMonitor", true, "Quiet background checks for long-running lobbies. Most useful for hosts; use /hh run for a manual check.");
        EnableCommunityBanListSync = Config.Bind("Lobby Safety", "EnableCommunityBanListSync", publicDefaults.CommunitySafetyList, "On by default. BlueSage checks the public community safety list locally and adds only verified missing entries to your game's own list. Existing entries are never changed or removed, and a refresh never kicks a player.");
        EnableFocusAnywhere = Config.Bind("Focus Anywhere", "EnableFocusAnywhere", true, "Allow focus mode from most places instead of only vanilla focus surfaces.");
        EnableEnhancedPlayerPanel = Config.Bind("Enhanced Player Panel", "EnableEnhancedPlayerPanel", true, "Add local @mention and ID-card buttons, plus a verified Steam-ID tooltip on the native green profile button. Native player-panel actions remain available.");
        EnableEnhancedPlayerPanel.SettingChanged += (_, __) =>
        {
            if (!EnableEnhancedPlayerPanel.Value)
            {
                Patches.PlayerPanelSurfaceController.ClearAll();
            }
        };
        EnableCloneShield = Config.Bind("Enhanced Player Panel", "EnableCloneShield", true, "Send one cautious public alert when someone newly matches your full avatar/outfit or exact styled name. The alert is a clue, not an automatic moderation action.");
        EnablePlayerRenderSaver = Config.Bind("Performance", "EnablePlayerRenderSaver", publicDefaults.PlayerRenderSaver, "Retired for now (it caused visual glitches); this setting has no effect and nothing is hidden. Kept so the feature can return later.");
        EnablePerformanceOverlay = Config.Bind("Performance Overlay", "EnablePerformanceOverlay", publicDefaults.PerformanceOverlay, "On by default. The draggable FPS/PING/RAM bar with WORK/RELAX; drag it anywhere, or turn it off in Toggles.");
        PerformanceOverlayRefreshSeconds = Config.Bind("Performance Overlay", "RefreshSeconds", 0.25f, "Overlay refresh interval. Runtime clamps 0.25 to 5 seconds.");
        PerformanceOverlayFontSize = Config.Bind("Performance Overlay", "FontSize", 13, "Overlay font size. Runtime clamps 8 to 32.");
        PerformanceOverlayOffsetX = Config.Bind("Performance Overlay", "OffsetX", publicDefaults.PerformanceOverlayOffsetX, "Saved draggable overlay X position.");
        PerformanceOverlayOffsetY = Config.Bind("Performance Overlay", "OffsetY", publicDefaults.PerformanceOverlayOffsetY, "Saved draggable overlay Y position.");
        PerformanceOverlayWidth = Config.Bind("Performance Overlay", "Width", 320f, "Overlay width. Runtime keeps at least 320 pixels so FPS/PING/RAM remain one straight line.");
        PlayerRenderSaverRadiusMeters = Config.Bind("Performance", "PlayerRenderSaverRadiusMeters", PlayerRenderSaverPolicy.DefaultRadiusMeters, "Player Render Saver keeps remote players inside this distance visible. Safe range 10 to 250 meters.");
        PlayerRenderSaverMaxVisiblePlayers = Config.Bind("Performance", "PlayerRenderSaverMaxVisiblePlayers", PlayerRenderSaverPolicy.DefaultMaxVisiblePlayers, "Player Render Saver also keeps this many closest remote players visible. Safe range 1 to 128.");
        EnablePingMentions = Config.Bind("Ping Mentions", "EnablePingMentions", true, "Highlight personal mentions, offer simple autocomplete, and play a local ping sound when you are mentioned. Lobby-wide pings are limited to the current host and assigned Helpers.");
        EnablePingSound = Config.Bind("Ping Mentions", "EnablePingSound", true, "Play the local UI ping sound when a message mentions you.");
        PingHighlightColor = Config.Bind("Ping Mentions", "PingHighlightColor", "9B59B6", "Six-character hex color for mentioned-message background highlight.");
        PingMentionColor = Config.Bind("Ping Mentions", "PingMentionColor", "FFD700", "Six-character hex color for @mention text.");
        PingSoundMode = Config.Bind("Ping Mentions", "PingSoundMode", "ticket", "Local mention ping sound: click, change, error, task, or ticket. Default ticket. Use /pingsound off to mute.");
        LobbyHelpers = Config.Bind("Helpers", "LobbyHelpers", string.Empty, "Exact SteamID64 values assigned by the current lobby host. Helpers may use @lobby and request host-validated moderation actions.");
        LobbyHelperOwnerSteamId = Config.Bind("Helpers", "LobbyHelperOwnerSteamId", string.Empty, "Steam ID of the host who granted the helper list; prevents permissions carrying across unrelated hosts.");
        LobbyPingDelegates = Config.Bind("Legacy Helpers", "LobbyPingDelegates", string.Empty, "Legacy @lobby helper list. BlueSage imports matching host-scoped entries into Helpers.");
        LobbyPingDelegateOwnerSteamId = Config.Bind("Legacy Helpers", "LobbyPingDelegateOwnerSteamId", string.Empty, "Legacy @lobby helper host scope retained for safe migration.");
        LobbyModerationDelegates = Config.Bind("Legacy Helpers", "LobbyModerationDelegates", string.Empty, "Legacy moderation helper list. BlueSage imports matching host-scoped entries into Helpers.");
        LobbyModerationDelegateOwnerSteamId = Config.Bind("Legacy Helpers", "LobbyModerationDelegateOwnerSteamId", string.Empty, "Legacy moderation helper host scope retained for safe migration.");
        EnableChatCopy = Config.Bind("Chat Copy", "EnableChatCopy", true, "Ctrl-left-click a chat message to copy only its text while plain left-click stays with the vanilla ID card. Right-click remains supported where the game delivers it. Local-only; no names, timestamps, or status labels.");
        EnableChatUrlLinks = Config.Bind("Chat Copy", "EnableChatUrlLinks", true, "Highlight safe http/https links. Shift-left-click once copies and warns; repeat within 10 seconds to open. Plain left-click remains the vanilla ID card.");
        EnableMiniMapPlayerLabels = Config.Bind("MiniMap", "EnableMiniMapPlayerLabels", true, "Local minimap ID-click helper. Dot click can open ID cards when player index data resolves safely. Floating name labels were removed because they can cause FPS/CPU lag in large lobbies.");
        EnableMiniMapPlayerLabels.SettingChanged += (_, __) =>
        {
            if (!EnableMiniMapPlayerLabels.Value)
            {
                Patches.MiniMapPlayerLabelPatch.ClearAll();
            }
        };
        EnableMiniMapFriendColors = Config.Bind("MiniMap", "EnableMiniMapFriendColors", true, "Color Steam friends green on the local minimap using cached Steam relationship checks. Automatically stands down when MinimapFriends is installed.");
        UiThemePreset = Config.Bind("Style Helper", "UiThemePreset", publicDefaults.Theme, "Menu theme: BlueSage Harbor, Midnight Dock, Vanilla Cream OT, or OG. Midnight Dock is the public default; existing saved choices are preserved.");
        EnableLocalAuditExports = Config.Bind("Local Audit", "EnableLocalAuditExports", false, "Optional, off by default. Keeps a local copy of chat and the game log in BepInEx/BlueSageQoLModSuite/logs, one folder per session. The newest 3 sessions (or 3 days, or 24 MB, whichever comes first) stay as plain files; older ones are zipped into archive and kept 14 days, about 48 MB in total. The whole BlueSage folder never stays over 1 GB. One session is at most about 20 MB plus 2 snapshots of up to 8 MB. Steam IDs are added only while you host or are a Helper. Never uploaded. Use /auditlog on, off, snapshot or folder.");
        BindOptionalLocalBridgeConfig();
        EnableLocalAuditExports.SettingChanged += (_, __) => SessionAuditController.ReconcileEnabledState();
        ConfigMigrationVersion = Config.Bind("Internal", "ConfigMigrationVersion", string.Empty, "Tracks one-time BlueSage QoL default migrations. Safe to ignore.");
        StatusBaseNameLegacyBackup = Config.Bind("Internal", "StatusBaseNameLegacyBackup", string.Empty, "One local rollback copy of a malformed pre-0.2.0 styled base repaired by BlueSage. Safe to leave unchanged.");
        StyleStateMigrationVersion = Config.Bind("Internal", "StyleStateMigrationVersion", string.Empty, "Tracks the idempotent pre-0.2.0 style-state repair. Safe to ignore.");
    }

    private bool _configFileExistedAtStartup;

    private void ApplyOneTimeConfigMigration()
    {
        // A fresh install already has today's defaults; historical migrations are only for older configs.
        bool freshInstall = !_configFileExistedAtStartup;
        bool finalDefaultsMigrated = freshInstall || ConfigMigrationPolicy.HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.FinalDefaultsV2Migration);
        bool sweepDefaultsMigrated = ConfigMigrationPolicy.HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.SweepFifteenMinuteV1Migration);
        bool sweepTenMinuteSafetyMigrated = ConfigMigrationPolicy.HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.SweepTenMinuteSafetyV2Migration);
        bool sweepAutomaticOptInMigrated = ConfigMigrationPolicy.HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.SweepAutomaticOptInV3Migration);
        bool sweepOverlapAvoidanceMigrated = ConfigMigrationPolicy.HasCompletedMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.SweepFortyMinuteOverlapV4Migration);
        bool communityBanAnyClientOptInMigrated = ConfigMigrationPolicy.HasCompletedMigration(
            ConfigMigrationVersion?.Value,
            ConfigMigrationPolicy.CommunityBanAnyClientOptInV1Migration);
        bool sweepNotifyQuietMigrated = ConfigMigrationPolicy.HasCompletedMigration(
            ConfigMigrationVersion?.Value,
            ConfigMigrationPolicy.SweepNotifyQuietV5Migration);
        bool localAuditOffMigrated = ConfigMigrationPolicy.HasCompletedMigration(
            ConfigMigrationVersion?.Value,
            ConfigMigrationPolicy.LocalAuditOffV6Migration);
        bool changed = false;
        if (EnableLocalAuditExports != null)
        {
            // Round 5d: the local audit becomes opt-in; existing installs turn it off once, a later "on" sticks.
            bool migrated = LocalFileBudgetPolicy.MigrateLocalAudit(EnableLocalAuditExports.Value, freshInstall || localAuditOffMigrated);
            if (migrated != EnableLocalAuditExports.Value)
            {
                EnableLocalAuditExports.Value = migrated;
                changed = true;
            }
        }

        if (NotifyOnSweep != null)
        {
            // Round 5 F: existing installs go quiet once; a fresh install already has the new default.
            bool migrated = SweepSettingsPolicy.MigrateSweepNotification(NotifyOnSweep.Value, freshInstall || sweepNotifyQuietMigrated);
            if (migrated != NotifyOnSweep.Value)
            {
                NotifyOnSweep.Value = migrated;
                changed = true;
            }
        }

        if (SweepIntervalMinutes != null)
        {
            int migrated = SweepSettingsPolicy.ResolveStartupIntervalMinutes(SweepIntervalMinutes.Value, freshInstall, sweepDefaultsMigrated, sweepTenMinuteSafetyMigrated, sweepOverlapAvoidanceMigrated);
            if (migrated != SweepIntervalMinutes.Value)
            {
                SweepIntervalMinutes.Value = migrated;
                changed = true;
            }
        }

        if (EnableAutoSweep != null)
        {
            bool migrated = SweepSettingsPolicy.MigrateAutomaticCleanupOptIn(EnableAutoSweep.Value, sweepAutomaticOptInMigrated);
            if (migrated != EnableAutoSweep.Value)
            {
                EnableAutoSweep.Value = migrated;
                changed = true;
            }
        }

        if (ChatOutlineIntensity != null)
        {
            int migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(ChatOutlineIntensity.Value, 50, ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent, finalDefaultsMigrated);
            migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(migrated, 60, ChatReadabilityStylePolicy.DefaultOutlineIntensityPercent, finalDefaultsMigrated);
            if (migrated != ChatOutlineIntensity.Value)
            {
                ChatOutlineIntensity.Value = migrated;
                changed = true;
            }
        }

        if (ChatUiScalePercent != null)
        {
            int clamped = ChatAccessibilityScalePolicy.ClampScalePercent(ChatUiScalePercent.Value);
            if (clamped != ChatUiScalePercent.Value)
            {
                ChatUiScalePercent.Value = clamped;
                changed = true;
            }
        }

        if (ChatFontSizePercent != null)
        {
            int clamped = ChatFontSizePolicy.ClampFontSizePercent(ChatFontSizePercent.Value);
            if (clamped != ChatFontSizePercent.Value)
            {
                ChatFontSizePercent.Value = clamped;
                changed = true;
            }
        }

        if (ChatWindowHeightPercent != null)
        {
            int clamped = ChatWindowHeightPolicy.ClampHeightPercent(ChatWindowHeightPercent.Value);
            if (clamped != ChatWindowHeightPercent.Value)
            {
                ChatWindowHeightPercent.Value = clamped;
                changed = true;
            }
        }

        if (ChatHistoryRows != null)
        {
            int migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(ChatHistoryRows.Value, 100, ChatHistoryLimitPolicy.DefaultRows, finalDefaultsMigrated);
            if (migrated != ChatHistoryRows.Value)
            {
                ChatHistoryRows.Value = migrated;
                changed = true;
            }
        }

        if (BlackChatOutlineColor != null)
        {
            string migrated = ConfigMigrationPolicy.MigrateOldDefaultString(BlackChatOutlineColor.Value, "FFFFFF", "C0C0C0", finalDefaultsMigrated);
            if (!string.Equals(migrated, BlackChatOutlineColor.Value, StringComparison.OrdinalIgnoreCase))
            {
                BlackChatOutlineColor.Value = migrated;
                changed = true;
            }
        }

        if (Use24HourTime != null)
        {
            bool migrated = ConfigMigrationPolicy.MigrateOldDefaultBool(Use24HourTime.Value, true, false, finalDefaultsMigrated);
            if (migrated != Use24HourTime.Value)
            {
                Use24HourTime.Value = migrated;
                changed = true;
            }
        }

        if (EnableExtendedAvatarStyles != null)
        {
            bool migrated = ConfigMigrationPolicy.MigrateOldDefaultBool(EnableExtendedAvatarStyles.Value, false, true, finalDefaultsMigrated);
            if (migrated != EnableExtendedAvatarStyles.Value)
            {
                EnableExtendedAvatarStyles.Value = migrated;
                changed = true;
            }
        }

        if (EnableChalkboardPersistence != null)
        {
            bool migrated = ConfigMigrationPolicy.MigrateOldDefaultBool(EnableChalkboardPersistence.Value, false, true, finalDefaultsMigrated);
            if (migrated != EnableChalkboardPersistence.Value)
            {
                EnableChalkboardPersistence.Value = migrated;
                changed = true;
            }
        }

        if (PlayerRenderSaverRadiusMeters != null)
        {
            int clamped = PlayerRenderSaverPolicy.ClampRadiusMeters(PlayerRenderSaverRadiusMeters.Value);
            if (clamped != PlayerRenderSaverRadiusMeters.Value)
            {
                PlayerRenderSaverRadiusMeters.Value = clamped;
                changed = true;
            }
        }

        if (PlayerRenderSaverMaxVisiblePlayers != null)
        {
            int clamped = PlayerRenderSaverPolicy.ClampMaxVisiblePlayers(PlayerRenderSaverMaxVisiblePlayers.Value);
            if (clamped != PlayerRenderSaverMaxVisiblePlayers.Value)
            {
                PlayerRenderSaverMaxVisiblePlayers.Value = clamped;
                changed = true;
            }
        }

        if (MaxChatCharacters != null)
        {
            int migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(MaxChatCharacters.Value, 1500, InputFieldLimitPolicy.DefaultMaxChatCharacters, finalDefaultsMigrated);
            migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(migrated, 2000, InputFieldLimitPolicy.DefaultMaxChatCharacters, finalDefaultsMigrated);
            if (migrated != MaxChatCharacters.Value)
            {
                MaxChatCharacters.Value = migrated;
                changed = true;
            }
        }

        if (MaxProfileCharacters != null)
        {
            int migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(MaxProfileCharacters.Value, 1500, InputFieldLimitPolicy.DefaultMaxProfileCharacters, finalDefaultsMigrated);
            migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(migrated, 2000, InputFieldLimitPolicy.DefaultMaxProfileCharacters, finalDefaultsMigrated);
            if (migrated != MaxProfileCharacters.Value)
            {
                MaxProfileCharacters.Value = migrated;
                changed = true;
            }
        }

        if (MaxSessionNameCharacters != null)
        {
            int migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(MaxSessionNameCharacters.Value, 1500, InputFieldLimitPolicy.DefaultMaxSessionNameCharacters, finalDefaultsMigrated);
            migrated = ConfigMigrationPolicy.MigrateOldDefaultInt(migrated, 2000, InputFieldLimitPolicy.DefaultMaxSessionNameCharacters, finalDefaultsMigrated);
            if (migrated != MaxSessionNameCharacters.Value)
            {
                MaxSessionNameCharacters.Value = migrated;
                changed = true;
            }
        }

        if (PingSoundMode != null)
        {
            string migrated = ConfigMigrationPolicy.MigrateOldDefaultString(PingSoundMode.Value, "click", "ticket", finalDefaultsMigrated);
            if (!string.Equals(migrated, PingSoundMode.Value, StringComparison.Ordinal))
            {
                PingSoundMode.Value = migrated;
                changed = true;
            }
        }

        if (EnableSpoons != null && ConfigMigrationPolicy.ShouldDisableDefaultSpoons(EnableSpoons.Value, SpoonsUpdatedUtc?.Value, finalDefaultsMigrated))
        {
            EnableSpoons.Value = false;
            changed = true;
        }

        if (EnableCommunityBanListSync != null)
        {
            bool migrated = ConfigMigrationPolicy.MigrateOldDefaultBool(
                EnableCommunityBanListSync.Value,
                oldDefault: true,
                newDefault: false,
                // Fresh installs keep the documented On default (Blue, 2026-10-17); the July
                // opt-in flip only applies to configs that predate it.
                alreadyMigrated: freshInstall || communityBanAnyClientOptInMigrated);
            if (migrated != EnableCommunityBanListSync.Value)
            {
                EnableCommunityBanListSync.Value = migrated;
                changed = true;
            }
        }

        string migrationLedger = ConfigMigrationPolicy.RecordMigration(ConfigMigrationVersion?.Value, ConfigMigrationPolicy.FinalDefaultsV2Migration);
        migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepFifteenMinuteV1Migration);
        migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepTenMinuteSafetyV2Migration);
        migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepAutomaticOptInV3Migration);
        migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.CommunityBanAnyClientOptInV1Migration);
        migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepFortyMinuteOverlapV4Migration);
        migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.SweepNotifyQuietV5Migration);
        migrationLedger = ConfigMigrationPolicy.RecordMigration(migrationLedger, ConfigMigrationPolicy.LocalAuditOffV6Migration);
        if (ConfigMigrationVersion != null && !string.Equals(ConfigMigrationVersion.Value, migrationLedger, StringComparison.Ordinal))
        {
            ConfigMigrationVersion.Value = migrationLedger;
            changed = true;
        }

        if (changed)
        {
            Config.Save();
            Log?.LogInfo("Applied BlueSage QoL one-time config migration for release defaults.");
        }
    }

    private void StartWelcomeMessageLoop()
    {
        if (_welcomeMessageCoroutine != null)
        {
            StopCoroutine(_welcomeMessageCoroutine);
        }

        _welcomeMessageCoroutine = StartCoroutine(WelcomeMessageLoop());
    }

    private IEnumerator WelcomeMessageLoop()
    {
        yield return new WaitForSecondsRealtime(4f);

        while (true)
        {
            // Voice pass NOISE 1: settle once per visit whether this is a Reconnect Guard return to the same lobby.
            // Runs whatever the welcome toggle says, so the host-message poll and this loop always agree.
            bool quietReconnectVisit = TryGetCurrentLobbyKeyForWelcomeMessage(out string quietVisitKey) && IsQuietReconnectVisit(quietVisitKey);

            // The overlay is intentionally independent of EnableWelcomeMessage (that toggle only
            // controls the chat lines below) -- it must still offer itself even if the player has
            // turned the chat welcome message off. See WelcomeOverlayWiringContractTests.
            // Round 5e: the pop-up waits until the lobby id is known (so the quiet-return decision above is settled)
            // and stays closed after a Reconnect Guard return to the same lobby, like the welcome lines and Tip.
            if (TryGetCurrentLobbyKeyForWelcomeMessage(out string overlayLobbyKey) &&
                WelcomeRepeatPolicy.LobbyIdOf(overlayLobbyKey).Length > 0 &&
                WelcomeOverlayPolicy.ShouldOffer(
                    EnableWelcomeOverlay?.Value == true,
                    overlayLobbyKey,
                    _welcomeOverlayOfferedVisitKey))
            {
                _welcomeOverlayOfferedVisitKey = overlayLobbyKey;
                _welcomeOverlayVisible = !quietReconnectVisit;
            }

            // Round 4 WS6: calm compatibility heads-up when another Helper-like QoL mod is loaded. Once per
            // session, only in a lobby, never on top of the welcome pop-up or the full guide pop-up (review R3).
            // The shown flag is checked first so the lobby lookup stops once the notice is done, and it is set only
            // after the chat line lands (chat is not ready for the first moments in a lobby).
            if (!_otherQolModNoticeShown &&
                OtherQolModNoticePolicy.ShouldFire(TryGetCurrentLobbyKeyForWelcomeMessage(out _), _welcomeOverlayVisible || _guideOverlayVisible, _otherQolModNoticeShown))
            {
                if (!OtherQolModNoticePolicy.TryBuildNotice(Chainloader.PluginInfos.Keys, out string otherModNotice))
                {
                    _otherQolModNoticeShown = true;
                }
                else if (AddLocalNotification(otherModNotice))
                {
                    _otherQolModNoticeShown = true;
                    ShowToast(otherModNotice);
                }
            }

            // Hotkey-repair notices are not part of the optional chat welcome: a player who turned
            // the welcome off still needs to know a hotkey was changed or switched off.
            if (_pendingHotkeyRepairNotices.Count > 0 && TryGetCurrentLobbyKeyForWelcomeMessage(out _))
            {
                FlushPendingHotkeyRepairNotices();
            }

            if (EnableWelcomeMessage == null || !EnableWelcomeMessage.Value)
            {
                ResetWelcomeMessageLobbyState();
                yield return new WaitForSecondsRealtime(2f);
                continue;
            }

            if (TryGetCurrentLobbyKeyForWelcomeMessage(out string lobbyKey))
            {
                if (!WelcomeLobbyKeyPolicy.IsSameVisit(_welcomeLobbyKey, lobbyKey))
                {
                    _welcomeLobbyKey = lobbyKey;
                    // UAT evidence: every join and rejoin should log a new visit key (review 2026-10-17).
                    Log?.LogInfo("[BlueSageVisit] new lobby visit; key=" + WelcomeLobbyKeyPolicy.Redact(lobbyKey));
                    _welcomeMessageShownForLobby = false;
                    _welcomePrimaryShownForLobby = false;
                    _welcomeContextShownForLobby = false;
                    _welcomeVoiceStateLogKey = string.Empty;
                }

                if (!_welcomeMessageShownForLobby)
                {
                    _welcomeMessageShownForLobby = ShowWelcomeMessageForLobby(quietReconnectVisit);
                }
            }
            else
            {
                ResetWelcomeMessageLobbyState();
            }

            yield return new WaitForSecondsRealtime(2f);
        }
    }

    private void ResetWelcomeMessageLobbyState()
    {
        _welcomeLobbyKey = string.Empty;
        _welcomeMessageShownForLobby = false;
        _welcomePrimaryShownForLobby = false;
        _welcomeContextShownForLobby = false;
        _welcomeVoiceStateLogKey = string.Empty;
    }

    // One key per lobby visit: Steam lobby id (or code / visible-lobby fallback) plus the game
    // scene instance, which is new on every join and rejoin, so reconnects and lobby-hops each get
    // their own chat welcome (UAT 861AEF4EFB58, 2026-10-17).
    internal static bool TryGetCurrentLobbyVisitKey(out string visitKey) =>
        TryGetCurrentLobbyKeyForWelcomeMessage(out visitKey);

    private static bool TryGetCurrentLobbyKeyForWelcomeMessage(out string lobbyKey)
    {
        lobbyKey = string.Empty;
        MultiplayerManager multiplayerManager = null;
        MainSceneManager scene = null;
        string lobbyCode = string.Empty;
        ulong steamLobbyId = 0UL;

        try
        {
            scene = MonoSingleton<MainSceneManager>.I;
            multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            if (multiplayerManager != null && multiplayerManager.LobbyStatus)
            {
                lobbyCode = multiplayerManager.LobbyCode ?? string.Empty;
            }

            if (TryGetRescueLobby(out CSteamID lobby) && lobby != CSteamID.Nil)
            {
                steamLobbyId = (ulong)lobby;
            }
        }
        catch
        {
            // Lobby join detection should stay quiet; the loop has a visible-count fallback below.
        }

        if (multiplayerManager == null || scene == null)
        {
            return false;
        }

        lobbyKey = WelcomeLobbyKeyPolicy.Build(steamLobbyId, scene.GetInstanceID(), lobbyCode, HasVisibleLobbyForWelcome);
        return lobbyKey.Length > 0;
    }

    private static bool HasVisibleLobbyForWelcome()
    {
        return TryGetVisibleLobbyCounts(out int? memberCount, out _) &&
            memberCount.HasValue &&
            memberCount.Value >= 1;
    }

    private bool ShowWelcomeMessageForLobby(bool quietReconnectVisit)
    {
        // NOISE 1: a Reconnect Guard return to the same lobby already saw the welcome and the Tip.
        if (!WelcomeRepeatPolicy.ShouldShowWelcome(quietReconnectVisit))
        {
            _welcomePrimaryShownForLobby = true;
            _welcomeContextShownForLobby = true;
            return true;
        }

        bool firstShown = _welcomePrimaryShownForLobby || AddLocalNotification(NavigationCalloutPolicy.BuildPrimaryWelcomeMessage(PluginVersion, QolMenuKey.Value.ToString(), StyleMenuKey.Value.ToString()));
        _welcomePrimaryShownForLobby = firstShown;
        bool secondShown = _welcomeContextShownForLobby;
        if (!secondShown)
        {
            // NOISE 2: the long Tip line only on the first lobby visit of a game session. A skipped Tip counts as
            // shown, so this visit's welcome settles instead of retrying every tick.
            if (!WelcomeRepeatPolicy.ShouldShowTip(_welcomeTipShownThisSession, quietReconnectVisit))
            {
                secondShown = true;
            }
            else
            {
                string voiceGuidance = BuildVoiceWelcomeGuidance();
                secondShown = AddLocalNotification("Tip: " + voiceGuidance +
                    " The game's Pomodoro/focus timer can earn tickets and XP for focus time; " + NavigationCalloutPolicy.BuildCommandBrowserHint() + " Quick status: /brb, /afk, /back. Local Audit is off unless you turn it on; /auditlog status shows your private local logs. Green Steam-profile buttons verify player accounts.");
                _welcomeTipShownThisSession = secondShown;
            }
        }
        _welcomeContextShownForLobby = secondShown;
        return firstShown && secondShown;
    }

    // Voice pass (2026-10-23) NOISE 1. The welcome loop and the host-message poll both ask; whichever ticks first
    // records the visit, so they always agree, and the answer is settled once per visit. A visit whose key has no
    // lobby id yet ("visible|scene:") waits for the id before deciding.
    internal static bool IsQuietReconnectVisit(string visitKey)
    {
        if (string.IsNullOrEmpty(visitKey))
        {
            return false;
        }

        string lobbyId = WelcomeRepeatPolicy.LobbyIdOf(visitKey);
        if (!WelcomeLobbyKeyPolicy.IsSameVisit(_quietTrackedVisitKey, visitKey))
        {
            _quietTrackedVisitKey = visitKey;
            _quietTrackedVisitDecided = false;
            _quietTrackedVisitIsReconnectReturn = false;
        }
        else
        {
            // Same visit; the prefix can upgrade (code: or visible first, steam: once the Steam id resolves).
            _quietTrackedVisitKey = visitKey;
        }

        // Code review: while Reconnect Guard is still joining, the visit that was current when it started (the menu
        // scene) can already report the target lobby id; deciding there would spend the target before the real
        // in-game visit. Only a later, different visit may decide a return.
        bool stillTheVisitBeforeTheJoin = _reconnectReturnTargetLobbyId.Length > 0 &&
                                          WelcomeLobbyKeyPolicy.IsSameVisit(_reconnectReturnNotedVisitKey, visitKey);
        if (!_quietTrackedVisitDecided && lobbyId.Length > 0 && !stillTheVisitBeforeTheJoin)
        {
            _quietTrackedVisitDecided = true;
            _quietTrackedVisitIsReconnectReturn = WelcomeRepeatPolicy.IsQuietReconnectReturn(visitKey, _reconnectReturnTargetLobbyId, _reconnectReturnLobbyIdBeforeDrop);
            _reconnectReturnTargetLobbyId = string.Empty;
            _reconnectReturnLobbyIdBeforeDrop = string.Empty;
            if (_quietTrackedVisitIsReconnectReturn)
            {
                Log?.LogInfo("Welcome: Reconnect Guard returned you to the same lobby; the welcome, Tip and host message stay quiet this visit.");
            }
        }

        if (lobbyId.Length > 0)
        {
            _lastVisitLobbyId = lobbyId;
        }

        return _quietTrackedVisitIsReconnectReturn;
    }

    // Remembered when Reconnect Guard starts joining, so the next visit can tell a same-lobby return.
    private static void NoteReconnectReturnTarget(string targetLobbyId)
    {
        _reconnectReturnTargetLobbyId = (targetLobbyId ?? string.Empty).Trim();
        _reconnectReturnLobbyIdBeforeDrop = _lastVisitLobbyId;
        _reconnectReturnNotedVisitKey = _quietTrackedVisitKey ?? string.Empty;
    }

    private static void ClearReconnectReturnTarget()
    {
        _reconnectReturnTargetLobbyId = string.Empty;
        _reconnectReturnLobbyIdBeforeDrop = string.Empty;
        _reconnectReturnNotedVisitKey = string.Empty;
    }

    private string BuildVoiceWelcomeGuidance()
    {
        bool? pushToTalk = null;
        bool? voicePlayback = null;
        bool? lobbyVoiceOn = null;
        try
        {
            SettingsData settings = MonoSingleton<DataManager>.I?.SettingsData;
            if (settings != null)
            {
                pushToTalk = settings.PushToTalk;
                voicePlayback = settings.VoiceChat;
            }
            VoiceManager voice = MonoSingleton<VoiceManager>.I;
            if (voice != null) lobbyVoiceOn = !voice.IsVoiceLobbyOff;
        }
        catch (Exception ex)
        {
            Log?.LogDebug("Welcome voice-state read deferred safely: " + ex.GetType().Name + ": " + ex.Message);
        }

        string mode = pushToTalk == true ? "Push to Talk" : pushToTalk == false ? "Voice Activation" : "not ready";
        string lobby = lobbyVoiceOn == true ? "on" : lobbyVoiceOn == false ? "off" : "starting";
        string playback = voicePlayback == true ? "on" : voicePlayback == false ? "off" : "unknown";
        string stateLogKey = _welcomeLobbyKey + "|" + lobby + "|" + mode + "|" + playback;
        if (!string.Equals(_welcomeVoiceStateLogKey, stateLogKey, StringComparison.Ordinal))
        {
            _welcomeVoiceStateLogKey = stateLogKey;
            Log?.LogInfo("Welcome voice-state check: lobbyVoice=" + lobby + ", micMode=" + mode + ", voicePlayback=" + playback + ". No microphone device name was logged.");
        }

        return VoiceWelcomePolicy.Build(voicePlayback, lobbyVoiceOn, pushToTalk);
    }

    private void RefreshCompatibility(string source)
    {
        string[] loadedGuids = Chainloader.PluginInfos.Keys.ToArray();
        PluginCompatibilityResult previous = _compatibility;

        _compatibility = PluginCompatibility.Evaluate(
            loadedGuids,
            AutoDisableSweepWhenAndrewSweepInstalled.Value,
            AllowManualSweepWhenAndrewSweepInstalled.Value,
            AutoDisableChatTweaksWhenSimpleQoLInstalled.Value);

        Log.LogInfo($"Compatibility scan ({source}) checked {loadedGuids.Length} loaded BepInEx plugin IDs only. Disabled, renamed, old, or backup DLL files are ignored.");

        foreach (string reason in _compatibility.Reasons)
        {
            Log.LogWarning(reason);
        }

        if (IsPluginLoaded(PluginCompatibility.LegacyCopyChatGuid))
        {
            Log.LogWarning("Legacy CopyChat is loaded; BlueSage Chat Copy is disabled to avoid duplicate click handlers.");
        }

        if (previous != null &&
            (previous.AutoSweepAllowed != _compatibility.AutoSweepAllowed ||
             previous.ManualSweepAllowed != _compatibility.ManualSweepAllowed ||
             previous.ChatTweaksAllowed != _compatibility.ChatTweaksAllowed ||
             previous.HostHealthRepairsAllowed != _compatibility.HostHealthRepairsAllowed))
        {
            Log.LogInfo($"Compatibility changed after {source}. Restarting BlueSage-controlled maintenance loops with updated ownership.");
            RestartSweepLoop();
            RestartHostHealthLoop();
        }
    }

    internal static string GetDefensiveCompatibilitySummary()
    {
        PluginCompatibilityResult compatibility = Instance?._compatibility;
        if (compatibility == null)
        {
            return "Still checking which mods are installed. Try again in a moment.";
        }

        // Voice pass (2026-10-23): plain sentences, other mods by their mod name only (no modder names).
        var parts = new List<string>();
        if (compatibility.HushLoaded)
            parts.Add("Hush is installed: it shares the game's ban list; BlueSage still asks you to confirm each ban.");
        if (compatibility.EchoLoaded)
            parts.Add("Echo is installed: it can copy names and outfits on purpose, so Clone Shield treats lookalikes as clues, not proof.");
        if (compatibility.DesyncLoaded)
            parts.Add("Desync is installed: it handles lobby repairs, so BlueSage Host Health just watches.");
        if (compatibility.ChalkyLoaded)
            parts.Add("Chalky is installed: it saves the chalkboards, so BlueSage leaves them alone.");
        if (compatibility.FomoLoaded)
            parts.Add("Fomo is installed: it may read visible chat; assigned Helper actions travel outside chat.");
        if (compatibility.AlphaLoaded && parts.Count == 0)
            parts.Add("Alpha is installed: nothing in BlueSage overlaps with it.");

        return parts.Count == 0
            ? "No other safety mods found that overlap with BlueSage."
            : string.Join(" ", parts.ToArray());
    }

    private IEnumerator RefreshCompatibilityAfterLoadSettles()
    {
        yield return new WaitForSecondsRealtime(3f);
        RefreshCompatibility("delayed post-load recheck");
        _compatibilityRefreshCoroutine = null;
    }

    private void RestartSweepLoop()
    {
        if (_sweepCoroutine != null)
        {
            StopCoroutine(_sweepCoroutine);
            _sweepCoroutine = null;
        }
        _sweepSchedule.Reset();

        if (EnableAutoSweep.Value && AutoSweepAllowed)
        {
            EnsureSweepBackgroundExecution();
            _sweepSchedule.Start(Time.realtimeSinceStartupAsDouble, SweepIntervalMinutes.Value);
            _sweepCoroutine = StartCoroutine(SweepLoop());
            return;
        }

        ReleaseSweepBackgroundExecution();
    }

    private void EnsureSweepBackgroundExecution()
    {
        if (_ownsSweepRunInBackgroundOverride || Application.runInBackground)
        {
            return;
        }

        _runInBackgroundBeforeSweepOverride = Application.runInBackground;
        Application.runInBackground = true;
        _ownsSweepRunInBackgroundOverride = true;
        Log?.LogInfo("Asset Sweep enabled background-safe scheduling for its real-time timer; the prior Unity run-in-background state will be restored when automatic Sweep stops.");
    }

    private void ReleaseSweepBackgroundExecution()
    {
        if (!_ownsSweepRunInBackgroundOverride)
        {
            return;
        }

        bool priorState = _runInBackgroundBeforeSweepOverride;
        _ownsSweepRunInBackgroundOverride = false;
        _runInBackgroundBeforeSweepOverride = false;
        Application.runInBackground = priorState;
        Log?.LogInfo($"Asset Sweep restored Unity run-in-background to {priorState}.");
    }

    private IEnumerator SweepLoop()
    {
        while (true)
        {
            double nowSeconds = Time.realtimeSinceStartupAsDouble;
            _sweepSchedule.ReconcileInterval(nowSeconds, SweepIntervalMinutes.Value);
            bool automaticDue = _sweepSchedule.IsDue(nowSeconds);
            // Round 4 Stage G (Marbles, 2026-10-20): never start a sweep just before or after the game's own 180 s
            // autosave; the two stalls would stack. Retried every second until the quiet middle of the period.
            if (automaticDue &&
                TryReadAutosaveTimerSeconds(out float autosaveTimerSeconds) &&
                AutosaveBoundaryPolicy.ShouldDefer(autosaveTimerSeconds))
            {
                if (!_sweepAutosaveDeferralLogged)
                {
                    _sweepAutosaveDeferralLogged = true;
                    float untilAutosave = Math.Max(0f, AutosaveBoundaryPolicy.AutosavePeriodSeconds - autosaveTimerSeconds);
                    Log.LogDebug($"[BlueSageSweep] automatic sweep deferred: the game autosaves in {untilAutosave:0}s or just did; retrying each second.");
                }

                yield return new WaitForSecondsRealtime(1f);
                continue;
            }

            _sweepAutosaveDeferralLogged = false;
            bool vanillaStateAvailable = false;
            float vanillaTimerSeconds = 0f;
            float vanillaDurationSeconds = 0f;
            bool vanillaSweepActive = false;
            if (automaticDue)
            {
                vanillaStateAvailable = ReadNativeSweepCoordinationState(
                    out vanillaTimerSeconds,
                    out vanillaDurationSeconds,
                    out vanillaSweepActive);
            }

            NativeSweepCoordinationDecision decision = NativeAssetSweepPolicy.DecideAutomaticSweep(
                automaticDue,
                _sweepOperationGate.IsRunning,
                vanillaStateAvailable,
                vanillaTimerSeconds,
                vanillaDurationSeconds,
                vanillaSweepActive);
            if (decision == NativeSweepCoordinationDecision.Run)
            {
                RunSweep("automatic timer", requireManualPermission: false);
            }
            else if (decision == NativeSweepCoordinationDecision.SatisfiedByVanilla)
            {
                _sweepSchedule.MarkAutomaticAttemptFinished(
                    nowSeconds,
                    SweepIntervalMinutes.Value);
                Log.LogDebug("[BlueSageSweep] pending automatic interval satisfied by the observed vanilla cleanup boundary.");
            }

            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private static bool _sweepAutosaveDeferralLogged;
    private static bool _autosaveTimerFieldResolved;
    private static FieldInfo _autosaveTimerField;

    // Round 4 Stage G: the game's autosave clock (DataManager.saveTimer, private, counts up to 180 while the main
    // scene is loaded). Unknown (menu, missing field, read failure) reads as "no timer", which never defers.
    private static bool TryReadAutosaveTimerSeconds(out float seconds)
    {
        seconds = float.NaN;
        try
        {
            if (!_autosaveTimerFieldResolved)
            {
                _autosaveTimerFieldResolved = true;
                Type dataManagerType = AccessTools.TypeByName("DataManager");
                _autosaveTimerField = dataManagerType == null ? null : AccessTools.Field(dataManagerType, "saveTimer");
            }

            if (_autosaveTimerField == null || MonoSingleton<MainSceneManager>.I == null)
            {
                return false;
            }

            object manager = MonoSingleton<DataManager>.I;
            if (manager == null)
            {
                return false;
            }

            seconds = Convert.ToSingle(_autosaveTimerField.GetValue(manager));
            return true;
        }
        catch
        {
            seconds = float.NaN;
            return false;
        }
    }

    private static bool ReadNativeSweepCoordinationState(
        out float vanillaTimerSeconds,
        out float vanillaDurationSeconds,
        out bool vanillaSweepActive)
    {
        vanillaTimerSeconds = 0f;
        vanillaDurationSeconds = 0f;
        vanillaSweepActive = false;

        try
        {
            object manager = MonoSingleton<MainSceneManager>.I;
            object settings = MonoSingleton<DataManager>.I?.SettingsData;
            Type managerType = AccessTools.TypeByName("MainSceneManager");
            Type settingsType = AccessTools.TypeByName("SettingsData");
            FieldInfo timerField = AccessTools.Field(managerType, "_sweepTimer");
            FieldInfo sweepUiField = AccessTools.Field(managerType, "_sweepUI");
            FieldInfo durationField = AccessTools.Field(settingsType, "SweepDuration");
            if (manager == null || settings == null ||
                timerField == null || sweepUiField == null || durationField == null)
            {
                return false;
            }

            vanillaTimerSeconds = Convert.ToSingle(timerField.GetValue(manager));
            vanillaDurationSeconds = Convert.ToSingle(durationField.GetValue(settings));
            GameObject sweepUi = sweepUiField.GetValue(manager) as GameObject;
            vanillaSweepActive = sweepUi != null && sweepUi.activeSelf;
            return true;
        }
        catch
        {
            vanillaTimerSeconds = 0f;
            vanillaDurationSeconds = 0f;
            vanillaSweepActive = false;
            return false;
        }
    }

    internal bool RunSweep(string reason, bool requireManualPermission)
    {
        _lastManualSweepRefusalNotice = string.Empty;
        if (requireManualPermission && !ManualSweepAllowed)
        {
            Log.LogWarning($"Skipping asset cleanup via {reason}: a compatibility guard is active.");
            AnswerManualSweep(ManualSweepPausedNotice, reason);
            _lastManualSweepRefusalNotice = ManualSweepPausedNotice;
            return false;
        }

        if (!_sweepOperationGate.TryStart(reason, out int sequence))
        {
            Log.LogInfo($"[BlueSageSweep] utc={DateTime.UtcNow:O}; phase=skip; seq={sequence}; trigger={SanitizeSweepLogValue(reason)}; activeTrigger={SanitizeSweepLogValue(_sweepOperationGate.ActiveReason)}; reason=operation-in-flight");
            if (requireManualPermission)
            {
                AnswerManualSweep(ManualSweepBusyNotice, reason);
                _lastManualSweepRefusalNotice = ManualSweepBusyNotice;
            }

            return false;
        }

        try
        {
            _activeSweepOperationCoroutine = StartCoroutine(CompleteSweepOperation(sequence, reason));
            return true;
        }
        catch (Exception ex)
        {
            _sweepOperationGate.Complete(sequence);
            _activeSweepOperationCoroutine = null;
            Log.LogWarning($"[BlueSageSweep] utc={DateTime.UtcNow:O}; phase=failed; seq={sequence}; trigger={SanitizeSweepLogValue(reason)}; error={ex.GetType().Name}");
            if (requireManualPermission)
            {
                AnswerManualSweep(ManualSweepFailedNotice, reason);
                _lastManualSweepRefusalNotice = ManualSweepFailedNotice;
            }

            return false;
        }
    }

    // The menu and /sweep answer with the same specific notice RunSweep just gave (voice pass 2026-10-23).
    private string _lastManualSweepRefusalNotice = string.Empty;

    // A manual cleanup always answers, once (round 5d review): the hotkey has no other reply, so it gets a chat line
    // and a toast (the line still arrives with Toast Pop-ups off); /sweep and the menu print the same sentence
    // themselves, so they only get the toast.
    private void AnswerManualSweep(string notice, string reason)
    {
        if (string.Equals(reason, "manual hotkey", StringComparison.Ordinal))
        {
            AddLocalNotificationWithToast(notice);
        }
        else
        {
            ShowToast(notice);
        }
    }

    private string DescribeManualSweep(bool started)
    {
        return SweepMenuActionPolicy.BuildCompletionMessage(started, _lastManualSweepRefusalNotice);
    }

    // Round 5d (Blue: "more toasts not less"): a cleanup you asked for always answers, even when it cannot run.
    // Voice pass (2026-10-23): the wording lives in SweepMenuActionPolicy ("cleanup", never sweep/tidy/asset).
    internal const string ManualSweepPausedNotice = SweepMenuActionPolicy.PausedNotice;
    internal const string ManualSweepBusyNotice = SweepMenuActionPolicy.BusyNotice;
    internal const string ManualSweepFailedNotice = SweepMenuActionPolicy.StartFailedNotice;
    internal const string ManualSweepRunFailedNotice = SweepMenuActionPolicy.RunFailedNotice;
    internal const string SweepCompletedNotice = SweepMenuActionPolicy.CompletedNotice;
    internal const string ReconnectBackInNotice = "You're back in the lobby. Welcome back!";

    internal static bool IsQolSweepCreatingNativeRoutine { get; private set; }

    private static bool TryCreateNativeSweepRoutine(
        out IEnumerator nativeSweepRoutine)
    {
        nativeSweepRoutine = null;

        try
        {
            object manager = MonoSingleton<MainSceneManager>.I;
            if (manager == null)
            {
                return false;
            }

            MethodInfo sweepMethod = AccessTools.Method(
                manager.GetType(),
                "SweepRoutine",
                Type.EmptyTypes);
            if (sweepMethod == null)
            {
                return false;
            }

            // The VanillaSweepToastPatch postfix runs synchronously inside this Invoke; the flag tells
            // it this iterator is BlueSage's own sweep, not the game's 30-minute cleanup.
            IsQolSweepCreatingNativeRoutine = true;
            try
            {
                nativeSweepRoutine =
                    sweepMethod.Invoke(manager, null) as IEnumerator;
            }
            finally
            {
                IsQolSweepCreatingNativeRoutine = false;
            }
            return nativeSweepRoutine != null;
        }
        catch (Exception ex)
        {
            Log?.LogWarning(
                $"Native sweep routine was unavailable; using compatibility cleanup: {ex.GetType().Name}: {ex.Message}");
            nativeSweepRoutine = null;
            return false;
        }
    }

    private IEnumerator CompleteSweepOperation(int sequence, string reason)
    {
        long managedBeforeBytes = GC.GetTotalMemory(false);
        // Round 5 Stage D: Unity's own counters around the sweep, so the extra pass is measured, not argued.
        SweepMemoryProbe memoryBefore = SweepMemoryProbe.Capture();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Log.LogInfo($"[BlueSageSweep] utc={DateTime.UtcNow:O}; phase=start; seq={sequence}; trigger={SanitizeSweepLogValue(reason)}; managedBeforeMiB={managedBeforeBytes / 1048576d:0.0}; forceGc={ForceManagedGcCollect.Value}; nativeAllocMiB={SweepMemoryReadingPolicy.FormatMiB(memoryBefore.AllocatedBytes)}; nativeReservedMiB={SweepMemoryReadingPolicy.FormatMiB(memoryBefore.ReservedBytes)}; monoHeapMiB={SweepMemoryReadingPolicy.FormatMiB(memoryBefore.MonoHeapBytes)}; monoUsedMiB={SweepMemoryReadingPolicy.FormatMiB(memoryBefore.MonoUsedBytes)}");

        bool nativeSweepFailed = false;
        if (TryCreateNativeSweepRoutine(out IEnumerator nativeSweepRoutine))
        {
            while (true)
            {
                bool movedNext;
                object current = null;
                try
                {
                    movedNext = nativeSweepRoutine.MoveNext();
                    if (movedNext)
                    {
                        current = nativeSweepRoutine.Current;
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning(
                        $"[BlueSageSweep] utc={DateTime.UtcNow:O}; phase=native-fallback; seq={sequence}; trigger={SanitizeSweepLogValue(reason)}; error={ex.GetType().Name}");
                    nativeSweepFailed = true;
                    break;
                }

                if (!movedNext)
                {
                    break;
                }

                yield return current;
            }
        }

        if (nativeSweepRoutine == null || nativeSweepFailed)
        {
            AsyncOperation operation;
            try
            {
                operation = Resources.UnloadUnusedAssets();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log.LogWarning($"[BlueSageSweep] utc={DateTime.UtcNow:O}; phase=failed; seq={sequence}; trigger={SanitizeSweepLogValue(reason)}; elapsedMs={stopwatch.ElapsedMilliseconds}; error={ex.GetType().Name}");
                FinishSweepOperation(sequence, reason);
                // Voice pass NOISE 5: a cleanup the player asked for answers even when it fails mid-run.
                if (SweepMenuActionPolicy.ShouldAnnounceRunFailure(reason))
                {
                    AddLocalNotificationWithToast(ManualSweepRunFailedNotice);
                }

                yield break;
            }

            if (operation != null)
            {
                yield return operation;
            }
        }

        if (ForceManagedGcCollect.Value)
        {
            GC.Collect();
        }

        stopwatch.Stop();
        long managedAfterBytes = GC.GetTotalMemory(false);
        double managedDeltaMiB = (managedAfterBytes - managedBeforeBytes) / 1048576d;
        SweepMemoryProbe memoryAfter = SweepMemoryProbe.Capture();
        Log.LogInfo($"[BlueSageSweep] utc={DateTime.UtcNow:O}; phase=complete; seq={sequence}; trigger={SanitizeSweepLogValue(reason)}; elapsedMs={stopwatch.ElapsedMilliseconds}; managedBeforeMiB={managedBeforeBytes / 1048576d:0.0}; managedAfterMiB={managedAfterBytes / 1048576d:0.0}; managedDeltaMiB={managedDeltaMiB:+0.0;-0.0;0.0}; forceGc={ForceManagedGcCollect.Value}; nativeAllocAfterMiB={SweepMemoryReadingPolicy.FormatMiB(memoryAfter.AllocatedBytes)}; nativeAllocDeltaMiB={SweepMemoryReadingPolicy.FormatDeltaMiB(memoryBefore.AllocatedBytes, memoryAfter.AllocatedBytes)}; nativeReservedAfterMiB={SweepMemoryReadingPolicy.FormatMiB(memoryAfter.ReservedBytes)}; monoHeapAfterMiB={SweepMemoryReadingPolicy.FormatMiB(memoryAfter.MonoHeapBytes)}; monoUsedAfterMiB={SweepMemoryReadingPolicy.FormatMiB(memoryAfter.MonoUsedBytes)}; monoUsedDeltaMiB={SweepMemoryReadingPolicy.FormatDeltaMiB(memoryBefore.MonoUsedBytes, memoryAfter.MonoUsedBytes)}");
        SessionSummaryController.NoteSweep();

        FinishSweepOperation(sequence, reason);
        // Round 5 F: automatic sweeps are silent unless asked for; a cleanup the user triggered always confirms.
        if (SweepSettingsPolicy.ShouldNotifyOnSweep(NotifyOnSweep.Value, reason))
        {
            AddLocalNotification(SweepCompletedNotice);
            ShowToast(SweepCompletedNotice);
        }
    }

    private void FinishSweepOperation(int sequence, string reason)
    {
        _sweepOperationGate.Complete(sequence);
        _activeSweepOperationCoroutine = null;
        if (string.Equals(reason, "automatic timer", StringComparison.Ordinal))
        {
            _sweepSchedule.MarkAutomaticAttemptFinished(
                Time.realtimeSinceStartupAsDouble,
                SweepIntervalMinutes.Value);
        }
    }

    private static string SanitizeSweepLogValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unspecified";
        return value.Trim().Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
    }

    internal static string BuildTimestampPrefix()
    {
        return TimestampFormatter.BuildPrefix(DateTime.Now, Use24HourTime.Value, TimestampSizePercent.Value, TimestampColor.Value);
    }

    // Round 5h: one note, once per player, the first time the avatar panel opens with the extra outfit slots in place.
    internal static void ShowOutfitSlotsHintOnce(int extraRowsReady)
    {
        if (Instance == null || OutfitSlotsHintShown == null)
        {
            return;
        }

        if (!OutfitSlotsHintPolicy.ShouldShow(OutfitSlotsHintShown.Value, EnableToastNotifications?.Value == true, extraRowsReady))
        {
            return;
        }

        Instance.ShowToast(OutfitSlotsHintPolicy.Notice);
        OutfitSlotsHintShown.Value = true;
        Instance.Config.Save();
    }

    // Chat line plus a toast: for events a player on a small screen could easily miss in busy chat.
    internal static bool AddLocalNotificationWithToast(string text)
    {
        Instance?.ShowToast(text);
        return AddLocalNotification(text);
    }

    internal void ShowHostAnnouncementToast(string text)
    {
        if (EnableHostAnnouncements?.Value == true)
        {
            ShowToastUnchecked(text, pinned: true);
        }
    }

    internal static bool AddLocalNotification(string text)
    {
        try
        {
            TextChannelManager textChannelManager = NetworkSingleton<TextChannelManager>.I;
            if (textChannelManager == null)
            {
                return false;
            }

            textChannelManager.AddNotification(text);
            return true;
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Could not show local notification: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    internal static string FormatBlueSageNotice(string message)
    {
        return $"<color=#70D6FF><b>BlueSage</b></color> {message}";
    }

    internal static string FormatBlueSageWarning(string message)
    {
        return $"<color=#FFD45D><b>BlueSage</b></color> {message}";
    }

    internal static string FormatBlueSageAction(string action, string detail, bool warning = false)
    {
        string brandColor = warning ? "FFD45D" : "70D6FF";
        string cleanAction = string.IsNullOrWhiteSpace(action) ? "Notice" : action.Trim();
        string cleanDetail = string.IsNullOrWhiteSpace(detail) ? string.Empty : " " + detail.Trim();
        return $"<color=#{brandColor}><b>BlueSage</b></color> <color=#FFD700><b>{cleanAction}</b></color>{cleanDetail}";
    }

    internal static bool HandleOutgoingChatCommand(byte[] message)
    {
        if (Instance == null || message == null || Instance._commandRegistry == null)
        {
            return true;
        }

        string text;
        try
        {
            text = Encoding.Unicode.GetString(message).Trim();
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"BlueSage command decode failed: {ex.GetType().Name}: {ex.Message}");
            return true;
        }

        BlueSageCommandResult result = Instance._commandRegistry.TryExecute(text);
        if (!result.Handled)
        {
            return true;
        }

        foreach (string response in result.Messages)
        {
            AddLocalNotification(response);
        }

        string loggedCommand = BlueSageCommandRegistry.TryParse(text, out string commandName, out string commandArguments)
            ? commandName.ToLowerInvariant()
            : "unknown";

        // Round 5h: commands that change something answer with a toast too, like the matching QoL menu switch.
        if (result.Messages.Count > 0)
        {
            BlueSageCommand matched = Instance._commandRegistry.Commands.FirstOrDefault(candidate => candidate.Matches(loggedCommand));
            string[] tokens = (commandArguments ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (CommandToastPolicy.ShouldToastFirstMessage(matched?.Name ?? loggedCommand, tokens))
            {
                Instance.ShowToast(ToastPolicy.Summarize(result.Messages[0]));
            }
        }

        Log?.LogInfo("BlueSage command handled locally: /" + loggedCommand + " (arguments redacted).");
        return false;
    }

    internal static IReadOnlyList<string> GetCommandSuggestions(string input, int maxResults)
    {
        if (Instance == null || Instance._commandRegistry == null)
        {
            return Array.Empty<string>();
        }

        return Instance._commandRegistry.Suggest(input, maxResults, CanSuggestCommand);
    }

    private void ApplyStyleStateMigration()
    {
        string configuredBase = (StatusBaseName?.Value ?? string.Empty).Trim();
        bool changed = false;
        bool baseChanged = false;
        bool usedPlainTextFallback = false;

        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            DetectedDisplayNameState normalized = StyleHelperNamePolicy.DetectCurrentState(
                configuredBase,
                configuredBase,
                StatusMessage?.Value ?? string.Empty,
                GetStatusColor(),
                StatusBrackets?.Value ?? "()",
                ShouldApplySpoons,
                SpoonCount?.Value ?? 5,
                GetSpoonLabel(),
                BuildStatusSuffix(),
                BuildSpoonSuffix());
            string normalizedBase = (normalized.BaseName ?? string.Empty).Trim();
            if (!string.Equals(normalizedBase, configuredBase, StringComparison.Ordinal))
            {
                if (StatusBaseNameLegacyBackup != null && string.IsNullOrEmpty(StatusBaseNameLegacyBackup.Value))
                {
                    StatusBaseNameLegacyBackup.Value = configuredBase;
                }
                StatusBaseName.Value = normalizedBase;
                baseChanged = true;
                usedPlainTextFallback = normalized.UsedPlainTextFallback;
                changed = true;
            }
        }

        if (StyleStateMigrationVersion != null &&
            !string.Equals(StyleStateMigrationVersion.Value, StyleStateMigrationTarget, StringComparison.Ordinal))
        {
            StyleStateMigrationVersion.Value = StyleStateMigrationTarget;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        Config.Save();
        if (baseChanged)
        {
            Log?.LogWarning(
                $"Status Helper repaired legacy local style state without exposing its contents. " +
                $"beforeLength={configuredBase.Length}, afterLength={(StatusBaseName.Value ?? string.Empty).Length}, " +
                $"plainTextFallback={usedPlainTextFallback}. The original remains in the local rollback config entry.");
        }
    }

    internal static bool CanUseLobbySafety(out bool isHost)
    {
        return SteamIdModerationController.CanLocalIssue(out isHost, out _);
    }

    internal static bool ShouldShowLobbySafetyTab(out LobbySafetyAccessState state, out string reason)
    {
        return SteamIdModerationController.ShouldShowLocalSurface(out state, out reason);
    }

    internal static bool IsStyleHelperVisible => Instance?._styleHelperWindow?.IsVisible == true;

    internal static bool CanManageHelpers()
    {
        return CanUseLobbySafety(out bool isHost) && isHost;
    }

    internal static bool IsHelperManagementCommandName(string commandName)
    {
        string normalized = (commandName ?? string.Empty).Trim().TrimStart('/').ToLowerInvariant();
        return normalized == "helper" || normalized == "lobbyhelper" || normalized == "lobbymod" ||
               normalized == "modhelper" || normalized == "moderationhelper";
    }

    internal static bool IsLobbySafetyCommandName(string commandName)
    {
        string normalized = (commandName ?? string.Empty).Trim().TrimStart('/').ToLowerInvariant();
        return IsHelperManagementCommandName(normalized) || normalized == "sidlist" ||
               normalized == "sidwho" || normalized == "sidlookup" || normalized == "sidban" ||
               normalized == "sidunban" || normalized == "unbansid" ||
               normalized == "sidkick" || normalized == "sidmute" || normalized == "sidunmute" ||
               normalized == "sidincident" || normalized == "cloneincident";
    }

    private static bool CanSuggestCommand(BlueSageCommand command)
    {
        if (command == null) return false;
        if (command.Matches("helper")) return CanManageHelpers();
        if (command.Matches("sidlist") || command.Matches("sidwho") ||
            command.Matches("sidban") || command.Matches("sidunban") || command.Matches("sidincident") ||
            command.Matches("sidkick") || command.Matches("sidmute") || command.Matches("sidunmute"))
            return CanUseLobbySafety(out _);
        return true;
    }

    internal static IReadOnlyList<string> GetModerationTargetSuggestions(bool unban)
    {
        if (!CanUseLobbySafety(out bool isHost)) return Array.Empty<string>();
        if (unban)
        {
            // The native ban list belongs to the host. A Helper's local list is
            // not authoritative and must never be offered as a host target.
            if (!isHost) return Array.Empty<string>();
            return SteamIdModerationController.GetBannedSteamIds().Take(12).ToArray();
        }

        string[] incidents = CloneIncidentLedger.Snapshot
            .Reverse()
            .Take(12)
            .Select(item => item.Id)
            .ToArray();
        return incidents.Length > 0 ? incidents : Array.Empty<string>();
    }

    internal static IReadOnlyList<string> GetIdentityLookupSuggestions()
    {
        if (!CanUseLobbySafety(out _)) return Array.Empty<string>();
        return PlayerIdentityEvidenceController.GetLookupSuggestions();
    }

    internal string ShowCloneEvidenceFromQolMenu()
    {
        BlueSageCommandResult result = HandleSidListCommand(new BlueSageCommandContext("/sidlist", "sidlist", string.Empty));
        foreach (string line in result.Messages) AddLocalNotification(line);
        return result.Messages.Count > 1
            ? "Clone Shield evidence was sent to local chat."
            : result.Messages.FirstOrDefault() ?? "No Clone Shield evidence is available.";
    }

    internal string CheckModerationAccessFromQolMenu()
    {
        LobbySafetyAccessState state = SteamIdModerationController.GetLocalAccessState(out _, out string reason);
        string message = state switch
        {
            LobbySafetyAccessState.Host => "Helper / Safety access: you are the current lobby host.",
            LobbySafetyAccessState.HelperReady => "Helper / Safety access: this host added you as a Helper. The host still rechecks and performs each moderation action.",
            LobbySafetyAccessState.HelperWaitingForHost => "Helper / Safety waiting: " + reason,
            _ => "Helper / Safety access: " + reason
        };
        AddLocalNotification(message);
        return message;
    }

    internal string UpdateHelperFromQolMenu(string steamId, bool add)
    {
        string action = add ? "add " : "remove ";
        BlueSageCommandResult result = HandleHelperCommand(new BlueSageCommandContext("/helper " + action + steamId, "helper", action + steamId));
        string message = result.Messages.FirstOrDefault() ?? "Helpers: no change.";
        AddLocalNotification(message);
        return message;
    }

    internal string ResetHelpersFromQolMenu()
    {
        BlueSageCommandResult result = HandleHelperCommand(new BlueSageCommandContext("/helper reset", "helper", "reset"));
        string message = result.Messages.FirstOrDefault() ?? "Helpers: no change.";
        AddLocalNotification(message);
        return message;
    }

    internal string PreviewSidBanFromQolMenu(string steamId)
    {
        BlueSageCommandResult result = HandleSidBanCommand(new BlueSageCommandContext("/sidban " + steamId, "sidban", steamId));
        foreach (string line in result.Messages) AddLocalNotification(line);
        return result.Messages.FirstOrDefault() ?? "SID ban preview was not available.";
    }

    internal string ConfirmSidBanFromQolMenu(string steamId)
    {
        BlueSageCommandResult result = HandleSidBanCommand(new BlueSageCommandContext("/sidban " + steamId + " confirm", "sidban", steamId + " confirm"));
        foreach (string line in result.Messages) AddLocalNotification(line);
        return result.Messages.FirstOrDefault() ?? "SID ban confirmation was not available.";
    }

    // Round 4 Stage E #3: the selected-row Kick and Mute buttons reuse the command flow (preview, then confirm).
    internal string PreviewSidLiveActionFromQolMenu(string action, string steamId)
    {
        BlueSageCommandResult result = HandleSidLiveActionCommand(new BlueSageCommandContext("/sid" + action + " " + steamId, "sid" + action, steamId), action, "SID " + action);
        foreach (string line in result.Messages) AddLocalNotification(line);
        return result.Messages.FirstOrDefault() ?? "SID " + action + " preview was not available.";
    }

    internal string ConfirmSidLiveActionFromQolMenu(string action, string steamId)
    {
        BlueSageCommandResult result = HandleSidLiveActionCommand(new BlueSageCommandContext("/sid" + action + " " + steamId + " confirm", "sid" + action, steamId + " confirm"), action, "SID " + action);
        foreach (string line in result.Messages) AddLocalNotification(line);
        return result.Messages.FirstOrDefault() ?? "SID " + action + " confirmation was not available.";
    }

    internal string PreviewSidUnbanFromQolMenu(string steamId)
    {
        BlueSageCommandResult result = HandleSidUnbanCommand(new BlueSageCommandContext("/sidunban " + steamId, "sidunban", steamId));
        foreach (string line in result.Messages) AddLocalNotification(line);
        return result.Messages.FirstOrDefault() ?? "SID unban preview was not available.";
    }

    internal string ConfirmSidUnbanFromQolMenu(string steamId)
    {
        BlueSageCommandResult result = HandleSidUnbanCommand(new BlueSageCommandContext("/sidunban " + steamId + " confirm", "sidunban", steamId + " confirm"));
        foreach (string line in result.Messages) AddLocalNotification(line);
        return result.Messages.FirstOrDefault() ?? "SID unban confirmation was not available.";
    }

    internal ModerationConfirmationReadiness GetModerationConfirmationReadiness(string action, string target)
    {
        PendingModerationAction pending = _pendingModerationAction;
        if (pending == null)
        {
            return new ModerationConfirmationReadiness(false, 0);
        }

        return ModerationProtocolPolicy.EvaluateConfirmation(
            action,
            target,
            MonoSingleton<MultiplayerManager>.I?.LobbyCode ?? string.Empty,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            pending.Action,
            pending.TargetSteamId,
            pending.IncidentId,
            pending.LobbyCode,
            pending.ExpiresUnixSeconds);
    }

    internal string CopySteamIdFromQolMenu(string steamId)
    {
        if (!CanUseLobbySafety(out _)) return SteamIdModerationController.LobbySafetyDeniedMessage;
        if (!PlayerIdentityEvidenceController.TryResolveExact(steamId, out PlayerIdentityEvidence evidence, out string error))
            return "Steam ID copy failed safely: " + error + ". Re-select a verified active row.";
        GUIUtility.systemCopyBuffer = evidence.SteamId;
        return "Copied exact SteamID64 " + evidence.SteamId + ".";
    }

    internal string CopySavedBanSteamIdFromQolMenu(string steamId)
    {
        if (!CanManageHelpers()) return "Only the verified current host can copy saved native-ban identities.";
        bool recordsAvailable = SteamIdModerationController.TryGetNativeBanRecords(out IReadOnlyList<NativeBanRecord> records, out string error);
        if (!ModerationProtocolPolicy.IsSteamId64(steamId) || !recordsAvailable ||
            !records.Any(record => string.Equals(record.SteamId, steamId, StringComparison.Ordinal)))
        {
            string reason = string.IsNullOrWhiteSpace(error) ? "that exact native-ban row is no longer available" : error;
            return "Saved SteamID64 copy failed safely: " + reason + ". Refresh the list.";
        }

        GUIUtility.systemCopyBuffer = steamId;
        return "Copied saved native-ban SteamID64 " + steamId + ".";
    }

    internal string CopyIdentityEvidenceFromQolMenu(string steamId)
    {
        if (!CanUseLobbySafety(out _)) return SteamIdModerationController.LobbySafetyDeniedMessage;
        if (!PlayerIdentityEvidenceController.TryResolveExact(steamId, out PlayerIdentityEvidence evidence, out string error))
            return "Identity evidence copy failed safely: " + error + ". Re-select a verified active row.";
        GUIUtility.systemCopyBuffer = PlayerIdentityEvidenceController.BuildCopyableEvidence(evidence);
        return "Copied the verified live identity record for row #" + (evidence.RosterIndex + 1) + " and Steam …" + PlayerIdentityEvidenceController.Suffix(evidence.SteamId) + ".";
    }

    internal string OpenSteamProfileFromQolMenu(string steamId)
    {
        if (!CanUseLobbySafety(out _)) return SteamIdModerationController.LobbySafetyDeniedMessage;
        if (!PlayerIdentityEvidenceController.TryResolveExact(steamId, out PlayerIdentityEvidence evidence, out string error) ||
            !SteamManager.Initialized || !ulong.TryParse(evidence.SteamId, out ulong parsed) || parsed == 0UL)
            return "Steam profile could not open safely: " + error + ". Re-select a verified active row.";
        SteamFriends.ActivateGameOverlayToUser("steamid", new CSteamID(parsed));
        return "Opened the selected Steam profile for verification.";
    }

    internal string OpenOfflineSteamProfileFromQolMenu(string steamId)
    {
        if (!CanManageHelpers()) return "Only the verified current host can inspect an off-roster profile from this form.";
        if (!ModerationProtocolPolicy.IsSteamId64(steamId) || !ulong.TryParse(steamId, out ulong parsed) || parsed == 0UL)
            return "Steam profile could not open safely: enter one exact 17-digit SteamID64.";

        IReadOnlyList<string> currentSteamIds = PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIds(out string rosterError);
        if (!string.IsNullOrWhiteSpace(rosterError))
            return "Steam profile could not open safely while current lobby coverage is incomplete: " + rosterError;
        if (currentSteamIds.Contains(steamId, StringComparer.Ordinal))
            return "That SteamID64 is currently in the lobby. Select its verified live row instead.";
        if (!SteamManager.Initialized)
            return "Steam profile could not open safely because Steam is not initialized.";

        SteamFriends.ActivateGameOverlayToUser("steamid", new CSteamID(parsed));
        return "Opened the exact off-roster Steam profile for verification.";
    }

    private void RegisterBlueSageCommands()
    {
        _commandRegistry = new BlueSageCommandRegistry();
        WorkRelaxCommandPolicy.RegisterCommands(
            _commandRegistry,
            () => WorkRelaxRuntimeController.TryGetCurrentMode(out WorkRelaxMode mode)
                ? mode
                : (WorkRelaxMode?)null,
            WorkRelaxRuntimeController.SetMode);
        _commandRegistry.Register(new BlueSageCommand(
            "bsqol",
            "Open the BlueSage guide pop-up (status, hosthealth for checks)",
            new[] { "qol", "guide", "faq" },
            HandleBlueSageQoLCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "fishaid",
            "Accessibility fishing aid: take out your rod (H) near water and it fishes for you (/fishaid on|off|status)",
            new[] { "autofish" },
            context => context.Tokens.Count > 0 && string.Equals(context.Tokens[0], "status", StringComparison.OrdinalIgnoreCase)
                ? BlueSageCommandResult.HandledWithMessages(Patches.FishingAidRuntime.Status())
                : HandleToggleCommand(context, "Fishing Aid", EnableFishingAid)));
        _commandRegistry.Register(new BlueSageCommand(
            "motd",
            "Host: set the welcome QoL players see when they join (/motd <text>, /motd clear, /motd to view)",
            Array.Empty<string>(),
            HandleMotdCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "announce",
            "Host or Helper: send a heads-up toast to everyone with QoL (restarts, breaks)",
            Array.Empty<string>(),
            context => BlueSageCommandResult.HandledWithMessages(LobbyAnnouncementController.Send(string.Join(" ", context.Tokens)))));
        _commandRegistry.Register(new BlueSageCommand(
            "assetsweep",
            "Toggle BlueSage Automatic Cleanup or run one cleanup now",
            new[] { "sweep", "as" },
            HandleAssetSweepCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "rebind",
            "Change the key for the QoL menu, Style Helper, Fly, or Run Cleanup",
            new[] { "bind", "kb", "keys" },
            HandleRebindCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "welcome",
            "Reopen the welcome overlay now, even if you closed it or turned it off",
            new[] { "howto" },
            HandleWelcomeOverlayCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "hosthealth",
            "Toggle Host Health or run a check now",
            new[] { "hh" },
            context => HandleToggleCommand(context, "Host Health", EnableHostHealthMonitor, RestartHostHealthLoop, "run", () => RunHostHealthCheck("manual command"))));
        _commandRegistry.Register(new BlueSageCommand(
            "leavenotices",
            "Toggle leave notices",
            new[] { "ln" },
            context => HandleToggleCommand(context, "Leave Notices", EnableLeaveNotifications)));
        _commandRegistry.Register(new BlueSageCommand(
            "timestamps",
            "Toggle chat and notification timestamps",
            new[] { "tt" },
            HandleTimestampCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chatreadability",
            "Toggle local chat text outline and shadow",
            new[] { "chatread" },
            context => HandleToggleCommand(context, "Chat Readability", EnableChatReadability)));
        _commandRegistry.Register(new BlueSageCommand(
            "chatbackdrop",
            "Toggle persistent local chat backdrop readability",
            new[] { "chatbg" },
            context => HandleToggleCommand(context, "Chat Backdrop", EnablePersistentChatBackdrop)));
        _commandRegistry.Register(new BlueSageCommand(
            "chatoutline",
            "Set chat outline intensity from 25 to 100",
            new[] { "outline" },
            HandleChatOutlineCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chatscale",
            "Set whole local chat UI accessibility scale from 75 to 200 percent",
            new[] { "chatresize", "cr" },
            HandleChatScaleCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chatfont",
            "Set local chat message font size from 75 to 200 percent",
            new[] { "chatfontsize", "cf" },
            HandleChatFontCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chatheight",
            "Resize local chat window height from 100 to 200 percent without scaling text",
            new[] { "chatstretch", "chath" },
            HandleChatHeightCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chatrows",
            "Set retained chat rows from 25 to 250; default/reset is 150",
            new[] { "chathistory", "chatr" },
            HandleChatHistoryCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chatvanilla",
            "Reset whole chat UI scale, message font size, and window height to vanilla",
            new[] { "chatreset", "chatdefault" },
            HandleChatVanillaCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chatoutlinecolor",
            "Set normal chat outline color as hex",
            new[] { "outlinecolor" },
            context => HandleOutlineColorCommand(context, "Normal outline", ChatOutlineColor, "000000")));
        _commandRegistry.Register(new BlueSageCommand(
            "blackoutlinecolor",
            "Set true-black chat/name outline color as hex",
            new[] { "blackchatoutline", "blackoutline" },
            context => HandleOutlineColorCommand(context, "Black text outline", BlackChatOutlineColor, "C0C0C0")));
        _commandRegistry.Register(new BlueSageCommand(
            "bettermove",
            "Toggle Shift run and Ctrl slow-walk",
            new[] { "bm" },
            context => HandleToggleCommand(context, "BetterMove", EnableBetterMove)));
        _commandRegistry.Register(new BlueSageCommand(
            "unlimitedconsumables",
            "Toggle reusable canteen items and chalk",
            new[] { "consumables", "infiniteconsumables" },
            context => HandleToggleCommand(context, "Reusable Consumables", EnableUnlimitedConsumables)));
        _commandRegistry.Register(new BlueSageCommand(
            "reconnectguard",
            "Toggle Reconnect Guard",
            new[] { "rg" },
            context => HandleToggleCommand(context, "Reconnect Guard", EnableReconnectGuard, () =>
            {
                if (!EnableReconnectGuard.Value)
                {
                    StopManagedReconnect("disabled by command");
                }
            })));
        _commandRegistry.Register(new BlueSageCommand(
            "reconnectmessage",
            "Toggle reconnect success message",
            new[] { "rm" },
            context => HandleToggleCommand(context, "Reconnect Message", EnableReconnectAnnouncement)));
        _commandRegistry.Register(new BlueSageCommand(
            "focusanywhere",
            "Toggle Focus Anywhere",
            new[] { "fa" },
            context => HandleToggleCommand(context, "Focus Anywhere", EnableFocusAnywhere)));
        _commandRegistry.Register(new BlueSageCommand(
            "minimaplabels",
            "Toggle local minimap dot-to-ID-card clicks",
            new[] { "mml" },
            context => HandleToggleCommand(context, "MiniMap ID Click", EnableMiniMapPlayerLabels)));
        _commandRegistry.Register(new BlueSageCommand(
            "cloneshield",
            "Toggle public alerts when someone newly copies your avatar or styled name",
            new[] { "cloneguard", "identityshield" },
            context => HandleToggleCommand(context, "Clone Shield", EnableCloneShield)));
        _commandRegistry.Register(new BlueSageCommand(
            "playersaver",
            "Toggle local-only distant player render saver for large lobbies",
            new[] { "rendercull", "playercull" },
            HandlePlayerRenderSaverCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "communitybans",
            "Local additive community BanData status or refresh",
            new[] { "communityban", "bansync" },
            HandleCommunityBanCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chalk",
            "View/save/delete local chalk data or request exact authorized load/clear actions",
            new[] { "chalkboard" },
            HandleChalkCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chalksave",
            "Save the current chalkboard locally: /chalksave name [board index]",
            Array.Empty<string>(),
            HandleChalkSaveCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chalkload",
            "Authorized exact shared chalkboard restore: /chalkload name board-index",
            new[] { "chalkrestore" },
            HandleChalkLoadCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "chalklist",
            "List local BlueSage chalkboard saves",
            Array.Empty<string>(),
            context => BlueSageCommandResult.HandledWithMessages(ChalkboardPersistenceController.List())));
        _commandRegistry.Register(new BlueSageCommand(
            "copychat",
            "Toggle right-click-to-copy chat messages",
            new[] { "ccopy" },
            context => HandleToggleCommand(context, "Chat Copy", EnableChatCopy)));
        _commandRegistry.Register(new BlueSageCommand(
            "chatlinks",
            "Toggle safe chat link opening",
            new[] { "clinks" },
            context => HandleToggleCommand(context, "Chat Links", EnableChatUrlLinks)));
        _commandRegistry.Register(new BlueSageCommand(
            "auditlog",
            "Local Audit: keep a copy of your chat and the game log on this computer",
            new[] { "sessionaudit", "localaudit" },
            HandleAuditLogCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "qolmenu",
            "Open the BlueSage QoL menu",
            new[] { "qm" },
            HandleQolMenuCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "selfmove",
            "Self movement: status, save, teleport, fly, speed, jump, gravity, or reset",
            new[] { "bluemove", "qolmove" },
            HandleSelfMovementCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "style",
            "Build styled name or status text you can paste",
            new[] { "sty" },
            HandleStyleCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "styleui",
            "Open Style Helper for names, status, Spoons, colors, and templates",
            new[] { "sui" },
            HandleStyleUiCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "setpingcolor",
            "Set BlueSage mention highlight color",
            Array.Empty<string>(),
            HandleSetPingColorCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "pingsound",
            "Toggle or choose BlueSage local ping sound",
            Array.Empty<string>(),
            HandlePingSoundCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "helper",
            "Host-managed helpers: add, remove, list, or reset",
            new[] { "lobbyhelper", "lobbymod", "modhelper", "moderationhelper" },
            HandleHelperCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "sidlist",
            "Show local Clone Shield incident evidence and Steam ID suffixes",
            Array.Empty<string>(),
            HandleSidListCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "sidincident",
            "Review, resolve, or locally clear Clone Shield incidents",
            new[] { "cloneincident" },
            HandleSidIncidentCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "sidwho",
            "Find verified live players by name, persona, SteamID digits, or regex",
            new[] { "sidlookup" },
            HandleSidWhoCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "idsync",
            "Identity status/local refresh, Host repair, or authenticated Helper request",
            new[] { "identitysync", "rostersync" },
            HandleIdentityResyncCommand));
        // Round 4 Stage E #3: kick (ban, wait until gone, unban) and lobby-wide mute, same preview + confirm flow,
        // same host check; Helpers relay through the host like /sidban.
        _commandRegistry.Register(new BlueSageCommand(
            "sidkick",
            "Preview or confirm a kick: the host bans, waits until the player is gone (max 20 s), then unbans",
            Array.Empty<string>(),
            HandleSidKickCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "sidmute",
            "Preview or confirm a lobby-wide mute: the host stops relaying that player's chat (this hosting session)",
            Array.Empty<string>(),
            HandleSidMuteCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "sidunmute",
            "Preview or confirm lifting a lobby-wide mute",
            Array.Empty<string>(),
            HandleSidUnmuteCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "sidban",
            "Preview or confirm a Steam ID ban checked by the host",
            Array.Empty<string>(),
            HandleSidBanCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "sidunban",
            "Preview or confirm removal from the host's Steam ID ban list",
            new[] { "unbansid" },
            HandleSidUnbanCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "setname",
            "Set your BlueSage base display name",
            Array.Empty<string>(),
            HandleSetNameCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "status",
            "Set your BlueSage status message",
            new[] { "setstatus", "set-status" },
            HandleStatusCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "brb",
            "Set your BlueSage status to BRB",
            Array.Empty<string>(),
            context => SetPresetStatus("BRB")));
        _commandRegistry.Register(new BlueSageCommand(
            "afk",
            "Set your BlueSage status to AFK",
            Array.Empty<string>(),
            context => SetPresetStatus("AFK")));
        _commandRegistry.Register(new BlueSageCommand(
            "back",
            "Restore your status from before BRB/AFK, or clear it",
            Array.Empty<string>(),
            HandleBackStatusCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "clearstatus",
            "Clear your BlueSage status message",
            new[] { "clear-status" },
            HandleClearStatusCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "statuscolor",
            "Set your BlueSage status color",
            Array.Empty<string>(),
            HandleStatusColorCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "spoons",
            "Set, show, or hide your optional spoon tag",
            Array.Empty<string>(),
            HandleSpoonsCommand));
        _commandRegistry.Register(new BlueSageCommand(
            "setmood",
            "Customize the short label shown in your optional 0/5 tag",
            new[] { "spoonlabel" },
            HandleSetMoodCommand));
        _commandRegistry.Register(new BlueSageCommand(
            BuildBlueSageClueCommandName(),
            "BlueSage internal",
            Array.Empty<string>(),
            HandleBlueSageClueCommand,
            isHidden: true));
        _commandRegistry.Register(new BlueSageCommand(
            "boop",
            "BlueSage internal",
            Array.Empty<string>(),
            HandleBoopCommand,
            isHidden: true));
        _commandRegistry.Register(new BlueSageCommand(
            "sagewave",
            "BlueSage internal",
            Array.Empty<string>(),
            HandleSageWaveCommand,
            isHidden: true));
        // Round 4 review R7: tombstone for the removed developer diagnostics command. Older UAT notes still
        // mention it; without this, typing it would broadcast the text to the whole lobby.
        _commandRegistry.Register(new BlueSageCommand(
            "bluedevdebug",
            "Removed; use the QoL menu's Local Audit snapshot",
            Array.Empty<string>(),
            HandleRetiredDeveloperDiagnosticsCommand,
            isHidden: true));
    }

    private BlueSageCommandResult HandleRetiredDeveloperDiagnosticsCommand(BlueSageCommandContext context)
    {
        return BlueSageCommandResult.HandledWithMessages(
            FormatBlueSageNotice("/bluedevdebug was removed. To share logs, open the QoL menu, turn on Local Audit Exports, then use Snapshot Now or Copy Audit Folder. This message stayed on your screen only."));
    }

    private BlueSageCommandResult HandleBlueSageQoLCommand(BlueSageCommandContext context)
    {
        string firstToken = context.Tokens.Count > 0 ? context.Tokens[0].ToLowerInvariant() : "help";
        if (firstToken == "status")
        {
            LogDetailedQolStatus();
            return BlueSageCommandResult.HandledWithMessages(BuildQolStatusMessage());
        }

        if (firstToken == "hosthealth")
        {
            return BlueSageCommandResult.HandledWithMessages(RunHostHealthCheck("manual command"));
        }

        if (firstToken == "help" || firstToken == "guide" || firstToken == "faq")
        {
            return BlueSageCommandResult.HandledWithMessages(ShowGuideOverlayNow() + " Type /bsqol commands for the text list of commands.");
        }

        var messages = new List<string>
        {
            NavigationCalloutPolicy.BuildHelpBasicsLine(QolMenuKey.Value.ToString(), StyleMenuKey.Value.ToString()),
            "Chat: /timestamps, /chatreadability, /chatbackdrop, /chatoutline, /chatscale, /chatfont, /chatheight, /chatrows, /chatvanilla, /copychat, /chatlinks.",
            "Style: /style, /setname, /status, /clearstatus, /brb, /afk, /back, /spoons, /setmood.",
            "Comfort: /as, /bettermove, /rg, /fa, /minimaplabels, /cloneshield. Large-lobby drawing relief is under Home > Toggles. Chalkboards: /chalk boards, then authorized Host/Helper /chalk clear <index>. Type / to browse commands."
        };
        if (CanUseLobbySafety(out bool isHost))
        {
            messages.Add(isHost
                ? "Host-only Helper management and evidence tools are grouped in Lobby Safety. /idsync status is read-only, refresh is local-only, and verified Host repair runs one bounded exact-correlation transaction."
                : "This host assigned you as a Helper. /idsync status is read-only, refresh is local-only, and /idsync request uses authenticated member data for current-Host revalidation.");
        }
        return BlueSageCommandResult.HandledWithMessages(messages.ToArray());
    }

    private BlueSageCommandResult HandleQolMenuCommand(BlueSageCommandContext context)
    {
        if (_qolMenuWindow == null)
        {
            StartQolMenuWindow();
        }

        if (_qolMenuWindow == null)
        {
            return BlueSageCommandResult.HandledWithMessages(SomethingWentWrongNotice);
        }

        _qolMenuWindow.ToggleVisible();
        return BlueSageCommandResult.HandledWithMessages("QoL menu toggled. Use the BlueSage window to manage QoL features.");
    }

    private BlueSageCommandResult HandleStyleCommand(BlueSageCommandContext context)
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild(context.Arguments, LockedMaxIdCardCharacters);
        if (!result.Success)
        {
            return BlueSageCommandResult.HandledWithMessages(result.Message);
        }

        GUIUtility.systemCopyBuffer = result.GeneratedText;
        return BlueSageCommandResult.HandledWithMessages($"Style Helper: copied {result.RawLength}/{LockedMaxIdCardCharacters} chars. Paste into an ID/name/profile/room field.");
    }

    // Voice pass (2026-10-23): one calm line when a window or update can't start; the details are in the log.
    internal const string SomethingWentWrongNotice = "Something went wrong; try again, or restart the game if it keeps happening.";

    private BlueSageCommandResult HandleStyleUiCommand(BlueSageCommandContext context)
    {
        if (!EnableStyleUi.Value)
        {
            return BlueSageCommandResult.HandledWithMessages("Style Helper is turned off. Turn it on in the QoL menu Toggles.");
        }

        if (_styleHelperWindow == null)
        {
            StartStyleHelperWindow();
        }

        if (_styleHelperWindow == null)
        {
            return BlueSageCommandResult.HandledWithMessages(SomethingWentWrongNotice);
        }

        _styleHelperWindow.ToggleVisible();
        return BlueSageCommandResult.HandledWithMessages(_styleHelperWindow.IsVisible
            ? "Style Helper opened. Build and copy your name or status text there."
            : "Style Helper closed.");
    }

    internal string TryApplyStyledNameFromStyleUi(string styledName)
    {
        return TryApplyStyledNameFromStyleUi(styledName, out _);
    }

    internal string TryApplyStyledNameFromStyleUi(string styledName, out bool applied)
    {
        applied = false;
        string requestedName = (styledName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return "Style Helper: build a style before applying it to your name.";
        }

        if (requestedName.Length > LockedMaxIdCardCharacters)
        {
            return $"Style Helper: styled name is too long ({requestedName.Length}/{LockedMaxIdCardCharacters}). Shorten it or use fewer per-letter colors.";
        }

        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: false);
        draft.BaseName = requestedName;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, "Style Helper: name updated.", out applied);
        return result.Messages.Count > 0 ? result.Messages[0] : "Style Helper: name updated.";
    }

    internal string TryApplyPlainNameFromStyleUi(string plainName)
    {
        return TryApplyPlainNameFromStyleUi(plainName, out _);
    }

    internal string TryApplyPlainNameFromStyleUi(string plainName, out bool applied)
    {
        applied = false;
        string requestedName = (plainName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(requestedName) || string.Equals(requestedName, "YOURNAMEHERE", StringComparison.OrdinalIgnoreCase))
        {
            return "Style Helper: type your name first.";
        }

        if (requestedName.Length > LockedMaxIdCardCharacters)
        {
            return $"Style Helper: name is too long ({requestedName.Length}/{LockedMaxIdCardCharacters}).";
        }

        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: false);
        draft.BaseName = requestedName;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, "Style Helper: name updated.", out applied);
        return result.Messages.Count > 0 ? result.Messages[0] : "Style Helper: name updated.";
    }

    internal string TryApplyStatusFromStyleUi(string plainName, string status, string color)
    {
        return TryApplyStatusFromStyleUi(plainName, status, color, out _);
    }

    internal string TryApplyStatusFromStyleUi(string plainName, string status, string color, out bool applied)
    {
        applied = false;
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        string requestedName = (plainName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(requestedName) && !string.Equals(requestedName, "YOURNAMEHERE", StringComparison.OrdinalIgnoreCase))
        {
            draft.BaseName = requestedName;
        }

        draft.StatusMessage = (status ?? string.Empty).Trim();
        if (RichTextStyleBuilder.TryNormalizeHex(color, out string hex))
        {
            draft.StatusColor = hex;
        }

        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, "Style Helper: status updated.", out applied);
        return result.Messages.Count > 0 ? result.Messages[0] : "Style Helper: status updated.";
    }

    internal string TryClearStatusFromStyleUi()
    {
        return TryClearStatusFromStyleUi(out _);
    }

    internal string TryClearStatusFromStyleUi(out bool applied)
    {
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        draft.StatusMessage = string.Empty;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, "Style Helper: status cleared.", out applied);
        return result.Messages.Count > 0 ? result.Messages[0] : "Style Helper: status cleared.";
    }

    internal string GetCurrentPlayerDisplayNameForStyleUi()
    {
        return GetStatusBaseName();
    }

    internal string GetCurrentPlayerPlainNameForStyleUi()
    {
        return RichTextStyleBuilder.ToPlainStyleEditorText(GetCurrentDisplayNameStateForStyleUi().BaseName);
    }

    internal string GetCurrentPlayerStyledNameForStyleUi()
    {
        return GetCurrentDisplayNameStateForStyleUi().BaseName;
    }

    internal DetectedDisplayNameState GetCurrentDisplayNameStateForStyleUi()
    {
        return StyleHelperNamePolicy.DetectCurrentState(
            GetLivePlayerDisplayName(),
            GetStatusBaseName(),
            StatusMessage?.Value ?? string.Empty,
            GetStatusColor(),
            StatusBrackets?.Value ?? "()",
            ShouldApplySpoons,
            SpoonCount?.Value ?? 5,
            GetSpoonLabel(),
            BuildStatusSuffix(),
            BuildSpoonSuffix());
    }

    private static string GetLivePlayerDisplayName()
    {
        try
        {
            string playerDataName = MonoSingleton<DataManager>.I?.PlayerData?.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(playerDataName))
            {
                return playerDataName;
            }
        }
        catch
        {
        }

        try
        {
            return NetworkSingleton<TextChannelManager>.I?.UserName?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private BlueSageCommandResult HandleSetPingColorCommand(BlueSageCommandContext context)
    {
        string requestedColor = context.Arguments.Trim();
        if (string.IsNullOrWhiteSpace(requestedColor))
        {
            return BlueSageCommandResult.HandledWithMessages($"Ping Mentions color: #{GetPingHighlightColor()}.");
        }

        if (!RichTextStyleBuilder.TryNormalizeHex(requestedColor, out string hex))
        {
            return BlueSageCommandResult.HandledWithMessages("Ping Mentions: expected a hex color like #9B59B6.");
        }

        PingHighlightColor.Value = hex;
        Config.Save();
        return BlueSageCommandResult.HandledWithMessages($"Ping Mentions color set to <color=#{hex}>#{hex}</color>.");
    }

    private BlueSageCommandResult HandleBlueSageClueCommand(BlueSageCommandContext context)
    {
        string name = GetStatusBaseName();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "You";
        }

        string egg = EasterEggTracker.Discover("command_clue", "the secret BlueSage clue glyph");
        return BlueSageCommandResult.HandledWithMessages(
            $"\u2665\u263B {name} found a Clue! \u263B\u2665 Screenshot this to @jollyblue (Blues) in Discord to make him giggle. {egg}");
    }

    private BlueSageCommandResult HandleBoopCommand(BlueSageCommandContext context)
    {
        string egg = EasterEggTracker.Discover("command_boop", "the respectful server boop");
        return BlueSageCommandResult.HandledWithMessages("Boop accepted. The lobby turtle has been emotionally supported. " + egg);
    }

    private BlueSageCommandResult HandleSageWaveCommand(BlueSageCommandContext context)
    {
        string egg = EasterEggTracker.Discover("command_sagewave", "the tiny Sage wave from behind the UI");
        return BlueSageCommandResult.HandledWithMessages("Sage waves from the menu bar, then pretends nothing happened. " + egg);
    }

    private BlueSageCommandResult HandleSetNameCommand(BlueSageCommandContext context)
    {
        string requestedName = context.Arguments.Trim();
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return BlueSageCommandResult.HandledWithMessages($"Status Helper name: {GetStatusBaseName()}.");
        }

        if (string.Equals(requestedName, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedName, "default", StringComparison.OrdinalIgnoreCase))
        {
            string personaName = GetSteamPersonaName();
            if (string.IsNullOrWhiteSpace(personaName))
            {
                return BlueSageCommandResult.HandledWithMessages("Status Helper: Steam persona name is not ready yet; no name change was made.");
            }

            DisplayNameDraft resetDraft = CaptureDisplayNameDraft(captureLiveBase: false);
            resetDraft.BaseName = personaName;
            return TryApplyDisplayNameDraft(resetDraft, "Status Helper: name reset to your current Steam persona name.", out _);
        }

        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: false);
        draft.BaseName = requestedName;
        return TryApplyDisplayNameDraft(draft, "Status Helper: name updated.", out _);
    }

    private BlueSageCommandResult HandleStatusCommand(BlueSageCommandContext context)
    {
        string requestedStatus = context.Arguments.Trim();
        if (string.IsNullOrWhiteSpace(requestedStatus))
        {
            return BlueSageCommandResult.HandledWithMessages(string.IsNullOrWhiteSpace(StatusMessage.Value)
                ? "Status Helper: no status set. Use /status BRB or /clearstatus."
                : $"Status Helper status: {StatusMessage.Value}.");
        }

        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        draft.StatusMessage = requestedStatus;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, "Status Helper: status updated.", out bool applied);
        if (applied)
        {
            _lastStatusBeforePreset = string.Empty;
        }
        return result;
    }

    private BlueSageCommandResult SetPresetStatus(string status)
    {
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        string priorStatus = (draft.StatusMessage ?? string.Empty).Trim();
        string nextLastStatus = _lastStatusBeforePreset;
        if (!string.Equals(priorStatus, "BRB", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(priorStatus, "AFK", StringComparison.OrdinalIgnoreCase))
        {
            nextLastStatus = priorStatus;
        }
        draft.StatusMessage = status;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, $"Status Helper: status set to {status}.", out bool applied);
        if (applied)
        {
            _lastStatusBeforePreset = nextLastStatus;
        }
        return result;
    }

    private BlueSageCommandResult HandleBackStatusCommand(BlueSageCommandContext context)
    {
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        string restoredStatus = (_lastStatusBeforePreset ?? string.Empty).Trim();
        draft.StatusMessage = restoredStatus;

        string message = string.IsNullOrWhiteSpace(restoredStatus)
            ? "Status Helper: back cleared your BRB/AFK status."
            : $"Status Helper: back restored {restoredStatus}.";
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, message, out bool applied);
        if (applied)
        {
            _lastStatusBeforePreset = string.Empty;
        }
        return result;
    }

    private BlueSageCommandResult HandleClearStatusCommand(BlueSageCommandContext context)
    {
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        draft.StatusMessage = string.Empty;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, "Status Helper: status cleared.", out bool applied);
        if (applied)
        {
            _lastStatusBeforePreset = string.Empty;
        }
        return result;
    }

    private BlueSageCommandResult HandleStatusColorCommand(BlueSageCommandContext context)
    {
        string requestedColor = context.Arguments.Trim();
        if (string.IsNullOrWhiteSpace(requestedColor))
        {
            return BlueSageCommandResult.HandledWithMessages($"Status Helper color: #{GetStatusColor()}.");
        }

        if (string.Equals(requestedColor, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedColor, "default", StringComparison.OrdinalIgnoreCase))
        {
            DisplayNameDraft resetDraft = CaptureDisplayNameDraft(captureLiveBase: true);
            resetDraft.StatusColor = "FFD45D";
            return TryApplyDisplayNameDraft(resetDraft, "Status Helper color: reset to #FFD45D.", out _);
        }

        if (!RichTextStyleBuilder.TryNormalizeHex(requestedColor, out string hex))
        {
            return BlueSageCommandResult.HandledWithMessages("Status Helper: expected a hex color like #FFD45D.");
        }

        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        draft.StatusColor = hex;
        return TryApplyDisplayNameDraft(draft, $"Status Helper color: #{hex}.", out _);
    }

    private BlueSageCommandResult HandleSpoonsCommand(BlueSageCommandContext context)
    {
        string argument = context.Arguments.Trim();
        if (string.IsNullOrWhiteSpace(argument) || string.Equals(argument, "status", StringComparison.OrdinalIgnoreCase))
        {
            return BlueSageCommandResult.HandledWithMessages("Spoons: " + FormatSpoonStatus() + ".");
        }

        if (string.Equals(argument, "off", StringComparison.OrdinalIgnoreCase))
        {
            DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
            draft.SpoonsEnabled = false;
            return TryApplyDisplayNameDraft(draft, "Spoons: hidden.", out _);
        }

        if (string.Equals(argument, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "default", StringComparison.OrdinalIgnoreCase))
        {
            DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
            draft.SpoonCount = 5;
            draft.SpoonLabel = DefaultSpoonLabel;
            draft.SpoonsEnabled = false;
            draft.SpoonsUpdatedUtc = DateTime.UtcNow.ToString("O");
            return TryApplyDisplayNameDraft(draft, "Spoons: reset to hidden default [5/5sp]. Use /spoons on when you want it visible.", out _);
        }

        if (string.Equals(argument, "on", StringComparison.OrdinalIgnoreCase))
        {
            DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
            draft.SpoonsEnabled = true;
            return TryApplyDisplayNameDraft(draft, "Spoons: visible as [" + ClampSpoons(draft.SpoonCount) + "/5" + GetSpoonLabel(draft.SpoonLabel) + "].", out _);
        }

        if (!int.TryParse(argument, out int requested))
        {
            return BlueSageCommandResult.HandledWithMessages("Spoons: use /spoons 0 through /spoons 5, /spoons status, /spoons on, /spoons off, or /spoons reset.");
        }

        DisplayNameDraft countDraft = CaptureDisplayNameDraft(captureLiveBase: true);
        countDraft.SpoonCount = ClampSpoons(requested);
        countDraft.SpoonsEnabled = true;
        countDraft.SpoonsUpdatedUtc = DateTime.UtcNow.ToString("O");
        return TryApplyDisplayNameDraft(countDraft, "Spoons: set to [" + countDraft.SpoonCount + "/5" + GetSpoonLabel(countDraft.SpoonLabel) + "].", out _);
    }

    private BlueSageCommandResult HandleSetMoodCommand(BlueSageCommandContext context)
    {
        string requested = context.Arguments.Trim();
        if (string.IsNullOrWhiteSpace(requested) || string.Equals(requested, "status", StringComparison.OrdinalIgnoreCase))
        {
            return BlueSageCommandResult.HandledWithMessages("Mood label: " + GetSpoonLabel() + ". Use /setmood energy, /setmood spoons, or /setmood default.");
        }

        if (string.Equals(requested, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requested, "reset", StringComparison.OrdinalIgnoreCase))
        {
            requested = DefaultSpoonLabel;
        }

        string cleaned = new string(requested.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').Take(12).ToArray());
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return BlueSageCommandResult.HandledWithMessages("Mood label: use 1-12 letters/numbers, '-' or '_'. Example: /setmood energy.");
        }

        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        draft.SpoonLabel = cleaned;
        draft.SpoonsEnabled = true;
        draft.SpoonsUpdatedUtc = DateTime.UtcNow.ToString("O");
        return TryApplyDisplayNameDraft(draft, "Mood label: now [" + ClampSpoons(draft.SpoonCount) + "/5" + cleaned + "]. /setmood default restores [x/5sp].", out _);
    }

    internal string ApplySpoonVisibilityFromMenu()
    {
        return TrySetSpoonVisibilityFromMenu(ShouldApplySpoons, out _);
    }

    internal string TrySetSpoonVisibilityFromMenu(bool enabled, out bool applied)
    {
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        draft.SpoonsEnabled = enabled;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, enabled
            ? "Spoons: visible as [" + ClampSpoons(draft.SpoonCount) + "/5" + GetSpoonLabel(draft.SpoonLabel) + "]."
            : "Spoons: hidden.", out applied);
        return result.Messages.Count > 0
            ? result.Messages[0]
            : enabled ? "Spoons: visible." : "Spoons: hidden.";
    }

    internal string TryApplySpoonsFromStyleUi(int count, string label, bool enabled, string updatedUtc, out bool applied)
    {
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: true);
        draft.SpoonCount = ClampSpoons(count);
        draft.SpoonLabel = GetSpoonLabel(label);
        draft.SpoonsEnabled = enabled;
        draft.SpoonsUpdatedUtc = updatedUtc ?? string.Empty;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(draft, enabled
            ? "Spoons: visible as [" + draft.SpoonCount + "/5" + draft.SpoonLabel + "]."
            : "Spoons: hidden.", out applied);
        return result.Messages.Count > 0 ? result.Messages[0] : "Spoons updated.";
    }

    internal void CaptureLiveStyledBaseBeforeSuffixChange()
    {
        // Kept for binary compatibility with the earlier 0.2.0 surface. Mutations now capture this value
        // into a prospective draft and only commit it after the native update succeeds.
    }

    private BlueSageCommandResult ApplyStatusNameToPlayerData(string successMessage)
    {
        return TryApplyDisplayNameDraft(CaptureDisplayNameDraft(captureLiveBase: false), successMessage, out _);
    }

    private DisplayNameDraft CaptureDisplayNameDraft(bool captureLiveBase)
    {
        var draft = new DisplayNameDraft
        {
            BaseName = GetStatusBaseName(),
            StatusMessage = StatusMessage?.Value ?? string.Empty,
            StatusColor = GetStatusColor(),
            StatusBrackets = StatusBrackets?.Value ?? "()",
            SpoonsEnabled = ShouldApplySpoons,
            SpoonCount = ClampSpoons(SpoonCount?.Value ?? 5),
            SpoonLabel = GetSpoonLabel(),
            SpoonsUpdatedUtc = SpoonsUpdatedUtc?.Value ?? string.Empty
        };

        if (captureLiveBase && string.IsNullOrWhiteSpace(draft.BaseName))
        {
            string liveBaseName = GetCurrentDisplayNameStateForStyleUi().BaseName;
            if (!string.IsNullOrWhiteSpace(RichTextStyleBuilder.ToPlainStyleEditorText(liveBaseName)))
            {
                draft.BaseName = liveBaseName;
            }
        }

        return draft;
    }

    public string GetCompanionStyledBase()
    {
        return CaptureDisplayNameDraft(captureLiveBase: false).BaseName ?? string.Empty;
    }

    public bool TryApplyCompanionStyledBase(string baseName, out string resultMessage)
    {
        DisplayNameDraft draft = CaptureDisplayNameDraft(captureLiveBase: false);
        draft.BaseName = baseName ?? string.Empty;
        BlueSageCommandResult result = TryApplyDisplayNameDraft(
            draft,
            "Companion style change applied through BlueSage QoL.",
            out bool applied);
        resultMessage = result.Messages.Count > 0
            ? result.Messages[0]
            : (applied ? "Companion style change applied through BlueSage QoL." : "Companion style change failed.");
        return applied;
    }

    public bool TrySetCompanionHostRoleLabel(string richText, out string resultMessage)
    {
        return Patches.PlayerRoleLabelController.TrySetLocalHostLabelOverride(
            richText,
            out resultMessage);
    }

    public bool TryClearCompanionHostRoleLabel(out string resultMessage)
    {
        return Patches.PlayerRoleLabelController.TryClearLocalHostLabelOverride(
            out resultMessage);
    }

    public bool TryFocusCompanionHostConsole(out string resultMessage)
    {
        if (_qolMenuWindow == null)
        {
            StartQolMenuWindow();
        }
        if (_qolMenuWindow == null || !_qolMenuWindow.ShowHostConsole())
        {
            resultMessage =
                "Host Console unavailable; current verified host access is required.";
            return false;
        }

        resultMessage = "BlueSage Host Console opened.";
        return true;
    }

    // Round 3 (2026-10-17): the companion reads the live, rebindable menu hotkeys instead of
    // assuming Home/Insert.
    public bool TryGetCompanionHotkeys(out KeyCode qolMenuKey, out KeyCode styleMenuKey)
    {
        qolMenuKey = QolMenuKey?.Value ?? KeyCode.Home;
        styleMenuKey = StyleMenuKey?.Value ?? KeyCode.Insert;
        return QolMenuKey != null && StyleMenuKey != null;
    }

    public bool TryResolveCompanionIdentityTruthV1(
        ulong exactSteamId,
        ulong exactPurrNetId,
        out BlueSageIdentityTruthSnapshotV1 truth,
        out string resultMessage)
    {
        return Patches.PlayerBadgeController.TryResolveFreshIdentityTruth(
            exactSteamId,
            exactPurrNetId,
            out truth,
            out resultMessage);
    }

    public bool TryResolveCompanionIdentityTruthV2(
        ulong exactSteamId,
        out BlueSageIdentityTruthSnapshotV1 truth,
        out string resultMessage)
    {
        return Patches.PlayerBadgeController.TryResolveFreshIdentityTruthBySteam(
            exactSteamId,
            out truth,
            out resultMessage);
    }

    public bool TryResolveCompanionClientCapabilitySnapshotV1(
        ulong exactSteamId,
        out BlueSageClientCapabilitySnapshotV1 snapshot,
        out string resultMessage)
    {
        return Patches.PlayerBadgeController
            .TryResolveFreshClientCapabilitySnapshotBySteam(
                exactSteamId,
                out snapshot,
                out resultMessage);
    }

    private BlueSageCommandResult TryApplyDisplayNameDraft(DisplayNameDraft draft, string successMessage, out bool applied)
    {
        applied = false;
        if (draft == null)
        {
            return BlueSageCommandResult.HandledWithMessages("Status Helper: player name is not ready yet.");
        }

        int effectiveNameLimit = Patches.PlayerIdInfoLimitPatch.HasExpectedCoverage
            ? LockedMaxIdCardCharacters
            : Math.Min(LockedMaxIdCardCharacters, Patches.PlayerIdInfoLimitPatch.VanillaSafeMaxCharacters);
        if (!DisplayNameMutationPolicy.TryCompose(
                draft.BaseName,
                BuildStatusSuffix(draft),
                BuildSpoonSuffix(draft),
                effectiveNameLimit,
                out string displayName,
                out string validationError))
        {
            Log?.LogWarning(
                $"Status Helper rejected a display-name draft before mutation. " +
                $"baseLength={(draft.BaseName ?? string.Empty).Length}, limit={effectiveNameLimit}, reason={validationError}");
            return BlueSageCommandResult.HandledWithMessages(validationError);
        }

        DataManager dataManager;
        try
        {
            dataManager = MonoSingleton<DataManager>.I;
        }
        catch
        {
            dataManager = null;
        }

        if (dataManager == null || dataManager.PlayerData == null)
        {
            return BlueSageCommandResult.HandledWithMessages("Status Helper: player data is not ready yet.");
        }

        TextChannelManager textChannelManager;
        try
        {
            textChannelManager = NetworkSingleton<TextChannelManager>.I;
        }
        catch
        {
            textChannelManager = null;
        }

        if (textChannelManager == null || textChannelManager.MainCustomizationController == null)
        {
            return BlueSageCommandResult.HandledWithMessages("Your player info is still loading. Try again in a moment.");
        }

        DisplayNameDraft previousConfig = CaptureDisplayNameDraft(captureLiveBase: false);
        string previousPlayerName = dataManager.PlayerData.Name;
        string previousChannelName = textChannelManager.UserName;
        UIManager uiManager = null;
        string previousUiName = string.Empty;
        try
        {
            uiManager = MonoSingleton<UIManager>.I;
            if (uiManager != null && uiManager.PlayerText != null)
            {
                previousUiName = uiManager.PlayerText.text;
            }

            dataManager.PlayerData.Name = displayName;
            dataManager.SavePlayerData();

            PlayerIDInfo playerInfo = dataManager.PlayerData.GetPlayerIdInfo();
            string serializedName = Encoding.Unicode.GetString(playerInfo.Name ?? new byte[0]).TrimEnd('\0');
            if (!string.Equals(dataManager.PlayerData.Name, displayName, StringComparison.Ordinal) ||
                !string.Equals(serializedName, displayName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"native display-name readback did not match the requested value " +
                    $"(requested={displayName.Length}, playerData={(dataManager.PlayerData.Name ?? string.Empty).Length}, serialized={serializedName.Length})");
            }

            textChannelManager.UserName = displayName;
            textChannelManager.MainCustomizationController.UpdatePlayerInfo(playerInfo);

            if (uiManager != null && uiManager.PlayerText != null)
            {
                uiManager.PlayerText.text = displayName;
            }

            if (!string.Equals(textChannelManager.UserName, displayName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("text-channel display-name readback did not match the requested value");
            }
            if (uiManager != null && uiManager.PlayerText != null &&
                !string.Equals(uiManager.PlayerText.text, displayName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("local UI display-name readback did not match the requested value");
            }

            CommitDisplayNameDraft(draft);
            Config.Save();
            applied = true;
            return BlueSageCommandResult.HandledWithMessages(successMessage);
        }
        catch (Exception ex)
        {
            RestoreDisplayNameConfig(previousConfig);
            TryRestoreNativeDisplayName(dataManager, previousPlayerName, textChannelManager, previousChannelName, uiManager, previousUiName);
            Log.LogWarning($"Status Helper update failed: {ex.GetType().Name}: {ex.Message}");
            return BlueSageCommandResult.HandledWithMessages(SomethingWentWrongNotice);
        }
    }

    private static void CommitDisplayNameDraft(DisplayNameDraft draft)
    {
        StatusBaseName.Value = (draft.BaseName ?? string.Empty).Trim();
        StatusMessage.Value = (draft.StatusMessage ?? string.Empty).Trim();
        StatusColor.Value = GetNormalizedStatusColor(draft.StatusColor);
        StatusBrackets.Value = string.IsNullOrWhiteSpace(draft.StatusBrackets) ? "()" : draft.StatusBrackets;
        EnableSpoons.Value = draft.SpoonsEnabled;
        SpoonCount.Value = ClampSpoons(draft.SpoonCount);
        SpoonLabel.Value = GetSpoonLabel(draft.SpoonLabel);
        SpoonsUpdatedUtc.Value = draft.SpoonsUpdatedUtc ?? string.Empty;
    }

    private static void RestoreDisplayNameConfig(DisplayNameDraft previous)
    {
        if (previous == null)
        {
            return;
        }

        CommitDisplayNameDraft(previous);
        try
        {
            Instance?.Config.Save();
        }
        catch
        {
        }
    }

    private static void TryRestoreNativeDisplayName(
        DataManager dataManager,
        string playerName,
        TextChannelManager textChannelManager,
        string channelName,
        UIManager uiManager,
        string uiName)
    {
        try
        {
            if (dataManager?.PlayerData != null)
            {
                dataManager.PlayerData.Name = playerName;
                dataManager.SavePlayerData();
            }
            if (textChannelManager != null)
            {
                textChannelManager.UserName = channelName;
                if (textChannelManager.MainCustomizationController != null && dataManager?.PlayerData != null)
                {
                    textChannelManager.MainCustomizationController.UpdatePlayerInfo(dataManager.PlayerData.GetPlayerIdInfo());
                }
            }
            if (uiManager != null && uiManager.PlayerText != null)
            {
                uiManager.PlayerText.text = uiName;
            }
        }
        catch
        {
        }
    }

    private static string BuildStatusDisplayName(string baseName)
    {
        string statusTag = BuildStatusSuffix();
        string spoonTag = BuildSpoonSuffix();
        return DisplayNameCompositionPolicy.Compose(baseName, statusTag, spoonTag);
    }

    private static string BuildStatusSuffix()
    {
        return BuildStatusSuffix(new DisplayNameDraft
        {
            StatusMessage = StatusMessage?.Value ?? string.Empty,
            StatusColor = GetStatusColor(),
            StatusBrackets = StatusBrackets?.Value ?? "()"
        });
    }

    private static string BuildStatusSuffix(DisplayNameDraft draft)
    {
        return StatusSuffixPolicy.Build(
            draft?.StatusMessage ?? string.Empty,
            GetNormalizedStatusColor(draft?.StatusColor),
            draft?.StatusBrackets ?? "()");
    }

    internal static string BuildSpoonSuffix()
    {
        return BuildSpoonSuffix(new DisplayNameDraft
        {
            SpoonsEnabled = ShouldApplySpoons,
            SpoonCount = SpoonCount?.Value ?? 5,
            SpoonLabel = GetSpoonLabel()
        });
    }

    private static string BuildSpoonSuffix(DisplayNameDraft draft)
    {
        if (draft == null || !draft.SpoonsEnabled)
        {
            return string.Empty;
        }

        int spoons = ClampSpoons(draft.SpoonCount);
        return RichTextStyleBuilder.BuildColorTag("70FFBD", $"[{spoons}/5{GetSpoonLabel(draft.SpoonLabel)}]");
    }

    private static string FormatSpoonStatus()
    {
        int spoons = ClampSpoons(SpoonCount?.Value ?? 5);
        string updated = SpoonsUpdatedUtc?.Value ?? string.Empty;
        string updatedText = string.IsNullOrWhiteSpace(updated) ? "not stamped yet" : "updated " + updated;
        string visibility = ShouldApplySpoons ? "visible" : "hidden";
        return $"{spoons}/5{GetSpoonLabel()}, {visibility}, {updatedText}";
    }

    private static string GetSpoonLabel()
    {
        return GetSpoonLabel(SpoonLabel?.Value);
    }

    private static string GetSpoonLabel(string value)
    {
        string label = value ?? DefaultSpoonLabel;
        string cleaned = new string(label.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').Take(12).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? DefaultSpoonLabel : cleaned;
    }

    private static int ClampSpoons(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 5 ? 5 : value;
    }

    private static void StampSpoonsUpdated()
    {
        if (SpoonsUpdatedUtc != null)
        {
            SpoonsUpdatedUtc.Value = DateTime.UtcNow.ToString("O");
        }
    }

    private static string GetStatusBaseName()
    {
        string configured = (StatusBaseName.Value ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        try
        {
            return MonoSingleton<DataManager>.I?.PlayerData?.Name?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetStatusColor()
    {
        return GetNormalizedStatusColor(StatusColor?.Value);
    }

    private static string GetSteamPersonaName()
    {
        try
        {
            if (!SteamManager.Initialized) return string.Empty;
            return (SteamFriends.GetPersonaName() ?? string.Empty).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetNormalizedStatusColor(string value)
    {
        return RichTextStyleBuilder.TryNormalizeHex(value, out string hex) ? hex : "FFD45D";
    }

    internal static string GetPingHighlightColor()
    {
        return RichTextStyleBuilder.TryNormalizeHex(PingHighlightColor.Value, out string hex) ? hex : "9B59B6";
    }

    internal static string GetPingMentionColor()
    {
        return RichTextStyleBuilder.TryNormalizeHex(PingMentionColor.Value, out string hex) ? hex : "FFD700";
    }

    internal static string GetPingSoundMode()
    {
        string value = (PingSoundMode?.Value ?? "ticket").Trim().ToLowerInvariant();
        switch (value)
        {
            case "change":
            case "error":
            case "task":
            case "ticket":
                return value;
            default:
                return "ticket";
        }
    }

    private BlueSageCommandResult HandlePingSoundCommand(BlueSageCommandContext context)
    {
        string requested = (context.Arguments ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(requested) || requested == "status")
        {
            return BlueSageCommandResult.HandledWithMessages($"Ping Sound: {(EnablePingSound.Value ? "on" : "off")}, mode={GetPingSoundMode()}. Use /pingsound click|change|error|task|ticket|on|off.");
        }

        if (requested == "on" || requested == "off")
        {
            EnablePingSound.Value = requested == "on";
            Config.Save();
            return BlueSageCommandResult.HandledWithMessages("Ping Sound: " + (EnablePingSound.Value ? "on." : "off."));
        }

        switch (requested)
        {
            case "click":
            case "change":
            case "error":
            case "task":
            case "ticket":
                PingSoundMode.Value = requested;
                EnablePingSound.Value = true;
                Config.Save();
                MentionPingController.PlayConfiguredPing();
                return BlueSageCommandResult.HandledWithMessages("Ping Sound mode set to " + requested + ".");
            default:
                return BlueSageCommandResult.HandledWithMessages("Ping Sound: use /pingsound click, change, error, task, ticket, on, off, or status.");
        }
    }

    private void StartMentionPingController()
    {
        if (_mentionPingControllerObject != null)
        {
            return;
        }

        _mentionPingControllerObject = new GameObject("BlueSage_MentionPingController");
        _mentionPingControllerObject.AddComponent<MentionPingController>();
        DontDestroyOnLoad(_mentionPingControllerObject);
    }

    private void StartCommandTypeaheadController()
    {
        if (_commandTypeaheadControllerObject != null)
        {
            return;
        }

        _commandTypeaheadControllerObject = new GameObject("BlueSage_CommandTypeaheadController");
        _commandTypeaheadControllerObject.AddComponent<CommandTypeaheadController>();
        DontDestroyOnLoad(_commandTypeaheadControllerObject);
    }

    private void StartChatReadabilityController()
    {
        if (_chatReadabilityControllerObject != null)
        {
            return;
        }

        _chatReadabilityControllerObject = new GameObject("BlueSage_ChatReadabilityController");
        _chatReadabilityControllerObject.AddComponent<ChatReadabilityController>();
        DontDestroyOnLoad(_chatReadabilityControllerObject);
    }

    private void StartSessionAuditController()
    {
        if (_sessionAuditControllerObject != null) return;
        _sessionAuditControllerObject = new GameObject("BlueSage_SessionAuditController");
        _sessionAuditControllerObject.AddComponent<SessionAuditController>();
        DontDestroyOnLoad(_sessionAuditControllerObject);
    }

    private void StartSessionRunMarkerController()
    {
        if (_sessionRunMarkerControllerObject != null) return;
        _sessionRunMarkerControllerObject = new GameObject("BlueSage_SessionRunMarkerController");
        _sessionRunMarkerControllerObject.AddComponent<SessionRunMarkerController>();
        DontDestroyOnLoad(_sessionRunMarkerControllerObject);
    }


    // Optional local bridges compile only in builds that opt in (see the project file). Public builds
    // have no implementation, so the compiler removes these calls entirely.
    partial void BindOptionalLocalBridgeConfig();
    partial void StartOptionalLocalBridges();
    partial void StopOptionalLocalBridges();

    internal static bool IsLobbyMassPingAuthorized(string senderSteamId)
    {
        if (!TryGetCurrentSteamLobby(out _)) return false;
        if (string.IsNullOrWhiteSpace(senderSteamId) ||
            !PlayerIdentityEvidenceController.TryResolveExact(senderSteamId, out _, out _)) return false;
        return SteamIdModerationController.IsAuthorizedCurrentIssuer(senderSteamId);
    }

    private void StartRescueReceiverCapabilityPublisher()
    {
        if (_rescueReceiverCapabilityCoroutine != null)
        {
            StopCoroutine(_rescueReceiverCapabilityCoroutine);
        }

        _rescueReceiverCapabilityCoroutine = StartCoroutine(RescueReceiverCapabilityLoop());
    }

    private IEnumerator RescueReceiverCapabilityLoop()
    {
        while (true)
        {
            PublishRescueReceiverCapability();
            yield return new WaitForSecondsRealtime(RescueReceiverCapabilityPolicy.PublishIntervalSeconds);
        }
    }

    private void PublishRescueReceiverCapability()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        try
        {
            if (!Patches.OptInRescuePatch.IsReceiverAvailable ||
                !SteamManager.Initialized ||
                !TryGetRescueLobby(out CSteamID lobby) ||
                lobby == CSteamID.Nil ||
                !IsLocalCurrentLobbyMember(lobby))
            {
                ClearRescueReceiverCapability();
                return;
            }

            if (_rescueReceiverCapabilityLobby != CSteamID.Nil && _rescueReceiverCapabilityLobby != lobby)
            {
                ClearRescueReceiverCapability();
            }

            string payload = RescueReceiverCapabilityPolicy.BuildPayload(PluginVersion, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (payload.Length == 0)
            {
                ClearRescueReceiverCapability();
                return;
            }

            SteamMatchmaking.SetLobbyMemberData(lobby, RescueReceiverCapabilityPolicy.MemberDataKey, payload);
            _rescueReceiverCapabilityLobby = lobby;
        }
        catch
        {
            ClearRescueReceiverCapability();
        }
    }

    private void ClearRescueReceiverCapability()
    {
        if (PluginShutdownController.IsShuttingDown) return;
        CSteamID lobby = _rescueReceiverCapabilityLobby;
        _rescueReceiverCapabilityLobby = CSteamID.Nil;
        if (lobby == CSteamID.Nil || !SteamManager.Initialized)
        {
            return;
        }

        try
        {
            SteamMatchmaking.SetLobbyMemberData(lobby, RescueReceiverCapabilityPolicy.MemberDataKey, string.Empty);
        }
        catch
        {
            // Member data disappears with lobby membership and the signed timestamp
            // expires after 15 seconds, so unload/lobby-change cleanup is fail-safe.
        }
    }

    private void ClearRescueReceiverCapability(SteamRuntimeSnapshot snapshot)
    {
        CSteamID lobby = _rescueReceiverCapabilityLobby != CSteamID.Nil
            ? _rescueReceiverCapabilityLobby
            : snapshot?.Lobby ?? CSteamID.Nil;
        _rescueReceiverCapabilityLobby = CSteamID.Nil;
        if (snapshot == null || !snapshot.SteamWasReady || lobby == CSteamID.Nil)
        {
            return;
        }

        try
        {
            SteamMatchmaking.SetLobbyMemberData(
                lobby,
                RescueReceiverCapabilityPolicy.MemberDataKey,
                string.Empty);
        }
        catch
        {
            // Cached Steam cleanup is best-effort; no runtime singleton is resolved here.
        }
    }

    private static bool IsLocalCurrentLobbyMember(CSteamID lobby)
    {
        CSteamID local = SteamUser.GetSteamID();
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int index = 0; index < memberCount; index++)
        {
            if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, index) == local)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryGetRescueLobby(out CSteamID lobbyId)
    {
        if (PluginShutdownController.IsShuttingDown)
        {
            lobbyId = CSteamID.Nil;
            return false;
        }
        return TryGetCurrentSteamLobby(out lobbyId);
    }

    private BlueSageCommandResult HandleHelperCommand(BlueSageCommandContext context)
    {
        if (!CanManageHelpers())
        {
            return BlueSageCommandResult.HandledWithMessages("Helper management is available only to the current lobby host. No Helper details were shown.");
        }

        if (!TryGetCurrentSteamLobby(out CSteamID lobbyId))
        {
            return BlueSageCommandResult.HandledWithMessages("Helpers: join a lobby first.");
        }

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
        if (owner == CSteamID.Nil || SteamUser.GetSteamID() != owner || NetworkManager.main == null || !NetworkManager.main.isHost)
        {
            return BlueSageCommandResult.HandledWithMessages("Helpers: this function is host only. You are not the current lobby host.");
        }

        string[] tokens = (context.Arguments ?? string.Empty).Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        string action = tokens.Length > 0 ? tokens[0].ToLowerInvariant() : "list";
        string ownerSteamId = ((ulong)owner).ToString();
        var helpers = GetUnifiedHelpersForOwner(ownerSteamId, importLegacy: true);
        if (action == "list" || action == "status")
        {
            string visible = helpers.Count == 0 ? "none" : string.Join(", ", helpers.Select(FormatSteamIdSuffix));
            return BlueSageCommandResult.HandledWithMessages("Helpers: " + visible + ". One host-scoped list grants @lobby and host-validated moderation requests.");
        }

        if (action == "reset" || action == "clear")
        {
            SaveUnifiedHelpers(ownerSteamId, Array.Empty<string>());
            SteamIdModerationController.PublishHostCapability();
            Patches.PlayerIdentityRoleHighlightController.RefreshAll();
            return BlueSageCommandResult.HandledWithMessages("Helpers: cleared. Only the host remains authorized.");
        }

        if ((action != "add" && action != "remove" && action != "revoke") || tokens.Length < 2)
        {
            return BlueSageCommandResult.HandledWithMessages("Helpers: use /helper add <current player>, remove <current player>, list, or reset.");
        }

        if (!TryResolveLobbyMemberSteamId(tokens[1], out string steamId, out string error))
        {
            return BlueSageCommandResult.HandledWithMessages("Helpers: " + error);
        }

        if (steamId == ownerSteamId)
        {
            return BlueSageCommandResult.HandledWithMessages("Helpers: the host is already authorized.");
        }

        bool adding = action == "add";
        bool changed = adding ? helpers.Add(steamId) : helpers.Remove(steamId);
        SaveUnifiedHelpers(ownerSteamId, helpers);
        SteamIdModerationController.PublishHostCapability();
        Patches.PlayerIdentityRoleHighlightController.RefreshAll();
        return BlueSageCommandResult.HandledWithMessages("Helpers: " +
            (changed ? (adding ? "authorized " : "revoked ") : "no change for ") + FormatSteamIdSuffix(steamId) +
            ". Helpers may use @lobby and request host-validated moderation actions.");
    }

    internal static HashSet<string> GetUnifiedHelpersForOwner(string ownerSteamId, bool importLegacy)
    {
        string serialized = UnifiedHelperPolicy.MergeForOwner(
            ownerSteamId,
            LobbyHelpers?.Value ?? string.Empty,
            LobbyHelperOwnerSteamId?.Value ?? string.Empty,
            importLegacy ? LobbyPingDelegates?.Value ?? string.Empty : string.Empty,
            importLegacy ? LobbyPingDelegateOwnerSteamId?.Value ?? string.Empty : string.Empty,
            importLegacy ? LobbyModerationDelegates?.Value ?? string.Empty : string.Empty,
            importLegacy ? LobbyModerationDelegateOwnerSteamId?.Value ?? string.Empty : string.Empty);
        var result = new HashSet<string>(LobbyPingAuthorizationPolicy.ParseDelegates(serialized), StringComparer.Ordinal);
        if (importLegacy && (LobbyHelperOwnerSteamId == null || !string.Equals(LobbyHelperOwnerSteamId.Value, ownerSteamId, StringComparison.Ordinal) ||
            !string.Equals(LobbyHelpers?.Value ?? string.Empty, serialized, StringComparison.Ordinal)))
        {
            SaveUnifiedHelpers(ownerSteamId, result);
        }
        return result;
    }

    private static void SaveUnifiedHelpers(string ownerSteamId, IEnumerable<string> helpers)
    {
        string serialized = LobbyPingAuthorizationPolicy.SerializeDelegates(helpers ?? Array.Empty<string>());
        LobbyHelpers.Value = serialized;
        LobbyHelperOwnerSteamId.Value = ownerSteamId;

        // Keep the old keys mirrored so an in-place downgrade does not silently
        // split permissions again. New code reads only the canonical Helpers keys.
        LobbyPingDelegates.Value = serialized;
        LobbyPingDelegateOwnerSteamId.Value = ownerSteamId;
        LobbyModerationDelegates.Value = serialized;
        LobbyModerationDelegateOwnerSteamId.Value = ownerSteamId;
        Instance?.Config.Save();
    }

    private BlueSageCommandResult HandleSidListCommand(BlueSageCommandContext context)
    {
        if (!CanUseLobbySafety(out _)) return LobbySafetyDeniedResult();
        IReadOnlyList<CloneIncident> incidents = CloneIncidentLedger.Snapshot;
        if (incidents.Count == 0)
        {
            return BlueSageCommandResult.HandledWithMessages(
                "Clone Shield evidence: no open current-lobby incidents. Open the Lobby Safety tab for the verified roster, Helper controls, and closed history.");
        }

        var messages = new List<string>
        {
            "Clone Shield: " + incidents.Count + " open incident(s). Open the Lobby Safety tab for exact IDs, profile buttons, highlights, preview, resolve, and dismiss controls."
        };
        foreach (CloneIncident incident in incidents.OrderByDescending(item => item.LastObservedUnixSeconds).Take(3))
        {
            messages.Add(incident.Id + " | protected victim …" + FormatSteamIdSuffix(incident.VictimSteamId).Replace("Steam …", string.Empty) +
                         " | suspect …" + FormatSteamIdSuffix(incident.SuspectSteamId).Replace("Steam …", string.Empty) + " | " + incident.MatchType + ".");
        }
        if (incidents.Count > 3) messages.Add((incidents.Count - 3) + " more open incident(s) are available in Lobby Safety without filling chat.");
        return BlueSageCommandResult.HandledWithMessages(messages.ToArray());
    }

    private BlueSageCommandResult HandleSidIncidentCommand(BlueSageCommandContext context)
    {
        if (!CanUseLobbySafety(out _)) return LobbySafetyDeniedResult();
        string[] tokens = (context.Arguments ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || string.Equals(tokens[0], "status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(tokens[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            int open = CloneIncidentActionController.GetOpenIncidents().Count;
            int closed = CloneIncidentActionController.GetIncidentHistory().Count(item => item.State != CloneIncidentState.Open);
            return BlueSageCommandResult.HandledWithMessages("Clone incidents: " + open + " open, " + closed +
                " closed locally. Use /sidincident <C001> resolve|dismiss, clear-player <verified player>, dismiss-all, clear-history, or clear-all.");
        }

        string action = tokens[0].ToLowerInvariant();
        if (action == "dismiss-all" || action == "dismissall")
        {
            CloneIncidentActionController.TryDismissAllOpenLocal(out string message);
            return BlueSageCommandResult.HandledWithMessages(message);
        }
        if (action == "clear-history" || action == "clearhistory")
        {
            int removed = CloneIncidentActionController.ClearClosedHistory();
            return BlueSageCommandResult.HandledWithMessages("Cleared " + removed + " closed local Clone Shield " +
                (removed == 1 ? "record" : "records") + ". Open incidents and lobby moderation state were unchanged.");
        }
        if (action == "clear-all" || action == "clearall")
        {
            CloneIncidentActionController.TryDismissAllOpenLocal(out _);
            int removed = CloneIncidentActionController.ClearClosedHistory();
            return BlueSageCommandResult.HandledWithMessages("Cleared the local Clone Shield incident view (" + removed +
                " archived record" + (removed == 1 ? string.Empty : "s") + "). No bans, Helper state, or lobby moderation state changed.");
        }

        if ((action == "clear-player" || action == "clear-row" || action == "clearplayer" || action == "clearrow") && tokens.Length >= 2)
        {
            string query = string.Join(" ", tokens.Skip(1));
            if (!PlayerIdentityEvidenceController.TryResolveLookup(query, out PlayerIdentityEvidence evidence, out string lookupError))
                return BlueSageCommandResult.HandledWithMessages("Clone incident player cleanup refused: " + lookupError);
            CloneIncidentActionController.TryDismissOpenForVerifiedPlayer(evidence.SteamId, out string message);
            return BlueSageCommandResult.HandledWithMessages(message);
        }

        if (tokens.Length >= 2 && tokens[0].StartsWith("C", StringComparison.OrdinalIgnoreCase))
        {
            string incidentId = tokens[0];
            string incidentAction = tokens[1].ToLowerInvariant();
            if (incidentAction == "dismiss")
            {
                CloneIncidentActionController.TryDismissLocal(incidentId, out string message);
                return BlueSageCommandResult.HandledWithMessages(message);
            }
            if (incidentAction == "resolve")
            {
                CloneIncidentActionController.TryResolveForLobby(incidentId, out string message);
                return BlueSageCommandResult.HandledWithMessages(message);
            }
        }

        return BlueSageCommandResult.HandledWithMessages(
            "Clone incidents: use /sidincident status, <C001> resolve, <C001> dismiss, clear-player <verified player>, dismiss-all, clear-history, or clear-all. Clear actions are local-only.");
    }

    private BlueSageCommandResult HandleSidWhoCommand(BlueSageCommandContext context)
    {
        if (!CanUseLobbySafety(out _)) return LobbySafetyDeniedResult();
        string arguments = (context.Arguments ?? string.Empty).Trim();
        bool copyEvidence = arguments.StartsWith("copy ", StringComparison.OrdinalIgnoreCase);
        string lookup = copyEvidence ? arguments.Substring("copy ".Length).Trim() : arguments;
        string[] tokens = arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0 && string.Equals(tokens[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<PlayerIdentityEvidence> roster = PlayerIdentityEvidenceController.GetVerifiedRoster();
            return BlueSageCommandResult.HandledWithMessages(roster.Count == 0
                ? "Lobby Safety: no player rows currently pass both native-roster and Steam-lobby verification."
                : "Lobby Safety: " + roster.Count + " verified player row(s). Open the authorized Lobby Safety tab for paging, exact copy, profile verification, incident evidence, and host-validated actions without filling chat.");
        }

        if (string.IsNullOrWhiteSpace(arguments))
        {
            return BlueSageCommandResult.HandledWithMessages(
                "SID lookup: use /sidwho list, or /sidwho <C001 | partial display name | regex | partial Steam persona | 4+ SteamID digits | _host | _self>.",
                "This command is read-only. /sidban still refuses names and requires preview plus confirmation.");
        }

        IReadOnlyList<PlayerIdentityEvidence> matches = PlayerIdentityEvidenceController.ResolveLookupMatches(lookup, out string error);
        if (matches.Count == 0)
        {
            return BlueSageCommandResult.HandledWithMessages("Couldn't find that player (" + (error ?? string.Empty).Trim().TrimEnd('.') + "). Nothing was done.");
        }

        if (matches.Count > 1)
        {
            var ambiguous = new List<string>
            {
                "SID lookup: " + matches.Count + " verified current players match '" + lookup + "'. No player was selected and no moderation action was taken."
            };
            foreach (PlayerIdentityEvidence match in matches.Take(8))
            {
                ambiguous.Add("row #" + (match.RosterIndex + 1) + " | " + match.DisplayName + " | " + match.SteamPersona + " | " + match.SteamId + " | " + match.Role);
            }
            if (matches.Count > 8) ambiguous.Add((matches.Count - 8) + " more match(es). Refine the query or use Lobby Safety's live filter.");
            ambiguous.Add("Read-only results only. Select the exact live row in Lobby Safety before any preview action.");
            return BlueSageCommandResult.HandledWithMessages(ambiguous.ToArray());
        }

        PlayerIdentityEvidence evidence = matches[0];

        if (copyEvidence)
        {
            GUIUtility.systemCopyBuffer = PlayerIdentityEvidenceController.BuildCopyableEvidence(evidence);
            return BlueSageCommandResult.HandledWithMessages(
                "Player details copied (row #" + (evidence.RosterIndex + 1) + ": name, Steam name, SteamID64, PlayerID and role). Nothing was sent to chat.");
        }

        var messages = new List<string>
        {
            "Found player | row #" + (evidence.RosterIndex + 1) + " | " + evidence.DisplayName,
            "SteamID64: " + evidence.SteamId + " | PlayerID " + evidence.NetworkPlayerId + " | ending …" + PlayerIdentityEvidenceController.Suffix(evidence.SteamId) + " | " + evidence.Role,
        };
        if (!string.IsNullOrWhiteSpace(evidence.SteamPersona)) messages.Add("Cached Steam persona: " + evidence.SteamPersona + ". Use the green profile button to open this exact account.");
        if (!string.Equals(evidence.DisplayNameRaw, evidence.DisplayName, StringComparison.Ordinal))
            messages.Add("Their name has hidden styling. Lobby Safety shows the exact text, and Local Audit keeps a copy if it's on.");
        messages.Add("Read-only evidence only. Recheck the profile before /sidban; cloned names never authorize or select a ban target.");
        return BlueSageCommandResult.HandledWithMessages(messages.ToArray());
    }

    private BlueSageCommandResult HandleIdentityResyncCommand(BlueSageCommandContext context)
    {
        return IdentityResyncController.HandleCommand(context);
    }

    private BlueSageCommandResult HandleSidBanCommand(BlueSageCommandContext context)
    {
        if (!CanUseLobbySafety(out _)) return LobbySafetyDeniedResult();
        string arguments = (context.Arguments ?? string.Empty).Trim();
        bool confirm = arguments.Equals("confirm", StringComparison.OrdinalIgnoreCase) || arguments.EndsWith(" confirm", StringComparison.OrdinalIgnoreCase);
        string query = confirm
            ? (arguments.Equals("confirm", StringComparison.OrdinalIgnoreCase) ? string.Empty : arguments.Substring(0, arguments.Length - " confirm".Length).Trim())
            : arguments;

        if (confirm)
        {
            return ConfirmPendingModeration("ban", query);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return BlueSageCommandResult.HandledWithMessages("SID ban: use a verified current row, C# incident, or exact SteamID64. The active host may also preview an exact off-roster SteamID64. Review the preview, then add confirm within 30 seconds.");
        }

        bool canIssue = SteamIdModerationController.CanLocalIssue(out bool isHost, out string unavailable);
        if (!canIssue) return BlueSageCommandResult.HandledWithMessages("SID ban: " + unavailable);

        string[] currentSteamIds = PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIds(out string rosterError).ToArray();
        if (!string.IsNullOrWhiteSpace(rosterError))
            return BlueSageCommandResult.HandledWithMessages("SID ban: verified live roster unavailable: " + rosterError + ". No preview was created.");
        bool exactOffRoster = ModerationProtocolPolicy.IsSteamId64(query) &&
                              !currentSteamIds.Contains(query, StringComparer.Ordinal);
        ModerationTarget target;
        string error;
        if (exactOffRoster)
        {
            if (!isHost)
            {
                return BlueSageCommandResult.HandledWithMessages("SID ban: only the active lobby host can preview an exact SteamID64 that is not in the current lobby. Helpers must select a verified current player.");
            }

            target = new ModerationTarget(query, "none");
        }
        else if (!ModerationTargetResolver.TryResolve(query, currentSteamIds, out target, out error))
        {
            return BlueSageCommandResult.HandledWithMessages("SID ban: " + error);
        }

        if (CloneIncidentLedger.IsProtectedVictim(target.SteamId))
        {
            return BlueSageCommandResult.HandledWithMessages("SID ban blocked: Steam " + FormatSteamIdSuffix(target.SteamId) + " is the protected victim in an open Clone Shield incident. Use the C# incident to target its suspect.");
        }

        if (SteamIdModerationController.IsAuthorizedCurrentIssuer(target.SteamId))
        {
            return BlueSageCommandResult.HandledWithMessages("SID ban blocked: the selected Steam account is the current host or an assigned Helper. No preview or pending action was created.");
        }

        _pendingModerationAction = BuildPendingModeration("ban", target, exactOffRoster);
        return BlueSageCommandResult.HandledWithMessages("Ban preview for " + target.SteamId +
            (target.IncidentId == "none" ? string.Empty : " from " + target.IncidentId) +
            (exactOffRoster
                ? " (not in this lobby, so no kick happens): check their Steam profile, then type /sidban " + target.SteamId + " confirm within 30 s."
                : ": check their Steam profile, then type /sidban " + (target.IncidentId == "none" ? target.SteamId : target.IncidentId) + " confirm within 30 s."));
    }

    private BlueSageCommandResult HandleSidKickCommand(BlueSageCommandContext context) => HandleSidLiveActionCommand(context, "kick", "SID kick");

    private BlueSageCommandResult HandleSidMuteCommand(BlueSageCommandContext context) => HandleSidLiveActionCommand(context, "mute", "SID mute");

    private BlueSageCommandResult HandleSidUnmuteCommand(BlueSageCommandContext context) => HandleSidLiveActionCommand(context, "unmute", "SID unmute");

    // Round 4 Stage E #3: kick, mute and unmute act only on a verified LIVE row (no offline path), through the same
    // preview + confirm + host-check flow as /sidban.
    private BlueSageCommandResult HandleSidLiveActionCommand(BlueSageCommandContext context, string action, string label)
    {
        if (!CanUseLobbySafety(out _)) return LobbySafetyDeniedResult();
        string arguments = (context.Arguments ?? string.Empty).Trim();
        bool confirm = arguments.Equals("confirm", StringComparison.OrdinalIgnoreCase) || arguments.EndsWith(" confirm", StringComparison.OrdinalIgnoreCase);
        string query = confirm
            ? (arguments.Equals("confirm", StringComparison.OrdinalIgnoreCase) ? string.Empty : arguments.Substring(0, arguments.Length - " confirm".Length).Trim())
            : arguments;

        if (confirm)
        {
            return ConfirmPendingModeration(action, query);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return BlueSageCommandResult.HandledWithMessages(label + ": use a verified current row, C# incident, or exact SteamID64 of a player who is in the lobby now. " +
                (action == "kick"
                    ? "Kick = ban through the game's own flow, wait until the player is gone (max 20 s), then unban; if they are still here at 20 s the ban stays and you are told."
                    : action == "mute"
                        ? "Mute = the host stops relaying that player's chat to everyone for this hosting session, including the host; the muted player's own messages stop appearing for them too, so they will notice. Voice is unaffected."
                        : "Unmute lifts a lobby-wide mute (an exact SteamID64 works even if the player already left)."));
        }

        if (!SteamIdModerationController.CanLocalIssue(out _, out string unavailable))
            return BlueSageCommandResult.HandledWithMessages(label + ": " + unavailable);

        string[] currentSteamIds = PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIds(out string rosterError).ToArray();
        if (!string.IsNullOrWhiteSpace(rosterError))
            return BlueSageCommandResult.HandledWithMessages(label + ": verified live roster unavailable: " + rosterError + ". No preview was created.");

        ModerationTarget target;
        bool exactOffRoster = false;
        if (action == "unmute" && ModerationProtocolPolicy.IsSteamId64(query) && !currentSteamIds.Contains(query, StringComparer.Ordinal))
        {
            // Lifting a mute is harmless, and the muted player may already have left: an exact id needs no live row.
            target = new ModerationTarget(query, "none");
            exactOffRoster = true;
        }
        else if (!ModerationTargetResolver.TryResolve(query, currentSteamIds, out target, out string error))
        {
            return BlueSageCommandResult.HandledWithMessages(label + ": " + error);
        }

        if (action != "unmute" && CloneIncidentLedger.IsProtectedVictim(target.SteamId))
        {
            return BlueSageCommandResult.HandledWithMessages(label + " blocked: Steam " + FormatSteamIdSuffix(target.SteamId) + " is the protected victim in an open Clone Shield incident.");
        }

        if (action != "unmute" && SteamIdModerationController.IsAuthorizedCurrentIssuer(target.SteamId))
        {
            return BlueSageCommandResult.HandledWithMessages(label + " blocked: the selected Steam account is the current host or an assigned Helper. No preview or pending action was created.");
        }

        _pendingModerationAction = BuildPendingModeration(action, target, exactOffRoster);
        string confirmWith = target.IncidentId == "none" ? target.SteamId : target.IncidentId;
        return BlueSageCommandResult.HandledWithMessages(label + " preview for " + target.SteamId +
            (target.IncidentId == "none" ? string.Empty : " from " + target.IncidentId) +
            ": check their Steam profile, then type /sid" + action + " " + confirmWith + " confirm within the countdown. The host checks the player is still here before acting; an assigned Helper's request goes through the host.");
    }

    private BlueSageCommandResult HandleSidUnbanCommand(BlueSageCommandContext context)
    {
        if (!CanUseLobbySafety(out _)) return LobbySafetyDeniedResult();
        string arguments = (context.Arguments ?? string.Empty).Trim();
        bool confirm = arguments.Equals("confirm", StringComparison.OrdinalIgnoreCase) || arguments.EndsWith(" confirm", StringComparison.OrdinalIgnoreCase);
        string query = confirm
            ? (arguments.Equals("confirm", StringComparison.OrdinalIgnoreCase) ? string.Empty : arguments.Substring(0, arguments.Length - " confirm".Length).Trim())
            : arguments;

        if (confirm) return ConfirmPendingModeration("unban", query);
        if (string.IsNullOrWhiteSpace(query))
        {
            return BlueSageCommandResult.HandledWithMessages("SID unban: host may use a full SteamID64 or unique 6+ digit suffix. Helpers must use the exact full SteamID64. Review the preview, then add confirm within 30 seconds.");
        }

        bool localCanIssue = SteamIdModerationController.CanLocalIssue(out bool isHost, out string unavailable);
        if (!localCanIssue) return BlueSageCommandResult.HandledWithMessages("SID unban: " + unavailable);

        ModerationTarget target;
        string error;
        if (isHost)
        {
            if (!ModerationTargetResolver.TryResolve(query, SteamIdModerationController.GetBannedSteamIds(), out target, out error))
                return BlueSageCommandResult.HandledWithMessages("SID unban: " + error);
        }
        else
        {
            if (!ModerationProtocolPolicy.IsSteamId64(query))
                return BlueSageCommandResult.HandledWithMessages("SID unban: Helpers must provide the exact 17-digit SteamID64 so the host can resolve its host-owned native ban list safely.");
            target = new ModerationTarget(query, "none");
        }

        _pendingModerationAction = BuildPendingModeration("unban", target, false);
        return BlueSageCommandResult.HandledWithMessages("SID unban PREVIEW: " + target.SteamId + ". Use /sidunban " + target.SteamId + " confirm within 30 seconds.");
    }

    private BlueSageCommandResult ConfirmPendingModeration(string action, string query)
    {
        if (!CanUseLobbySafety(out _))
        {
            _pendingModerationAction = null;
            return LobbySafetyDeniedResult();
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string lobbyCode = MonoSingleton<MultiplayerManager>.I?.LobbyCode ?? string.Empty;
        PendingModerationAction pending = _pendingModerationAction;
        if (pending == null || pending.Action != action || pending.ExpiresUnixSeconds < now || !string.Equals(pending.LobbyCode, lobbyCode, StringComparison.Ordinal))
        {
            _pendingModerationAction = null;
            return BlueSageCommandResult.HandledWithMessages("SID " + action + ": preview expired or lobby changed. Start again so the target is revalidated.");
        }

        if (string.IsNullOrWhiteSpace(query))
            return BlueSageCommandResult.HandledWithMessages("SID " + action + ": confirmation must repeat the previewed incident ID or full SteamID64; bare confirm is refused and no action was taken.");

        string expected = pending.IncidentId == "none" ? pending.TargetSteamId : pending.IncidentId;
        if (!string.Equals(query, expected, StringComparison.OrdinalIgnoreCase))
            return BlueSageCommandResult.HandledWithMessages("SID " + action + ": confirmation does not match the previewed target; no action was taken.");

        if (action == "ban")
        {
            if (pending.TargetWasOffline)
            {
                if (!SteamIdModerationController.CanLocalIssue(out bool isHost, out string unavailable) || !isHost ||
                    !ModerationProtocolPolicy.IsSteamId64(pending.TargetSteamId) ||
                    CloneIncidentLedger.IsProtectedVictim(pending.TargetSteamId))
                {
                    _pendingModerationAction = null;
                    return BlueSageCommandResult.HandledWithMessages("SID ban offline revalidation failed: " + unavailable + " No action was taken.");
                }
                IReadOnlyList<string> verifiedNow = PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIds(out string rosterError);
                if (!string.IsNullOrWhiteSpace(rosterError))
                {
                    _pendingModerationAction = null;
                    return BlueSageCommandResult.HandledWithMessages("SID ban offline revalidation failed: " + rosterError + ". No action was taken.");
                }
                if (verifiedNow.Contains(pending.TargetSteamId, StringComparer.Ordinal))
                {
                    _pendingModerationAction = null;
                    return BlueSageCommandResult.HandledWithMessages("SID ban offline revalidation failed: that SteamID joined the verified live roster after preview. Start a new live-player preview; no ban or kick was attempted.");
                }
            }
            else if (!ModerationTargetResolver.TryResolve(pending.IncidentId == "none" ? pending.TargetSteamId : pending.IncidentId,
                         SteamIdModerationController.GetCurrentPlayerSteamIds(), out ModerationTarget liveTarget, out string error) ||
                     !string.Equals(liveTarget.SteamId, pending.TargetSteamId, StringComparison.Ordinal) ||
                     CloneIncidentLedger.IsProtectedVictim(pending.TargetSteamId))
            {
                _pendingModerationAction = null;
                return BlueSageCommandResult.HandledWithMessages("SID ban revalidation failed: " + (error ?? "target or victim role changed") + ". No action was taken.");
            }
        }
        else if ((action == "kick" || action == "mute" || action == "unmute") && !(action == "unmute" && pending.TargetWasOffline))
        {
            // Round 4 Stage E #3: live actions re-resolve the exact row at confirm time, like a live ban. An exact-id
            // unmute of a player who already left skips this: the only effect is removing an id from the mute set.
            if (!ModerationTargetResolver.TryResolve(pending.IncidentId == "none" ? pending.TargetSteamId : pending.IncidentId,
                    SteamIdModerationController.GetCurrentPlayerSteamIds(), out ModerationTarget liveTarget, out string error) ||
                !string.Equals(liveTarget.SteamId, pending.TargetSteamId, StringComparison.Ordinal) ||
                (action != "unmute" && CloneIncidentLedger.IsProtectedVictim(pending.TargetSteamId)))
            {
                _pendingModerationAction = null;
                return BlueSageCommandResult.HandledWithMessages("SID " + action + " revalidation failed: " + (error ?? "target or victim role changed") + ". No action was taken.");
            }
        }

        _pendingModerationAction = null;
        bool sent = SteamIdModerationController.TrySendHelperRequest(action, pending.TargetSteamId, pending.IncidentId, out string message);
        // Round 4 Stage E #3: a host on an older BlueSage build ignores kick/mute/unmute requests silently, so a Helper is told.
        if (sent && (action == "kick" || action == "mute" || action == "unmute") &&
            SteamIdModerationController.CanLocalIssue(out bool localIsHost, out _) && !localIsHost)
        {
            message += " The host acts only if its BlueSage build accepts kick and mute (round 4 or later); an older host ignores this request silently.";
        }

        return BlueSageCommandResult.HandledWithMessages((sent ? "SID " + action + ": " : "SID " + action + " rejected: ") + message);
    }

    private static PendingModerationAction BuildPendingModeration(string action, ModerationTarget target, bool targetWasOffline)
    {
        return new PendingModerationAction
        {
            Action = action,
            TargetSteamId = target.SteamId,
            IncidentId = target.IncidentId,
            LobbyCode = MonoSingleton<MultiplayerManager>.I?.LobbyCode ?? string.Empty,
            TargetWasOffline = targetWasOffline,
            ExpiresUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30,
        };
    }

    private static BlueSageCommandResult LobbySafetyDeniedResult()
    {
        LobbySafetyAccessState state = SteamIdModerationController.GetLocalAccessState(out _, out string reason);
        return BlueSageCommandResult.HandledWithMessages(state == LobbySafetyAccessState.HelperWaitingForHost
            ? reason + " Protected data stayed locked and no moderation request was sent."
            : SteamIdModerationController.LobbySafetyDeniedMessage);
    }

    private static string SafeIncidentLabel(string value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private static bool TryGetCurrentSteamLobby(out CSteamID lobbyId)
    {
        lobbyId = CSteamID.Nil;
        if (PluginShutdownController.IsShuttingDown)
        {
            return false;
        }

        // Round 4 Stage I (advisor): vanilla's LobbyCode getter dereferences CurrentLobby, which a reconnect mod can
        // leave null after the game's host-lost teardown; a Helper in that state still needs the lobby to act, so the
        // Reconnect Guard's saved lobby id is the fallback.
        string code = null;
        try
        {
            code = MonoSingleton<MultiplayerManager>.I?.LobbyCode;
        }
        catch (Exception)
        {
            code = null;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            code = LastReconnectLobbyCode;
        }

        if (!SteamManager.Initialized || !ulong.TryParse(code, out ulong raw) || raw == 0UL)
        {
            return false;
        }
        lobbyId = new CSteamID(raw);
        return SteamMatchmaking.GetLobbyOwner(lobbyId) != CSteamID.Nil;
    }

    internal static string LastReconnectLobbyCode => Instance?._lastReconnectLobbyCode ?? string.Empty;

    private static bool TryResolveLobbyMemberSteamId(string query, out string steamId, out string error)
    {
        steamId = string.Empty;
        if (!PlayerIdentityEvidenceController.TryResolveLookup(query, out PlayerIdentityEvidence evidence, out error))
        {
            return false;
        }
        steamId = evidence.SteamId;
        return true;
    }

    private static string FormatSteamIdSuffix(string steamId) => "Steam …" + (steamId?.Length > 6 ? steamId.Substring(steamId.Length - 6) : steamId);

    private void StartCloneShieldController()
    {
        if (_cloneShieldControllerObject != null)
        {
            return;
        }

        _cloneShieldControllerObject = new GameObject("BlueSage_CloneShieldController");
        _cloneShieldControllerObject.AddComponent<CloneShieldController>();
        DontDestroyOnLoad(_cloneShieldControllerObject);
    }

    private void StartPlayerRenderSaverController()
    {
        if (PlayerRenderSaverPolicy.Retired)
        {
            return;
        }

        if (_playerRenderSaverControllerObject != null)
        {
            return;
        }

        _playerRenderSaverControllerObject = new GameObject("BlueSage_PlayerRenderSaverController");
        _playerRenderSaverControllerObject.AddComponent<PlayerRenderSaverController>();
        DontDestroyOnLoad(_playerRenderSaverControllerObject);
    }

    private void StartStyleHelperWindow()
    {
        if (_styleHelperWindow != null)
        {
            return;
        }

        GameObject windowObject = new GameObject("BlueSage_StyleHelperWindow");
        _styleHelperWindow = windowObject.AddComponent<StyleHelperWindow>();
        DontDestroyOnLoad(windowObject);
    }

    private void StartQolMenuWindow()
    {
        if (_qolMenuWindow != null)
        {
            return;
        }

        GameObject windowObject = new GameObject("BlueSage_QolMenuWindow");
        _qolMenuWindow = windowObject.AddComponent<QolMenuWindow>();
        _blueSageReportWindow = windowObject.AddComponent<BlueSageReportWindow>();
        DontDestroyOnLoad(windowObject);
    }

    internal static void OpenBlueSageReport()
    {
        if (Instance == null) return;
        if (Instance._qolMenuWindow == null) Instance.StartQolMenuWindow();
        Instance._blueSageReportWindow?.Open();
    }

    internal static void CloseBlueSageReport()
    {
        Patches.BlueSageReportTabPatch.RestoreNativeDefault();
    }

    internal static void CloseBlueSageReportWindowOnly()
    {
        Instance?._blueSageReportWindow?.CloseReport();
    }

    internal string ToggleStyleUiFromQolMenu()
    {
        if (!EnableStyleUi.Value)
        {
            return "Style Helper is turned off. Turn it on in the QoL menu Toggles.";
        }

        if (_styleHelperWindow == null)
        {
            StartStyleHelperWindow();
        }

        if (_styleHelperWindow == null)
        {
            return SomethingWentWrongNotice;
        }

        _styleHelperWindow.ToggleVisible();
        return _styleHelperWindow.IsVisible ? "Style Helper opened." : "Style Helper closed.";
    }

    internal string RunManualSweepFromQolMenu()
    {
        bool started = RunSweep("QoL menu", requireManualPermission: true);
        return DescribeManualSweep(started);
    }

    internal string RunHostHealthFromQolMenu()
    {
        return RunHostHealthCheck("QoL menu");
    }

    internal string RunQolStatusFromQolMenu()
    {
        LogDetailedQolStatus();
        string status = BuildQolStatusMessage();
        return AddLocalNotification(status)
            ? "QoL status was sent to your local chat."
            : "QoL status could not reach local chat yet; join a lobby or use /qol status after chat loads.";
    }

    private void StartPerformanceOverlayController()
    {
        if (_performanceOverlayControllerObject != null)
        {
            return;
        }

        _performanceOverlayControllerObject = new GameObject("BlueSage_PublicPerformanceOverlay");
        _performanceOverlayControllerObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(_performanceOverlayControllerObject);
        _performanceOverlayControllerObject.AddComponent<PerformanceOverlayController>();
    }

    internal void RunSweepRestartFromMenu()
    {
        RestartSweepLoop();
    }

    internal void ReconcileSweepScheduleFromMenu()
    {
        if (!EnableAutoSweep.Value || !AutoSweepAllowed)
        {
            return;
        }

        if (_sweepCoroutine == null)
        {
            RestartSweepLoop();
            return;
        }

        double nowSeconds = Time.realtimeSinceStartupAsDouble;
        _sweepSchedule.ReconcileInterval(nowSeconds, SweepIntervalMinutes.Value);
        double remainingSeconds = Math.Max(0d, _sweepSchedule.DueSeconds - nowSeconds);
        Log?.LogInfo($"Asset Sweep interval reconciled to {_sweepSchedule.ConfiguredIntervalMinutes} minutes without restarting elapsed time; next automatic deadline in {remainingSeconds:0} seconds.");
    }

    internal void RestartHostHealthFromMenu()
    {
        RestartHostHealthLoop();
    }

    internal void SaveConfigFromQolMenu()
    {
        Config.Save();
    }

    internal string SaveSelfPositionFromQolMenu()
    {
        TextChannelManager chat = NetworkSingleton<TextChannelManager>.I;
        Transform player = chat?.MainPlayer ?? chat?.MainMovementController?.transform;
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            return "Self movement: your player position is not ready yet.";
        }

        string value = SelfMovementPolicy.FormatPosition(player.position);
        SelfSavedPosition.Value = value;
        Config.Save();
        return "Self movement: saved your current position " + value + ".";
    }

    internal string TeleportSelfToSavedFromQolMenu()
    {
        if (!SelfMovementPolicy.TryParsePosition(SelfSavedPosition?.Value, out Vector3 target))
        {
            return "Self movement: no saved position yet. Use Save Pos first.";
        }

        Patches.SelfMovementPatch.QueueTeleport(target);
        return "Self movement: queued teleport to your saved position " + SelfMovementPolicy.FormatPosition(target) + ".";
    }

    internal string ToggleNoclipFlyFromQolMenu()
    {
        if (EnableNoclipFly == null)
        {
            return "Self movement: noclip/fly is not ready yet.";
        }

        EnableNoclipFly.Value = !EnableNoclipFly.Value;
        Patches.OptInRescuePatch.Stop();
        Patches.SelfMovementPatch.ResetVerticalVelocity();
        Config.Save();
        return EnableNoclipFly.Value
            ? "Self movement: noclip/fly on. WASD moves, Space/E rises, Ctrl/Q lowers, Shift boosts, and Alt gives precision control."
            : "Self movement: noclip/fly off. Vanilla collision and movement resumed.";
    }

    internal string SaveSelfMovementMultiplierFromQolMenu(string kind, string rawValue)
    {
        ConfigEntry<float> entry;
        float minimum;
        float maximum;
        string label;
        switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "speed":
                entry = SelfSpeedMultiplier;
                minimum = SelfMovementPolicy.MinimumSpeedMultiplier;
                maximum = SelfMovementPolicy.MaximumSpeedMultiplier;
                label = "speed";
                break;
            case "jump":
                entry = SelfJumpMultiplier;
                minimum = SelfMovementPolicy.MinimumJumpMultiplier;
                maximum = SelfMovementPolicy.MaximumJumpMultiplier;
                label = "jump";
                break;
            case "gravity":
                entry = SelfGravityMultiplier;
                minimum = SelfMovementPolicy.MinimumGravityMultiplier;
                maximum = SelfMovementPolicy.MaximumGravityMultiplier;
                label = "gravity";
                break;
            default:
                return "Self movement: unknown multiplier.";
        }

        string token = (rawValue ?? string.Empty).Trim();
        if (string.Equals(token, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "vanilla", StringComparison.OrdinalIgnoreCase))
        {
            token = "1";
        }

        if (entry == null || !float.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float requested))
        {
            return $"Self movement: {label} needs a multiplier from {minimum:0.##} to {maximum:0.##}, or reset.";
        }

        entry.Value = Mathf.Clamp(requested, minimum, maximum);
        Config.Save();
        return $"Self movement: {label} saved at {entry.Value:0.##}x.";
    }

    internal string ResetSelfMovementFromQolMenu()
    {
        EnableNoclipFly.Value = false;
        SelfSpeedMultiplier.Value = SelfMovementPolicy.DefaultMultiplier;
        SelfJumpMultiplier.Value = SelfMovementPolicy.DefaultMultiplier;
        SelfGravityMultiplier.Value = SelfMovementPolicy.DefaultMultiplier;
        Patches.SelfMovementPatch.CleanupRuntimeState();
        Config.Save();
        return "Self movement reset: noclip/fly off; speed, jump, and gravity are back to 1x vanilla.";
    }

    private BlueSageCommandResult HandleSelfMovementCommand(BlueSageCommandContext context)
    {
        string action = context.Tokens.Count > 0 ? context.Tokens[0].Trim().ToLowerInvariant() : "status";
        if (action == "status" || action == "?")
        {
            return BlueSageCommandResult.HandledWithMessages(
                $"Self movement: fly={FormatToggleState(ShouldApplyNoclipFly)}, speed={LockedSelfSpeedMultiplier:0.##}x, jump={LockedSelfJumpMultiplier:0.##}x, gravity={LockedSelfGravityMultiplier:0.##}x, saved={(string.IsNullOrWhiteSpace(SelfSavedPosition?.Value) ? "none" : SelfSavedPosition.Value)}.");
        }

        if (action == "save" || action == "savepos")
            return BlueSageCommandResult.HandledWithMessages(SaveSelfPositionFromQolMenu());
        if (action == "teleport" || action == "tp" || action == "saved")
            return BlueSageCommandResult.HandledWithMessages(TeleportSelfToSavedFromQolMenu());
        if (action == "reset")
            return BlueSageCommandResult.HandledWithMessages(ResetSelfMovementFromQolMenu());

        if (action == "fly" || action == "noclip")
        {
            string token = context.Tokens.Count > 1 ? context.Tokens[1].Trim().ToLowerInvariant() : "toggle";
            if (token == "status" || token == "?")
                return BlueSageCommandResult.HandledWithMessages("Self movement fly: " + FormatToggleState(ShouldApplyNoclipFly) + ".");
            bool? requested = ParseToggleRequestWithCurrent(token, ShouldApplyNoclipFly);
            if (!requested.HasValue)
                return BlueSageCommandResult.HandledWithMessages("Self movement fly: use on, off, toggle, or status.");
            if (requested.Value == ShouldApplyNoclipFly)
                return BlueSageCommandResult.HandledWithMessages("Self movement fly: " + FormatToggleState(ShouldApplyNoclipFly) + ".");
            return BlueSageCommandResult.HandledWithMessages(ToggleNoclipFlyFromQolMenu());
        }

        if (action == "speed" || action == "jump" || action == "gravity")
        {
            string value = context.Tokens.Count > 1 ? context.Tokens[1] : string.Empty;
            return BlueSageCommandResult.HandledWithMessages(SaveSelfMovementMultiplierFromQolMenu(action, value));
        }

        return BlueSageCommandResult.HandledWithMessages("Self movement: use status, save, teleport, fly [on|off], speed <0.25-5>, jump <0.25-5>, gravity <0.1-5>, or reset.");
    }

    private static string BuildQolStatusMessage()
    {
        return QolStatusTextPolicy.BuildPlayerSummary(
            NativeAssetSweepOwnership,
            EnableAutoSweep.Value && AutoSweepAllowed,
            ManualSweepAllowed,
            QolMenuKey.Value.ToString());
    }

    private static void LogDetailedQolStatus()
    {
        Log?.LogInfo("QoL detailed status: " + BuildDetailedQolStatusMessage());
    }

    private static string BuildDetailedQolStatusMessage()
    {
        return $"BlueSage QoL: AutomaticCleanup={FormatToggleState(EnableAutoSweep.Value && AutoSweepAllowed)}, ManualCleanup={FormatToggleState(ManualSweepAllowed)}, HostHealth={FormatToggleState(EnableHostHealthMonitor.Value)}, LeaveNotices={FormatToggleState(ShouldApplyLeaveNotifications)}, Welcome={FormatToggleState(EnableWelcomeMessage.Value)}, Timestamps={FormatToggleState(ShouldApplyChatTimestamps)}({(Use24HourTime.Value ? "24-hour" : "12-hour AM/PM")}), ChatReadability={FormatToggleState(ShouldApplyChatReadability)}, ChatBackdrop={FormatToggleState(ShouldApplyPersistentChatBackdrop)}, ChatOutline={LockedChatOutlineIntensityPercent}%, ChatUIScale={LockedChatUiScalePercent}%, ChatFont={LockedChatFontSizePercent}%, ChatWindowHeight={LockedChatWindowHeightPercent}%, ChatRows={LockedChatHistoryRows}, OutlineColors=#{LockedChatOutlineColorHex}/#{LockedBlackChatOutlineColorHex}, BlackNamesOutline={FormatToggleState(ShouldApplyBlackNamesOutline)}, BetterMove={FormatToggleState(ShouldApplyBetterMove)}, UnlimitedConsumables={FormatToggleState(ShouldApplyUnlimitedConsumables)}, FocusAnywhere={FormatToggleState(EnableFocusAnywhere.Value)}, PlayerPanel={FormatToggleState(EnableEnhancedPlayerPanel.Value)}, SelfFly={FormatToggleState(ShouldApplyNoclipFly)}, SelfMove={LockedSelfSpeedMultiplier:0.##}x/{LockedSelfJumpMultiplier:0.##}x/{LockedSelfGravityMultiplier:0.##}x, ReconnectGuard={FormatToggleState(EnableReconnectGuard.Value)}, ReconnectMessage={FormatToggleState(EnableReconnectAnnouncement.Value)}, PingMentions={FormatToggleState(EnablePingMentions.Value)}, PingSound={FormatToggleState(EnablePingSound.Value)}, CloneShield={FormatToggleState(ShouldApplyCloneShield)}, RenderSaver={FormatToggleState(ShouldApplyPlayerRenderSaver)}({LockedPlayerRenderSaverRadiusMeters}m/max{LockedPlayerRenderSaverMaxVisiblePlayers}, hidden={PlayerRenderSaverController.LastHiddenRemotePlayers}), ChatCopy={FormatToggleState(ShouldApplyChatCopy)}, ChatLinks={FormatToggleState(ShouldApplyChatUrlLinks)}, LocalAudit={FormatToggleState(EnableLocalAuditExports?.Value == true)}, Spoons={FormatToggleState(ShouldApplySpoons)}, StyleUI={FormatToggleState(EnableStyleUi.Value)}, AvatarSlots4-9={FormatToggleState(EnableExtendedAvatarStyles?.Value == true)}, Chalkboards={FormatToggleState(ChalkboardPersistenceController.Available)}, MiniMapIDClick={FormatToggleState(EnableMiniMapPlayerLabels.Value)}, MiniMapFriends={FormatToggleState(ShouldApplyMiniMapFriendColors)}, QoLMenu={QolMenuKey.Value} or /qolmenu, LongerText=always on.";
    }

    private BlueSageCommandResult HandleChatOutlineCommand(BlueSageCommandContext context)
    {
        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim().ToLowerInvariant() : "status";

        if (token == "status" || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages($"Chat Outline: {LockedChatOutlineIntensityPercent}%.");
        }

        if (!int.TryParse(token.TrimEnd('%'), out int requested))
        {
            return BlueSageCommandResult.HandledWithMessages("Chat Outline: use a number from 25 to 100, or status.");
        }

        int clamped = ChatReadabilityStylePolicy.ClampOutlineIntensityPercent(requested);
        ChatOutlineIntensity.Value = clamped;
        Config.Save();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RefreshVisibleChat();
        return BlueSageCommandResult.HandledWithMessages($"Chat Outline: {clamped}%.");
    }

    private BlueSageCommandResult HandleChatScaleCommand(BlueSageCommandContext context)
    {
        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim() : "status";

        if (string.Equals(token, "status", StringComparison.OrdinalIgnoreCase) || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages($"Chat UI Scale (whole panel): {LockedChatUiScalePercent}%.");
        }

        if (!ChatAccessibilityScalePolicy.TryParseScaleToken(token, out int clamped))
        {
            return BlueSageCommandResult.HandledWithMessages("Chat UI Scale grows the whole panel/input/backdrop/text. Use 75-200, decimal scale like 1.25, reset, or status. Use /chatfont for message text only.");
        }

        ChatUiScalePercent.Value = clamped;
        Config.Save();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
        return BlueSageCommandResult.HandledWithMessages($"Chat UI Scale (whole panel): {clamped}%.");
    }

    private BlueSageCommandResult HandleChatFontCommand(BlueSageCommandContext context)
    {
        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim() : "status";

        if (string.Equals(token, "status", StringComparison.OrdinalIgnoreCase) || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages($"Chat Font (message text only): {LockedChatFontSizePercent}%.");
        }

        if (string.Equals(token, "toggle", StringComparison.OrdinalIgnoreCase))
        {
            int toggled = LockedChatFontSizePercent > ChatFontSizePolicy.DefaultFontSizePercent
                ? ChatFontSizePolicy.DefaultFontSizePercent
                : ChatFontSizePolicy.EnabledFontSizePercent;
            ChatFontSizePercent.Value = toggled;
            Config.Save();
            BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
            return BlueSageCommandResult.HandledWithMessages($"Chat Font (message text only): {toggled}%.");
        }

        if (!ChatFontSizePolicy.TryParseFontSizeToken(token, out int clamped))
        {
            return BlueSageCommandResult.HandledWithMessages("Chat Font changes message text only. Use on/off/toggle, 75-200, decimal size like 1.25, reset, or status. Use /chatscale for the whole panel.");
        }

        ChatFontSizePercent.Value = clamped;
        Config.Save();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
        return BlueSageCommandResult.HandledWithMessages($"Chat Font (message text only): {clamped}%.");
    }

    private BlueSageCommandResult HandleChatHeightCommand(BlueSageCommandContext context)
    {
        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim() : "status";

        if (string.Equals(token, "status", StringComparison.OrdinalIgnoreCase) || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages($"Chat Window Height (vertical log space): {LockedChatWindowHeightPercent}%.");
        }

        if (string.Equals(token, "toggle", StringComparison.OrdinalIgnoreCase))
        {
            int toggled = LockedChatWindowHeightPercent > ChatWindowHeightPolicy.DefaultHeightPercent
                ? ChatWindowHeightPolicy.DefaultHeightPercent
                : ChatWindowHeightPolicy.EnabledHeightPercent;
            ChatWindowHeightPercent.Value = toggled;
            Config.Save();
            BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
            return BlueSageCommandResult.HandledWithMessages($"Chat Window Height (vertical log space): {toggled}%.");
        }

        if (!ChatWindowHeightPolicy.TryParseHeightToken(token, out int clamped))
        {
            return BlueSageCommandResult.HandledWithMessages("Chat Window Height adds vertical log space only. Use on/off/toggle, 100-200, decimal size like 1.5, reset, or status.");
        }

        ChatWindowHeightPercent.Value = clamped;
        Config.Save();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
        return BlueSageCommandResult.HandledWithMessages($"Chat Window Height (vertical log space): {clamped}%.");
    }

    private BlueSageCommandResult HandleChatHistoryCommand(BlueSageCommandContext context)
    {
        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim() : "status";
        if (string.Equals(token, "status", StringComparison.OrdinalIgnoreCase) || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages($"Chat Rows: {LockedChatHistoryRows}. Default 150; range 25-250. Higher vanilla/other-mod limits remain untouched.");
        }

        if (string.Equals(token, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "default", StringComparison.OrdinalIgnoreCase))
        {
            ChatHistoryRows.Value = ChatHistoryLimitPolicy.DefaultRows;
            Config.Save();
            return BlueSageCommandResult.HandledWithMessages("Chat Rows: reset to 150 (balanced default).");
        }

        if (!int.TryParse(token, out int requested))
        {
            return BlueSageCommandResult.HandledWithMessages("Chat Rows: use 25-250, reset, or status.");
        }

        int clamped = ChatHistoryLimitPolicy.ClampRows(requested);
        ChatHistoryRows.Value = clamped;
        Config.Save();
        return BlueSageCommandResult.HandledWithMessages($"Chat Rows: {clamped}. Larger histories cost more UI memory/work in busy lobbies.");
    }

    private BlueSageCommandResult HandleChatVanillaCommand(BlueSageCommandContext context)
    {
        ResetChatUiToVanilla();
        return BlueSageCommandResult.HandledWithMessages("Chat UI reset: whole-panel scale=100%, message font=100%, window height=100%.");
    }

    internal string ResetChatUiToVanillaFromMenu()
    {
        ResetChatUiToVanilla();
        return "Chat UI reset to vanilla whole-panel scale, message font size, and window height.";
    }

    private void ResetChatUiToVanilla()
    {
        if (ChatUiScalePercent != null)
        {
            ChatUiScalePercent.Value = ChatAccessibilityScalePolicy.DefaultScalePercent;
        }

        if (ChatFontSizePercent != null)
        {
            ChatFontSizePercent.Value = ChatFontSizePolicy.DefaultFontSizePercent;
        }

        if (ChatWindowHeightPercent != null)
        {
            ChatWindowHeightPercent.Value = ChatWindowHeightPolicy.DefaultHeightPercent;
        }

        Config.Save();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RestoreChatWindowHeight();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RestoreChatAccessibilityScale();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RestoreChatFontSize();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
    }

    private BlueSageCommandResult HandlePlayerRenderSaverCommand(BlueSageCommandContext context)
    {
        if (PlayerRenderSaverPolicy.Retired)
        {
            return BlueSageCommandResult.HandledWithMessages(PlayerRenderSaverPolicy.RetiredMessage);
        }

        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim().ToLowerInvariant() : "status";

        if (token == "status" || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages(BuildPlayerRenderSaverStatus());
        }

        if (token == "reset")
        {
            PlayerRenderSaverRadiusMeters.Value = PlayerRenderSaverPolicy.DefaultRadiusMeters;
            PlayerRenderSaverMaxVisiblePlayers.Value = PlayerRenderSaverPolicy.DefaultMaxVisiblePlayers;
            Config.Save();
            if (!ShouldApplyPlayerRenderSaver)
            {
                PlayerRenderSaverController.RestoreAll();
            }

            return BlueSageCommandResult.HandledWithMessages(BuildPlayerRenderSaverStatus());
        }

        if (token == "radius")
        {
            if (context.Tokens.Count < 2 || !int.TryParse(context.Tokens[1].Trim().TrimEnd('m'), out int requestedRadius))
            {
                return BlueSageCommandResult.HandledWithMessages("Player Render Saver radius: use /playersaver radius 45, range 10-250 meters.");
            }

            PlayerRenderSaverRadiusMeters.Value = PlayerRenderSaverPolicy.ClampRadiusMeters(requestedRadius);
            Config.Save();
            return BlueSageCommandResult.HandledWithMessages(BuildPlayerRenderSaverStatus());
        }

        if (token == "max" || token == "maxvisible")
        {
            if (context.Tokens.Count < 2 || !int.TryParse(context.Tokens[1].Trim(), out int requestedMax))
            {
                return BlueSageCommandResult.HandledWithMessages("Player Render Saver max visible: use /playersaver max 32, range 1-128 closest players.");
            }

            PlayerRenderSaverMaxVisiblePlayers.Value = PlayerRenderSaverPolicy.ClampMaxVisiblePlayers(requestedMax);
            Config.Save();
            return BlueSageCommandResult.HandledWithMessages(BuildPlayerRenderSaverStatus());
        }

        bool? requested = ParseToggleRequestWithCurrent(token, EnablePlayerRenderSaver.Value);
        if (!requested.HasValue)
        {
            return BlueSageCommandResult.HandledWithMessages("Player Render Saver: use on, off, toggle, status, reset, radius 45, or max 32.");
        }

        EnablePlayerRenderSaver.Value = requested.Value;
        Config.Save();
        if (!EnablePlayerRenderSaver.Value)
        {
            PlayerRenderSaverController.RestoreAll();
            return BlueSageCommandResult.HandledWithMessages("Player Render Saver: off. Restored all hidden player renderers.");
        }

        return BlueSageCommandResult.HandledWithMessages(BuildPlayerRenderSaverStatus());
    }

    private BlueSageCommandResult HandleChalkSaveCommand(BlueSageCommandContext context)
    {
        if (context.Tokens.Count < 1)
        {
            return BlueSageCommandResult.HandledWithMessages("Chalkboard save: use /chalksave name [board index].");
        }
        string board = context.Tokens.Count > 1 ? context.Tokens[1] : string.Empty;
        return BlueSageCommandResult.HandledWithMessages(ChalkboardPersistenceController.Save(context.Tokens[0], board));
    }

    private BlueSageCommandResult HandleChalkLoadCommand(BlueSageCommandContext context)
    {
        if (context.Tokens.Count < 2)
        {
            return BlueSageCommandResult.HandledWithMessages("Chalkboard restore: first run /chalk boards, then use /chalkload name <exact board index 0-2>.");
        }
        return BlueSageCommandResult.HandledWithMessages(
            ChalkboardPersistenceController.Load(context.Tokens[0], context.Tokens[1]));
    }

    private BlueSageCommandResult HandleChalkCommand(BlueSageCommandContext context)
    {
        string action = context.Tokens.Count > 0
            ? context.Tokens[0].Trim().ToLowerInvariant()
            : "status";
        if (action == "status" || action == "help" || action == "?")
        {
            return BlueSageCommandResult.HandledWithMessages(
                ChalkboardPersistenceController.Status(),
                "Use /chalk boards or /chalk refresh to map exact indices 0-2. Local: view/save/list/delete/refresh. Shared: Host-direct or authenticated Helper-to-Host load/clear after a fresh map. Legacy /chalksave, /chalkload, and /chalklist remain available.");
        }
        if (action == "boards" || action == "where" || action == "here")
        {
            return BlueSageCommandResult.HandledWithMessages(ChalkboardPersistenceController.Boards());
        }
        if (action == "refresh")
        {
            return BlueSageCommandResult.HandledWithMessages(ChalkboardPersistenceController.RefreshMap());
        }
        if (action == "view")
        {
            string boardToken = context.Tokens.Count > 1 ? context.Tokens[1] : string.Empty;
            return BlueSageCommandResult.HandledWithMessages(ChalkboardPersistenceController.View(boardToken));
        }
        if (action == "clear")
        {
            string boardToken = context.Tokens.Count > 1 ? context.Tokens[1] : string.Empty;
            return BlueSageCommandResult.HandledWithMessages(ChalkboardPersistenceController.TryClear(boardToken));
        }
        if (action == "save")
        {
            if (context.Tokens.Count < 2)
            {
                return BlueSageCommandResult.HandledWithMessages("Chalkboard save: use /chalk save name [board index].");
            }
            string boardToken = context.Tokens.Count > 2 ? context.Tokens[2] : string.Empty;
            return BlueSageCommandResult.HandledWithMessages(
                ChalkboardPersistenceController.Save(context.Tokens[1], boardToken));
        }
        if (action == "load" || action == "restore")
        {
            if (context.Tokens.Count < 3)
            {
                return BlueSageCommandResult.HandledWithMessages("Chalkboard restore: first run /chalk boards, then use /chalk load name <exact board index 0-2>.");
            }
            return BlueSageCommandResult.HandledWithMessages(
                ChalkboardPersistenceController.Load(context.Tokens[1], context.Tokens[2]));
        }
        if (action == "list")
        {
            return BlueSageCommandResult.HandledWithMessages(ChalkboardPersistenceController.List());
        }
        if (action == "delete")
        {
            if (context.Tokens.Count < 2)
            {
                return BlueSageCommandResult.HandledWithMessages("Chalkboard local delete: use /chalk delete name.");
            }
            return BlueSageCommandResult.HandledWithMessages(
                ChalkboardPersistenceController.Delete(context.Tokens[1]));
        }

        return BlueSageCommandResult.HandledWithMessages(
            "Chalkboards: /chalk boards|refresh, view <index>, save name [index], list, delete name, load name <index>, or clear <index>. Index 3 is never accepted.");
    }

    private static string BuildPlayerRenderSaverStatus()
    {
        return $"Player Render Saver: {FormatToggleState(ShouldApplyPlayerRenderSaver)}, radius={LockedPlayerRenderSaverRadiusMeters}m, maxVisible={LockedPlayerRenderSaverMaxVisiblePlayers}, candidates={PlayerRenderSaverController.LastCandidateRemotePlayers}, visible={PlayerRenderSaverController.LastVisibleRemotePlayers}, hidden={PlayerRenderSaverController.LastHiddenRemotePlayers}, renderersSuppressed={PlayerRenderSaverController.LastSuppressedRenderers}, state={PlayerRenderSaverController.LastRuntimeState}. Local-only optimization; chat and player list stay visible.";
    }

    private BlueSageCommandResult HandleOutlineColorCommand(BlueSageCommandContext context, string label, ConfigEntry<string> entry, string fallback)
    {
        if (entry == null)
        {
            return BlueSageCommandResult.HandledWithMessages(label + ": config is not ready yet.");
        }

        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim() : "status";
        if (string.Equals(token, "status", StringComparison.OrdinalIgnoreCase) || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages(label + ": #" + ChatReadabilityStylePolicy.NormalizeHexColor(entry.Value, fallback) + ".");
        }

        string normalized = ChatReadabilityStylePolicy.NormalizeHexColor(token, string.Empty);
        if (string.IsNullOrEmpty(normalized))
        {
            return BlueSageCommandResult.HandledWithMessages(label + ": use a six-character hex color like #000000, or status.");
        }

        entry.Value = normalized;
        Config.Save();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RefreshVisibleChat();
        return BlueSageCommandResult.HandledWithMessages(label + ": #" + normalized + ".");
    }

    private static string BuildBlueSageClueCommandName()
    {
        return DecodeCommandName("cmvftdmvft");
    }

    private static string DecodeCommandName(string encoded)
    {
        char[] chars = encoded.ToCharArray();
        for (int index = 0; index < chars.Length; index++)
        {
            chars[index] = (char)(chars[index] - 1);
        }

        return new string(chars);
    }

    private BlueSageCommandResult HandleToggleCommand(
        BlueSageCommandContext context,
        string label,
        ConfigEntry<bool> configEntry,
        Action afterChange = null,
        string actionToken = null,
        Func<string> actionHandler = null)
    {
        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim().ToLowerInvariant() : "toggle";

        if (token == "status" || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages($"{label}: {FormatToggleState(configEntry.Value)}.");
        }

        if (!string.IsNullOrWhiteSpace(actionToken) &&
            (token == actionToken || (actionToken == "run" && token == "now") || (actionToken == "now" && token == "run")))
        {
            return BlueSageCommandResult.HandledWithMessages(actionHandler?.Invoke() ?? $"{label}: action complete.");
        }

        bool? requested = ParseToggleRequestWithCurrent(token, configEntry.Value);
        if (!requested.HasValue)
        {
            return BlueSageCommandResult.HandledWithMessages($"{label}: use on, off, toggle, or status.");
        }

        configEntry.Value = requested.Value;
        Config.Save();
        afterChange?.Invoke();
        return BlueSageCommandResult.HandledWithMessages($"{label}: {FormatToggleState(configEntry.Value)}.");
    }

    private BlueSageCommandResult HandleAssetSweepCommand(BlueSageCommandContext context)
    {
        if (context.Tokens.Count == 0)
        {
            return BlueSageCommandResult.HandledWithMessages(
                DescribeManualSweep(RunSweep("manual command", requireManualPermission: true)));
        }

        return HandleToggleCommand(
            context,
            "Automatic Cleanup",
            EnableAutoSweep,
            RestartSweepLoop,
            "now",
            () => DescribeManualSweep(RunSweep("manual command", requireManualPermission: true)));
    }

    private BlueSageCommandResult HandleTimestampCommand(BlueSageCommandContext context)
    {
        string token = context.Tokens.Count > 0 ? context.Tokens[0].Trim().ToLowerInvariant() : "toggle";

        if (token == "status" || token == "?")
        {
            return BlueSageCommandResult.HandledWithMessages($"Timestamps: chat={FormatToggleState(EnableChatTimestamps.Value)}, join/leave={FormatToggleState(EnableNotificationTimestamps.Value)}, format={(Use24HourTime.Value ? "24-hour" : "12-hour AM/PM")}.");
        }

        if (token == "12" || token == "12h" || token == "12hr")
        {
            Use24HourTime.Value = false;
            Config.Save();
            return BlueSageCommandResult.HandledWithMessages("Timestamps: 12-hour format with AM/PM.");
        }

        if (token == "24" || token == "24h" || token == "24hr")
        {
            Use24HourTime.Value = true;
            Config.Save();
            return BlueSageCommandResult.HandledWithMessages("Timestamps: 24-hour format.");
        }

        if (token == "reset" || token == "default")
        {
            EnableChatTimestamps.Value = true;
            EnableNotificationTimestamps.Value = true;
            Use24HourTime.Value = false;
            Config.Save();
            return BlueSageCommandResult.HandledWithMessages("Timestamps: reset to enabled 12-hour AM/PM defaults.");
        }

        bool? requested = ParseToggleRequestWithCurrent(token, EnableChatTimestamps.Value && EnableNotificationTimestamps.Value);
        if (!requested.HasValue)
        {
            return BlueSageCommandResult.HandledWithMessages("Timestamps: use on, off, toggle, status, 12, 24, or reset.");
        }

        EnableChatTimestamps.Value = requested.Value;
        SyncNotificationTimestampsWithChatTimestamps("timestamp command");
        Config.Save();

        return BlueSageCommandResult.HandledWithMessages($"Timestamps: {FormatToggleState(EnableChatTimestamps.Value)}.");
    }

    private static void SyncNotificationTimestampsWithChatTimestamps(string reason)
    {
        if (EnableNotificationTimestamps.Value == EnableChatTimestamps.Value)
        {
            return;
        }

        EnableNotificationTimestamps.Value = EnableChatTimestamps.Value;
        Instance?.Config.Save();
        Log?.LogInfo($"Timestamps: synced join/leave/system notices with chat timestamps via {reason}.");
    }

    private static bool? ParseToggleRequest(string token)
    {
        switch (token)
        {
            case "":
            case "toggle":
            case "t":
                return null;
            case "on":
            case "1":
            case "true":
            case "yes":
            case "enable":
            case "enabled":
                return true;
            case "off":
            case "0":
            case "false":
            case "no":
            case "disable":
            case "disabled":
                return false;
            default:
                return bool.TryParse(token, out bool parsed) ? parsed : (bool?)null;
        }
    }

    private bool? ParseToggleRequestWithCurrent(string token, bool current)
    {
        bool? requested = ParseToggleRequest(token);
        if (requested.HasValue)
        {
            return requested.Value;
        }

        if (string.IsNullOrWhiteSpace(token) || token == "toggle" || token == "t")
        {
            return !current;
        }

        return null;
    }

    private static string FormatToggleState(bool enabled)
    {
        return enabled ? "on" : "off";
    }

    private void RestartHostHealthLoop()
    {
        if (_hostHealthCoroutine != null)
        {
            StopCoroutine(_hostHealthCoroutine);
            _hostHealthCoroutine = null;
        }

        if (EnableHostHealthMonitor.Value && HostHealthRepairsAllowed)
        {
            _hostHealthCoroutine = StartCoroutine(HostHealthLoop());
        }
    }

    private IEnumerator HostHealthLoop()
    {
        yield return new WaitForSecondsRealtime(30f);

        while (true)
        {
            RunHostHealthCheck("automatic timer");
            int intervalSeconds = LockedHostHealthIntervalSeconds;
            yield return new WaitForSecondsRealtime(intervalSeconds);
        }
    }

    private string RunHostHealthCheck(string reason)
    {
        bool isManualRun = reason.IndexOf("manual", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!EnableHostHealthMonitor.Value && !isManualRun)
        {
            return "Host Health is disabled.";
        }

        HostHealthInput input = CaptureHostHealthInput(out CSteamID? lobbyId, out string[] playerSteamIds);
        PlayerLimitCompanionStatus playerLimitStatus = CapturePlayerLimitCompanionStatusWithLobby();
        HostHealthDecision decision = HostHealthPolicy.Evaluate(input);
        bool applyRepairs = HostHealthRepairsAllowed;

        if (!decision.ShouldRunDiagnostics)
        {
            // Round 5d sweep: a guest's automatic tick wrote this every 5 minutes (about 288 lines a day); manual runs still say it.
            if (isManualRun) Log.LogInfo($"Host Health skipped via {reason}: local player is not the host/lobby owner.");
            else Log.LogDebug($"Host Health skipped via {reason}: local player is not the host/lobby owner.");
            return "Host Health: only the lobby host runs these checks.";
        }

        string issueText = decision.Issues.Count == 0 ? "healthy" : string.Join(", ", decision.Issues.Select(issue => issue.ToString()).ToArray());
        // Round 4 Stage D log diet (#7): automatic ticks log only when the health picture changed (one line per
        // streak); manual runs always answer. The member counts are not part of the key: they change on every join
        // or leave and now live in the host journal's status rows.
        string healthKey = issueText;
        string previousHealthKey = _lastHostHealthLogKey;
        bool healthChanged = !string.Equals(_lastHostHealthLogKey, healthKey, StringComparison.Ordinal);
        _lastHostHealthLogKey = healthKey;
        if (isManualRun || healthChanged)
        {
            Log.LogInfo($"Host Health check via {reason}: {issueText}. {playerLimitStatus.ToDiagnosticText()}.");
        }

        HostJournalController.RecordHostHealth(input.LobbyValid, issueText);

        if (applyRepairs && decision.Actions.Contains(HostHealthAction.RefreshLobbyMetadata) && LockedHostHealthRefreshLobbyMetadata)
        {
            RefreshLobbyMetadataHeartbeat(lobbyId, playerLimitStatus);
        }

        if (applyRepairs && decision.Actions.Contains(HostHealthAction.RequestPersonaRefresh) && LockedHostHealthRequestPersonaRefresh)
        {
            RequestPersonaRefresh(playerSteamIds);
        }

        if (decision.Issues.Count > 0 && (isManualRun || healthChanged))
        {
            // One warning per streak, not one per tick (#7).
            Log.LogWarning($"Host Health diagnostic issue via {reason}: {issueText}. No recreate-lobby warning is emitted without join-failure evidence.");
        }

        if (decision.Actions.Contains(HostHealthAction.NotifyHostLobbyLost) && healthChanged &&
            string.Equals(previousHealthKey, "healthy", StringComparison.Ordinal))
        {
            // Round 5b: on 2026-10-23 the host sat on LobbyOwnerInvalid for 40 minutes without a word while guests
            // cycled through reconnects. One toast per streak, and only on a healthy-to-lost transition, so a session
            // that never had a Steam lobby (offline or solo hosting) never hears it. The journal row carries the state.
            AddLocalNotificationWithToast(HostLobbyLostNotice);
        }

        // Voice pass (2026-10-23): the reply is plain; the issue names and the PlayerLimit detail stay in the
        // "Host Health check via" log line above.
        string repairText = applyRepairs
            ? "BlueSage refreshes your lobby listing for you."
            : "Another mod handles lobby repairs, so BlueSage just watches.";
        return "Host Health: " + DescribeHostHealthForPlayer(decision.Issues) + ". " + repairText;
    }

    private static string DescribeHostHealthForPlayer(IEnumerable<HostHealthIssue> issues)
    {
        var plain = new List<string>();
        foreach (HostHealthIssue issue in issues)
        {
            switch (issue)
            {
                case HostHealthIssue.LobbyOwnerInvalid:
                    // Worded apart from HostLobbyLostNotice: the log readout counts that notice's phrase once per loss.
                    plain.Add("Steam lost your lobby, so new players can't join");
                    break;
                case HostHealthIssue.HostIdentityLost:
                    plain.Add("the game lost track of you as host");
                    break;
                case HostHealthIssue.HostMissingFromPlayerList:
                    plain.Add("you're missing from the player list");
                    break;
                case HostHealthIssue.HostPersonaEmpty:
                    plain.Add("your Steam name hasn't loaded yet");
                    break;
                default:
                    plain.Add(issue.ToString());
                    break;
            }
        }

        return plain.Count == 0 ? "all good" : string.Join("; ", plain.ToArray());
    }

    private void StartCommunityBanListMaintenance()
    {
        if (_communityBanListCoroutine != null)
        {
            StopCoroutine(_communityBanListCoroutine);
        }

        _communityBanListCoroutine = StartCoroutine(CommunityBanListMaintenanceLoop());
        if (EnableCommunityBanListSync?.Value == true)
        {
            CommunityBanListController.SetNextAttempt(DateTime.UtcNow.AddSeconds(45));
        }
        else
        {
            CommunityBanListController.MarkDisabled();
        }
    }

    private IEnumerator CommunityBanListMaintenanceLoop()
    {
        yield return new WaitForSecondsRealtime(45f);

        while (true)
        {
            bool completed = false;
            bool succeeded = false;
            if (EnableCommunityBanListSync != null && EnableCommunityBanListSync.Value)
            {
                yield return CommunityBanListController.ReconcileFromSource(result =>
                {
                    succeeded = result;
                    completed = true;
                });
            }
            else
            {
                CommunityBanListController.MarkDisabled();
            }

            float nextCheckSeconds = completed && succeeded
                ? CommunityBanListController.SuccessIntervalSeconds
                : CommunityBanListController.RetryIntervalSeconds;
            if (EnableCommunityBanListSync?.Value == true)
            {
                CommunityBanListController.SetNextAttempt(DateTime.UtcNow.AddSeconds(nextCheckSeconds));
            }
            yield return new WaitForSecondsRealtime(nextCheckSeconds);
        }
    }

    internal string RefreshCommunityBanListFromQolMenu()
    {
        if (EnableCommunityBanListSync?.Value != true)
        {
            return "Community BanData refresh is off. Enable the local opt-in first.";
        }
        if (CommunityBanListController.RefreshInFlight)
        {
            Log?.LogInfo("Community BanData detailed status: " + CommunityBanListController.StatusText());
            return "Community BanData refresh is already running. " +
                   CommunityBanListController.FriendlyStatusText();
        }
        StartCoroutine(RunCommunityBanManualRefresh());
        return "Checking the Community Safety List now. It only adds names; it never kicks or bans.";
    }

    internal void ReconcileCommunityBanMaintenanceFromQolMenu()
    {
        StartCommunityBanListMaintenance();
    }

    private IEnumerator RunCommunityBanManualRefresh()
    {
        bool succeeded = false;
        yield return CommunityBanListController.ReconcileFromSource(result => succeeded = result);
        Log?.LogInfo(
            "Community BanData manual refresh " + (succeeded ? "completed. " : "failed closed. ") +
            CommunityBanListController.StatusText());
        AddLocalNotification(CommunityBanListController.FriendlyStatusText());
    }

    private BlueSageCommandResult HandleCommunityBanCommand(BlueSageCommandContext context)
    {
        string action = context.Tokens.Count > 0
            ? context.Tokens[0].Trim().ToLowerInvariant()
            : "status";
        Log?.LogInfo("Community BanData detailed status: " + CommunityBanListController.StatusText());
        if (action == "status" || action == "?")
        {
            return BlueSageCommandResult.HandledWithMessages(
                CommunityBanListController.FriendlyStatusText());
        }
        if (action == "attestation")
        {
            return BlueSageCommandResult.HandledWithMessages(
                CommunityBanListController.FriendlyStatusText(),
                "Attestation: local BlueSageQoLModSuite/trackers/community-ban-attestation.json (list facts only, no player names).");
        }
        if (action == "refresh" || action == "validate")
        {
            return BlueSageCommandResult.HandledWithMessages(
                RefreshCommunityBanListFromQolMenu());
        }
        if (action == "on" || action == "off")
        {
            EnableCommunityBanListSync.Value = action == "on";
            Config.Save();
            StartCommunityBanListMaintenance();
            return BlueSageCommandResult.HandledWithMessages(
                CommunityBanListController.FriendlyStatusText());
        }
        return BlueSageCommandResult.HandledWithMessages(
            "Community BanData: use /communitybans status, refresh, on, off, or attestation. Refresh is local/additive and never performs a live moderation action.");
    }

    private BlueSageCommandResult HandleAuditLogCommand(BlueSageCommandContext context)
    {
        string action = (context.Arguments ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action) || action == "status" || action == "?")
            return BlueSageCommandResult.HandledWithMessages(SessionAuditController.StatusText());

        if (action == "on")
        {
            EnableLocalAuditExports.Value = true;
            Config.Save();
            SessionAuditController.ReconcileEnabledState();
            return BlueSageCommandResult.HandledWithMessages(SessionAuditController.StatusText());
        }

        if (action == "off")
        {
            EnableLocalAuditExports.Value = false;
            Config.Save();
            SessionAuditController.ReconcileEnabledState();
            return BlueSageCommandResult.HandledWithMessages("Local Audit is off. Your saved files stay on this computer; type /auditlog on to turn it back on.");
        }

        if (action == "path" || action == "folder" || action == "copy")
            return BlueSageCommandResult.HandledWithMessages(SessionAuditController.CopyAuditFolder());

        if (action == "snapshot" || action == "flush")
            return BlueSageCommandResult.HandledWithMessages(SessionAuditController.SnapshotNow());

        return BlueSageCommandResult.HandledWithMessages("Local Audit: type /auditlog status, on, off, path, or snapshot. Your files stay on this computer and are never uploaded.");
    }

    private HostHealthInput CaptureHostHealthInput(out CSteamID? lobbyId, out string[] playerSteamIds)
    {
        lobbyId = null;
        playerSteamIds = Array.Empty<string>();

        bool isHost = false;
        bool lobbyValid = false;
        string localSteamId = string.Empty;
        string lobbyOwnerSteamId = string.Empty;
        int serverPlayerCount = 0;
        string hostPersonaName = string.Empty;

        try
        {
            isHost = NetworkManager.main != null && NetworkManager.main.isHost;
        }
        catch
        {
            isHost = false;
        }

        try
        {
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            lobbyValid = multiplayerManager != null && multiplayerManager.LobbyStatus;
            string lobbyCode = multiplayerManager?.LobbyCode;
            if (!string.IsNullOrWhiteSpace(lobbyCode) && ulong.TryParse(lobbyCode, out ulong lobbyCodeValue))
            {
                lobbyId = new CSteamID(lobbyCodeValue);
                CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobbyId.Value);
                lobbyOwnerSteamId = ((ulong)owner).ToString();
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Host Health could not read Steam lobby owner: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            localSteamId = ((ulong)SteamUser.GetSteamID()).ToString();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Host Health could not read local SteamID: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel != null)
            {
                playerSteamIds = panel.PlayerSteamIDs?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? Array.Empty<string>();
                serverPlayerCount = panel.PlayerIDs?.Count(playerId => playerId.isServer) ?? 0;
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Host Health could not read player panel state: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            if (TryParseSteamId(lobbyOwnerSteamId, out CSteamID ownerId))
            {
                hostPersonaName = SteamFriends.GetFriendPersonaName(ownerId);
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Host Health could not read host persona: {ex.GetType().Name}: {ex.Message}");
        }

        if (_hostHealthStartedAt < 0f)
        {
            _hostHealthStartedAt = Time.unscaledTime;
        }

        double lobbyAgeHours = Math.Max(0d, (Time.unscaledTime - _hostHealthStartedAt) / 3600d);

        return new HostHealthInput(
            isHost,
            lobbyValid,
            localSteamId,
            lobbyOwnerSteamId,
            playerSteamIds,
            serverPlayerCount,
            hostPersonaName,
            lobbyAgeHours,
            LockedHostHealthWarnAfterHours);
    }

    // Round 5 F: the heartbeat line is written on change only (451 of 690 QoL Info lines in a 37.5 h host were it).
    private static string _lastHostHealthHeartbeatLogKey = string.Empty;

    private static void RefreshLobbyMetadataHeartbeat(CSteamID? lobbyId, PlayerLimitCompanionStatus playerLimitStatus)
    {
        if (PluginShutdownController.IsShuttingDown) return;
        try
        {
            if (!lobbyId.HasValue || (ulong)lobbyId.Value == 0UL)
            {
                Log.LogWarning("Host Health H1: cannot refresh lobby metadata because lobby ID is unavailable.");
                return;
            }

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string steamHeartbeatTicks = DateTime.UtcNow.Ticks.ToString();
            bool ok = SteamMatchmaking.SetLobbyData(lobbyId.Value, "bluesage_qol_heartbeat", timestamp);
            bool heartbeatCompatOk = SteamMatchmaking.SetLobbyData(lobbyId.Value, "heartbeat", steamHeartbeatTicks);
            int refreshedSurfaceKeys = RefreshLongRunningLobbySurfaceMetadata(lobbyId.Value);
            PublishPlayerLimitCompanionMetadata(lobbyId.Value, playerLimitStatus);

            if (ok || heartbeatCompatOk)
            {
                string playerLimitText = playerLimitStatus?.ToDiagnosticText() ?? "PlayerLimit=unknown";
                string heartbeatLogKey = HostHealthHeartbeatLogPolicy.BuildKey(heartbeatCompatOk, refreshedSurfaceKeys, playerLimitText);
                if (HostHealthHeartbeatLogPolicy.ShouldLog(_lastHostHealthHeartbeatLogKey, heartbeatLogKey))
                {
                    _lastHostHealthHeartbeatLogKey = heartbeatLogKey;
                    Log.LogInfo($"Host Health H1: refreshed Steam lobby heartbeat metadata at {timestamp}; heartbeatCompat={(heartbeatCompatOk ? "ok" : "failed")}; preservedSurfaceKeys={refreshedSurfaceKeys}; {playerLimitText}. Logged on change only; the lobby count is in the host journal.");
                }
            }
            else
            {
                Log.LogWarning("Host Health H1: SteamMatchmaking.SetLobbyData returned false.");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Host Health H1 refresh failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static int RefreshLongRunningLobbySurfaceMetadata(CSteamID lobbyId)
    {
        int refreshed = 0;
        string[] keys =
        {
            "Name",
            "focus",
            "mature",
            "chill",
            "break",
            "modded",
        };

        foreach (string key in keys)
        {
            try
            {
                string value = SteamMatchmaking.GetLobbyData(lobbyId, key);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (SteamMatchmaking.SetLobbyData(lobbyId, key, value))
                {
                    refreshed++;
                }
            }
            catch (Exception ex)
            {
                Log.LogDebug($"Host Health H1: skipped lobby surface metadata key '{key}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        return refreshed;
    }

    private static void PublishPlayerLimitCompanionMetadata(CSteamID lobbyId, PlayerLimitCompanionStatus playerLimitStatus)
    {
        if (playerLimitStatus == null || !playerLimitStatus.IsLoaded)
        {
            return;
        }

        SetOptionalLobbyData(lobbyId, "bluesage_playerlimit_cap", playerLimitStatus.EffectiveMaxLobbySize);
        SetOptionalLobbyData(lobbyId, "bluesage_playerlimit_default", playerLimitStatus.EffectiveDefaultLobbySize);
        SetOptionalLobbyData(lobbyId, "bluesage_playerlimit_shift", playerLimitStatus.EffectiveShiftSkipRate);
        if (playerLimitStatus.ChatRelayPatchEnabled.HasValue)
        {
            SteamMatchmaking.SetLobbyData(lobbyId, "bluesage_playerlimit_chat_relay", playerLimitStatus.ChatRelayPatchEnabled.Value ? "true" : "false");
        }
    }

    private static void SetOptionalLobbyData(CSteamID lobbyId, string key, int? value)
    {
        if (value.HasValue)
        {
            SteamMatchmaking.SetLobbyData(lobbyId, key, value.Value.ToString());
        }
    }

    internal static PlayerLimitCompanionStatus GetPlayerLimitCompanionStatus() =>
        CapturePlayerLimitCompanionStatusWithLobby();

    private static PlayerLimitCompanionStatus CapturePlayerLimitCompanionStatusWithLobby()
    {
        PlayerLimitCompanionStatus status = CapturePlayerLimitCompanionStatus();
        TryGetVisibleLobbyCounts(out int? memberCount, out int? steamMaxPlayers);
        return status.WithLobby(memberCount, steamMaxPlayers);
    }

    private static PlayerLimitCompanionStatus CapturePlayerLimitCompanionStatus()
    {
        if (!IsPluginLoaded(PluginCompatibility.BlueSagePlayerLimitLiftGuid))
        {
            return PlayerLimitCompanionStatus.NotLoaded();
        }

        try
        {
            Type pluginType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("BlueSagePatched.PlayerLimitLift.Plugin", false))
                .FirstOrDefault(candidate => candidate != null);

            if (pluginType == null)
            {
                return PlayerLimitCompanionStatus.FromLoadedValues(null, null, null, null);
            }

            return PlayerLimitCompanionStatus.FromLoadedValues(
                ReadStaticNullableInt(pluginType, "EffectiveMaxLobbySize"),
                ReadStaticNullableInt(pluginType, "EffectiveDefaultLobbySize"),
                ReadStaticNullableInt(pluginType, "EffectiveShiftSkipRate"),
                ReadStaticConfigEntryBool(pluginType, "EnableChatRelayPatch"));
        }
        catch (Exception ex)
        {
            Log.LogWarning($"PlayerLimit companion probe failed: {ex.GetType().Name}: {ex.Message}");
            return PlayerLimitCompanionStatus.FromLoadedValues(null, null, null, null);
        }
    }

    private static int? ReadStaticNullableInt(Type type, string propertyName)
    {
        object value = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null, null);
        return value is int intValue ? intValue : (int?)null;
    }

    private static bool? ReadStaticConfigEntryBool(Type type, string propertyName)
    {
        object configEntry = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null, null);
        object value = configEntry?.GetType().GetProperty("Value")?.GetValue(configEntry, null);
        return value is bool boolValue ? boolValue : (bool?)null;
    }

    private static void RequestPersonaRefresh(IEnumerable<string> playerSteamIds)
    {
        int requested = 0;
        foreach (string steamId in playerSteamIds ?? Array.Empty<string>())
        {
            try
            {
                if (!TryParseSteamId(steamId, out CSteamID csteamId))
                {
                    continue;
                }

                SteamFriends.RequestUserInformation(csteamId, true);
                requested++;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Host Health H4 persona refresh failed for {steamId}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Log.LogInfo($"Host Health H4: requested Steam persona refresh for {requested} player(s).");
    }

    private static bool TryParseSteamId(string value, out CSteamID steamId)
    {
        steamId = default;
        if (string.IsNullOrWhiteSpace(value) || !ulong.TryParse(value, out ulong raw) || raw == 0UL)
        {
            return false;
        }

        steamId = new CSteamID(raw);
        return true;
    }

    internal static bool HandleConnectionLostForReconnectGuard(NotificationStatus notificationStatus)
    {
        if (Instance == null)
        {
            return true;
        }

        return Instance.HandleConnectionLostForReconnectGuardInstance(notificationStatus);
    }

    private bool HandleConnectionLostForReconnectGuardInstance(NotificationStatus notificationStatus)
    {
        if (!EnableReconnectGuard.Value)
        {
            return true;
        }

        if (_reconnectMenuFallbackInProgress)
        {
            Log.LogInfo("Reconnect Guard: menu fallback is already in progress; allowing normal disconnect flow.");
            return true;
        }

        if (_cleanMenuReconnectActive)
        {
            Log.LogInfo($"Reconnect Guard: generation {_cleanMenuReconnectGeneration} already owns recovery in phase {_cleanMenuReconnectPhase}; suppressing duplicate ConnectionLost callback.");
            return false;
        }

        if (_lastLobbyRejoinInProgress)
        {
            Log.LogInfo("Reconnect Guard: saved-lobby recovery is already in progress; suppressing re-entrant disconnect handling.");
            return false;
        }

        bool andrewReconnectLoaded = IsPluginLoaded(PluginCompatibility.AndrewReconnectGuid);
        bool isHost = false;
        bool isConnected = false;
        try
        {
            isHost = NetworkManager.main != null && NetworkManager.main.isHost;
            isConnected = NetworkManager.main != null && NetworkManager.main.clientState == PurrNet.Transports.ConnectionState.Connected;
        }
        catch
        {
            // If PurrNet state is unavailable, let the base game and Reconnect handle it.
        }

        if (isHost || isConnected)
        {
            StopManagedReconnect("host/local client is already connected");
            return true;
        }

        TryCaptureReconnectLobbyCode();

        TryGetVisibleLobbyCounts(out int? memberCount, out int? maxPlayers);
        if (memberCount.HasValue && memberCount.Value >= 1 && _lastLobbyVisibleAt < 0f)
        {
            _lastLobbyVisibleAt = Time.unscaledTime;
        }

        ReconnectGuardDecision decision = ReconnectGuardPolicy.Evaluate(new ReconnectGuardInput(
            guardEnabled: EnableReconnectGuard.Value,
            reconnectLoaded: andrewReconnectLoaded,
            suppressHostKickReconnect: !LockedReconnectManageHostKick,
            blockDuringInitialLobbyGrace: LockedReconnectUseLobbyGrace,
            requireVisibleLobby: LockedReconnectRequireVisibleLobby,
            reconnectAttemptIntervalSeconds: LockedReconnectRetryIntervalSeconds,
            initialLobbyGraceSeconds: LockedReconnectLobbyGraceSeconds,
            maxGuardedAttempts: LockedReconnectMaxAttempts,
            isHost: isHost,
            isIntentionalLeave: false,
            isAlreadyReconnecting: _cleanMenuReconnectActive,
            notificationStatusName: notificationStatus.ToString(),
            secondsSinceLobbyCreated: _lastLobbyVisibleAt < 0f ? (float?)null : Time.unscaledTime - _lastLobbyVisibleAt,
            secondsSinceLastManagedAttempt: _lastManagedReconnectAttemptAt < 0f ? (float?)null : Time.unscaledTime - _lastManagedReconnectAttemptAt,
            reconnectAttempt: Math.Max(1, _managedReconnectAttempt + 1),
            visibleLobbyMemberCount: memberCount,
            visibleLobbyMaxPlayers: maxPlayers,
            lobbyGenerationsUsed: GetReconnectGenerationsUsedForLobby(_lastReconnectLobbyCode),
            maxGenerationsPerLobby: LockedReconnectMaxGenerationsPerLobby,
            secondsSinceLastGuardActivity: GetSecondsSinceReconnectGuardIdle(),
            budgetIdleResetSeconds: LockedReconnectBudgetIdleResetSeconds));

        switch (decision.Action)
        {
            case ReconnectGuardAction.AllowReconnectMod:
                Log.LogInfo($"Reconnect Guard: {decision.Reason}");
                return true;
            case ReconnectGuardAction.Noop:
                Log.LogInfo($"Reconnect Guard: {decision.Reason}");
                return true;
            case ReconnectGuardAction.StopManagedRetry:
                Log.LogWarning($"Reconnect Guard: {decision.Reason}");
                StopManagedReconnect(decision.Reason);
                if (decision.BudgetSpent)
                {
                    // The native ReturnMenu runs (return true); the player lands on the menu and hears why.
                    AddLocalNotificationWithToast(ReconnectBudgetSpentNotice);
                }

                return true;
            case ReconnectGuardAction.WaitForManagedRetryCadence:
                Log.LogInfo($"Reconnect Guard: {decision.Reason}");
                return true;
            case ReconnectGuardAction.ScheduleManagedRetry:
            case ReconnectGuardAction.RunManagedRetryNow:
                Log.LogWarning($"Reconnect Guard: {decision.Reason}");
                if (andrewReconnectLoaded)
                {
                    MarkAndrewReconnectIntentionalLeave();
                }

                StartCleanMenuReconnect($"native {notificationStatus}", nativeTeardownAlreadyStarting: true);
                return true;
            default:
                Log.LogWarning($"Reconnect Guard: unexpected policy action {decision.Action}; allowing base reconnect flow.");
                return true;
        }
    }

    private void StartCleanMenuReconnect(string source, bool nativeTeardownAlreadyStarting)
    {
        if (_cleanMenuReconnectActive || _cleanMenuReconnectCoroutine != null)
        {
            Log.LogInfo($"Reconnect Guard: existing generation {_cleanMenuReconnectGeneration} retained; ignored overlapping start from {source}.");
            return;
        }

        _cleanMenuReconnectActive = true;
        NoteReconnectGenerationStarted();
        _managedReconnectSuccessAnnounced = false;
        _reconnectReadySampleCount = 0;
        _reconnectQuorumReadySince = -1f;
        int generation = ++_cleanMenuReconnectGeneration;
        _cleanMenuReconnectCoroutine = StartCoroutine(CleanMenuReconnectLoop(generation, source, nativeTeardownAlreadyStarting));
    }

    private IEnumerator CleanMenuReconnectLoop(int generation, string source, bool nativeTeardownAlreadyStarting)
    {
        _cleanMenuReconnectPhase = "native-teardown";
        Log.LogWarning($"Reconnect Guard generation {generation}: {source}; native teardown must reach the clean menu before any join attempt.");
        AddLocalNotificationWithToast(ReconnectStartingNotice);

        if (!nativeTeardownAlreadyStarting)
        {
            bool initialMenuReady = IsCleanMenuReadyForReconnect(requireBlueSageJoinIdle: false, out string initialMenuReason);
            if (!initialMenuReady)
            {
                RequestNativeReturnToMenuForReconnect();
            }
            else
            {
                Log.LogWarning($"Reconnect Guard generation {generation}: initial menu state is already clean; skipped redundant native ReturnMenu ({initialMenuReason}).");
            }
        }

        float menuWaited = 0f;
        int consecutiveMenuReadyFrames = 0;
        while (menuWaited < LockedReconnectCleanupTimeoutSeconds)
        {
            yield return null;
            menuWaited += Time.unscaledDeltaTime;
            if (generation != _cleanMenuReconnectGeneration)
            {
                yield break;
            }

            bool menuReady = IsCleanMenuReadyForReconnect(out string menuReason);
            consecutiveMenuReadyFrames = menuReady ? consecutiveMenuReadyFrames + 1 : 0;
            if (consecutiveMenuReadyFrames >= 3)
            {
                Log.LogWarning($"Reconnect Guard generation {generation}: clean menu confirmed after {menuWaited:0.0}s ({menuReason}).");
                break;
            }
        }

        if (consecutiveMenuReadyFrames < 3)
        {
            FinishCleanMenuReconnect(generation, "native teardown did not reach a clean menu; no rejoin was attempted");
            AddLocalNotificationWithToast(ReconnectStoppedNotice);
            yield break;
        }

        string targetLobbyCode = (_lastReconnectLobbyCode ?? string.Empty).Trim();
        if (!ulong.TryParse(targetLobbyCode, out ulong targetLobbyId) || targetLobbyId == 0UL)
        {
            FinishCleanMenuReconnect(generation, "saved lobby id was unavailable after native teardown");
            yield break;
        }

        // Hold before rejoining: the host may still be counting this player's old connection, and an
        // instant rejoin double-counts them (wrong player counts, possibly a non-joinable lobby).
        _cleanMenuReconnectPhase = "holding-before-rejoin";
        int holdSeconds = ReconnectRejoinHoldPolicy.ClampHoldSeconds(ReconnectRejoinHoldSeconds?.Value ?? ReconnectRejoinHoldPolicy.DefaultHoldSeconds);
        Log.LogWarning($"Reconnect Guard generation {generation}: holding {holdSeconds}s before rejoining so the host can clear the old connection.");
        AddLocalNotificationWithToast(ReconnectRejoinHoldPolicy.BuildHoldNotice(holdSeconds));
        float heldSeconds = 0f;
        float notReadyStreakSeconds = 0f;
        // Baseline for edge-triggered abort: only a join that starts DURING the hold cancels it.
        bool joinInProgressAtHoldStart = IsPlayerJoinInProgress();
        Log.LogWarning($"Reconnect Guard generation {generation}: join-in-progress at hold start = {joinInProgressAtHoldStart}.");
        while (!ReconnectRejoinHoldPolicy.ShouldJoinNow(heldSeconds, holdSeconds))
        {
            yield return null;
            heldSeconds += Time.unscaledDeltaTime;
            if (generation != _cleanMenuReconnectGeneration)
            {
                yield break;
            }

            // Definitive signals abort at once: Reconnect Guard switched off, or the player started
            // a join themselves (the game's own IsConnecting / a lobby).
            if (ReconnectRejoinHoldPolicy.ShouldAbortImmediately(EnableReconnectGuard.Value, IsPlayerJoinInProgress(), joinInProgressAtHoldStart))
            {
                string abortReason = EnableReconnectGuard.Value ? "you started a join yourself" : "Reconnect Guard was turned off";
                FinishCleanMenuReconnect(generation, $"rejoin hold aborted: {abortReason}; no automatic rejoin");
                AddLocalNotificationWithToast(ReconnectStoppedNotice);
                yield break;
            }

            // The player left the clean menu: never stack an automatic join on top of theirs. This
            // check is noisy, so only a sustained not-ready streak cancels; a single flicker must
            // not kill a legitimate reconnect.
            bool holdMenuReady = IsCleanMenuReadyForReconnect(out string holdMenuReason);
            notReadyStreakSeconds = ReconnectRejoinHoldPolicy.UpdateNotReadyStreak(notReadyStreakSeconds, holdMenuReady, Time.unscaledDeltaTime);
            if (ReconnectRejoinHoldPolicy.ShouldCancelHold(notReadyStreakSeconds))
            {
                FinishCleanMenuReconnect(generation, $"player left the clean menu during the rejoin hold ({holdMenuReason}); no automatic rejoin");
                AddLocalNotificationWithToast(ReconnectStoppedNotice);
                yield break;
            }
        }

        // Final check on the exact frame before joining: the streak tolerance above must never let
        // an automatic join land on a join the player started in the hold's last moments.
        bool finalMenuReady = IsCleanMenuReadyForReconnect(out string finalMenuReason);
        if (!ReconnectRejoinHoldPolicy.IsSafeToJoinNow(EnableReconnectGuard.Value, IsPlayerJoinInProgress(), joinInProgressAtHoldStart, finalMenuReady))
        {
            FinishCleanMenuReconnect(generation, $"final pre-join check failed ({finalMenuReason}); no automatic rejoin");
            AddLocalNotificationWithToast(ReconnectStoppedNotice);
            yield break;
        }

        _cleanMenuReconnectPhase = "joining-saved-lobby";
        _lastLobbyRejoinInProgress = true;
        NoteReconnectReturnTarget(targetLobbyCode);
        Log.LogWarning($"Reconnect Guard generation {generation}: joining saved lobby {RedactLobbyCode(targetLobbyCode)} from clean menu with isChangeLobby=false.");
        AddLocalNotificationWithToast(ReconnectJoiningNotice);

        try
        {
            MonoSingleton<MultiplayerManager>.I.JoinByCode(targetLobbyCode, false);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Reconnect Guard generation {generation}: clean-menu join could not start: {ex.GetType().Name}: {ex.Message}");
            FinishCleanMenuReconnect(generation, "clean-menu join threw before it started");
            yield break;
        }

        _cleanMenuReconnectPhase = "validating-playable-scene";
        float connectedSince = -1f;
        float validationWaited = 0f;
        while (validationWaited < LockedReconnectJoinValidationTimeoutSeconds)
        {
            yield return null;
            validationWaited += Time.unscaledDeltaTime;
            if (generation != _cleanMenuReconnectGeneration)
            {
                yield break;
            }

            bool clientConnected = IsNetworkClientConnected();
            if (clientConnected && connectedSince < 0f)
            {
                connectedSince = Time.unscaledTime;
            }
            else if (!clientConnected)
            {
                connectedSince = -1f;
            }

            ReconnectRuntimeHealthDecision decision = EvaluateManagedReconnectRuntime(connectedSince);
            if (decision.Action == ReconnectRuntimeHealthAction.ConfirmSuccess)
            {
                Log.LogWarning($"Reconnect Guard generation {generation}: playable reconnect confirmed after {validationWaited:0.0}s: {decision.Reason}");
                AnnounceManagedReconnectSuccess();
                FinishCleanMenuReconnect(generation, "playable scene confirmed");
                yield break;
            }
        }

        Log.LogWarning($"Reconnect Guard generation {generation}: saved-lobby join did not produce a playable scene within {LockedReconnectJoinValidationTimeoutSeconds}s; returning through native cleanup.");
        AddLocalNotificationWithToast(ReconnectIncompleteNotice);
        bool terminalMenuReady = IsCleanMenuReadyForReconnect(requireBlueSageJoinIdle: false, out string terminalMenuReason);
        if (!terminalMenuReady)
        {
            RequestNativeReturnToMenuForReconnect();
        }
        else
        {
            Log.LogWarning($"Reconnect Guard generation {generation}: terminal menu state is already clean; skipped redundant native ReturnMenu ({terminalMenuReason}).");
        }

        float fallbackMenuWaited = 0f;
        int fallbackMenuReadyFrames = 0;
        while (fallbackMenuWaited < LockedReconnectCleanupTimeoutSeconds)
        {
            yield return null;
            fallbackMenuWaited += Time.unscaledDeltaTime;
            bool menuReady = IsCleanMenuReadyForReconnect(requireBlueSageJoinIdle: false, out _);
            fallbackMenuReadyFrames = menuReady ? fallbackMenuReadyFrames + 1 : 0;
            if (fallbackMenuReadyFrames >= 3)
            {
                break;
            }
        }

        FinishCleanMenuReconnect(generation, "saved-lobby validation failed without another same-lobby retry");
        if (fallbackMenuReadyFrames >= 3 && TryStartReplacementLobbyDiscovery())
        {
            yield break;
        }

        AddLocalNotificationWithToast(ReconnectStoppedNotice);
    }

    private void FinishCleanMenuReconnect(int generation, string reason)
    {
        if (generation != _cleanMenuReconnectGeneration)
        {
            return;
        }

        Log.LogWarning($"Reconnect Guard generation {generation} finished in phase {_cleanMenuReconnectPhase}: {reason}.");
        if (!string.Equals(reason, "playable scene confirmed", StringComparison.Ordinal))
        {
            // A reconnect that never got in must not quiet a later manual join to the same lobby.
            ClearReconnectReturnTarget();
        }

        MarkReconnectGuardIdle();
        _cleanMenuReconnectActive = false;
        _cleanMenuReconnectPhase = "idle";
        _cleanMenuReconnectCoroutine = null;
        _lastLobbyRejoinInProgress = false;
        _reconnectMenuFallbackInProgress = false;
        _reconnectReadySampleCount = 0;
        _reconnectQuorumReadySince = -1f;
    }

    // True when the game itself reports a join underway or a lobby: the player started their own
    // join. Used by the rejoin hold (IsConnecting used to be computed for logs only).
    private static bool IsPlayerJoinInProgress()
    {
        try
        {
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            return multiplayerManager != null && (multiplayerManager.IsConnecting || multiplayerManager.LobbyStatus);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCleanMenuReadyForReconnect(out string reason)
    {
        return IsCleanMenuReadyForReconnect(requireBlueSageJoinIdle: true, out reason);
    }

    private static bool IsCleanMenuReadyForReconnect(bool requireBlueSageJoinIdle, out string reason)
    {
        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            bool menuReady = MonoSingleton<MainMenuUIController>.I != null;
            bool transportStopped = !IsNetworkClientConnected();
            bool lobbyCleared = multiplayerManager == null || !multiplayerManager.LobbyStatus;
            bool vanillaIsConnecting = multiplayerManager != null && multiplayerManager.IsConnecting;
            bool blueSageJoinIdle = Instance == null || !Instance._lastLobbyRejoinInProgress;
            var state = new ReconnectMenuReadinessState(
                sceneLoaded: activeScene.isLoaded,
                activeSceneBuildIndex: activeScene.buildIndex,
                menuAvailable: menuReady,
                transportStopped: transportStopped,
                lobbyCleared: lobbyCleared,
                blueSageJoinIdle: blueSageJoinIdle);
            reason = $"scene={activeScene.buildIndex}, menu={menuReady}, transportStopped={transportStopped}, lobbyCleared={lobbyCleared}, vanillaIsConnecting={vanillaIsConnecting}, blueSageJoinIdle={blueSageJoinIdle}, requireBlueSageJoinIdle={requireBlueSageJoinIdle}";
            return requireBlueSageJoinIdle
                ? ReconnectTerminalCleanupPolicy.IsReadyForNewJoin(state)
                : ReconnectTerminalCleanupPolicy.IsReady(state);
        }
        catch (Exception ex)
        {
            reason = "menu readiness unavailable: " + ex.GetType().Name;
            return false;
        }
    }

    private void TryCaptureReconnectLobbyCode()
    {
        try
        {
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            if (multiplayerManager == null || !multiplayerManager.LobbyStatus)
            {
                return;
            }

            string lobbyCode = multiplayerManager.LobbyCode;
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                return;
            }

            lobbyCode = lobbyCode.Trim();
            if (!ulong.TryParse(lobbyCode, out ulong lobbyId) || lobbyId == 0UL)
            {
                return;
            }

            if (!string.Equals(_lastReconnectLobbyCode, lobbyCode, StringComparison.Ordinal))
            {
                _lastReconnectLobbyCode = lobbyCode;
                _lastLobbyRejoinInProgress = false;
                _replacementLobbyDiscoveryAttempted = false;
                Log.LogInfo("Reconnect Guard saved current lobby id for last-lobby fallback.");
            }

            CSteamID currentLobbyId = new CSteamID(lobbyId);
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
            if (owner != CSteamID.Nil)
            {
                _lastReconnectLobbyOwnerSteamId = ((ulong)owner).ToString();
            }

            string lobbyName = SteamMatchmaking.GetLobbyData(currentLobbyId, "Name");
            _lastReconnectLobbyNameFingerprint = GetLobbyNameFingerprint(lobbyName);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Reconnect Guard could not capture current lobby id: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void HandleReconnectFailureFallback(string reason)
    {
        if (TryRejoinLastLobbyBeforeMenuFallback(reason))
        {
            return;
        }

        AddLocalNotificationWithToast(ReconnectLobbyNotFoundNotice);
        ReturnToMenuAfterFailedReconnectCore();
    }

    private bool TryRejoinLastLobbyBeforeMenuFallback(string reason)
    {
        if (_cleanMenuReconnectActive || string.IsNullOrWhiteSpace(_lastReconnectLobbyCode))
        {
            return false;
        }

        if (!ulong.TryParse(_lastReconnectLobbyCode, out ulong lobbyId) || lobbyId == 0UL)
        {
            Log.LogWarning("Reconnect Guard saved lobby id was invalid; skipping last-lobby rejoin fallback.");
            return false;
        }

        Log.LogWarning($"Reconnect Guard legacy fallback redirected into one clean-menu generation: {reason}.");
        StartCleanMenuReconnect("legacy fallback redirected: " + reason, nativeTeardownAlreadyStarting: false);
        return true;
    }

    private bool TryStartReplacementLobbyDiscovery()
    {
        if (_replacementLobbyDiscoveryAttempted ||
            string.IsNullOrWhiteSpace(_lastReconnectLobbyOwnerSteamId) ||
            _replacementLobbyDiscoveryCoroutine != null)
        {
            return false;
        }

        _replacementLobbyDiscoveryAttempted = true;
        _lastLobbyRejoinInProgress = true;
        _replacementLobbyDiscoveryCoroutine = StartCoroutine(ReplacementLobbyDiscoveryLoop());
        AddLocalNotificationWithToast(ReconnectSearchingNotice);
        return true;
    }

    private IEnumerator ReplacementLobbyDiscoveryLoop()
    {
        int[] pollDelays = { 5, 5, 10, 10, 15 };
        foreach (int delaySeconds in pollDelays)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);

            List<string> candidates = new List<string>();
            for (int distance = 1; distance <= 3; distance++)
            {
                Task searchTask = StartReplacementLobbySearch(distance);
                if (searchTask == null)
                {
                    continue;
                }

                float taskWaited = 0f;
                while (!searchTask.IsCompleted && taskWaited < 8f)
                {
                    yield return null;
                    taskWaited += Time.unscaledDeltaTime;
                }

                if (!searchTask.IsCompleted || searchTask.IsFaulted || searchTask.IsCanceled)
                {
                    continue;
                }

                foreach (object lobby in ReadTaskResultItems(searchTask))
                {
                    string candidateId = ReadLobbyString(lobby, "lobbyId");
                    string candidateName = ReadLobbyString(lobby, "Name");
                    if (IsReplacementLobbyCandidate(candidateId, candidateName) && !candidates.Contains(candidateId))
                    {
                        candidates.Add(candidateId);
                    }
                }
            }

            if (candidates.Count == 1)
            {
                _lastReconnectLobbyCode = candidates[0];
                _lastLobbyRejoinInProgress = false;
                _replacementLobbyDiscoveryCoroutine = null;
                Log.LogWarning("Reconnect Guard found one validated replacement lobby owned by the original host; starting one clean-menu join generation.");
                AddLocalNotificationWithToast(ReconnectFoundNewLobbyNotice);
                StartCleanMenuReconnect("validated same-host replacement lobby discovered", nativeTeardownAlreadyStarting: false);
                yield break;
            }

            if (candidates.Count > 1)
            {
                Log.LogWarning("Reconnect Guard found multiple same-host replacement candidates; refusing to guess.");
                break;
            }
        }

        _lastLobbyRejoinInProgress = false;
        _replacementLobbyDiscoveryCoroutine = null;
        AddLocalNotificationWithToast(ReconnectLobbyNotFoundNotice);
        ReturnToMenuAfterFailedReconnectCore();
    }

    private static Task StartReplacementLobbySearch(int distance)
    {
        try
        {
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            object lobbyManager = AccessTools.Field(typeof(MultiplayerManager), "_lobbyManager")?.GetValue(multiplayerManager);
            object provider = lobbyManager?.GetType().GetProperty("CurrentProvider")?.GetValue(lobbyManager, null);
            MethodInfo search = provider?.GetType().GetMethod("SearchLobbiesAsync", new[] { typeof(int), typeof(int), typeof(Dictionary<string, string>) });
            Dictionary<string, string> filters = new Dictionary<string, string>
            {
                { "public", "t" },
                { "version", ConstData.Version.ToLowerInvariant() },
            };
            return search?.Invoke(provider, new object[] { distance, 50, filters }) as Task;
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Reconnect Guard replacement-lobby search could not start: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<object> ReadTaskResultItems(Task task)
    {
        object result = task?.GetType().GetProperty("Result")?.GetValue(task, null);
        if (result is IEnumerable enumerable)
        {
            foreach (object item in enumerable)
            {
                yield return item;
            }
        }
    }

    private bool IsReplacementLobbyCandidate(string candidateId, string candidateName)
    {
        if (!ulong.TryParse(candidateId, out ulong candidateValue) || candidateValue == 0UL ||
            string.Equals(candidateId, _lastReconnectLobbyCode, StringComparison.Ordinal))
        {
            return false;
        }

        CSteamID lobbyId = new CSteamID(candidateValue);
        string owner = ((ulong)SteamMatchmaking.GetLobbyOwner(lobbyId)).ToString();
        if (!string.Equals(owner, _lastReconnectLobbyOwnerSteamId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(SteamMatchmaking.GetLobbyData(lobbyId, "public"), "t", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(SteamMatchmaking.GetLobbyData(lobbyId, "request"), "t", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(SteamMatchmaking.GetLobbyData(lobbyId, "version"), ConstData.Version.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int members = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        int limit = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
        if (members < 1 || limit < 1 || members >= limit - 1)
        {
            return false;
        }

        string fingerprint = GetLobbyNameFingerprint(candidateName);
        return string.IsNullOrEmpty(_lastReconnectLobbyNameFingerprint) ||
               string.Equals(fingerprint, _lastReconnectLobbyNameFingerprint, StringComparison.Ordinal);
    }

    private static string ReadLobbyString(object lobby, string memberName)
    {
        if (lobby == null)
        {
            return string.Empty;
        }

        object value = lobby.GetType().GetProperty(memberName)?.GetValue(lobby, null) ??
                       lobby.GetType().GetField(memberName)?.GetValue(lobby);
        return value?.ToString() ?? string.Empty;
    }

    private static string GetLobbyNameFingerprint(string lobbyName)
    {
        string plain = Regex.Replace(lobbyName ?? string.Empty, "<[^>]*>", string.Empty);
        string normalized = new string(plain.Where(character => !char.IsWhiteSpace(character)).ToArray()).ToLowerInvariant();
        return normalized.Contains("♪blues♪") ? "♪blues♪" : normalized;
    }

    private static bool IsNetworkClientConnected()
    {
        try
        {
            return NetworkManager.main != null && NetworkManager.main.clientState == PurrNet.Transports.ConnectionState.Connected;
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Reconnect Guard could not read PurrNet client state: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static ReconnectRuntimeHealthDecision EvaluateManagedReconnectRuntime(float connectedSince)
    {
        bool clientConnected = IsNetworkClientConnected();
        bool authoritativeLobbyRead = TryGetAuthoritativeLobbyState(out int? memberCount, out _, out bool localUserIsMember);
        bool visibleLobbyConfirmed = authoritativeLobbyRead && memberCount.HasValue && memberCount.Value >= 1;
        bool gameRuntimeUsable = IsGameRuntimeUsableForReconnect(out string gameRuntimeReason);
        bool voiceRuntimeReady = IsVoiceRuntimeReadyForReconnect();
        bool readySample = clientConnected && visibleLobbyConfirmed && localUserIsMember && gameRuntimeUsable;
        float readyStableSeconds = 0f;
        if (Instance != null)
        {
            Instance._reconnectReadySampleCount = readySample ? Instance._reconnectReadySampleCount + 1 : 0;
            if (readySample)
            {
                if (Instance._reconnectQuorumReadySince < 0f)
                {
                    Instance._reconnectQuorumReadySince = Time.unscaledTime;
                }
                readyStableSeconds = Time.unscaledTime - Instance._reconnectQuorumReadySince;
            }
            else
            {
                Instance._reconnectQuorumReadySince = -1f;
            }
        }
        float? secondsSinceConnected = clientConnected && connectedSince >= 0f
            ? Time.unscaledTime - connectedSince
            : (float?)null;

        ReconnectRuntimeHealthDecision decision = ReconnectRuntimeHealthPolicy.Evaluate(new ReconnectRuntimeHealthInput(
            clientTransportConnected: clientConnected,
            requireVisibleLobby: LockedReconnectRequireVisibleLobby,
            visibleLobbyConfirmed: visibleLobbyConfirmed,
            localUserIsAuthoritativeLobbyMember: localUserIsMember,
            gameRuntimeUsable: gameRuntimeUsable,
            voiceRuntimeReady: voiceRuntimeReady,
            consecutiveReadySamples: Instance?._reconnectReadySampleCount ?? 0,
            secondsSinceClientTransportConnected: secondsSinceConnected,
            postConnectValidationSeconds: LockedReconnectPostConnectValidationSeconds,
            readyStableSeconds: readyStableSeconds,
            requiredReadyStabilitySeconds: LockedReconnectReadyStabilitySeconds));

        if (decision.Action == ReconnectRuntimeHealthAction.ConfirmSuccess)
        {
            return decision;
        }

        return new ReconnectRuntimeHealthDecision(
            decision.Action,
            $"{decision.Reason} Runtime probe: {gameRuntimeReason}; voiceReady={voiceRuntimeReady}.");
    }

    private static bool TryGetAuthoritativeLobbyState(out int? memberCount, out int? maxPlayers, out bool localUserIsMember)
    {
        memberCount = null;
        maxPlayers = null;
        localUserIsMember = false;

        try
        {
            string lobbyCode = MonoSingleton<MultiplayerManager>.I?.LobbyCode;
            if (!ulong.TryParse(lobbyCode, out ulong lobbyValue) || lobbyValue == 0UL || !SteamManager.Initialized)
            {
                return false;
            }

            CSteamID lobbyId = new CSteamID(lobbyValue);
            CSteamID localUser = SteamUser.GetSteamID();
            int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            memberCount = count;
            maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
            for (int index = 0; index < count; index++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, index) == localUser)
                {
                    localUserIsMember = true;
                    break;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Reconnect Guard could not read authoritative Steam lobby membership: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool IsVoiceRuntimeReadyForReconnect()
    {
        try
        {
            string lobbyCode = MonoSingleton<MultiplayerManager>.I?.LobbyCode;
            if (!ulong.TryParse(lobbyCode, out ulong lobbyValue) || lobbyValue == 0UL)
            {
                return false;
            }

            CSteamID lobbyId = new CSteamID(lobbyValue);
            bool voiceEnabled = !string.Equals(SteamMatchmaking.GetLobbyData(lobbyId, "voice"), "f", StringComparison.OrdinalIgnoreCase);
            if (!voiceEnabled)
            {
                return true;
            }

            VoiceManager voiceManager = MonoSingleton<VoiceManager>.I;
            return voiceManager != null && voiceManager.IsJoined && string.Equals(voiceManager.LobbyIDName, lobbyCode, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Reconnect Guard could not verify Vivox lobby alignment: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool IsGameRuntimeUsableForReconnect(out string reason)
    {
        reason = "unknown";
        try
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded || activeScene.buildIndex != 2)
            {
                reason = $"scene={activeScene.buildIndex}, loaded={activeScene.isLoaded}";
                return false;
            }

            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            if (multiplayerManager == null || !multiplayerManager.LobbyStatus)
            {
                reason = $"multiplayer={(multiplayerManager == null ? "missing" : "present")}, lobbyStatus={multiplayerManager?.LobbyStatus}";
                return false;
            }

            string expectedLobbyCode = Instance?._lastReconnectLobbyCode;
            if (!string.IsNullOrWhiteSpace(expectedLobbyCode) &&
                !string.Equals(multiplayerManager.LobbyCode, expectedLobbyCode, StringComparison.Ordinal))
            {
                reason = "active lobby does not match the reconnect target";
                return false;
            }

            TextChannelManager textChannelManager = NetworkSingleton<TextChannelManager>.I;
            if (textChannelManager == null ||
                textChannelManager.MainPlayer == null ||
                textChannelManager.MainPlayerController == null ||
                !textChannelManager.MainPlayer.gameObject.activeInHierarchy ||
                !textChannelManager.MainPlayerController.gameObject.activeInHierarchy)
            {
                reason = $"chat={(textChannelManager == null ? "missing" : "present")}, player={textChannelManager?.MainPlayer != null}, controller={textChannelManager?.MainPlayerController != null}";
                return false;
            }

            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel == null || panel.PlayerSteamIDs == null || panel.PlayerControllers == null || panel.PlayerTransforms == null || !SteamManager.Initialized)
            {
                reason = $"panel={(panel == null ? "missing" : "present")}, steamInitialized={SteamManager.Initialized}";
                return false;
            }

            string localSteamId = ((ulong)SteamUser.GetSteamID()).ToString();
            int localOccurrences = panel.PlayerSteamIDs.Count(value => string.Equals(value, localSteamId, StringComparison.Ordinal));
            int localIndex = panel.PlayerSteamIDs.FindIndex(value => string.Equals(value, localSteamId, StringComparison.Ordinal));
            bool ready = localOccurrences == 1 &&
                         localIndex >= 0 &&
                         localIndex < panel.PlayerControllers.Count &&
                         localIndex < panel.PlayerTransforms.Count &&
                         panel.PlayerControllers[localIndex] != null &&
                         panel.PlayerTransforms[localIndex] != null;
            reason = ready
                ? "scene, lobby, chat/player controller, and unique local roster entry are playable"
                : $"localRosterOccurrences={localOccurrences}, localIndex={localIndex}, controllers={panel.PlayerControllers.Count}, transforms={panel.PlayerTransforms.Count}";
            return ready;
        }
        catch (Exception ex)
        {
            reason = $"probe exception {ex.GetType().Name}";
            Log?.LogWarning($"Reconnect Guard could not read local runtime readiness: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private void StopManagedReconnect(string reason)
    {
        if (_cleanMenuReconnectCoroutine != null)
        {
            StopCoroutine(_cleanMenuReconnectCoroutine);
            _cleanMenuReconnectCoroutine = null;
            _cleanMenuReconnectGeneration++;
        }

        if (_cleanMenuReconnectActive)
        {
            Log.LogWarning($"Reconnect Guard stopped generation in phase {_cleanMenuReconnectPhase}: {reason}.");
        }

        _cleanMenuReconnectActive = false;
        _cleanMenuReconnectPhase = "idle";

        if (_managedReconnectCoroutine != null)
        {
            StopCoroutine(_managedReconnectCoroutine);
            _managedReconnectCoroutine = null;
        }

        if (_managedReconnectActive)
        {
            Log.LogWarning($"Reconnect Guard stopped: {reason}.");
        }

        MarkReconnectGuardIdle();
        _managedReconnectActive = false;
        _managedReconnectAttempt = 0;
        _lastManagedReconnectAttemptAt = -1f;
        _reconnectReadySampleCount = 0;
        _reconnectQuorumReadySince = -1f;
    }

    // Round 5b: generations are counted per saved lobby id. The count survives StopManagedReconnect and
    // FinishCleanMenuReconnect on purpose (that reset was the loop) and is NOT cleared by the success confirmation
    // (a rejoin that gets in for two seconds and drops again is the loop too). It clears when the saved lobby
    // changes, or lazily on the next disconnect once the guard has been idle for LockedReconnectBudgetIdleResetSeconds
    // (the player evidently had a working session in between, managed or manual).
    private void NoteReconnectGenerationStarted()
    {
        if (!string.Equals(_reconnectBudgetLobbyCode, _lastReconnectLobbyCode, StringComparison.Ordinal))
        {
            _reconnectBudgetLobbyCode = _lastReconnectLobbyCode;
            _reconnectBudgetGenerationsUsed = 0;
        }

        _reconnectBudgetGenerationsUsed++;
        _reconnectBudgetIdleSince = -1f;
        Log.LogInfo($"Reconnect Guard budget: generation {_reconnectBudgetGenerationsUsed}/{LockedReconnectMaxGenerationsPerLobby} for the saved lobby.");
    }

    private void MarkReconnectGuardIdle()
    {
        _reconnectBudgetIdleSince = Time.unscaledTime;
    }

    private float? GetSecondsSinceReconnectGuardIdle()
    {
        return _reconnectBudgetIdleSince < 0f ? (float?)null : Time.unscaledTime - _reconnectBudgetIdleSince;
    }

    private int GetReconnectGenerationsUsedForLobby(string lobbyCode)
    {
        if (string.IsNullOrEmpty(lobbyCode) || !string.Equals(_reconnectBudgetLobbyCode, lobbyCode, StringComparison.Ordinal))
        {
            return 0;
        }

        float? idleSeconds = GetSecondsSinceReconnectGuardIdle();
        if (_reconnectBudgetGenerationsUsed > 0 && ReconnectGuardPolicy.BudgetClearsAfterIdle(idleSeconds, LockedReconnectBudgetIdleResetSeconds))
        {
            ResetReconnectBudget($"guard idle for {idleSeconds.Value:0}s");
        }

        return _reconnectBudgetGenerationsUsed;
    }

    private void ResetReconnectBudget(string reason)
    {
        if (_reconnectBudgetGenerationsUsed > 0)
        {
            Log.LogInfo($"Reconnect Guard budget reset after {_reconnectBudgetGenerationsUsed} generation(s): {reason}.");
        }

        _reconnectBudgetGenerationsUsed = 0;
    }

    private void AnnounceManagedReconnectSuccess()
    {
        if (_managedReconnectSuccessAnnounced)
        {
            return;
        }

        _managedReconnectSuccessAnnounced = true;
        if (LockedReconnectShowNotifications)
        {
            // Every failure stage already toasts; the happy ending does too (round 5d).
            ShowToast(ReconnectBackInNotice);
        }

        if (!EnableReconnectAnnouncement.Value)
        {
            return;
        }

        string message = LockedReconnectAnnouncementMessage.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _reconnectAnnouncementCoroutine = StartCoroutine(DelayedReconnectAnnouncement(message));
    }

    private IEnumerator DelayedReconnectAnnouncement(string message)
    {
        int delaySeconds = LockedReconnectAnnouncementDelaySeconds;
        if (delaySeconds > 0)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }

        if (!TrySendLobbyChatMessage(message))
        {
            AddLocalNotification(message);
        }

        _reconnectAnnouncementCoroutine = null;
    }

    internal bool TrySendDoNotPressLobbyMessage()
    {
        return TrySendLobbyChatMessage("@Blue I found the tiny BlueSage lobby joke :3");
    }

    internal static bool TrySendLobbyChatMessage(string message)
    {
        try
        {
            TextChannelManager textChannelManager = NetworkSingleton<TextChannelManager>.I;
            if (textChannelManager == null)
            {
                return false;
            }

            string userName = textChannelManager.UserName ?? "BlueSage QoL";
            string playerId = AccessTools.Field(typeof(TextChannelManager), "_playerId")?.GetValue(textChannelManager) as string ?? string.Empty;
            Vector3 position = textChannelManager.MainPlayer != null ? textChannelManager.MainPlayer.position : Vector3.zero;

            textChannelManager.SendMessageAsync(
                Encoding.Unicode.GetBytes(message),
                Encoding.Unicode.GetBytes(userName),
                false,
                position,
                playerId);

            return true;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Lobby chat send failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    internal static bool IsPluginLoaded(string pluginGuid)
    {
        return Chainloader.PluginInfos.Keys.Any(key => string.Equals(key, pluginGuid, StringComparison.OrdinalIgnoreCase));
    }

    private static void RequestNativeReturnToMenuForReconnect()
    {
        Plugin instance = Instance;
        if (instance != null)
        {
            instance._reconnectMenuFallbackInProgress = true;
        }

        try
        {
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            if (multiplayerManager != null)
            {
                multiplayerManager._notificationState = NotificationStatus.HostLost;
            }

            MainSceneManager mainSceneManager = MonoSingleton<MainSceneManager>.I;
            if (mainSceneManager != null)
            {
                mainSceneManager.ReturnMenu(false);
                return;
            }

            multiplayerManager?.QuitSession();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Reconnect Guard could not request native menu teardown: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ReturnToMenuAfterFailedReconnectCore()
    {
        Plugin instance = Instance;
        if (instance != null)
        {
            instance._reconnectMenuFallbackInProgress = true;
        }

        try
        {
            try
            {
                MonoSingleton<MultiplayerManager>.I?.QuitSession();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Reconnect Guard could not quit unhealthy session before menu fallback: {ex.GetType().Name}: {ex.Message}");
            }

            MainSceneManager mainSceneManager = MonoSingleton<MainSceneManager>.I;
            if (mainSceneManager == null)
            {
                return;
            }

            AccessTools.Field(typeof(MainSceneManager), "_returnMenuStarted")?.SetValue(mainSceneManager, false);

            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            if (multiplayerManager != null)
            {
                multiplayerManager._notificationState = NotificationStatus.HostLost;
            }

            mainSceneManager.ReturnMenu(false);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Reconnect Guard fallback to menu failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (instance != null)
            {
                instance.StartCoroutine(instance.ClearReconnectMenuFallbackFlagSoon());
            }
        }
    }

    private IEnumerator ClearReconnectMenuFallbackFlagSoon()
    {
        yield return null;
        yield return null;
        _reconnectMenuFallbackInProgress = false;
    }

    private static string RedactLobbyCode(string lobbyCode)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            return "[none]";
        }

        lobbyCode = lobbyCode.Trim();
        if (lobbyCode.Length <= 4)
        {
            return "[redacted]";
        }

        return $"[redacted:{lobbyCode.Substring(lobbyCode.Length - 4)}]";
    }

    private static void MarkAndrewReconnectIntentionalLeave()
    {
        try
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Reconnect.ReconnectPlugin", false))
                .FirstOrDefault(candidate => candidate != null);

            PropertyInfo property = type?.GetProperty("IsIntentionalLeave", BindingFlags.Public | BindingFlags.Static);
            if (property?.CanWrite == true)
            {
                property.SetValue(null, true, null);
                Log.LogInfo("Reconnect Guard marked AndrewLin Reconnect intentional-leave flag to prevent immediate retry.");
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Reconnect Guard could not mark AndrewLin Reconnect intentional leave: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool TryGetVisibleLobbyCounts(out int? memberCount, out int? maxPlayers)
    {
        memberCount = null;
        maxPlayers = null;

        try
        {
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            object lobbyManager = AccessTools.Field(typeof(MultiplayerManager), "_lobbyManager")?.GetValue(multiplayerManager);
            object currentLobby = lobbyManager?.GetType().GetProperty("CurrentLobby")?.GetValue(lobbyManager, null);
            if (currentLobby == null)
            {
                return false;
            }

            // PurrLobby.Lobby keeps Members and MaxPlayers as plain fields (UAT 2026-10-18).
            ReflectedMember.TryRead(currentLobby, "Members", out object members);
            ReflectedMember.TryRead(currentLobby, "MaxPlayers", out object maxPlayersValue);

            if (ReflectedMember.TryRead(members, "Count", out object countValue) && countValue != null)
            {
                memberCount = Convert.ToInt32(countValue);
            }

            if (maxPlayersValue != null)
            {
                maxPlayers = Convert.ToInt32(maxPlayersValue);
            }

            return memberCount.HasValue || maxPlayers.HasValue;
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"Reconnect Guard could not read visible lobby counts: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
