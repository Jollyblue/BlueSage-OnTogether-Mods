using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BlueSage.QoLTweaks.Contracts;
using BlueSage.QoLTweaks.Core;
using BlueSage.QoLTweaks.Data;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class QolMenuWindow : MonoBehaviour
{
    // Theme palettes supply 0.99f near-opaque window backgrounds so the menu stays readable in bright lobbies.
    private const float MinWindowWidth = 920f;
    private const float MinWindowHeight = 640f;
    private const float ResizeGripSize = 42f;
    private const float FooterHeight = 120f;
    private const float ContentChromeHeight = FooterHeight + 152f;
    private const float ContentHorizontalChrome = 96f;
    private const string DoNotPressEggKey = "menu_do_not_press";

    private sealed class ThanksCard
    {
        internal ThanksCard(string name, string received, params string[] messages)
        {
            Name = name;
            Received = received;
            Messages = messages ?? Array.Empty<string>();
        }

        internal string Name { get; }
        internal string Received { get; }
        internal IReadOnlyList<string> Messages { get; }
    }

    private static readonly ThanksCard[] CommunityCrewCards =
    {
        new ThanksCard("bloo_bamboo", "Quality \"the sacrifice\" Tester", "Large-lobby QA, crash logs, and patient reconnect testing when things go sideways."),
        new ThanksCard("Rowan", "Partner in Crime ♥", "BlueSage's trusted co-conspirator for testing, experiments, and keeping Blue honest.")
    };

    private static readonly SupporterIdentityRollup[] SupporterRollups =
        SupporterLedgerPolicy.RollUp(SupporterLedgerData.Transactions);

    private Rect _windowRect = new Rect(120f, 95f, 960f, 720f);
    private bool _visible;
    internal bool IsVisible => _visible;
    private bool _hasOpened;
    private int _tab;
    private string _message = "Manage BlueSage QoL features without memorizing slash commands.";
    private string _sweepIntervalText = string.Empty;
    private string _reconnectRejoinHoldText = string.Empty;
    private string _flyPrecisionText = string.Empty;
    private string _hostMotdText = string.Empty;
    private string _fishingAidReactionText = string.Empty;
    private string _fishingAidClicksText = string.Empty;
    private string _fishingAidReleaseText = string.Empty;
    // Label column for text-setting rows, sized to the section that draws them (no clipped labels).
    private float _textSettingLabelWidth = 170f;
    private const float CustomizeTwoColumnMinWidth = 1100f;
    private string _pingHighlightText = string.Empty;
    private string _pingMentionText = string.Empty;
    private string _pingSoundModeText = string.Empty;
    private string _chatOutlineIntensityText = string.Empty;
    private string _chatUiScaleText = string.Empty;
    private string _menuUiScaleText = string.Empty;
    private string _toastHoldText = string.Empty;
    private string _toastScaleText = string.Empty;
    private string _chatFontSizeText = string.Empty;
    private string _chatWindowHeightText = string.Empty;
    private string _chatHistoryRowsText = string.Empty;
    private string _localChatRangeText = string.Empty;
    private string _chatOutlineColorText = string.Empty;
    private string _blackChatOutlineColorText = string.Empty;
    private string _chatLimitText = string.Empty;
    private string _profileLimitText = string.Empty;
    private string _sessionLimitText = string.Empty;
    private string _selfSpeedText = string.Empty;
    private string _selfJumpText = string.Empty;
    private string _selfGravityText = string.Empty;
    private int _safetyRosterPage;
    private string _selectedSafetySteamId = string.Empty;
    private string _safetyRosterQuery = string.Empty;
    private int _nativeBanPage;
    private string _selectedBannedSteamId = string.Empty;
    private string _offlineBanSteamIdText = string.Empty;
    private bool _showClosedIncidentHistory;
    private bool _showHostToolsTechnicalDetails;
    // Host Tools card state, captured once per Layout pass (see DrawHostReadinessCard).
    private bool _hostCardCaptured;
    private HostToolsPresentation _hostCardPresentation;
    private bool _hostCardHasCapability;
    private BlueSageHostCapabilitySnapshotV1 _hostCardCapability;
    private bool _hostCardShowDetails;
    private int _selectedSupporterIndex;
    private Vector2 _contentScroll;
    private Vector2 _hostJournalScroll;
    private HostJournalBoxFilter _hostJournalFilter = HostJournalBoxFilter.Actions;
    private GUIStyle _windowStyle;
    private GUIStyle _headerBoxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _activeButtonStyle;
    private GUIStyle _enabledButtonStyle;
    private GUIStyle _disabledButtonStyle;
    private GUIStyle _experimentalButtonStyle;
    private GUIStyle _boxStyle;
    private GUIStyle _sectionBoxStyle;
    private GUIStyle _comfortSectionBoxStyle;
    private GUIStyle _chatSectionBoxStyle;
    private GUIStyle _identitySectionBoxStyle;
    private GUIStyle _personalizationSectionBoxStyle;
    private GUIStyle _accessibilitySectionBoxStyle;
    private GUIStyle _experimentalBoxStyle;
    private GUIStyle _sectionHeaderStyle;
    private GUIStyle _navigationHintStyle;
    private GUIStyle _footerBoxStyle;
    private GUIStyle _footerTextStyle;
    private GUIStyle _textFieldStyle;
    private Texture2D _windowBackgroundTexture;
    private bool _isResizing;
    private string _renderedThemeKey = string.Empty;
    private int _renderedTextureGeneration;
    private BlueSageUiThemePalette _theme;
    private string _stateLabel = "Saved";
    private string _stateDetail = "Loaded saved menu values.";
    private readonly StatisticsPulseReader _statisticsPulseReader = new StatisticsPulseReader();
    private StatisticsPulseSnapshot _statisticsPulse;
    private string _statisticsPulseUnavailableReason = "Press Refresh to load the game's saved stats.";
    private string _statisticsPulseNote = "Achievements are counted only when you press Refresh.";
    private readonly Dictionary<string, string> _settingUndoValues = new Dictionary<string, string>(StringComparer.Ordinal);

    public void ToggleVisible()
    {
        _visible = !_visible;
        BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.QolMenu, _visible);
        if (!_visible)
        {
            BlueSageWindowHoverScope.UnregisterWindow(47058);
        }
        if (_visible)
        {
            WindowFitResult fitted = BlueSageWindowCoordinator.ResolveForOpen(
                BlueSagePublicWindow.QolMenu,
                _hasOpened,
                _windowRect,
                MinWindowWidth,
                MinWindowHeight,
                MenuDisplayScaleRuntime.LogicalScreenWidth,
                MenuDisplayScaleRuntime.LogicalScreenHeight);
            _windowRect = new Rect(fitted.X, fitted.Y, fitted.Width, fitted.Height);
            _hasOpened = true;
            HydrateFields();
            RefreshStatisticsPulse(false);
            MarkSavedQuiet("Loaded saved menu values. Edit Customize fields to start a draft.");
        }
    }

    internal bool ShowHostConsole()
    {
        if (!Plugin.ShouldShowLobbySafetyTab(
                out LobbySafetyAccessState access,
                out _) ||
            access != LobbySafetyAccessState.Host)
        {
            return false;
        }

        _tab = 4;
        _contentScroll = Vector2.zero;
        if (!_visible)
        {
            ToggleVisible();
        }
        return true;
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.QolMenu, false);
            return;
        }

        BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.QolMenu, true);
        EnsureStyles();
        ClampWindowToScreen();
        BlueSageWindowHoverScope.RegisterWindow(47058, MenuDisplayScaleRuntime.ToPhysicalRect(_windowRect), 0);
        ImGuiTextFocusTracker.BlurOnClickOutsideWindows();
        MenuDisplayScaleRuntime.Apply(out Matrix4x4 previousMatrix);
        try
        {
            _windowRect = GUI.Window(47058, _windowRect, DrawWindow, string.Empty, _windowStyle);
            ImGuiTextFocusTracker.ReportWindowDrawn();
            ClampWindowToScreen();
        }
        finally
        {
            MenuDisplayScaleRuntime.Restore(previousMatrix);
        }
    }

    private void DrawWindow(int id)
    {
        ImGuiTextFocusTracker.BlurOnClickInsideWindow();
        // IMGUI exposes one process-wide tooltip string. Clear any tooltip left
        // by the other overlapping BlueSage window before drawing this one.
        GUI.tooltip = string.Empty;
        bool showLobbySafetyTab = Plugin.ShouldShowLobbySafetyTab(out _, out _);
        if (_tab == 4 && !showLobbySafetyTab)
        {
            _tab = 0;
            _contentScroll = Vector2.zero;
            ClearLobbySafetyTransientState();
        }

        if (_windowBackgroundTexture != null)
        {
            GUI.DrawTexture(new Rect(0f, 0f, _windowRect.width, _windowRect.height), _windowBackgroundTexture);
        }

        DrawHeader();
        GUILayout.Space(10f);
        DrawTabs();
        GUILayout.Space(8f);
        _contentScroll = GUILayout.BeginScrollView(_contentScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(GetContentScrollHeight()));

        if (_tab == 0)
        {
            DrawMainTab();
        }
        else if (_tab == 1)
        {
            DrawTogglesTab();
        }
        else if (_tab == 2)
        {
            DrawCustomizeTab();
        }
        else if (_tab == 3)
        {
            DrawCreditsOtherTab();
        }
        else if (showLobbySafetyTab)
        {
            DrawLobbySafetyTab();
        }
        else
        {
            DrawMainTab();
        }

        GUILayout.EndScrollView();
        GUILayout.FlexibleSpace();
        DrawFooter();
        DrawResizeGrip();
        HeaderActionLayout headerLayout = HeaderActionLayoutPolicy.Resolve(_windowRect.width);
        GUI.DragWindow(ToUnityRect(headerLayout.Drag));
    }

    private void DrawHeader()
    {
        Rect header = new Rect(0f, 0f, _windowRect.width, 38f);
        GUI.Box(header, GUIContent.none, _headerBoxStyle);
        HeaderActionLayout layout = HeaderActionLayoutPolicy.Resolve(_windowRect.width);
        GUI.Label(ToUnityRect(layout.Title), $"▣ BlueSage QoL Menu  v{Plugin.PluginDisplayVersion}", _headerStyle);
        if (GUI.Button(ToUnityRect(layout.Website), new GUIContent(layout.WebsiteLabel, HeaderActionLayoutPolicy.WebsiteTooltip), _buttonStyle))
        {
            Plugin.Instance?.ShowToast(HeaderActionLayoutPolicy.WebsiteComingSoonNotice);
        }
        if (GUI.Button(ToUnityRect(layout.Thunderstore), new GUIContent(layout.ThunderstoreLabel, "Open the BlueSage QoL Tweaks Thunderstore page."), _buttonStyle))
        {
            Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl);
        }
        if (GUI.Button(ToUnityRect(layout.Kofi), new GUIContent(layout.KofiLabel, "Support Blue's community modding work on Ko-fi."), _buttonStyle))
        {
            Application.OpenURL("https://ko-fi.com/Q5Q1JRPW");
        }
        if (GUI.Button(ToUnityRect(layout.Discord), new GUIContent(layout.DiscordLabel, "Open Blue's On-Together community Discord."), _buttonStyle))
        {
            Application.OpenURL("https://discord.gg/JujMEwtN3q");
        }
        if (GUI.Button(ToUnityRect(layout.Close), new GUIContent(layout.CloseLabel, "Close this window"), _activeButtonStyle))
        {
            _visible = false;
            BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.QolMenu, false);
            BlueSageWindowHoverScope.UnregisterWindow(47058);
        }
    }

    private static Rect ToUnityRect(HeaderActionRect rect) =>
        new Rect(rect.X, rect.Y, rect.Width, rect.Height);

    private float GetContentScrollHeight()
    {
        HelpFrameLayout frame = BlueSageHelpFrame.Resolve(
            BlueSageHelpFrameKind.QolMenu,
            _windowRect);
        return Mathf.Max(24f, frame.ContentHeight);
    }

    private float GetContentLayoutWidth()
    {
        // Reserve the window padding plus the always-visible vertical rail so
        // responsive cards never create an accidental horizontal scrollbar.
        return Mathf.Max(280f, _windowRect.width - ContentHorizontalChrome);
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"v{Plugin.PluginDisplayVersion}  •  Updated {Plugin.PluginReleaseDate}", _smallStyle);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        DrawTabButton(0, "Main", "Fast actions, how-to examples, current feature status, and the quickest path to Style Helper.");
        DrawTabButton(1, "Toggles", "Turn major QoL features on/off with one normal On/Off button per row.");
        DrawTabButton(2, "Customize", "Edit movement, ping, cleanup, and text-limit settings. Names, status, and Spoons live in Style Helper.");
        DrawTabButton(3, "Credits / Other", "Themes, credits, community notes, and one optional lobby joke.");
        if (Plugin.ShouldShowLobbySafetyTab(out LobbySafetyAccessState accessState, out _))
        {
            bool isHost = accessState == LobbySafetyAccessState.Host;
            bool ready = accessState == LobbySafetyAccessState.Host || accessState == LobbySafetyAccessState.HelperReady;
            DrawTabButton(4, isHost ? "Host Tools" : "Safety", accessState == LobbySafetyAccessState.Host
                ? "Host-only Helper management plus verified identities, Clone Shield evidence, and careful moderation actions."
                : ready
                    ? "Assigned Helper access: verified identities, Clone Shield evidence, and host-validated actions."
                    : "You're an assigned Helper here. Your tools unlock by themselves once the host's BlueSage answers.");
        }
        GUILayout.EndHorizontal();
    }

    private void DrawTabButton(int index, string label, string tooltip)
    {
        bool active = _tab == index;
        if (GUILayout.Button(new GUIContent(label, tooltip), active ? _activeButtonStyle : _buttonStyle, GUILayout.Height(34f)))
        {
            _tab = index;
            _contentScroll = Vector2.zero;
        }
    }

    private void DrawMainTab()
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label("Welcome to BlueSage QoL", _sectionHeaderStyle);
        GUILayout.Label("Comfort, clarity, personalization, and safer community tools—each one can be adjusted without memorizing commands.", _smallStyle);
        GUILayout.Space(8f);
        GUILayout.Label("Quick actions", _labelStyle);
        GUILayout.BeginHorizontal();
        float actionButtonWidth = Mathf.Max(150f, (_windowRect.width - 108f) / 3f);
        SweepMenuActionState cleanupAction = SweepMenuActionPolicy.Resolve(Plugin.ManualSweepAllowed);
        bool priorGuiEnabled = GUI.enabled;
        GUI.enabled = priorGuiEnabled && cleanupAction.Enabled;
        bool cleanupRequested = GUILayout.Button(
            new GUIContent(cleanupAction.Label, cleanupAction.Tooltip),
            _buttonStyle,
            GUILayout.Width(actionButtonWidth),
            GUILayout.Height(34f));
        GUI.enabled = priorGuiEnabled;
        if (cleanupRequested)
        {
            _message = Plugin.Instance != null ? Plugin.Instance.RunManualSweepFromQolMenu() : "Plugin is not ready.";
            MarkActionQuiet("Run Cleanup Now requested.");
        }
        if (GUILayout.Button(new GUIContent("Run QoL Status", "Shows the same easy feature summary as /qol status so players can quickly see what is on or off."), _buttonStyle, GUILayout.Width(actionButtonWidth), GUILayout.Height(34f)))
        {
            if (Plugin.Instance != null)
            {
                _message = Plugin.Instance.RunQolStatusFromQolMenu();
            }
            else
            {
                _message = "Plugin is not ready.";
            }
            MarkActionQuiet(_message);
        }
        bool styleHelperOpen = Plugin.IsStyleHelperVisible;
        if (GUILayout.Button(new GUIContent(styleHelperOpen ? "Close Style Helper" : "Open Style Helper", "Open or close Style Helper for name styling, status tags, templates, and Spoons."), styleHelperOpen ? _enabledButtonStyle : _buttonStyle, GUILayout.Width(actionButtonWidth), GUILayout.Height(34f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ToggleStyleUiFromQolMenu() : "Plugin is not ready.";
            MarkAction(_message);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10f);
        DrawMainStatusStrip();
        DrawFocusPulse();
        DrawLobbySafetyAccessBanner();
        GUILayout.Space(12f);
        GUILayout.Label("Getting started", _labelStyle);
        GUILayout.Label(NavigationCalloutPolicy.BuildMenuNavigationHint(_theme.NavigationKeywordHex, Plugin.QolMenuKey.Value.ToString(), Plugin.StyleMenuKey.Value.ToString()), _navigationHintStyle);
        GUILayout.Label("Use /styleui for name, status, and Spoons identity edits. Name Styler changes your visible name style; Status Styler changes only the activity tag and optional Spoons tag.", _smallStyle);
        GUILayout.Label("Chat actions: plain left-click opens the game's ID card. Ctrl-left-click copies message text. Shift-left-click a safe http/https link once to copy and warn, then repeat to open.", _smallStyle);
        GUILayout.Label("Quick checks: /qol status or Run QoL Status shows enabled features. Type / in chat to browse commands with autocomplete.", _smallStyle);
        GUILayout.Label("When something feels noisy: open Toggles and turn off only that feature. Use Customize for ping colors, ping sound, cleanup timing, and text limits.", _smallStyle);
        GUILayout.Space(12f);
        DrawCommunityThanks();
        GUILayout.EndVertical();
    }

    private void DrawMainStatusStrip()
    {
        float availableWidth = GetContentLayoutWidth();
        bool compact = _windowRect.width < 1040f;
        float cardWidth = compact ? (availableWidth - 8f) / 2f : (availableWidth - 24f) / 4f;

        GUILayout.BeginHorizontal();
        DrawStatusCard("Comfort", Plugin.EnableChatReadability?.Value == true ? "Ready" : "Adjusted", "Chat, movement, and notices", cardWidth);
        GUILayout.Space(8f);
        DrawStatusCard("Styling", Plugin.EnableStyleUi?.Value == true ? "Ready" : "Off", "Names, status, Spoons, templates", cardWidth);
        if (!compact)
        {
            GUILayout.Space(8f);
            DrawStatusCard("Reconnect Guard", Plugin.EnableReconnectGuard?.Value == true ? "Ready" : "Off", "Saved-lobby recovery protection", cardWidth);
            GUILayout.Space(8f);
            DrawStatusCard("Fishing Aid", Plugin.EnableFishingAid?.Value == true ? "Ready" : "Off", "Accessibility fishing help", cardWidth);
        }
        GUILayout.EndHorizontal();

        if (compact)
        {
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawStatusCard("Reconnect Guard", Plugin.EnableReconnectGuard?.Value == true ? "Ready" : "Off", "Saved-lobby recovery protection", cardWidth);
            GUILayout.Space(8f);
            DrawStatusCard("Fishing Aid", Plugin.EnableFishingAid?.Value == true ? "Ready" : "Off", "Accessibility fishing help", cardWidth);
            GUILayout.EndHorizontal();
        }
    }

    private void DrawStatusCard(string title, string state, string detail, float width, bool experimental = false)
    {
        GUILayout.BeginVertical(experimental ? _experimentalBoxStyle : _sectionBoxStyle, GUILayout.Width(width), GUILayout.Height(86f));
        GUILayout.Label(title + "  •  " + state, _labelStyle);
        GUILayout.Label(detail, _smallStyle);
        GUILayout.EndVertical();
    }

    private void DrawFocusPulse()
    {
        const string nativeReportTooltip = "The full achievement gallery is in the game's Report. Focus Pulse never opens or changes it.";
        GUILayout.Space(8f);
        GUILayout.BeginVertical(_identitySectionBoxStyle, GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Focus Pulse", _sectionHeaderStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(
                new GUIContent(BlueSageReportSurfacePolicy.QolFallbackLabel,
                    "Open the BlueSage Report, your Work / Relax view. The game's Report stays as it is."),
                _buttonStyle,
                GUILayout.Width(190f),
                GUILayout.Height(30f)))
        {
            Plugin.OpenBlueSageReport();
            MarkAction("BlueSage Report opened.");
        }
        if (GUILayout.Button(
                new GUIContent("Refresh", "Reload these numbers from the game's saved stats and count your achievements once. Nothing is changed."),
                _buttonStyle,
                GUILayout.Width(112f),
                GUILayout.Height(30f)))
        {
            RefreshStatisticsPulse(true);
            _message = _statisticsPulse != null
                ? "Focus Pulse refreshed from the game's saved stats."
                : "This game version doesn't share those stats.";
            MarkAction(_message);
        }
        GUILayout.EndHorizontal();

        if (_statisticsPulse == null)
        {
            GUILayout.Label("The game's saved stats can't be shown here. " + _statisticsPulseUnavailableReason, _smallStyle);
            GUILayout.Label(new GUIContent("Full Report - Open with ESC", nativeReportTooltip), _smallStyle);
            GUILayout.EndVertical();
            return;
        }

        float availableWidth = GetContentLayoutWidth();
        float metricWidth = Mathf.Max(140f, (availableWidth - 24f) / 4f);
        GUILayout.BeginHorizontal();
        DrawFocusPulseMetric("Total focus", _statisticsPulse.TotalFocusLabel, metricWidth);
        GUILayout.Space(8f);
        DrawFocusPulseMetric("Today's sessions", _statisticsPulse.TodaySessions.ToString(), metricWidth);
        GUILayout.Space(8f);
        DrawFocusPulseMetric("Current streak", StatisticsPulsePresentationPolicy.BuildStreakValue(
            _statisticsPulse.CurrentStreak,
            _statisticsPulse.LongestStreak), metricWidth);
        GUILayout.Space(8f);
        DrawFocusPulseMetric("Task checks recorded", _statisticsPulse.TaskChecks.ToString(), metricWidth);
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        DrawFocusPulseMetric("Achievements unlocked", _statisticsPulse.AchievementsLabel, metricWidth);
        GUILayout.Space(8f);
        GUILayout.BeginVertical(_sectionBoxStyle, GUILayout.ExpandWidth(true), GUILayout.Height(64f));
        GUILayout.Label(new GUIContent("Full Report - Open with ESC", nativeReportTooltip), _labelStyle);
        GUILayout.Label("Focus Pulse reads the game's saved totals without opening or changing the Report. " + _statisticsPulseNote, _smallStyle);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawFocusPulseMetric(string title, string value, float width)
    {
        GUILayout.BeginVertical(_sectionBoxStyle, GUILayout.Width(width), GUILayout.Height(64f));
        GUILayout.Label(title, _smallStyle);
        GUILayout.Label(value, _labelStyle);
        GUILayout.EndVertical();
    }

    private void RefreshStatisticsPulse(bool includeAchievements)
    {
        if (!_statisticsPulseReader.TryCapturePulse(
                out StatisticsPulseSnapshot snapshot,
                out string unavailableReason))
        {
            _statisticsPulse = null;
            _statisticsPulseUnavailableReason = string.IsNullOrWhiteSpace(unavailableReason)
                ? "This game version doesn't share those stats."
                : unavailableReason;
            _statisticsPulseNote = "The rest of the QoL menu remains available.";
            return;
        }

        _statisticsPulseUnavailableReason = string.Empty;
        _statisticsPulseNote = "Achievements are counted only when you press Refresh.";
        string achievementUnavailableReason = string.Empty;
        if (includeAchievements &&
            _statisticsPulseReader.TryCountAchievements(
                out int unlocked,
                out int total,
                out achievementUnavailableReason))
        {
            snapshot = snapshot.WithAchievementProgress(unlocked, total);
            _statisticsPulseNote = "Achievements counted once from the game's gallery.";
        }
        else if (includeAchievements)
        {
            _statisticsPulseNote = string.IsNullOrWhiteSpace(achievementUnavailableReason)
                ? "This game version doesn't share achievement progress."
                : achievementUnavailableReason;
        }

        _statisticsPulse = snapshot;
    }

    private void DrawLobbySafetyAccessBanner()
    {
        if (!Plugin.ShouldShowLobbySafetyTab(out LobbySafetyAccessState state, out _)) return;

        bool ready = state == LobbySafetyAccessState.Host || state == LobbySafetyAccessState.HelperReady;
        string status = state switch
        {
            LobbySafetyAccessState.Host => "HOST  •  Helper management and Lobby Safety ready",
            LobbySafetyAccessState.HelperReady => "HELPER  •  Lobby Safety ready",
            _ => "Helper assigned  •  waiting for the host"
        };

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal(_identitySectionBoxStyle);
        GUILayout.Label(status, _smallStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(new GUIContent("Open Helper / Safety", ready
                ? "Open authorized Helper and Lobby Safety tools."
                : "You're an assigned Helper here. Your tools unlock by themselves once the host's BlueSage answers."),
                ready ? _enabledButtonStyle : _buttonStyle, GUILayout.Width(210f), GUILayout.Height(30f)))
        {
            _tab = 4;
            _contentScroll = Vector2.zero;
            _message = Plugin.Instance != null ? Plugin.Instance.CheckModerationAccessFromQolMenu() : "Helper / Safety is not ready yet.";
            MarkActionQuiet(_message);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawCommunityThanks()
    {
        GUILayout.BeginVertical(_identitySectionBoxStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("♥ COMMUNITY SUPPORTERS ♥", _sectionHeaderStyle);
        GUILayout.Label(
            "15 community supporters shared 16 public thank-you messages. Their thank-you wall lives in Credits / Other.",
            _smallStyle);
        if (GUILayout.Button(
                new GUIContent(
                    "Open Community Supporters in Credits / Other",
                    "Open the community thank-you wall. Supporter totals and public messages are shown; account and payment identifiers are never included."),
                _buttonStyle,
                GUILayout.Height(34f)))
        {
            OpenCommunitySupporters();
        }
        GUILayout.EndVertical();
    }

    private void OpenCommunitySupporters()
    {
        _tab = 3;
        _contentScroll = Vector2.zero;
        _message = "Community Supporters opened in Credits / Other.";
        MarkActionQuiet(_message);
    }

    private void DrawSupporterCredits()
    {
        GUILayout.Label("15 community supporters • 16 public thank-you messages", _labelStyle);
        GUILayout.Label(
            "Choose a supporter to read their public messages. Only each person's combined support is shown; account and payment identifiers are excluded.",
            _smallStyle);
        GUILayout.Space(8f);
        DrawSupporterDirectory();
        GUILayout.Space(10f);
        DrawSelectedSupporterMessages();
    }

    private void DrawSupporterDirectory()
    {
        float availableWidth = GetContentLayoutWidth();
        int columns = GetThanksGridColumnCount(availableWidth);
        float gaps = (columns - 1) * 8f;
        float buttonWidth = Mathf.Max(180f, (availableWidth - gaps) / columns);

        for (int rowStart = 0; rowStart < SupporterRollups.Length; rowStart += columns)
        {
            GUILayout.BeginHorizontal();
            int rowEnd = Math.Min(SupporterRollups.Length, rowStart + columns);
            for (int index = rowStart; index < rowEnd; index++)
            {
                SupporterIdentityRollup supporter = SupporterRollups[index];
                bool selected = index == _selectedSupporterIndex;
                if (GUILayout.Button(
                        new GUIContent(
                            SupporterLedgerPolicy.SanitizeForDisplay(supporter.Name) +
                            " ♥ " +
                            SupporterLedgerPolicy.FormatReceived(supporter.TotalReceived),
                            selected
                                ? "Close this supporter's thank-you messages."
                                : "Read this supporter's public thank-you messages."),
                        selected ? _activeButtonStyle : _buttonStyle,
                        GUILayout.Width(buttonWidth),
                        GUILayout.Height(34f)))
                {
                    SelectSupporterInCredits(index);
                }

                if (index + 1 < rowEnd)
                {
                    GUILayout.Space(8f);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (rowEnd < SupporterRollups.Length)
            {
                GUILayout.Space(6f);
            }
        }
    }

    private void SelectSupporterInCredits(int index)
    {
        if (index < 0 || index >= SupporterRollups.Length)
        {
            return;
        }

        _selectedSupporterIndex = _selectedSupporterIndex == index ? -1 : index;
        _tab = 3;
        _message = _selectedSupporterIndex < 0
            ? "Community supporter details closed."
            : "Community supporter selected: " + SupporterRollups[index].Name + ".";
        MarkActionQuiet(_message);
    }

    private void DrawSelectedSupporterMessages()
    {
        if (_selectedSupporterIndex < 0 || _selectedSupporterIndex >= SupporterRollups.Length)
        {
            return;
        }

        SupporterIdentityRollup supporter = SupporterRollups[_selectedSupporterIndex];
        SupporterTransaction[] messages = SupporterLedgerPolicy.GetPublicMessages(supporter);
        GUILayout.BeginVertical(_identitySectionBoxStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label(
            SupporterLedgerPolicy.SanitizeForDisplay(supporter.Name) +
            " ♥ " +
            SupporterLedgerPolicy.FormatReceived(supporter.TotalReceived) +
            " total",
            _sectionHeaderStyle);
        if (messages.Length == 0)
        {
            GUILayout.Label("Supported with love and chose not to leave a public message.", _smallStyle);
        }
        GUILayout.EndVertical();

        for (int index = 0; index < messages.Length; index++)
        {
            SupporterTransaction transaction = messages[index];
            GUILayout.Space(6f);
            GUILayout.BeginVertical(_sectionBoxStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("♥ " + SupporterLedgerPolicy.FormatDateLabel(transaction.DateTimeUtc), _labelStyle);
            GUILayout.Label(SupporterLedgerPolicy.SanitizeForDisplay(transaction.Message), _smallStyle);
            GUILayout.EndVertical();
            if (index + 1 < messages.Length)
            {
                GUILayout.Space(6f);
            }
        }
    }

    private void DrawCommunityCrew()
    {
        DrawThanksCardGrid(CommunityCrewCards);
    }

    private void DrawThanksCardGrid(IReadOnlyList<ThanksCard> cards)
    {
        float availableWidth = GetContentLayoutWidth();
        int columns = GetThanksGridColumnCount(availableWidth);
        float gaps = (columns - 1) * 8f;
        float cardWidth = Mathf.Max(220f, (availableWidth - gaps) / columns);

        for (int rowStart = 0; rowStart < cards.Count; rowStart += columns)
        {
            GUILayout.BeginHorizontal();
            int rowEnd = Math.Min(cards.Count, rowStart + columns);
            for (int index = rowStart; index < rowEnd; index++)
            {
                ThanksCard card = cards[index];
                DrawThanksCard(card.Name, card.Received, card.Messages, cardWidth);
                if (index + 1 < rowEnd)
                {
                    GUILayout.Space(8f);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (rowEnd < cards.Count)
            {
                GUILayout.Space(8f);
            }
        }
    }

    private static int GetThanksGridColumnCount(float availableWidth)
    {
        if (availableWidth >= 1400f) return 4;
        if (availableWidth >= 1020f) return 3;
        if (availableWidth >= 680f) return 2;
        return 1;
    }

    private void DrawThanksCard(string name, string received, IReadOnlyList<string> messages, float cardWidth)
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.Width(cardWidth), GUILayout.MinHeight(118f));
        GUILayout.Label(name, _labelStyle);
        GUILayout.Label(received, _smallStyle);
        foreach (string message in messages)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                GUILayout.Label(message, _smallStyle);
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawTogglesTab()
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label("Click a feature's On/Off button, then read the footer for what changed. Your saved choices remain yours.", _smallStyle);
        GUILayout.Space(8f);

        int columnCount = _windowRect.width >= 1320f ? 3 : (_windowRect.width >= 880f ? 2 : 1);
        float availableWidth = GetContentLayoutWidth();
        float columnWidth = (availableWidth - (12f * (columnCount - 1))) / columnCount;

        if (columnCount == 1)
        {
            DrawFeatureSection("Comfort & Movement", "Reduce friction and stay informed while you move.", DrawComfortMovementToggles, availableWidth, FeatureSectionTone.Comfort);
            GUILayout.Space(12f);
            DrawFeatureSection("Chat & Readability", "Improve chat clarity, context, and everyday usability.", DrawChatReadabilityToggles, availableWidth, FeatureSectionTone.Chat);
            GUILayout.Space(12f);
            DrawFeatureSection("Identity & Safety", "Protect identity and reduce reconnect friction.", DrawIdentitySafetyToggles, availableWidth, FeatureSectionTone.Identity);
            GUILayout.Space(12f);
            DrawFeatureSection("Personalization", "Customize how your BlueSage experience looks and feels.", DrawPersonalizationToggles, availableWidth, FeatureSectionTone.Personalization);
            GUILayout.Space(12f);
            DrawFeatureSection("Accessibility", "Helpers for sore or unsteady hands. Off until you turn them on.", DrawAccessibilityToggles, availableWidth, FeatureSectionTone.Accessibility);
        }
        else
        {
            GUILayout.BeginHorizontal();
            DrawFeatureSection("Comfort & Movement", "Reduce friction and stay informed while you move.", DrawComfortMovementToggles, columnWidth, FeatureSectionTone.Comfort);
            GUILayout.Space(12f);
            DrawFeatureSection("Chat & Readability", "Improve chat clarity, context, and everyday usability.", DrawChatReadabilityToggles, columnWidth, FeatureSectionTone.Chat);
            if (columnCount == 3)
            {
                GUILayout.Space(12f);
                DrawFeatureSection("Identity & Safety", "Protect identity and reduce reconnect friction.", DrawIdentitySafetyToggles, columnWidth, FeatureSectionTone.Identity);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            if (columnCount == 2)
            {
                DrawFeatureSection("Identity & Safety", "Protect identity and reduce reconnect friction.", DrawIdentitySafetyToggles, columnWidth, FeatureSectionTone.Identity);
                GUILayout.Space(12f);
                DrawFeatureSection("Personalization", "Customize how your BlueSage experience looks and feels.", DrawPersonalizationToggles, columnWidth, FeatureSectionTone.Personalization);
            }
            else
            {
                float lowerSectionWidth = (availableWidth - 12f) / 2f;
                DrawFeatureSection("Personalization", "Customize how your BlueSage experience looks and feels.", DrawPersonalizationToggles, lowerSectionWidth, FeatureSectionTone.Personalization);
                GUILayout.Space(12f);
                DrawFeatureSection("Accessibility", "Helpers for sore or unsteady hands. Off until you turn them on.", DrawAccessibilityToggles, lowerSectionWidth, FeatureSectionTone.Accessibility);
            }
            GUILayout.EndHorizontal();
            if (columnCount == 2)
            {
                GUILayout.Space(12f);
                DrawFeatureSection("Accessibility", "Helpers for sore or unsteady hands. Off until you turn them on.", DrawAccessibilityToggles, availableWidth, FeatureSectionTone.Accessibility);
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawComfortMovementToggles()
    {
        DrawToggle("Automatic Cleanup", Plugin.EnableAutoSweep, $"On for fresh installs every 40 minutes, never right before or after the game's own autosave or its 30-minute cleanup; an existing saved Off choice stays Off. BlueSage times its cleanup around the game's own cleanup. Run Cleanup Now, {Plugin.ManualSweepKey.Value}, and /sweep now remain independent; the game's timer is unchanged.", () => Plugin.Instance?.RunSweepRestartFromMenu());
        DrawToggle("Cleanup Note", Plugin.NotifyOnSweep, "Shows a quiet chat notice and a toast when an automatic cleanup finishes. Off by default (a note every 40 minutes is a lot in a cozy session); a cleanup you run yourself always says so.");
        DrawToggle("Extra Memory Cleanup", Plugin.ForceManagedGcCollect, "After a cleanup, also frees leftover memory. Off by default; it can cause a short stutter. Only try it if a helper asked you to.");
        DrawToggle("Host Health", Plugin.EnableHostHealthMonitor, "Background lobby-health checks for long sessions. Most useful for hosts; quiet/local unless a health action has something to report.", () => Plugin.Instance?.RestartHostHealthFromMenu());
        DrawToggle("Leave Notices", Plugin.EnableLeaveNotifications, "Shows you a local notice when someone leaves. Other players do not see it, and overlapping chat mods can make BlueSage back off.");
        DrawToggle("Welcome Message", Plugin.EnableWelcomeMessage, $"Shows two local tips once per lobby: {Plugin.QolMenuKey.Value}/{Plugin.StyleMenuKey.Value} navigation, commands/audit/profile checks, your saved Push to Talk or Voice Activation setting, and the game's Pomodoro/focus ticket + XP reminder. Never posts globally.");
        DrawToggle("Welcome Overlay", Plugin.EnableWelcomeOverlay, "Shows a dismissible on-screen how-to every time you join a lobby, separate from the chat Welcome Message above. Its Don't show this again button turns this off.");
        DrawToggle("Toast Pop-ups", Plugin.EnableToastNotifications, "Shows a brief on-screen toast alongside the usual chat line for hotkeys, rebinds, cleanups, reconnects, settings, and safety alerts -- easy to spot on a small screen or in busy chat.");
        DrawToggle("Host Announcements", Plugin.EnableHostAnnouncements, "Shows heads-up toasts the host or an assigned Helper sends with /announce, like restarts or breaks. The chat line always appears.");
        DrawToggle("Extended Camera Zoom", Plugin.EnableExtendedZoom, "Scroll to zoom much closer and farther than normal, walking or flying (desk mode keeps its own zoom). Off snaps back to the normal range.");
        if (GUILayout.Button(new GUIContent("Show Welcome Overlay Now", "Open the welcome overlay again right now, even if you closed it or turned it off."), _buttonStyle, GUILayout.Height(30f)) && Plugin.Instance != null)
        {
            Plugin.Instance.ShowWelcomeOverlayNow();
        }
        DrawToggle("BetterMove", Plugin.EnableBetterMove, "Local movement helper: hold Shift to run and Ctrl to slow-walk. It only affects your character input.");
        DrawToggle("Reusable Consumables", Plugin.EnableUnlimitedConsumables, "Once you own a canteen item, chalk color, or fishing bait, using it no longer lowers the count, so you do not keep spending tickets to replace it. Clothing, rods, and bobbers work as normal. Turn Off to restore normal item use immediately.");
        DrawToggle("Focus Anywhere", Plugin.EnableFocusAnywhere, "Lets focus mode start from more places, not only the game's focus spots.");
    }

    private void DrawChatReadabilityToggles()
    {
        DrawToggle("Chat + notice timestamps", Plugin.EnableChatTimestamps, "Adds local timestamps to chat plus join/leave/system notices. Use this if you like knowing when messages happened.", () =>
        {
            Plugin.EnableNotificationTimestamps.Value = Plugin.EnableChatTimestamps.Value;
            Plugin.Instance?.SaveConfigFromQolMenu();
        });
        DrawTimestampFormatRow();
        DrawToggle("Chat Readability", Plugin.EnableChatReadability, "Adds local outline/shadow to chat text so white or colored messages stay readable on bright maps. Does not change chat for others.", BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RefreshVisibleChat);
        DrawToggle("Persistent Chat Backdrop", Plugin.EnablePersistentChatBackdrop, "Keeps the chat panel/backdrop easier to read when the game exposes it. Local UI only; does not rewrite messages.", BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RefreshVisibleChat);
        DrawToggle("Ping Mentions", Plugin.EnablePingMentions, "Highlights messages that mention your name. Authorized lobby-wide pings are handled automatically for the host and assigned Helpers.");
        DrawToggle("Ping Sound", Plugin.EnablePingSound, "Plays your selected local ping sound when a message mentions you. Change the sound in Customize.");
        DrawToggle("Copy Chat Messages", Plugin.EnableChatCopy, "Plain left-click opens the game's ID card. Ctrl-left-click copies only the message text. Names, timestamps, statuses, and sender labels are left out.");
        DrawToggle("Chat Links", Plugin.EnableChatUrlLinks, "Recolors safe web links. Shift-left-click once copies/warns, then repeat within 10 seconds to open.");
        DrawToggle("Local Audit Exports", Plugin.EnableLocalAuditExports, "Optional, off by default. Keeps a copy of your chat and the game log on this computer for troubleshooting, one folder per session in BepInEx/BlueSageQoLModSuite/logs. The newest 3 sessions (or 3 days, or 24 MB) stay as plain files; older ones are zipped and kept 14 days. Nothing is uploaded. Use /auditlog folder or snapshot when you need the files.", SessionAuditController.ReconcileEnabledState);
        DrawLocalAuditActions(showIdentityNote: false);
    }

    private void DrawIdentitySafetyToggles()
    {
        DrawToggle("Reconnect Guard", Plugin.EnableReconnectGuard, "If you really get disconnected, BlueSage lets the game return to the menu (up to 10 seconds), waits the Reconnect wait (20 seconds by default, in Customize) so the host can clear your old spot, then rejoins your lobby once and checks for up to 45 seconds that you're back in. It tries at most 5 times per lobby; after that it stops, tells you, and leaves you on the menu.");
        DrawToggle("Reconnect Message", Plugin.EnableReconnectAnnouncement, "Sends the small BlueSage reconnect success message after a guarded reconnect works.");
        DrawToggle("Enhanced Player Panel", Plugin.EnableEnhancedPlayerPanel, "Adds local @mention and ID-card buttons. Hover the game's green Steam-profile button for a verified ID check. The game's own report, ignore, kick, and ban buttons stay where they are.");
        DrawToggle("Clone Shield", Plugin.EnableCloneShield, "Warns the lobby once when someone newly matches your avatar/outfit or exact styled name. The message is a clue, not proof, and asks everyone to check Steam profiles before acting.");
        DrawCommunityBanControls();
        DrawToggle("MiniMap ID Click", Plugin.EnableMiniMapPlayerLabels, "Click minimap player dots to open ID cards when BlueSage can safely match the player. Floating labels stay removed for performance.");
        DrawToggle("MiniMap Friend Colors", Plugin.EnableMiniMapFriendColors, "Colors Steam friends green on your minimap. Uses cached Steam relationship data and stands down when the standalone MinimapFriends mod is installed.");
    }

    private void DrawPersonalizationToggles()
    {
        DrawSpoonsToggle();
        DrawToggle("Style Helper", Plugin.EnableStyleUi, "Turns on Style Helper (/styleui) for names, status tags, Spoons, templates, and styled text.");
        DrawToggle("Performance Overlay", Plugin.EnablePerformanceOverlay, "Shows a small bar you can drag around with FPS / PING / RAM, plus the QoL Menu and Style Helper buttons and WORK / RELAX. As host, PING shows -- because you are the server; guests see their ping when it's known. The bar's × hides it; turn it back on here anytime.");
        DrawToggle("Extra Outfit Slots 4-9", Plugin.EnableExtendedAvatarStyles, "Adds six extra outfit slots (4 to 9) below the game's three in the avatar screen's style list. They start on. Styles 1 to 3 stay the game's own and never change; if you turn this off, your extra outfits wait for you.", BlueSage.QoLTweaks.Patches.AvatarStyleSlotsBridge.RefreshEnabledState);
        DrawToggle("Chalkboard Recovery", Plugin.EnableChalkboardPersistence, "Keeps local recovery copies. Use /chalk boards for 0 Stage, 1 Classroom A, and 2 Classroom B. View, save, list, delete, and refresh stay on this computer. Only the Host or an authenticated Helper can request a shared load or clear; BlueSage rechecks the exact board and saves recovery first. Existing Chalky takes ownership when installed.");
    }

    private void DrawCommunityBanControls()
    {
        const string tooltip =
            "On for fresh installs. Checks the Community Safety List fully, then adds only the names your game's own ban list is missing. It never removes anything, and it can't kick, ban, or give anyone powers. Turn it Off any time. /communitybans status shows where it's at.";
        DrawToggle(
            "Community Safety List",
            Plugin.EnableCommunityBanListSync,
            tooltip,
            () => Plugin.Instance?.ReconcileCommunityBanMaintenanceFromQolMenu());
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(
                new GUIContent("Safety List Status", tooltip),
                _buttonStyle,
                GUILayout.Height(28f)))
        {
            _message = CommunityBanListController.FriendlyStatusText();
            MarkActionQuiet(_message);
        }
        if (GUILayout.Button(
                new GUIContent("Refresh Safety List", "Check the Community Safety List now. Your game's ban list only changes if the new copy checks out completely."),
                _buttonStyle,
                GUILayout.Height(28f)))
        {
            _message = Plugin.Instance != null
                ? Plugin.Instance.RefreshCommunityBanListFromQolMenu()
                : "The Community Safety List isn't ready yet.";
            MarkAction(_message);
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(CommunityBanListController.FriendlyStatusText(), _smallStyle);
    }

    private void DrawAccessibilityToggles()
    {
        DrawToggle("Fishing Aid", Plugin.EnableFishingAid, "For sore or unsteady hands. Take out your rod with your fishing key (H) near water and stand still: it finds the water, casts, answers bites, plays the reel mini-game, and recasts. Press H again to stop. A cast by hand sets your preferred spot. Off by default. Also /fishaid.", () => Patches.FishingAidRuntime.Reset());
        GUILayout.Space(4f);
        GUILayout.Label("Catches are kept like normal. Tune the aid here; your changes save right away.", _smallStyle);
        GUILayout.Space(4f);
        DrawTextSetting("Fishing Aid bite reaction (seconds)", ref _fishingAidReactionText, "Save 0.1-1.5", SaveFishingAidReaction, () => FormatAidNumber(Plugin.FishingAidReactionSeconds?.Value ?? FishingAidPolicy.DefaultReactionSeconds));
        DrawTextSetting("Fishing Aid last-round clicks per second", ref _fishingAidClicksText, "Save 3-15", SaveFishingAidClicks, () => FormatAidNumber(Plugin.FishingAidClicksPerSecond?.Value ?? FishingAidPolicy.DefaultClicksPerSecond));
        DrawTextSetting("Fishing Aid auto release after catch (seconds)", ref _fishingAidReleaseText, "Save 0.2-5", SaveFishingAidRelease, () => FormatAidNumber(Plugin.FishingAidReleaseSeconds?.Value ?? FishingAidPolicy.DefaultReleaseSeconds));
    }

    private void DrawTimestampFormatRow()
    {
        GUILayout.Label("Timestamp format", _labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("12-hour AM/PM", "Show timestamps like 3:37 PM."), Plugin.Use24HourTime != null && !Plugin.Use24HourTime.Value ? _activeButtonStyle : _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f)))
        {
            Plugin.Use24HourTime.Value = false;
            Plugin.Instance?.SaveConfigFromQolMenu();
            _message = "Timestamps: 12-hour format with AM/PM.";
            MarkSaved(_message);
        }
        GUILayout.Space(6f);
        if (GUILayout.Button(new GUIContent("24-hour", "Show timestamps like 15:37 without AM/PM."), Plugin.Use24HourTime != null && Plugin.Use24HourTime.Value ? _activeButtonStyle : _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f)))
        {
            Plugin.Use24HourTime.Value = true;
            Plugin.Instance?.SaveConfigFromQolMenu();
            _message = "Timestamps: 24-hour format.";
            MarkSaved(_message);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawFeatureSection(string title, string description, Action drawContent, float width, FeatureSectionTone tone)
    {
        GUIStyle style = GetFeatureSectionStyle(tone);
        _textSettingLabelWidth = Mathf.Clamp(width * 0.42f, 170f, 340f);
        GUILayout.BeginVertical(style, GUILayout.Width(width), GUILayout.ExpandHeight(false));
        GUILayout.Label(title, _sectionHeaderStyle);
        GUILayout.Label(description, _smallStyle);
        GUILayout.Space(5f);
        drawContent?.Invoke();
        GUILayout.EndVertical();
    }

    private GUIStyle GetFeatureSectionStyle(FeatureSectionTone tone)
    {
        return tone switch
        {
            FeatureSectionTone.Comfort => _comfortSectionBoxStyle,
            FeatureSectionTone.Chat => _chatSectionBoxStyle,
            FeatureSectionTone.Identity => _identitySectionBoxStyle,
            FeatureSectionTone.Personalization => _personalizationSectionBoxStyle,
            FeatureSectionTone.Accessibility => _accessibilitySectionBoxStyle,
            FeatureSectionTone.Experimental => _experimentalBoxStyle,
            _ => _sectionBoxStyle
        };
    }

    private Color GetFeatureSectionAccent(FeatureSectionTone tone)
    {
        return tone switch
        {
            FeatureSectionTone.Comfort => _theme.ComfortAccent,
            FeatureSectionTone.Chat => _theme.ChatAccent,
            FeatureSectionTone.Identity => _theme.IdentityAccent,
            FeatureSectionTone.Personalization => _theme.PersonalizationAccent,
            FeatureSectionTone.Accessibility => _theme.AccessibilityAccent,
            FeatureSectionTone.Experimental => _theme.ExperimentalAccent,
            _ => _theme.LabelText
        };
    }

    private void DrawCustomizeTab()
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label("Customize safely", _sectionHeaderStyle);
        GUILayout.Label("Every editable row has Save and Undo. Undo discards an unsaved draft or restores the value from before your last Save.", _smallStyle);
        bool styleHelperOpen = Plugin.IsStyleHelperVisible;
        if (GUILayout.Button(new GUIContent(styleHelperOpen ? "Close Style Helper" : "Open Style Helper", "Open or close the helper for names, status tags, colors, templates, and Spoons."), styleHelperOpen ? _enabledButtonStyle : _buttonStyle, GUILayout.Height(34f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ToggleStyleUiFromQolMenu() : "Plugin is not ready.";
            MarkAction(_message);
        }
        GUILayout.Space(8f);

        bool useTwoColumns = _windowRect.width >= CustomizeTwoColumnMinWidth;
        float availableWidth = GetContentLayoutWidth();
        float columnWidth = useTwoColumns ? (availableWidth - 12f) / 2f : availableWidth;

        if (useTwoColumns)
        {
            GUILayout.BeginHorizontal();
            // Round 3 (Blue, 2026-10-17): balance the two columns instead of stacking one side.
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            DrawSettingsSection("General", "Safe housekeeping timing.", DrawGeneralSettings, columnWidth);
            GUILayout.Space(12f);
            DrawHotkeysSection(columnWidth);
            GUILayout.Space(12f);
            DrawSettingsSection("Mentions & Pings", "Choose how local alerts look and sound.", DrawPingSettings, columnWidth);
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            DrawSelfMovementSection(columnWidth);
            GUILayout.Space(12f);
            DrawSettingsSection("Chat & Readability", "Scale text, space, outline, and retained history independently.", DrawChatSettings, columnWidth);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
            DrawSettingsSection("Text Limits", "Defaults favor stability; power users may raise them.", DrawTextLimitSettings, availableWidth);
        }
        else
        {
            DrawSettingsSection("General", "Safe housekeeping timing.", DrawGeneralSettings, columnWidth);
            GUILayout.Space(12f);
            DrawSelfMovementSection(columnWidth);
            GUILayout.Space(12f);
            DrawHotkeysSection(columnWidth);
            GUILayout.Space(12f);
            DrawSettingsSection("Mentions & Pings", "Choose how local alerts look and sound.", DrawPingSettings, columnWidth);
            GUILayout.Space(12f);
            DrawSettingsSection("Chat & Readability", "Scale text, space, outline, and retained history independently.", DrawChatSettings, columnWidth);
            GUILayout.Space(12f);
            DrawSettingsSection("Text Limits", "Defaults favor stability; power users may raise them.", DrawTextLimitSettings, columnWidth);
        }
        GUILayout.EndVertical();
    }

    private void DrawGeneralSettings()
    {
        DrawTextSetting("Automatic cleanup interval (minutes)", ref _sweepIntervalText, "Save 5-180", SaveSweepInterval, () => Plugin.SweepIntervalMinutes?.Value.ToString() ?? SweepSettingsPolicy.DefaultIntervalMinutes.ToString());
        DrawTextSetting("Reconnect wait before rejoining (seconds)", ref _reconnectRejoinHoldText, "Save 10-120", SaveReconnectRejoinHold, () => Plugin.ReconnectRejoinHoldSeconds?.Value.ToString() ?? "20");
        DrawTextSetting("Toast reading time (seconds)", ref _toastHoldText, "Save 2-15", SaveToastHold, () => Plugin.LockedToastHoldSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        DrawTextSetting("Toast size", ref _toastScaleText, "Save Auto/75-200", SaveToastScale, () => (Plugin.ToastScalePercent?.Value ?? 0) == 0 ? "Auto" : Plugin.ToastScalePercent.Value.ToString());
        GUILayout.Label("Menu size is display-aware. Use Auto, or choose 75-200%. This does not change chat size.", _smallStyle);
        DrawTextSetting("BlueSage menu scale", ref _menuUiScaleText, "Save Auto/75-200", SaveMenuUiScale, () => Plugin.MenuUiScalePercent?.Value == 0 ? "Auto" : Plugin.MenuUiScalePercent.Value.ToString());
    }

    private void DrawSelfMovementSection(float width)
    {
        DrawSettingsSection(
            "Self movement",
            "Optional self-only movement and rescue-from-a-glitch controls.",
            DrawSelfMovementTools,
            width);
    }

    private void DrawHotkeysSection(float width)
    {
        DrawSettingsSection(
            "Hotkeys",
            "Rebind any hotkey right here. No config file editing needed.",
            DrawHotkeySettings,
            width);
    }

    private void DrawHotkeySettings()
    {
        bool awaiting = Plugin.Instance != null && Plugin.Instance.IsAwaitingHotkeyRebind;
        if (awaiting)
        {
            GUILayout.Label($"Press any key to bind {Plugin.Instance.PendingRebindLabel} (Esc cancels)...", _smallStyle);
            if (GUILayout.Button(new GUIContent("Cancel Rebind", "Stop listening without changing this hotkey."), _buttonStyle, GUILayout.Height(30f)))
            {
                _message = Plugin.Instance.CancelRebindHotkey();
                MarkAction(_message);
            }
            GUILayout.Space(6f);
        }

        DrawHotkeyRow("QoL Menu", Plugin.QolMenuKey, Plugin.HotkeyBindTarget.QolMenu, awaiting);
        DrawHotkeyRow("Style Helper", Plugin.StyleMenuKey, Plugin.HotkeyBindTarget.StyleMenu, awaiting);
        DrawHotkeyRow("Noclip / Fly", Plugin.NoclipFlyKey, Plugin.HotkeyBindTarget.NoclipFly, awaiting);
        DrawHotkeyRow("Run Cleanup", Plugin.ManualSweepKey, Plugin.HotkeyBindTarget.ManualSweep, awaiting);
        DrawFlyRotateButtonRow();
        DrawTextSetting("Fly Alt precision (% of fly speed)", ref _flyPrecisionText, "Save 5-50", SaveFlyPrecision, () => Plugin.FlyPrecisionPercent?.Value.ToString() ?? FlyCameraPolicy.DefaultPrecisionPercent.ToString());

        GUILayout.Space(4f);
        if (GUILayout.Button(new GUIContent("Reset All Hotkeys to Defaults", "QoL Menu=Home, Style Helper=Insert, Noclip/Fly=End, Run Cleanup=F8."), _buttonStyle, GUILayout.Height(30f)) && !awaiting && Plugin.Instance != null)
        {
            _message = Plugin.Instance.ResetAllHotkeysToDefaults();
            MarkAction(_message);
        }
    }

    private void DrawFlyRotateButtonRow()
    {
        if (Plugin.FlyRotateButton == null)
        {
            return;
        }

        const string tooltip = "Hold this mouse button while flying to turn the camera. Click to cycle: Middle Mouse, Mouse 4, Mouse 5, Right Mouse.";
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Fly camera rotate", tooltip), _labelStyle, GUILayout.Width(190f));
        string current = FlyCameraPolicy.FriendlyRotateButtonName(Plugin.FlyRotateButton.Value);
        if (GUILayout.Button(new GUIContent("Change (" + current + ")", tooltip), _buttonStyle, GUILayout.Height(30f)))
        {
            Plugin.FlyRotateButton.Value = FlyCameraPolicy.NextRotateButton(Plugin.FlyRotateButton.Value);
            Plugin.Instance?.SaveConfigFromQolMenu();
            string next = FlyCameraPolicy.FriendlyRotateButtonName(Plugin.FlyRotateButton.Value);
            _message = "Fly camera rotate: " + next + ".";
            Plugin.Instance?.ShowToast("Fly camera rotate: " + next);
            MarkSavedQuiet("Fly camera rotate saved: " + next + ".");
        }
        GUILayout.EndHorizontal();
    }

    private static string FormatAidNumber(float value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private void SaveFishingAidReaction()
    {
        if (!float.TryParse(_fishingAidReactionText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            _message = "Fishing Aid bite reaction must be a number from 0.1 to 1.5 seconds.";
            MarkDraft("Fix Fishing Aid bite reaction to a number from 0.1 to 1.5.");
            return;
        }

        float clamped = FishingAidPolicy.ClampReactionSeconds(value);
        Plugin.FishingAidReactionSeconds.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _fishingAidReactionText = FormatAidNumber(clamped);
        _message = "Fishing Aid bite reaction saved: " + _fishingAidReactionText + " seconds.";
        MarkSaved("Fishing Aid bite reaction saved.");
    }

    private void SaveFishingAidRelease()
    {
        if (!float.TryParse(_fishingAidReleaseText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            _message = "Fishing Aid auto release must be a number from 0.2 to 5 seconds.";
            MarkDraft("Fix Fishing Aid auto release to a number from 0.2 to 5.");
            return;
        }

        float clamped = FishingAidPolicy.ClampReleaseSeconds(value);
        Plugin.FishingAidReleaseSeconds.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _fishingAidReleaseText = FormatAidNumber(clamped);
        _message = "Fishing Aid auto release saved: " + _fishingAidReleaseText + " seconds after a catch.";
        MarkSaved("Fishing Aid auto release saved.");
    }

    private void SaveFishingAidClicks()
    {
        if (!float.TryParse(_fishingAidClicksText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            _message = "Fishing Aid last-round speed must be a number from 3 to 15 clicks per second.";
            MarkDraft("Fix Fishing Aid last-round speed to a number from 3 to 15.");
            return;
        }

        float clamped = FishingAidPolicy.ClampClicksPerSecond(value);
        Plugin.FishingAidClicksPerSecond.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _fishingAidClicksText = FormatAidNumber(clamped);
        _message = "Fishing Aid last-round speed saved: " + _fishingAidClicksText + " clicks per second.";
        MarkSaved("Fishing Aid last-round speed saved.");
    }

    // Round 3 (Blue, 2026-10-17): a one-line field hid long messages. A wrapping box shows the whole MOTD
    // with a live count against the 160 characters the lobby actually carries.
    private void DrawMotdSettings()
    {
        string previous = _hostMotdText ?? string.Empty;
        string next = GUILayout.TextArea(previous, LobbyAnnouncementPolicy.MaxTextLength, _textFieldStyle, GUILayout.ExpandWidth(true), GUILayout.MinHeight(64f));
        if (!string.Equals(next, previous, StringComparison.Ordinal))
        {
            _hostMotdText = next;
            MarkDraft("Lobby welcome message changed. Press Save or Undo.");
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label((_hostMotdText ?? string.Empty).Length + "/" + LobbyAnnouncementPolicy.MaxTextLength + " characters. Empty uses \"" + LobbyMotdPolicy.DefaultMessage + "\".", _smallStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(new GUIContent("Save", "Save and publish this welcome message to lobbies you host."), _buttonStyle, GUILayout.Width(112f), GUILayout.Height(28f)))
        {
            _settingUndoValues["Lobby welcome message (MOTD)"] = Plugin.HostMotd?.Value ?? string.Empty;
            SaveHostMotd();
        }

        if (GUILayout.Button(new GUIContent("Undo", "Restore the message from before your last Save, or discard an unsaved edit."), _buttonStyle, GUILayout.Width(80f), GUILayout.Height(28f)))
        {
            if (_settingUndoValues.TryGetValue("Lobby welcome message (MOTD)", out string before))
            {
                _hostMotdText = before;
                _suppressSaveToast = true;
                try
                {
                    SaveHostMotd();
                }
                finally
                {
                    _suppressSaveToast = false;
                }

                _settingUndoValues.Remove("Lobby welcome message (MOTD)");
            }
            else
            {
                _hostMotdText = Plugin.HostMotd?.Value ?? string.Empty;
                MarkSaved("Unsaved welcome message edit discarded.");
            }
        }

        GUILayout.EndHorizontal();
    }

    private void SaveHostMotd()
    {
        if (Plugin.HostMotd == null)
        {
            return;
        }

        Plugin.HostMotd.Value = LobbyAnnouncementPolicy.Sanitize(_hostMotdText);
        Plugin.Instance?.SaveConfigFromQolMenu();
        SteamIdModerationController.PublishHostCapability();
        _hostMotdText = Plugin.HostMotd.Value;
        string effective = LobbyMotdPolicy.Effective(Plugin.HostMotd.Value);
        _message = "Welcome message saved: \"" + effective + "\". QoL players see it when they join your lobby.";
        Plugin.Instance?.ShowToast("Welcome message saved: " + effective);
        MarkSavedQuiet("Welcome message saved.");
    }

    private void SaveFlyPrecision()
    {
        if (!int.TryParse(_flyPrecisionText, out int value))
        {
            _message = "Fly Alt precision must be a number from 5 to 50.";
            MarkDraft("Fix Fly Alt precision to a number from 5 to 50.");
            return;
        }

        int clamped = FlyCameraPolicy.ClampPrecisionPercent(value);
        Plugin.FlyPrecisionPercent.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _flyPrecisionText = clamped.ToString();
        _message = "Fly Alt precision saved: " + clamped + "% of fly speed.";
        MarkSaved("Fly Alt precision saved.");
    }

    private void DrawHotkeyRow(string label, ConfigEntry<KeyCode> keyEntry, Plugin.HotkeyBindTarget target, bool awaiting)
    {
        // Matches DrawToggle's row rhythm (fixed-width _labelStyle name column, GUILayout.Height(30f)
        // controls) so this section doesn't visually clash with the toggle rows above/around it. The
        // current key's name is unpredictable-length (any of ~500 KeyCode values), so it lives inside
        // the auto-width action button's own text rather than a fixed-width label, the same way the
        // Self Movement section lets its "Noclip / Fly: On/Off" button grow instead of clipping.
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(label, "Current hotkey: " + keyEntry?.Value), _labelStyle, GUILayout.Width(190f));
        string buttonLabel = "Rebind (" + keyEntry?.Value + ")";
        if (GUILayout.Button(new GUIContent(buttonLabel, "Click, then press any key to set this hotkey. Esc cancels."), _buttonStyle, GUILayout.Height(30f)) && !awaiting)
        {
            if (Plugin.Instance != null)
            {
                _message = Plugin.Instance.BeginRebindHotkey(target);
                MarkAction(_message);
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawPingSettings()
    {
        DrawTextSetting("Ping highlight color", ref _pingHighlightText, "Save", () => SaveHex(Plugin.PingHighlightColor, _pingHighlightText, "Ping highlight"), () => Plugin.PingHighlightColor?.Value ?? "9B59B6");
        DrawTextSetting("Ping @mention color", ref _pingMentionText, "Save", () => SaveHex(Plugin.PingMentionColor, _pingMentionText, "Ping mention"), () => Plugin.PingMentionColor?.Value ?? "FFD700");
        DrawTextSetting("Ping sound mode", ref _pingSoundModeText, "Save", SavePingSoundMode, () => Plugin.GetPingSoundMode());
    }

    private void DrawChatSettings()
    {
        GUILayout.Label("Chat font size grows message text only. Whole chat UI scale grows the panel/input/backdrop too.", _smallStyle);
        GUILayout.Label("Chat window height adds vertical log space. Native Global/Local tabs stay game-owned.", _smallStyle);
        GUILayout.Space(5f);
        DrawTextSetting("Chat outline intensity", ref _chatOutlineIntensityText, "Save 25-100", SaveOutlineIntensity, () => Plugin.LockedChatOutlineIntensityPercent.ToString());
        DrawTextSetting("Whole chat UI scale", ref _chatUiScaleText, "Save 75-200", SaveChatUiScale, () => Plugin.LockedChatUiScalePercent.ToString());
        DrawTextSetting("Chat font size", ref _chatFontSizeText, "Save 75-200", SaveChatFontSize, () => Plugin.LockedChatFontSizePercent.ToString());
        DrawTextSetting("Chat window height", ref _chatWindowHeightText, "Save 100-200", SaveChatWindowHeight, () => Plugin.LockedChatWindowHeightPercent.ToString());
        DrawTextSetting("Retained chat rows", ref _chatHistoryRowsText, "Save 25-250", SaveChatHistoryRows, () => Plugin.LockedChatHistoryRows.ToString());
        DrawTextSetting("Local chat range (m)", ref _localChatRangeText, "Save 5-25", SaveLocalChatRange, () => Plugin.LockedLocalChatRangeMeters.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        if (GUILayout.Button(new GUIContent("Reset Chat to the Game's Default", "Sets chat size, message text size and window height back to 100% and puts the chat back where the game had it, where possible. Also /chatvanilla."), _buttonStyle, GUILayout.Height(30f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ResetChatUiToVanillaFromMenu() : "Plugin is not ready.";
            HydrateFields();
            MarkSaved("Chat UI reset to vanilla.");
        }
        DrawTextSetting("Normal outline color", ref _chatOutlineColorText, "Save", () => SaveHex(Plugin.ChatOutlineColor, _chatOutlineColorText, "Normal outline"), () => Plugin.LockedChatOutlineColorHex);
        DrawTextSetting("Black text outline color", ref _blackChatOutlineColorText, "Save", () => SaveHex(Plugin.BlackChatOutlineColor, _blackChatOutlineColorText, "Black text outline"), () => Plugin.LockedBlackChatOutlineColorHex);
        DrawToggle("BlackNamesOutline", Plugin.BlackNamesOutline, "Uses the black-text outline color for true-black chat/name rows. On by default for readable black styles; turn off to force the normal outline color.", BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RefreshVisibleChat);
    }

    private void DrawTextLimitSettings()
    {
        GUILayout.Label("Chat default is " + InputFieldLimitPolicy.DefaultMaxChatCharacters + ". Values above " + InputFieldLimitPolicy.ChatWrapWarningCharacters + " may wrap, clip, or look odd in some game UI.", _smallStyle);
        DrawTextSetting("Chat text limit", ref _chatLimitText, "Save 1-4000", () => SaveCharacterLimit(Plugin.MaxChatCharacters, _chatLimitText, "Chat text", InputFieldLimitPolicy.ClampChatCharacters, value => _chatLimitText = value.ToString()), () => Plugin.LockedMaxChatCharacters.ToString());
        DrawTextSetting("Name/ID limit", ref _profileLimitText, "Save 1-4000", () => SaveCharacterLimit(Plugin.MaxProfileCharacters, _profileLimitText, "Name and ID card text", InputFieldLimitPolicy.ClampProfileCharacters, value => _profileLimitText = value.ToString()), () => Plugin.LockedMaxIdCardCharacters.ToString());
        DrawTextSetting("Room name limit", ref _sessionLimitText, "Save 1-4000", () => SaveCharacterLimit(Plugin.MaxSessionNameCharacters, _sessionLimitText, "Room name text", InputFieldLimitPolicy.ClampSessionNameCharacters, value => _sessionLimitText = value.ToString()), () => Plugin.LockedMaxSessionNameCharacters.ToString());
    }

    private void DrawSettingsSection(string title, string description, Action drawContent, float width)
    {
        _textSettingLabelWidth = Mathf.Clamp(width * 0.42f, 170f, 340f);
        GUILayout.BeginVertical(_sectionBoxStyle, GUILayout.Width(width), GUILayout.ExpandHeight(false));
        GUILayout.Label(title, _sectionHeaderStyle);
        GUILayout.Label(description, _smallStyle);
        GUILayout.Space(5f);
        drawContent?.Invoke();
        GUILayout.EndVertical();
    }

    private void DrawCreditsOtherTab()
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label("Tools, safety, and credits", _sectionHeaderStyle);
        GUILayout.Label("Less-used things live here so the everyday tabs stay calm. Hover anything for a short explanation.", _smallStyle);
        GUILayout.Space(8f);

        bool useTwoColumns = _windowRect.width >= 1280f;
        float availableWidth = GetContentLayoutWidth();
        float columnWidth = useTwoColumns ? (availableWidth - 12f) / 2f : availableWidth;

        DrawSettingsSection(
            "♥ COMMUNITY SUPPORTERS ♥",
            "A simple thank-you wall for the people who helped BlueSage keep building, with private payment details excluded.",
            DrawSupporterCredits,
            availableWidth);
        GUILayout.Space(12f);
        DrawSettingsSection(
            "Community crew",
            "Testing, crash evidence, and trusted co-conspirators who help BlueSage stay honest.",
            DrawCommunityCrew,
            availableWidth);
        GUILayout.Space(12f);

        if (useTwoColumns)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            DrawSettingsSection("About BlueSage QoL", "Community-built comfort, clarity, and safer lobby tools.", DrawAboutAndCredits, columnWidth);
            GUILayout.EndVertical();
            GUILayout.Space(12f);
            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            DrawSettingsSection("Menu appearance", "Choose one shared theme for the QoL Menu, Style Helper, and command popups.", DrawThemeSelector, columnWidth);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        else
        {
            DrawSettingsSection("Menu appearance", "Choose one shared theme for the QoL Menu, Style Helper, and command popups.", DrawThemeSelector, columnWidth);
            GUILayout.Space(12f);
            DrawSettingsSection("About BlueSage QoL", "Community-built comfort, clarity, and safer lobby tools.", DrawAboutAndCredits, columnWidth);
        }
        GUILayout.EndVertical();
    }

    private void DrawAboutAndCredits()
    {
        GUILayout.Label("BlueSage QoL Tweaks brings common comfort, readability, personalization, reconnect, and community-safety tools into one guided menu.", _smallStyle);
        GUILayout.Label("Automatic Cleanup gently frees game memory that's no longer used. It never deletes files or touches your profile. MiniMap ID Click stays light; floating name labels stay off because they slowed big lobbies down.", _smallStyle);
        if (GUILayout.Button(new GUIContent("Run Host Health", "Optional host-focused lobby-health check. Most players only need Run QoL Status on Main."), _buttonStyle, GUILayout.Height(30f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.RunHostHealthFromQolMenu() : "Plugin is not ready.";
            MarkAction(_message);
        }
        GUILayout.Space(8f);
        GUILayout.Label("Optional lobby joke", _labelStyle);
        GUILayout.Label("Sends one playful lobby chat line the first time. It never changes moderation or game state.", _smallStyle);
        DrawDoNotPressButton();
        GUILayout.Label("Share finds in Blue's Discord if you uncover another harmless oddity.", _smallStyle);
    }

    private void DrawLobbySafetyTab()
    {
        LobbySafetyAccessState accessState = SteamIdModerationController.GetLocalAccessState(out bool isHost, out string accessReason);
        if (accessState == LobbySafetyAccessState.Denied)
        {
            _tab = 0;
            _contentScroll = Vector2.zero;
            ClearLobbySafetyTransientState();
            GUILayout.Label(SteamIdModerationController.LobbySafetyDeniedMessage, _smallStyle);
            return;
        }

        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label(isHost ? "Host Tools" : "Safety", _sectionHeaderStyle);
        if (isHost)
        {
            DrawHostReadinessCard();
            GUILayout.Space(10f);
            DrawHostMotdCard();
            GUILayout.Space(10f);
        }
        DrawIdentityResyncControls(accessState, isHost);
        GUILayout.Space(10f);
        if (accessState == LobbySafetyAccessState.HelperWaitingForHost)
        {
            GUILayout.Label("Helper assigned • waiting for the host", _labelStyle);
            GUILayout.Label("You're an assigned Helper here. Your tools unlock by themselves once the host's BlueSage answers. No command needed.", _smallStyle);
            GUILayout.Space(8f);
            if (GUILayout.Button(new GUIContent("Check Again", "Check again now. BlueSage also keeps checking on its own."), _buttonStyle, GUILayout.Height(32f)))
            {
                _message = Plugin.Instance != null ? Plugin.Instance.CheckModerationAccessFromQolMenu() : "Helper / Safety is not ready yet.";
                MarkActionQuiet("Checked again. Your tools unlock as soon as the host's BlueSage answers.");
            }
            GUILayout.EndVertical();
            return;
        }
        GUILayout.Label(isHost
            ? "Host access. Act only after checking the Steam profile for the exact current row; Clone Shield is an advisory clue, not proof."
            : "Helper access assigned by this host. Act only after checking the Steam profile for the exact current row; the host rechecks every request.", _smallStyle);
        GUILayout.Space(8f);

        IReadOnlyList<PlayerIdentityEvidence> verifiedRoster = PlayerIdentityEvidenceController.GetVerifiedRoster(out string rosterStatus);
        DrawVerifiedSafetyRoster(verifiedRoster, rosterStatus, isHost);
        GUILayout.Space(10f);

        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label("Other mods", _labelStyle);
        GUILayout.Label(Plugin.GetDefensiveCompatibilitySummary(), _smallStyle);
        GUILayout.Label("The Community Safety List is checked on this computer. It never removes your own ban entries, a bad download is thrown away, and it can't kick, ban, or give anyone powers. " + CommunityBanListController.FriendlyStatusText(), _smallStyle);
        GUILayout.EndVertical();
        GUILayout.Space(10f);

        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label("Local Audit Exports", _labelStyle);
        DrawLocalAuditActions(showIdentityNote: true);
        GUILayout.EndVertical();
        GUILayout.Space(10f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Check My Access", "Shows whether you are the current host, a host-assigned Helper, or a regular player."), _buttonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.CheckModerationAccessFromQolMenu() : "Lobby Safety is not ready yet.";
            MarkActionQuiet(_message);
        }
        if (isHost && GUILayout.Button(new GUIContent("Clear All Helpers", "Remove every Helper from your persistent host-owned list. Your own host access remains."), _buttonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ResetHelpersFromQolMenu() : "Lobby Safety is not ready yet.";
            MarkAction(_message);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10f);

        DrawCloneIncidentReview();
        if (isHost)
        {
            GUILayout.Space(12f);
            DrawOfflineBanEntry();
            GUILayout.Space(12f);
            DrawNativeBanRecords();
            GUILayout.Space(12f);
            DrawHostJournalBox();
        }
        GUILayout.EndVertical();
    }

    // Round 4 Stage D (Blue: "the host should always have audit and logs available alongside helper actions"), Stage H
    // (Blue: readable for hosts of 60-100 slots): actions first, one pinned status line, three filters, one-glance
    // rows with the full line on hover, click to copy a row, "Copy last 20". The file below keeps every row.
    private void DrawHostJournalBox()
    {
        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label("Host journal", _labelStyle);
        GUILayout.Label("Every ban, unban, kick or mute made through BlueSage (yours, a Helper's, or the game's own Ban buttons): who did it, to whom, the RECONNECTED/LOADING label and whether it worked. Hover a row for the full line with the Steam ID and reason; click it to copy. Always on while you host, and saved to the file below.", _smallStyle);
        GUILayout.Label(HostJournalBoxPolicy.PinnedStatusLine(HostJournalController.LatestStatus, HostJournalController.StatusChangeCount), _smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_hostJournalFilter == HostJournalBoxFilter.Actions, new GUIContent("Actions", "Bans, unbans, kicks and mutes, newest first (the last 100)."), _buttonStyle, GUILayout.Width(80f)))
        {
            _hostJournalFilter = HostJournalBoxFilter.Actions;
        }
        if (GUILayout.Toggle(_hostJournalFilter == HostJournalBoxFilter.Status, new GUIContent("Status", "Lobby or health changes, plus a check-in every 5 minutes (last 20)."), _buttonStyle, GUILayout.Width(80f)))
        {
            _hostJournalFilter = HostJournalBoxFilter.Status;
        }
        if (GUILayout.Toggle(_hostJournalFilter == HostJournalBoxFilter.RefusedOnly, new GUIContent("Refused only", "Actions BlueSage refused, with the reason on hover."), _buttonStyle, GUILayout.Width(110f)))
        {
            _hostJournalFilter = HostJournalBoxFilter.RefusedOnly;
        }
        GUILayout.FlexibleSpace();
        IReadOnlyList<HostJournalRow> rows = HostJournalBoxPolicy.Select(
            HostJournalController.SnapshotActionsNewestFirst(),
            HostJournalController.SnapshotStatusNewestFirst(),
            _hostJournalFilter);
        if (GUILayout.Button(new GUIContent("Copy last 20", "Copies the newest 20 rows of this view as full lines."), _buttonStyle, GUILayout.Width(110f)))
        {
            GUIUtility.systemCopyBuffer = HostJournalBoxPolicy.CopyBlock(rows, HostJournalBoxPolicy.CopyRowCount);
            MarkAction("Host journal rows copied.");
        }
        GUILayout.EndHorizontal();

        _hostJournalScroll = GUILayout.BeginScrollView(_hostJournalScroll, GUILayout.Height(rows.Count == 0 ? 36f : 160f));
        if (rows.Count == 0)
        {
            GUILayout.Label(_hostJournalFilter == HostJournalBoxFilter.RefusedOnly ? "Nothing refused this session." : "No rows yet this session.", _smallStyle);
        }

        foreach (HostJournalRow row in rows)
        {
            bool refused = row.Kind == HostJournalRowKind.Action && !row.Success;
            Color previous = GUI.contentColor;
            if (refused)
            {
                GUI.contentColor = new Color(1f, 0.78f, 0.55f, 1f);
            }

            if (GUILayout.Button(new GUIContent(HostJournalBoxPolicy.ToShortLine(row), HostJournalFormat.ToDisplayLine(row)), _smallStyle))
            {
                GUIUtility.systemCopyBuffer = HostJournalFormat.ToDisplayLine(row);
                MarkAction("Host journal row copied.");
            }

            GUI.contentColor = previous;
        }

        GUILayout.EndScrollView();
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Label(HostJournalController.FilePath, _smallStyle);
        if (GUILayout.Button(new GUIContent("Copy Path", "Copies the journal file path to the clipboard."), _buttonStyle, GUILayout.Width(90f)))
        {
            GUIUtility.systemCopyBuffer = HostJournalController.FilePath;
            MarkAction("Host journal path copied.");
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // Round 3 (Blue, UAT 2026-10-18): the MOTD is a lobby-host function, so it lives in Host Tools
    // beside the host readiness card instead of in everyone's Customize tab.
    private void DrawHostMotdCard()
    {
        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label("Lobby welcome message (MOTD)", _labelStyle);
        GUILayout.Label("What QoL players see when they join a lobby you host. Room for the full message.", _smallStyle);
        GUILayout.Space(4f);
        DrawMotdSettings();
        GUILayout.EndVertical();
    }

    private void DrawHostReadinessCard()
    {
        // IMGUI draws each frame twice (Layout, then Repaint) and both passes must produce the same controls.
        // A companion can publish its capability snapshot between the two (live log, 2026-10-18), so the card
        // reads live state only on Layout and reuses it for every other event of that frame.
        if (Event.current.type == EventType.Layout || !_hostCardCaptured)
        {
            _hostCardPresentation = HostConsoleController.CapturePresentation();
            _hostCardHasCapability = HostConsoleController.HasCapabilitySnapshot;
            _hostCardCapability = _hostCardHasCapability ? HostConsoleController.LastCapabilitySnapshot : default;
            _hostCardShowDetails = _showHostToolsTechnicalDetails;
            _hostCardCaptured = true;
        }

        HostToolsPresentation presentation = _hostCardPresentation;
        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label(presentation.Heading, _sectionHeaderStyle);
        GUILayout.Label("What this means: " + presentation.Meaning, _smallStyle);
        GUILayout.Label("What to do: " + presentation.Advice, _smallStyle);
        GUILayout.Space(6f);
        GUILayout.Label(presentation.PlayerSummary, _smallStyle);
        GUILayout.Label(presentation.CleanupSummary, _smallStyle);
        GUILayout.Label(presentation.ReconnectSummary, _smallStyle);
        GUILayout.Label(presentation.LargeLobbySummary, _smallStyle);
        GUILayout.Label(presentation.LargeLobbyChatSummary, _smallStyle);
        GUILayout.Space(6f);
        if (GUILayout.Button(
                new GUIContent(
                    _hostCardShowDetails
                        ? "Hide technical details"
                        : "Show technical details",
                    "Optional support details for troubleshooting. Most hosts can leave these hidden."),
                _buttonStyle,
                GUILayout.Height(28f)))
        {
            _showHostToolsTechnicalDetails = !_showHostToolsTechnicalDetails;
        }
        if (_hostCardShowDetails)
        {
            GUILayout.Label(presentation.TechnicalDetails, _smallStyle);
        }
        if (GUILayout.Button(
                new GUIContent(
                    "Check game moderation buttons",
                    "Read-only compatibility check for the game's Mute, Ignore, Report, and Host Remove buttons. It does not act on any player."),
                _buttonStyle,
                GUILayout.Height(30f)))
        {
            _message = HostConsoleController.AuditNativeModerationUi();
            MarkActionQuiet(_message);
        }
        if (_hostCardHasCapability)
        {
            BlueSageHostCapabilitySnapshotV1 capability = _hostCardCapability;
            GUILayout.Space(8f);
            GUILayout.Label(capability.Title, _labelStyle);
            GUILayout.Label(capability.Summary, _smallStyle);
            GUILayout.BeginHorizontal();
            if ((capability.Actions & BlueSageHostCapabilityActionMaskV1.RefreshSnapshot) != 0 &&
                GUILayout.Button("Refresh status", _buttonStyle, GUILayout.Height(28f)))
            {
                HostConsoleController.TryExecuteCapability(
                    BlueSageHostCapabilityActionV1.RefreshSnapshot,
                    out _message);
                MarkActionQuiet(_message);
            }
            if ((capability.Actions & BlueSageHostCapabilityActionMaskV1.ResetMovement) != 0 &&
                GUILayout.Button("Reset movement", _buttonStyle, GUILayout.Height(28f)))
            {
                HostConsoleController.TryExecuteCapability(
                    BlueSageHostCapabilityActionV1.ResetMovement,
                    out _message);
                MarkAction(_message);
            }
            if ((capability.Actions & BlueSageHostCapabilityActionMaskV1.AuditNativeModeration) != 0 &&
                GUILayout.Button("Check game buttons", _buttonStyle, GUILayout.Height(28f)))
            {
                HostConsoleController.TryExecuteCapability(
                    BlueSageHostCapabilityActionV1.AuditNativeModeration,
                    out _message);
                MarkActionQuiet(_message);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawIdentityResyncControls(
        LobbySafetyAccessState accessState,
        bool isHost)
    {
        // Round 4b: refreshing read, so the section never flashes the one-frame "stale" help text every 2 s.
        IdentityResyncCoverage coverage =
            PlayerIdentityEvidenceController.GetIdentityResyncCoverage();
        IdentityResyncHelpPresentation help =
            IdentityResyncHelpPolicy.Describe(coverage, isHost);

        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label(
            "Player matching and safe host actions",
            _labelStyle);
        GUILayout.Label(
            help.StatusHeading,
            _smallStyle);
        GUILayout.Label(
            "What this means: " + help.WhatThisMeans,
            _smallStyle);
        GUILayout.Label(
            "What to do: " + help.WhatToDo,
            _smallStyle);

        GUILayout.Space(4f);
        GUILayout.Label("Step 1 - safe display refresh", _smallStyle);
        GUILayout.Label(help.RefreshExplanation, _smallStyle);
        if (isHost)
        {
            GUILayout.Label("Runs by itself every 15 minutes while you host (rescan and relabel only, never a moderation action). It writes no journal row; the journal keeps decisions and changes.", _smallStyle);
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(
                new GUIContent(
                    help.RefreshButtonLabel,
                    help.RefreshExplanation),
                _buttonStyle,
                GUILayout.Height(30f)))
        {
            string rawResult =
                IdentityResyncController.ExecuteMenuAction("refresh");
            _message = IdentityResyncHelpPolicy.FormatActionResult(
                "refresh",
                rawResult,
                PlayerIdentityEvidenceController.GetIdentityResyncCoverage());
            MarkActionQuiet(_message);
        }
        if (isHost ||
            accessState == LobbySafetyAccessState.HelperReady)
        {
            if (GUILayout.Button(
                    new GUIContent(
                        help.RecheckButtonLabel,
                        help.RecheckExplanation + " " + help.ExpectedResult),
                    _buttonStyle,
                    GUILayout.Height(30f)))
            {
                string action = isHost ? "repair" : "request";
                string rawResult =
                    IdentityResyncController.ExecuteMenuAction(action);
                _message = IdentityResyncHelpPolicy.FormatActionResult(
                    action,
                    rawResult,
                    PlayerIdentityEvidenceController.GetIdentityResyncCoverage());
                MarkActionQuiet(_message);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(
            "Step 2 - host recheck: " + help.RecheckExplanation,
            _smallStyle);
        GUILayout.Label(help.ExpectedResult, _smallStyle);
        GUILayout.Label("Still not fixed? " + help.RecoveryAdvice, _smallStyle);
        GUILayout.Label(
            "These buttons repair BlueSage's player-list matching only. They do not fix lag, voice chat, or a player's internet connection.",
            _smallStyle);
        if (_showHostToolsTechnicalDetails)
        {
            GUILayout.Label(
                "Support details (safe to ignore): " + IdentityResyncController.StatusText(),
                _smallStyle);
        }
        GUILayout.EndVertical();
    }

    private void DrawCloneIncidentReview()
    {
        IReadOnlyList<CloneIncident> open = CloneIncidentActionController.GetOpenIncidents();
        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Clone Shield incidents • " + open.Count + " open", _sectionHeaderStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent(_showClosedIncidentHistory ? "Hide History" : "Show History", "Show or hide resolved and locally dismissed incident history."), _buttonStyle, GUILayout.Width(104f), GUILayout.Height(28f)))
            _showClosedIncidentHistory = !_showClosedIncidentHistory;
        GUILayout.EndHorizontal();
        GUILayout.Label("Case numbers (C001, C002...) are just a shortcut on this computer. The Steam IDs stay exact. Resolve for Lobby clears the highlights for everyone with QoL once the host or a Helper sends it.", _smallStyle);

        if (open.Count > 0 && GUILayout.Button(new GUIContent("Dismiss All Open Locally", "Clear every open Clone Shield advisory and highlight on this client only. This never bans, changes Helpers, or publishes lobby state. Use /sidincident dismiss-all for the same action."), _buttonStyle, GUILayout.Height(30f)))
        {
            CloneIncidentActionController.TryDismissAllOpenLocal(out _message);
            MarkAction(_message);
            GUILayout.EndVertical();
            return;
        }

        if (open.Count == 0)
        {
            GUILayout.Label("No open incidents. Clone Shield highlights and moderation protections are clear on this client.", _smallStyle);
        }

        foreach (CloneIncident incident in open.Take(4))
        {
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Label(incident.Id + "  •  " + incident.MatchType + "  •  " + incident.StableId, _labelStyle);
            GUILayout.Label("Protected victim: " + incident.VictimLabel + "  •  …" + PlayerIdentityEvidenceController.Suffix(incident.VictimSteamId), _smallStyle);
            GUILayout.Label("Suspect: " + incident.SuspectLabel + "  •  …" + PlayerIdentityEvidenceController.Suffix(incident.SuspectSteamId), _smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Select Victim", "Select the protected player in the player list above. Ban buttons stay blocked for them."), _buttonStyle, GUILayout.Height(28f)))
                SelectSafetyPlayer(incident.VictimSteamId);
            if (GUILayout.Button(new GUIContent("Select Suspect", "Select the possible copier in the player list above so you can check their Steam profile."), _buttonStyle, GUILayout.Height(28f)))
                SelectSafetyPlayer(incident.SuspectSteamId);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Copy Victim ID", "Copy the protected victim's exact SteamID64."), _buttonStyle, GUILayout.Height(28f)))
            {
                CloneIncidentActionController.TryCopySteamId(incident.Id, suspect: false, out _message);
                MarkAction(_message);
            }
            if (GUILayout.Button(new GUIContent("Copy Suspect ID", "Copy the suspect's exact SteamID64."), _buttonStyle, GUILayout.Height(28f)))
            {
                CloneIncidentActionController.TryCopySteamId(incident.Id, suspect: true, out _message);
                MarkAction(_message);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Preview Suspect Ban", "Create a safe preview for the exact suspect. Confirmation remains separate."), _experimentalButtonStyle, GUILayout.Height(28f)))
            {
                _message = Plugin.Instance != null ? Plugin.Instance.PreviewSidBanFromQolMenu(incident.SuspectSteamId) : "SID preview is not ready yet.";
                MarkActionQuiet(_message);
            }
            ModerationConfirmationReadiness confirmation = Plugin.Instance != null
                ? Plugin.Instance.GetModerationConfirmationReadiness("ban", incident.SuspectSteamId)
                : new ModerationConfirmationReadiness(false, 0);
            GUI.enabled = confirmation.IsReady;
            string confirmLabel = confirmation.IsReady ? $"Confirm Suspect Ban  •  {confirmation.SecondsRemaining}s" : "Preview Required";
            if (GUILayout.Button(new GUIContent(confirmLabel, confirmation.IsReady
                    ? "Confirm the matching exact-suspect preview before its visible countdown expires."
                    : "Create a matching suspect-ban preview first; confirmation stays disabled until then."),
                    confirmation.IsReady ? _enabledButtonStyle : _disabledButtonStyle, GUILayout.Height(28f)))
            {
                _message = Plugin.Instance != null ? Plugin.Instance.ConfirmSidBanFromQolMenu(incident.SuspectSteamId) : "SID confirmation is not ready yet.";
                MarkAction(_message);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Resolve for Lobby", "Host or Helper: mark this incident handled and clear its victim/suspect highlights for compatible QoL clients."), _enabledButtonStyle, GUILayout.Height(28f)))
            {
                CloneIncidentActionController.TryResolveForLobby(incident.Id, out _message);
                MarkActionQuiet(_message);
            }
            if (GUILayout.Button(new GUIContent("Dismiss Locally", "Remove this advisory incident and its highlights on this client only. No moderation state changes."), _buttonStyle, GUILayout.Height(28f)))
            {
                CloneIncidentActionController.TryDismissLocal(incident.Id, out _message);
                MarkAction(_message);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        if (open.Count > 4) GUILayout.Label("Showing the newest 4 open incidents. Use /sidlist for a short summary, dismiss the ones you've handled, or dismiss all on this computer to clear the list.", _smallStyle);

        if (_showClosedIncidentHistory)
        {
            IReadOnlyList<CloneIncident> closed = CloneIncidentActionController.GetIncidentHistory()
                .Where(item => item.State != CloneIncidentState.Open)
                .Take(12)
                .ToArray();
            GUILayout.Space(8f);
            GUILayout.Label("Closed history • " + closed.Count + (closed.Count == 12 ? "+" : string.Empty), _labelStyle);
            foreach (CloneIncident incident in closed)
                GUILayout.Label(incident.Id + "  •  " + incident.State + "  •  victim …" + PlayerIdentityEvidenceController.Suffix(incident.VictimSteamId) + "  •  suspect …" + PlayerIdentityEvidenceController.Suffix(incident.SuspectSteamId), _smallStyle);
            if (GUILayout.Button(new GUIContent("Clear Closed History", "Remove resolved and locally dismissed history. Open incidents are never removed by this button."), _buttonStyle, GUILayout.Height(28f)))
            {
                int removed = CloneIncidentActionController.ClearClosedHistory();
                _message = "Cleared " + removed + " closed Clone Shield " + (removed == 1 ? "incident" : "incidents") + ".";
                MarkAction(_message);
            }
        }
        GUILayout.EndVertical();
    }

    private void SelectSafetyPlayer(string steamId)
    {
        _safetyRosterQuery = string.Empty;
        _selectedSafetySteamId = steamId ?? string.Empty;
        IReadOnlyList<PlayerIdentityEvidence> roster = PlayerIdentityEvidenceController.GetVerifiedRoster();
        int index = roster.ToList().FindIndex(item => string.Equals(item.SteamId, _selectedSafetySteamId, StringComparison.Ordinal));
        if (index >= 0) _safetyRosterPage = index / 8;
    }

    private void DrawVerifiedSafetyRoster(
        IReadOnlyList<PlayerIdentityEvidence> liveRoster,
        string rosterStatus,
        bool isHost)
    {
        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label("Find a verified live player", _labelStyle);
        GUILayout.Label("Type part of a name (even its style codes), a Steam name, 4 or more Steam ID digits, _host, _self, or a pattern. Searching only narrows the list to matched players; it never gives anyone ban powers.", _smallStyle);
        GUILayout.Label(rosterStatus, _smallStyle);
        GUILayout.BeginHorizontal();
        string priorQuery = _safetyRosterQuery;
        _safetyRosterQuery = GUILayout.TextField(_safetyRosterQuery ?? string.Empty, 96, _textFieldStyle, GUILayout.Height(30f));
        if (!string.Equals(priorQuery, _safetyRosterQuery, StringComparison.Ordinal))
        {
            _safetyRosterPage = 0;
            _selectedSafetySteamId = string.Empty;
        }
        if (GUILayout.Button(new GUIContent("Clear", "Show every currently verified row again."), _buttonStyle, GUILayout.Width(72f), GUILayout.Height(30f)))
        {
            _safetyRosterQuery = string.Empty;
            _safetyRosterPage = 0;
            _selectedSafetySteamId = string.Empty;
        }
        GUILayout.EndHorizontal();
        IReadOnlyList<PlayerIdentityEvidence> roster = PlayerIdentityEvidenceController.FilterVerifiedRoster(liveRoster, _safetyRosterQuery, out string filterError);
        if (!string.IsNullOrWhiteSpace(filterError)) GUILayout.Label(filterError, _smallStyle);
        GUILayout.EndVertical();
        GUILayout.Space(8f);

        const int pageSize = 8;
        int pageCount = Math.Max(1, (roster.Count + pageSize - 1) / pageSize);
        _safetyRosterPage = Mathf.Clamp(_safetyRosterPage, 0, pageCount - 1);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Matched players • {roster.Count}/{liveRoster.Count} shown • page {_safetyRosterPage + 1}/{pageCount}", _labelStyle);
        GUILayout.FlexibleSpace();
        GUI.enabled = _safetyRosterPage > 0;
        if (GUILayout.Button(new GUIContent("◀ Previous", "Show the previous eight verified current-lobby rows."), _buttonStyle, GUILayout.Width(104f), GUILayout.Height(28f)))
        {
            _safetyRosterPage--;
            _selectedSafetySteamId = string.Empty;
        }
        GUI.enabled = _safetyRosterPage + 1 < pageCount;
        if (GUILayout.Button(new GUIContent("Next ▶", "Show the next eight verified current-lobby rows."), _buttonStyle, GUILayout.Width(104f), GUILayout.Height(28f)))
        {
            _safetyRosterPage++;
            _selectedSafetySteamId = string.Empty;
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label("Select a row below; BlueSage keeps the exact SteamID64 attached to that selection. No retyping is needed.", _smallStyle);

        if (roster.Count == 0)
        {
            GUILayout.Label(string.IsNullOrWhiteSpace(_safetyRosterQuery)
                ? "The player list isn't matched yet. Close and reopen the game's player list, then come back here."
                : "No current verified player matches this filter. Clear it or add more exact detail.", _smallStyle);
            return;
        }

        int start = _safetyRosterPage * pageSize;
        int end = Math.Min(roster.Count, start + pageSize);
        for (int index = start; index < end; index++)
        {
            PlayerIdentityEvidence row = roster[index];
            bool selected = string.Equals(_selectedSafetySteamId, row.SteamId, StringComparison.Ordinal);
            string label = $"#{row.RosterIndex + 1:00}  {row.DisplayName}  •  …{PlayerIdentityEvidenceController.Suffix(row.SteamId)}  •  {row.Role}";
            string rowTooltip = selected
                ? "Selected. Click again to clear this player selection."
                : "Select this exact verified player-to-SteamID row.";
            if (GUILayout.Button(new GUIContent(label, rowTooltip), selected ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
            {
                _selectedSafetySteamId = selected ? string.Empty : row.SteamId;
                MarkActionQuiet(selected
                    ? "Cleared the selected player."
                    : "Selected " + row.DisplayName + " (Steam …" + PlayerIdentityEvidenceController.Suffix(row.SteamId) + ").");
            }
        }

        PlayerIdentityEvidence selectedRow = roster.FirstOrDefault(item => string.Equals(item.SteamId, _selectedSafetySteamId, StringComparison.Ordinal));
        if (selectedRow == null)
        {
            _selectedSafetySteamId = string.Empty;
            GUILayout.Space(8f);
            GUILayout.Label("No player is selected. Click the player you want to look at; BlueSage never switches your selection to someone else when the list changes.", _smallStyle);
            return;
        }

        GUILayout.Space(10f);
        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Selected player", _sectionHeaderStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Clear Selection", "Deselect this player and disarm every selection-scoped moderation control."), _buttonStyle, GUILayout.Width(130f), GUILayout.Height(28f)))
        {
            _selectedSafetySteamId = string.Empty;
            MarkActionQuiet("Cleared the selected player.");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return;
        }
        GUILayout.EndHorizontal();
        GUILayout.Label(selectedRow.DisplayName + "  •  " + selectedRow.Role, _labelStyle);
        GUILayout.Label("Steam persona: " + (string.IsNullOrWhiteSpace(selectedRow.SteamPersona) ? "not cached" : selectedRow.SteamPersona), _smallStyle);
        GUILayout.Label("SteamID64: " + selectedRow.SteamId, _smallStyle);
        GUILayout.Label("Verified live row #" + (selectedRow.RosterIndex + 1) + " • PlayerID " + selectedRow.NetworkPlayerId, _smallStyle);
        GUILayout.Label("Name exactly as typed, with its style codes (you can copy it):", _smallStyle);
        _ = GUILayout.TextArea(selectedRow.DisplayNameRaw ?? string.Empty, _textFieldStyle, GUILayout.MinHeight(42f), GUILayout.MaxHeight(70f));

        bool isSelfRow = selectedRow.IsLocal;
        bool isHostRow = selectedRow.Role.Split('•').Any(role => role.Trim().StartsWith("HOST", StringComparison.Ordinal));
        bool isHelperRow = selectedRow.Role.Split('•').Any(role => role.Trim().StartsWith("HELPER", StringComparison.Ordinal));
        bool isProtectedVictim = selectedRow.Role.Contains("VICTIM");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Copy SteamID64", "Copy this exact verified 17-digit Steam ID to the clipboard."), _buttonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.CopySteamIdFromQolMenu(selectedRow.SteamId) : "Copy is not ready yet.";
            MarkAction(_message);
        }
        if (GUILayout.Button(new GUIContent("Open Steam Profile", "Open this exact Steam account in the Steam overlay for identity verification."), _buttonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.OpenSteamProfileFromQolMenu(selectedRow.SteamId) : "Steam profile lookup is not ready yet.";
            MarkAction(_message);
        }
        if (Plugin.CanManageHelpers())
        {
            GUI.enabled = !isHostRow;
            string helperLabel = isHelperRow ? "Remove Helper" : "Add Helper";
            if (GUILayout.Button(new GUIContent(helperLabel, "Manage your persistent host-owned Helper list. Exact SteamIDs keep access stable across lobbies you recreate."), _buttonStyle, GUILayout.Height(32f)))
            {
                _message = Plugin.Instance != null ? Plugin.Instance.UpdateHelperFromQolMenu(selectedRow.SteamId, !isHelperRow) : "Helper controls are not ready yet.";
                MarkAction(_message);
            }
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button(new GUIContent("Copy Identity Evidence", "Copy one note with the shown name, the name with its style codes, the Steam name, the exact Steam ID, the player's row and PlayerID, and their role. Nothing is sent to chat."), _buttonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.CopyIdentityEvidenceFromQolMenu(selectedRow.SteamId) : "Identity evidence copy is not ready yet.";
            MarkAction(_message);
        }
        bool hasIncidentFlags = selectedRow.Role.Contains("VICTIM") || selectedRow.Role.Contains("SUSPECT");
        if (hasIncidentFlags && GUILayout.Button(new GUIContent(
                "Clear This Player's Incident Flags Locally",
                "Dismiss every open Clone Shield incident involving this exact verified row on this client. Lifecycle evidence remains in the local audit; no ban, Helper, or lobby state changes."),
                _buttonStyle, GUILayout.Height(32f)))
        {
            CloneIncidentActionController.TryDismissOpenForVerifiedPlayer(selectedRow.SteamId, out _message);
            MarkAction(_message);
        }
        bool isProtectedRole = isSelfRow || isHostRow || isHelperRow || isProtectedVictim;
        ModerationConfirmationReadiness confirmation = Plugin.Instance != null
            ? Plugin.Instance.GetModerationConfirmationReadiness("ban", selectedRow.SteamId)
            : new ModerationConfirmationReadiness(false, 0);
        HostModerationSurfaceDecision moderationSurface =
            HostModerationSurfacePolicy.Evaluate(
                isHost,
                exactTargetReady: true,
                isSelf: isSelfRow,
                confirmationArmed: confirmation.IsReady);
        string banLabel = isProtectedVictim ? "Protected Victim" : isSelfRow ? "Protected Self" : isHostRow ? "Protected Host" : isHelperRow ? "Protected Helper" : "Preview SID Ban";
        string banTooltip = isProtectedRole
            ? "This verified role is protected from BlueSage ban previews."
            : "Create a safe preview only. The exact target is revalidated and still requires a separate confirmation.";
        GUI.enabled = !isProtectedRole &&
                      (!isHost || moderationSurface.CanArmHostRemove);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent(banLabel, banTooltip), _experimentalButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.PreviewSidBanFromQolMenu(selectedRow.SteamId) : "SID preview is not ready yet.";
            MarkActionQuiet(_message);
        }
        GUI.enabled = !isProtectedRole && confirmation.IsReady &&
                      (!isHost || moderationSurface.CanExecuteHostRemove);
        string confirmLabel = confirmation.IsReady ? $"Confirm Exact SID Ban  •  {confirmation.SecondsRemaining}s" : "Preview Required";
        if (GUILayout.Button(new GUIContent(confirmLabel, confirmation.IsReady
                ? "Confirm the matching exact-player preview before its visible countdown expires."
                : "Create a matching SID-ban preview first; confirmation stays disabled until then."),
                confirmation.IsReady ? _enabledButtonStyle : _disabledButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ConfirmSidBanFromQolMenu(selectedRow.SteamId) : "SID confirmation is not ready yet.";
            MarkAction(_message);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        // Round 4 Stage E #3: kick (ban, wait until gone, unban) and lobby-wide mute, same preview + confirm gating as the ban row.
        DrawSelectedRowLiveAction("kick", "Preview Kick", "Confirm Kick", "Kick = ban through the game's own flow, wait until the player is gone (max 20 s), then unban. If they are still here at 20 s the ban stays and you are told.", isProtectedRole, isHost, moderationSurface, selectedRow.SteamId);
        DrawSelectedRowLiveAction("mute", "Preview Mute", "Confirm Mute", "Lobby-wide mute: the host stops relaying this player's chat to everyone for this hosting session, including you. Their own messages stop appearing for them too, so they will notice. Voice is unaffected.", isProtectedRole, isHost, moderationSurface, selectedRow.SteamId);
        GUILayout.Label("Your own row is marked SELF and automatically protected; a host never adds itself as a Helper. A ban preview never acts immediately. Recheck the profile, then use the explicit confirmation shown locally. Protected victims, host, Helpers, self, and ambiguous rows are blocked.", _smallStyle);
        GUILayout.EndVertical();
    }

    // Round 4 Stage E #3: one preview + confirm row for a live action (kick or mute) on the selected verified player.
    private void DrawSelectedRowLiveAction(string action, string previewLabel, string confirmLabelBase, string explanation, bool isProtectedRole, bool isHost, HostModerationSurfaceDecision moderationSurface, string steamId)
    {
        ModerationConfirmationReadiness confirmation = Plugin.Instance != null
            ? Plugin.Instance.GetModerationConfirmationReadiness(action, steamId)
            : new ModerationConfirmationReadiness(false, 0);
        GUILayout.Space(4f);
        GUILayout.Label(explanation, _smallStyle);
        GUI.enabled = !isProtectedRole && (!isHost || moderationSurface.CanArmHostRemove);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent(isProtectedRole ? "Protected" : previewLabel, "Create a safe preview only. The exact live row is revalidated and still requires a separate confirmation."), _experimentalButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.PreviewSidLiveActionFromQolMenu(action, steamId) : "SID preview is not ready yet.";
            MarkActionQuiet(_message);
        }

        GUI.enabled = !isProtectedRole && confirmation.IsReady && (!isHost || moderationSurface.CanExecuteHostRemove);
        string confirmLabel = confirmation.IsReady ? $"{confirmLabelBase}  •  {confirmation.SecondsRemaining}s" : "Preview Required";
        if (GUILayout.Button(new GUIContent(confirmLabel, confirmation.IsReady
                ? "Confirm the matching exact-player preview before its visible countdown expires."
                : "Create a matching preview first; confirmation stays disabled until then."),
                confirmation.IsReady ? _enabledButtonStyle : _disabledButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ConfirmSidLiveActionFromQolMenu(action, steamId) : "SID confirmation is not ready yet.";
            MarkAction(_message);
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void DrawNativeBanRecords()
    {
        bool recordsAvailable = SteamIdModerationController.TryGetNativeBanRecords(out IReadOnlyList<NativeBanRecord> records, out string readError);
        const int pageSize = 8;
        int pageCount = Math.Max(1, (records.Count + pageSize - 1) / pageSize);
        _nativeBanPage = Mathf.Clamp(_nativeBanPage, 0, pageCount - 1);

        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"The game's ban list • {records.Count} saved • page {_nativeBanPage + 1}/{pageCount}", _sectionHeaderStyle);
        GUILayout.FlexibleSpace();
        GUI.enabled = _nativeBanPage > 0;
        if (GUILayout.Button(new GUIContent("◀ Previous", "Show the previous eight host-native ban records."), _buttonStyle, GUILayout.Width(104f), GUILayout.Height(28f)))
        {
            _nativeBanPage--;
            _selectedBannedSteamId = string.Empty;
        }
        GUI.enabled = _nativeBanPage + 1 < pageCount;
        if (GUILayout.Button(new GUIContent("Next ▶", "Show the next eight host-native ban records."), _buttonStyle, GUILayout.Width(104f), GUILayout.Height(28f)))
        {
            _nativeBanPage++;
            _selectedBannedSteamId = string.Empty;
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label("Host-only view of the game's paired saved nickname and SteamID64 records. Select a row to copy its exact ID or create a separate unban preview. Click the selected row again, or use Clear Selection, to disarm it.", _smallStyle);

        if (!recordsAvailable)
        {
            _selectedBannedSteamId = string.Empty;
            GUILayout.Label("The game's ban list can't be read right now (" + readError + "). Nothing is shown and no unban is offered until it can.", _smallStyle);
            GUILayout.EndVertical();
            return;
        }

        if (records.Count == 0)
        {
            _selectedBannedSteamId = string.Empty;
            GUILayout.Label("The game's native banned-player list is empty.", _smallStyle);
            GUILayout.EndVertical();
            return;
        }

        int start = _nativeBanPage * pageSize;
        int end = Math.Min(records.Count, start + pageSize);
        for (int index = start; index < end; index++)
        {
            NativeBanRecord record = records[index];
            bool isSelected = string.Equals(_selectedBannedSteamId, record.SteamId, StringComparison.Ordinal);
            string label = $"#{index + 1:00}  {record.DisplayName}  •  …{PlayerIdentityEvidenceController.Suffix(record.SteamId)}";
            string rowTooltip = isSelected
                ? "Selected. Click again to clear this selection."
                : "Select this exact paired native ban record.";
            if (GUILayout.Button(new GUIContent(label, rowTooltip), isSelected ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
            {
                _selectedBannedSteamId = isSelected ? string.Empty : record.SteamId;
                MarkActionQuiet(isSelected
                    ? "Cleared the selected native ban."
                    : "Selected native ban for " + record.DisplayName + " (Steam …" + PlayerIdentityEvidenceController.Suffix(record.SteamId) + ").");
            }
        }

        NativeBanRecord selected = records.FirstOrDefault(record => string.Equals(record.SteamId, _selectedBannedSteamId, StringComparison.Ordinal));
        if (selected == null)
        {
            _selectedBannedSteamId = string.Empty;
            GUILayout.Space(8f);
            GUILayout.Label("No saved ban is selected. Click the exact row you want to look at; BlueSage never picks or switches it for you.", _smallStyle);
            GUILayout.EndVertical();
            return;
        }

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Selected native ban: " + selected.DisplayName + "  •  SteamID64 " + selected.SteamId, _labelStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("Clear Selection", "Deselect this native ban and disarm every selection-scoped unban control."), _buttonStyle, GUILayout.Width(130f), GUILayout.Height(28f)))
        {
            _selectedBannedSteamId = string.Empty;
            MarkActionQuiet("Cleared the selected native ban.");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return;
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Copy Banned SteamID64", "Copy this exact saved native-ban Steam ID."), _buttonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.CopySavedBanSteamIdFromQolMenu(selected.SteamId) : "Copy is not ready yet.";
            MarkAction(_message);
        }
        if (GUILayout.Button(new GUIContent("Preview SID Unban", "Create a safe unban preview only. A separate exact confirmation is still required."), _experimentalButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.PreviewSidUnbanFromQolMenu(selected.SteamId) : "SID unban preview is not ready yet.";
            MarkActionQuiet(_message);
        }
        ModerationConfirmationReadiness confirmation = Plugin.Instance != null
            ? Plugin.Instance.GetModerationConfirmationReadiness("unban", selected.SteamId)
            : new ModerationConfirmationReadiness(false, 0);
        GUI.enabled = confirmation.IsReady;
        string confirmLabel = confirmation.IsReady ? $"Confirm SID Unban  •  {confirmation.SecondsRemaining}s" : "Preview Required";
        if (GUILayout.Button(new GUIContent(confirmLabel, confirmation.IsReady
                ? "Confirm the matching saved-ban preview before its visible countdown expires."
                : "Create a matching SID-unban preview first; confirmation stays disabled until then."),
                confirmation.IsReady ? _enabledButtonStyle : _disabledButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ConfirmSidUnbanFromQolMenu(selected.SteamId) : "SID unban confirmation is not ready yet.";
            MarkAction(_message);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.Label("Preview never removes a ban. Recheck the saved nickname and SteamID64, then use the exact confirmation shown locally within 30 seconds.", _smallStyle);
        GUILayout.EndVertical();
    }

    private void ClearLobbySafetyTransientState()
    {
        _selectedSafetySteamId = string.Empty;
        _selectedBannedSteamId = string.Empty;
        _safetyRosterPage = 0;
        _nativeBanPage = 0;
        _offlineBanSteamIdText = string.Empty;
        _showClosedIncidentHistory = false;
    }

    private void DrawOfflineBanEntry()
    {
        GUILayout.BeginVertical(_identitySectionBoxStyle);
        GUILayout.Label("Ban a Steam ID that isn't in the lobby", _sectionHeaderStyle);
        GUILayout.Label("Host only. Use this when another trusted host gives you an exact SteamID64 for advance protection. BlueSage saves the game's native ban entry without pretending the absent player was kicked.", _smallStyle);
        GUILayout.Label("Always open and verify the Steam profile first. Reports and names never auto-fill or authorize this field.", _smallStyle);

        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("SteamID64", "Paste the exact 17-digit SteamID64. Display names, profile names, suffixes, and URLs are intentionally refused here."), _labelStyle, GUILayout.Width(100f));
        string next = GUILayout.TextField(_offlineBanSteamIdText ?? string.Empty, 17, _textFieldStyle, GUILayout.Height(30f));
        if (!string.Equals(next, _offlineBanSteamIdText, StringComparison.Ordinal))
        {
            _offlineBanSteamIdText = next;
            MarkDraft("Offline SteamID64 changed. Verify its profile, then preview it.");
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Open Steam Profile", "Open this exact SteamID64 in the Steam overlay before previewing a ban."), _buttonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.OpenOfflineSteamProfileFromQolMenu(_offlineBanSteamIdText) : "Steam profile lookup is not ready yet.";
            MarkAction(_message);
        }
        if (GUILayout.Button(new GUIContent("Preview Offline SID Ban", "Create a 30-second host-only preview. This does not save or kick anything."), _experimentalButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.PreviewSidBanFromQolMenu(_offlineBanSteamIdText) : "SID preview is not ready yet.";
            MarkActionQuiet(_message);
        }
        ModerationConfirmationReadiness confirmation = Plugin.Instance != null
            ? Plugin.Instance.GetModerationConfirmationReadiness("ban", _offlineBanSteamIdText)
            : new ModerationConfirmationReadiness(false, 0);
        GUI.enabled = confirmation.IsReady;
        string confirmLabel = confirmation.IsReady ? $"Confirm Preview  •  {confirmation.SecondsRemaining}s" : "Preview Required";
        if (GUILayout.Button(new GUIContent(confirmLabel, confirmation.IsReady
                ? "Confirm this exact offline-SID preview before its visible countdown expires."
                : "Create a matching offline-SID preview first; confirmation stays disabled until then."),
                confirmation.IsReady ? _enabledButtonStyle : _disabledButtonStyle, GUILayout.Height(32f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ConfirmSidBanFromQolMenu(_offlineBanSteamIdText) : "SID confirmation is not ready yet.";
            MarkAction(_message);
        }
        GUI.enabled = true;
        if (GUILayout.Button(new GUIContent("Clear Field", "Clear the local draft. This does not change saved bans."), _buttonStyle, GUILayout.Width(112f), GUILayout.Height(32f)))
        {
            _offlineBanSteamIdText = string.Empty;
            MarkSaved("Cleared the offline SteamID64 draft. Saved native bans were unchanged.");
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("Every preview expires after 30 seconds or a lobby change. Saved entries appear below and can be reversed with Preview SID Unban plus confirmation.", _smallStyle);
        GUILayout.EndVertical();
    }

    private void DrawLocalAuditActions(bool showIdentityNote)
    {
        GUILayout.Label(SessionAuditController.StatusText(), _smallStyle);
        if (showIdentityNote)
            GUILayout.Label("For hosts and Helpers, the chat copy also notes each sender's Steam ID. Older sessions are zipped on this computer; nothing is uploaded.", _smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Snapshot Now", "Saves a copy of the game log right now (the newest 8 MB; the 2 newest are kept per session) and copies the snapshot's file path for you."), _buttonStyle, GUILayout.Height(30f)))
        {
            _message = SessionAuditController.SnapshotNow();
            MarkAction(_message);
        }
        if (GUILayout.Button(new GUIContent("Copy Audit Folder", "Copy the local session folder path so you can review or attach the files manually."), _buttonStyle, GUILayout.Height(30f)))
        {
            _message = SessionAuditController.CopyAuditFolder();
            MarkAction(_message);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSelfMovementTools()
    {
        GUILayout.Label($"These controls only move your own character. Save Pos stores one local spot; teleport returns you there. Noclip/fly resets off whenever BlueSage loads or unloads. Press {Plugin.NoclipFlyKey.Value} anytime to toggle fly without opening this menu.", _smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Save Pos", "Save your current local player position. It persists locally until you replace it."), _buttonStyle, GUILayout.Height(30f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.SaveSelfPositionFromQolMenu() : "Self movement is not ready yet.";
            MarkMovementResult(_message);
        }
        if (GUILayout.Button(new GUIContent("Teleport Saved", "Teleport only your own character to the position saved by Save Pos."), _buttonStyle, GUILayout.Height(30f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.TeleportSelfToSavedFromQolMenu() : "Self movement is not ready yet.";
            MarkMovementResult(_message);
        }
        string flyLabel = Plugin.ShouldApplyNoclipFly ? "Noclip / Fly: On" : "Noclip / Fly: Off";
        if (GUILayout.Button(new GUIContent(flyLabel, $"Self-only fly: WASD, Space/E up, Ctrl/Q down, Shift boost, Alt precision. Always starts off. Hotkey: {Plugin.NoclipFlyKey.Value}."), Plugin.ShouldApplyNoclipFly ? _enabledButtonStyle : _buttonStyle, GUILayout.Height(30f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ToggleNoclipFlyFromQolMenu() : "Self movement is not ready yet.";
            MarkMovementResult(_message);
        }
        GUILayout.EndHorizontal();
        DrawSelfMovementSetting("Speed x", ref _selfSpeedText, "speed", "0.25-5; 1 is the game's normal. This also scales fly speed.");
        DrawSelfMovementSetting("Jump x", ref _selfJumpText, "jump", "0.25-5; 1 is the game's normal.");
        DrawSelfMovementSetting("Gravity x", ref _selfGravityText, "gravity", "0.1-5; 1 is the game's normal.");
        if (GUILayout.Button(new GUIContent("Reset Self Movement", "Turn fly off and return speed, jump, and gravity to the game's normal 1x. Your saved position is kept."), _buttonStyle, GUILayout.Height(30f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.ResetSelfMovementFromQolMenu() : "Self movement is not ready yet.";
            HydrateSelfMovementFields();
            MarkMovementResult(_message);
        }
    }

    private void DrawSelfMovementSetting(string label, ref string value, string kind, string rangeHelp)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(label, "Self-only multiplier. " + rangeHelp), _labelStyle, GUILayout.Width(110f));
        string next = GUILayout.TextField(value ?? string.Empty, 12, _textFieldStyle, GUILayout.Height(28f));
        if (!string.Equals(next, value, StringComparison.Ordinal))
        {
            value = next;
            MarkDraft(label + " changed. Save it when ready.");
        }
        if (GUILayout.Button(new GUIContent("Save", "Save " + label + ". " + rangeHelp), _buttonStyle, GUILayout.Width(100f), GUILayout.Height(28f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.SaveSelfMovementMultiplierFromQolMenu(kind, value) : "Self movement is not ready yet.";
            HydrateSelfMovementFields();
            MarkMovementResult(_message);
        }
        if (GUILayout.Button(new GUIContent("Reset", "Return " + label + " to the game's normal 1x."), _buttonStyle, GUILayout.Width(100f), GUILayout.Height(28f)))
        {
            _message = Plugin.Instance != null ? Plugin.Instance.SaveSelfMovementMultiplierFromQolMenu(kind, "reset") : "Self movement is not ready yet.";
            HydrateSelfMovementFields();
            MarkMovementResult(_message);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawThemeSelector()
    {
        GUILayout.Label("Menu theme", _labelStyle);
        GUILayout.Label("Applies to the QoL Menu, Style Helper, and command/typeahead popups. Pick what is easiest on your eyes.", _smallStyle);
        string[] names = BlueSageUiTheme.Names;
        for (int index = 0; index < names.Length; index += 2)
        {
            GUILayout.BeginHorizontal();
            for (int offset = 0; offset < 2 && index + offset < names.Length; offset++)
            {
                string name = names[index + offset];
                bool active = string.Equals(BlueSageUiTheme.Normalize(Plugin.UiThemePreset?.Value), BlueSageUiTheme.Normalize(name), StringComparison.OrdinalIgnoreCase);
                if (GUILayout.Button(new GUIContent(active ? name + " ✓" : name, "Switch all BlueSage helper UI to " + name + "."), active ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
                {
                    SetTheme(name);
                }
            }
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button(new GUIContent("Cycle Theme", "Rotate through every available BlueSage menu theme."), _buttonStyle, GUILayout.Height(30f)))
        {
            SetTheme(BlueSageUiTheme.Next(Plugin.UiThemePreset?.Value));
        }
        if (GUILayout.Button(new GUIContent("Reset to BlueSage Harbor", "Return BlueSage helper UI to the balanced cream, coral, sage, and midnight default theme."), _buttonStyle, GUILayout.Height(30f)))
        {
            SetTheme(BlueSageUiTheme.DefaultThemeKey);
        }
    }

    private void SetTheme(string themeName)
    {
        if (Plugin.UiThemePreset == null)
        {
            _message = "Theme config is not ready yet.";
            return;
        }

        Plugin.UiThemePreset.Value = BlueSageUiTheme.Normalize(themeName);
        Plugin.Instance?.SaveConfigFromQolMenu();
        _message = "Theme set to " + BlueSageUiTheme.Current.DisplayName + ". Other BlueSage UI updates on the next draw.";
        MarkSaved("Theme saved.");
    }

    private void DrawDoNotPressButton()
    {
        GUIContent content = new GUIContent("DO NOT PRESS", "Extremely dangerous. Definitely not a harmless one-time easter egg. Probably.");
        if (GUILayout.Button(content, _buttonStyle, GUILayout.Width(168f), GUILayout.Height(28f)))
        {
            bool firstTime = !EasterEggTracker.HasDiscovered(DoNotPressEggKey);
            string eggMessage = EasterEggTracker.Discover(DoNotPressEggKey, "BlueSage lobby joke discovered");
            if (firstTime)
            {
                bool sent = Plugin.Instance != null && Plugin.Instance.TrySendDoNotPressLobbyMessage();
                _message = sent
                    ? eggMessage + ". One playful line was sent to lobby chat."
                    : eggMessage + ". Chat was unavailable, so nothing left this client.";
                MarkActionQuiet("Optional lobby joke requested.");
            }
            else
            {
                _message = "Already discovered. Repeat presses stay local and send nothing.";
                MarkActionQuiet("Optional lobby joke checked again.");
            }
        }
    }

    private void DrawFooter()
    {
        HelpFrameLayout frame = BlueSageHelpFrame.Resolve(
            BlueSageHelpFrameKind.QolMenu,
            _windowRect);
        BlueSageHelpFrame.BeginFooter(_footerBoxStyle, frame);
        string stateLine = "State: " + _stateLabel + " — " + _stateDetail;
        BlueSageHelpFrame.DrawStateAndHelp(
            _footerTextStyle,
            frame,
            stateLine,
            BuildScopedFooterTooltip());
        BlueSageHelpFrame.EndFooter();
    }

    private string BuildScopedFooterTooltip()
    {
        Vector2 mouse = Event.current != null ? Event.current.mousePosition : Vector2.zero;
        if (!BlueSageWindowHoverScope.IsWindowHovered(47058) ||
            !new Rect(0f, 0f, _windowRect.width, _windowRect.height).Contains(mouse) ||
            string.IsNullOrWhiteSpace(GUI.tooltip))
        {
            return "Tip: hover a visible QoL Menu control for exact help.";
        }

        return GUI.tooltip;
    }

    private void DrawToggle(string label, ConfigEntry<bool> entry, Action afterChange = null)
    {
        DrawToggle(label, entry, string.Empty, afterChange);
    }

    private void DrawToggle(string label, ConfigEntry<bool> entry, string tooltip, Action afterChange = null)
    {
        if (entry == null)
        {
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(label, tooltip), _labelStyle, GUILayout.Width(190f));
        if (GUILayout.Button(new GUIContent(entry.Value ? "On" : "Off", tooltip), entry.Value ? _enabledButtonStyle : _disabledButtonStyle, GUILayout.Width(88f), GUILayout.Height(30f)))
        {
            entry.Value = !entry.Value;
            Plugin.Instance?.SaveConfigFromQolMenu();
            _message = label + ": " + (entry.Value ? "on." : "off.");
            Plugin.Instance?.ShowToast(label + ": " + (entry.Value ? "On" : "Off"));
            afterChange?.Invoke();
            MarkSavedQuiet(label + " saved " + (entry.Value ? "On." : "Off."));
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSpoonsToggle()
    {
        if (Plugin.EnableSpoons == null)
        {
            return;
        }

        const string tooltip = "Shows or hides your compact [1/5sp] display-name tag. Edit the number in Style Helper > Status.";
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Spoons", tooltip), _labelStyle, GUILayout.Width(190f));
        if (GUILayout.Button(new GUIContent(Plugin.EnableSpoons.Value ? "On" : "Off", tooltip), Plugin.EnableSpoons.Value ? _enabledButtonStyle : _disabledButtonStyle, GUILayout.Width(88f), GUILayout.Height(30f)))
        {
            bool requested = !Plugin.EnableSpoons.Value;
            bool applied = false;
            _message = Plugin.Instance != null
                ? Plugin.Instance.TrySetSpoonVisibilityFromMenu(requested, out applied)
                : "Spoons could not change because the player is not ready yet.";
            if (applied)
            {
                MarkSaved("Spoons saved " + (requested ? "On." : "Off."));
            }
            else
            {
                MarkDraft("Spoons stayed unchanged; fix the reported problem and try again.");
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawTextSetting(string label, ref string value, string buttonLabel, Action saveAction, Func<string> savedValueProvider)
    {
        string tooltip = BuildTextSettingTooltip(label);
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(label, tooltip), _labelStyle, GUILayout.Width(_textSettingLabelWidth));
        string previous = value ?? string.Empty;
        string next = GUILayout.TextField(previous, 120, _textFieldStyle, GUILayout.MinWidth(130f), GUILayout.ExpandWidth(true), GUILayout.Height(28f));
        if (!string.Equals(next, previous, StringComparison.Ordinal))
        {
            MarkDraft(label + " changed. Press Save or Undo.");
        }

        value = next;
        // Sized to its own label (112px floor): a fixed 112px clipped "Save Auto/75-200" to
        // "ave Auto/75-20" (UAT F3FAB760BEB5).
        var saveContent = new GUIContent(buttonLabel, tooltip);
        float saveWidth = Mathf.Max(112f, _buttonStyle.CalcSize(saveContent).x + 8f);
        if (GUILayout.Button(saveContent, _buttonStyle, GUILayout.Width(saveWidth), GUILayout.Height(28f)))
        {
            string savedBefore = savedValueProvider != null ? savedValueProvider() : string.Empty;
            saveAction();
            string savedAfter = savedValueProvider != null ? savedValueProvider() : string.Empty;
            if (!string.Equals(savedBefore, savedAfter, StringComparison.Ordinal))
            {
                _settingUndoValues[label] = savedBefore;
            }
        }
        string undoTooltip = _settingUndoValues.ContainsKey(label)
            ? "Restore the value from before the last successful Save."
            : "Discard this unsaved edit and reload the saved value.";
        if (GUILayout.Button(new GUIContent("Undo", undoTooltip), _buttonStyle, GUILayout.Width(80f), GUILayout.Height(28f)))
        {
            if (_settingUndoValues.TryGetValue(label, out string valueBeforeLastSave))
            {
                value = valueBeforeLastSave;
                // The save inside an Undo must not toast "saved"; only "change undone" does.
                _suppressSaveToast = true;
                try
                {
                    saveAction();
                }
                finally
                {
                    _suppressSaveToast = false;
                }

                _settingUndoValues.Remove(label);
                _message = label + " restored to its previous saved value.";
                MarkSaved(label + " change undone.");
            }
            else
            {
                value = savedValueProvider != null ? savedValueProvider() : string.Empty;
                _message = label + " draft discarded; saved value restored.";
                MarkSaved(label + " draft discarded.");
            }
        }
        GUILayout.EndHorizontal();
    }

    private static string BuildTextSettingTooltip(string label)
    {
        switch (label)
        {
            case "Fishing Aid bite reaction (seconds)":
                return "How long the fishing aid waits before answering a bite. Slower looks more natural; 0.7 is the default.";
            case "Toast reading time (seconds)":
                return "How long a toast stays fully visible before it fades. Raise it if toasts vanish before you finish reading. Default 3.5.";
            case "Toast size":
                return "Auto grows toasts with your screen resolution (bigger on 1440p and 4K, never smaller than normal on laptops). Or pick 75-200 percent.";
            case "Fishing Aid auto release after catch (seconds)":
                return "How long a catch stays on screen before the fishing aid presses Release (or Recycle for trash) so it can cast again sooner. Default 3.5.";
            case "Fishing Aid last-round clicks per second":
                return "How fast the fishing aid clicks in the reel mini-game's last round. Raise it if hard fish escape.";
            case "Lobby welcome message (MOTD)":
                return "Hosts: the short welcome QoL players see as a toast and chat line when they join your lobby. Leave empty for \"Welcome to my lobby!\". Also /motd.";
            case "Fly Alt precision (% of fly speed)":
                return "How fast you fly while holding Alt, as a percent of normal fly speed. Lower is finer for careful positioning.";
            case "Ping sound mode":
                return "Use click, change, error, task, or ticket. Save plays a preview.";
            case "Ping highlight color":
                return "Color behind the whole pinged message. Use hex like FFD700.";
            case "Ping @mention color":
                return "Color for the @name or @lobby token. Default is ping-yellow.";
            case "Chat outline intensity":
                return "Percent from 25 to 100. Default 75 is a stronger readable outline; 100 is crispest.";
            case "Whole chat UI scale":
                return "Percent from 75 to 200. Default 100 keeps the game's size; larger values scale the whole chat panel, input, backdrop, visible text, and outlines.";
            case "Chat font size":
                return "Percent from 75 to 200. Default 100 keeps the game's text size; larger values enlarge only message text without scaling the chat shell.";
            case "Chat window height":
                return "Percent from 100 to 200. Default 100 keeps the game's height; larger values add vertical chat space and deeper visible scrollback without scaling text.";
            case "Normal outline color":
                return "Hex outline color for normal chat text. Default is black: 000000.";
            case "Black text outline color":
                return "Hex outline color for true-black styled chat/name text. Default is light grey: C0C0C0.";
            case "Spoons 0-5":
                return "Set your optional spoon tag from 0 to 5. Use /spoons off or Hide Spoon Tag to remove it from your name.";
            case "Chat text limit":
            case "Name/ID limit":
            case "Room name limit":
                return "Default is 4000, the maximum. Lower it if very long text wraps or clips; past 3000 can look messy.";
            case "Retained chat rows":
                return "25-250 rows. Default 150 balances useful scrollback with busy-lobby UI cost; BlueSage never lowers a higher limit set by the game or another mod.";
            default:
                return "Edit this safe local config value, then Save.";
        }
    }

    private void HydrateFields()
    {
        _sweepIntervalText = Plugin.SweepIntervalMinutes?.Value.ToString() ?? SweepSettingsPolicy.DefaultIntervalMinutes.ToString();
        _reconnectRejoinHoldText = Plugin.ReconnectRejoinHoldSeconds?.Value.ToString() ?? "20";
        _flyPrecisionText = Plugin.FlyPrecisionPercent?.Value.ToString() ?? FlyCameraPolicy.DefaultPrecisionPercent.ToString();
        _hostMotdText = Plugin.HostMotd?.Value ?? string.Empty;
        _fishingAidReactionText = FormatAidNumber(Plugin.FishingAidReactionSeconds?.Value ?? FishingAidPolicy.DefaultReactionSeconds);
        _fishingAidClicksText = FormatAidNumber(Plugin.FishingAidClicksPerSecond?.Value ?? FishingAidPolicy.DefaultClicksPerSecond);
        _fishingAidReleaseText = FormatAidNumber(Plugin.FishingAidReleaseSeconds?.Value ?? FishingAidPolicy.DefaultReleaseSeconds);
        _pingHighlightText = Plugin.PingHighlightColor?.Value ?? "9B59B6";
        _pingMentionText = Plugin.PingMentionColor?.Value ?? "FFD700";
        _pingSoundModeText = Plugin.GetPingSoundMode();
        _chatOutlineIntensityText = Plugin.LockedChatOutlineIntensityPercent.ToString();
        _chatUiScaleText = Plugin.LockedChatUiScalePercent.ToString();
        _menuUiScaleText = Plugin.MenuUiScalePercent?.Value == 0 ? "Auto" : Plugin.MenuUiScalePercent?.Value.ToString() ?? "Auto";
        _toastHoldText = Plugin.LockedToastHoldSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        _toastScaleText = (Plugin.ToastScalePercent?.Value ?? 0) == 0 ? "Auto" : Plugin.ToastScalePercent.Value.ToString();
        _chatFontSizeText = Plugin.LockedChatFontSizePercent.ToString();
        _chatWindowHeightText = Plugin.LockedChatWindowHeightPercent.ToString();
        _chatHistoryRowsText = Plugin.LockedChatHistoryRows.ToString();
        _localChatRangeText = Plugin.LockedLocalChatRangeMeters.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        _chatOutlineColorText = Plugin.LockedChatOutlineColorHex;
        _blackChatOutlineColorText = Plugin.LockedBlackChatOutlineColorHex;
        _chatLimitText = Plugin.LockedMaxChatCharacters.ToString();
        _profileLimitText = Plugin.LockedMaxIdCardCharacters.ToString();
        _sessionLimitText = Plugin.LockedMaxSessionNameCharacters.ToString();
        HydrateSelfMovementFields();
        MarkSavedQuiet("Loaded saved Customize values.");
    }

    private void HydrateSelfMovementFields()
    {
        _selfSpeedText = Plugin.LockedSelfSpeedMultiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        _selfJumpText = Plugin.LockedSelfJumpMultiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        _selfGravityText = Plugin.LockedSelfGravityMultiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private string BuildStatusLine()
    {
        string hostOnly = Plugin.CanManageHelpers() ? $", Helpers={Plugin.LockedLobbyHelperCount}" : string.Empty;
        return $"AutomaticCleanup={OnOff(Plugin.EnableAutoSweep)}, HostHealth={OnOff(Plugin.EnableHostHealthMonitor)}, LeaveNotices={OnOff(Plugin.EnableLeaveNotifications)}, Welcome={OnOff(Plugin.EnableWelcomeMessage)}, Timestamps={OnOff(Plugin.EnableChatTimestamps)}/{(Plugin.Use24HourTime?.Value == false ? "12hr" : "24hr")}, ChatReadability={OnOff(Plugin.EnableChatReadability)}, ChatBackdrop={OnOff(Plugin.EnablePersistentChatBackdrop)}, ChatOutline={Plugin.LockedChatOutlineIntensityPercent}%, ChatUIScale={Plugin.LockedChatUiScalePercent}%, ChatFont={Plugin.LockedChatFontSizePercent}%, ChatWindowHeight={Plugin.LockedChatWindowHeightPercent}%, ChatRows={Plugin.LockedChatHistoryRows}, OutlineColors=#{Plugin.LockedChatOutlineColorHex}/#{Plugin.LockedBlackChatOutlineColorHex}, BlackNamesOutline={OnOff(Plugin.BlackNamesOutline)}, BetterMove={OnOff(Plugin.EnableBetterMove)}, FocusAnywhere={OnOff(Plugin.EnableFocusAnywhere)}, PlayerPanel={OnOff(Plugin.EnableEnhancedPlayerPanel)}, ReconnectGuard={OnOff(Plugin.EnableReconnectGuard)}, ReconnectMessage={OnOff(Plugin.EnableReconnectAnnouncement)}, Ping={OnOff(Plugin.EnablePingMentions)}{hostOnly}, PingSound={OnOff(Plugin.EnablePingSound)}, SelfFly={OnOff(Plugin.EnableNoclipFly)}, SelfMove={Plugin.LockedSelfSpeedMultiplier:0.##}x/{Plugin.LockedSelfJumpMultiplier:0.##}x/{Plugin.LockedSelfGravityMultiplier:0.##}x, CloneShield={OnOff(Plugin.EnableCloneShield)}, RenderSaver=Retired, CopyChat={OnOff(Plugin.EnableChatCopy)}, ChatLinks={OnOff(Plugin.EnableChatUrlLinks)}, LocalAudit={OnOff(Plugin.EnableLocalAuditExports)}, Spoons={OnOff(Plugin.EnableSpoons)}, StyleUI={OnOff(Plugin.EnableStyleUi)}, AvatarSlots4-9={OnOff(Plugin.EnableExtendedAvatarStyles)}, Chalkboards={OnOff(Plugin.EnableChalkboardPersistence)}, MiniMapIDClick={OnOff(Plugin.EnableMiniMapPlayerLabels)}, MiniMapFriends={OnOff(Plugin.EnableMiniMapFriendColors)}, TextLimits={Plugin.LockedMaxChatCharacters}/{Plugin.LockedMaxIdCardCharacters}/{Plugin.LockedMaxSessionNameCharacters}.";
    }

    private static string OnOff(ConfigEntry<bool> entry)
    {
        return entry != null && entry.Value ? "on" : "off";
    }

    private void MarkDraft(string detail)
    {
        _stateLabel = "Draft";
        _stateDetail = detail ?? "Changes are local until you save or undo.";
    }

    // Round 5h: a saved setting or a finished action also shows as a toast (one shared rule instead of one hand-wired
    // toast per button). Switches and the few spots that raise their own toast use MarkSavedQuiet.
    private bool _suppressSaveToast;

    private void MarkSaved(string detail)
    {
        MarkSavedQuiet(detail);
        if (!_suppressSaveToast)
        {
            Plugin.Instance?.ShowToast(ToastPolicy.Summarize(detail));
        }
    }

    private void MarkSavedQuiet(string detail)
    {
        _stateLabel = "Saved";
        _stateDetail = detail ?? "Ready.";
    }

    private void MarkAction(string detail)
    {
        MarkActionQuiet(detail);
        Plugin.Instance?.ShowToast(ToastPolicy.Summarize(detail));
    }

    // Picking a row, a safe preview, a status read-out, or an action whose own code already toasts: footer only.
    private void MarkActionQuiet(string detail)
    {
        _stateLabel = "Done";
        _stateDetail = detail ?? "Done.";
    }

    private void MarkMovementResult(string detail)
    {
        string value = detail ?? string.Empty;
        bool failed = string.IsNullOrWhiteSpace(value) ||
                      value.IndexOf("not ready", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      value.IndexOf("no saved position", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      value.IndexOf("unknown multiplier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      value.IndexOf("use a number", StringComparison.OrdinalIgnoreCase) >= 0;
        if (failed)
        {
            MarkDraft(value.Length > 0 ? value : "Self movement action did not run.");
            return;
        }

        MarkSaved(value);
    }

    private void SaveReconnectRejoinHold()
    {
        if (!int.TryParse(_reconnectRejoinHoldText, out int value))
        {
            _message = "Reconnect wait must be a number from 10 to 120 seconds.";
            MarkDraft("Fix reconnect wait to a number from 10 to 120.");
            return;
        }

        int clamped = ReconnectRejoinHoldPolicy.ClampHoldSeconds(value);
        Plugin.ReconnectRejoinHoldSeconds.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _reconnectRejoinHoldText = clamped.ToString();
        _message = "Reconnect wait saved: " + clamped + " seconds.";
        MarkSaved("Reconnect wait saved.");
    }

    private void SaveSweepInterval()
    {
        if (!int.TryParse(_sweepIntervalText, out int value))
        {
            _message = "Automatic Cleanup interval must be a number from 5 to 180.";
            MarkDraft("Set the Automatic Cleanup interval to a number from 5 to 180.");
            return;
        }

        int clamped = SweepSettingsPolicy.ClampIntervalMinutes(value);
        Plugin.SweepIntervalMinutes.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        Plugin.Instance?.ReconcileSweepScheduleFromMenu();
        _sweepIntervalText = clamped.ToString();
        _message = "Automatic Cleanup interval saved: " + clamped + " minutes.";
        MarkSaved("Automatic Cleanup interval saved.");
    }

    private void SaveHex(ConfigEntry<string> entry, string value, string label)
    {
        if (entry == null)
        {
            return;
        }

        if (!RichTextStyleBuilder.TryNormalizeHex(value, out string hex))
        {
            _message = label + ": use a hex color like FF66CC or #83E.";
            MarkDraft("Fix " + label + " to a valid hex color.");
            return;
        }

        entry.Value = hex;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _message = label + " saved: #" + hex + ".";
        MarkSaved(label + " saved.");
    }

    private void SaveText(ConfigEntry<string> entry, string value, string label)
    {
        if (entry == null)
        {
            return;
        }

        entry.Value = (value ?? string.Empty).Trim();
        Plugin.Instance?.SaveConfigFromQolMenu();
        _message = label + " saved.";
        MarkSaved(label + " saved.");
    }

    private void SaveCharacterLimit(ConfigEntry<int> entry, string value, string label, Func<int, int> clamp, Action<int> hydrate)
    {
        if (entry == null)
        {
            return;
        }

        if (!int.TryParse(value, out int requested))
        {
            _message = label + ": enter a number from 1 to 4000. Default is 4000.";
            MarkDraft("Fix " + label + " to a number from 1 to 4000.");
            return;
        }

        int clamped = clamp(requested);
        entry.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        hydrate(clamped);
        _message = clamped > InputFieldLimitPolicy.ChatWrapWarningCharacters
            ? label + " saved at " + clamped + ". Heads up: past " + InputFieldLimitPolicy.ChatWrapWarningCharacters + " may wrap, clip, or look visually messy."
            : label + " saved at " + clamped + ".";
        MarkSaved(label + " saved.");
    }

    private void SaveOutlineIntensity()
    {
        if (Plugin.ChatOutlineIntensity == null)
        {
            return;
        }

        string raw = (_chatOutlineIntensityText ?? string.Empty).Trim().TrimEnd('%');
        if (!int.TryParse(raw, out int requested))
        {
            _message = "Chat outline intensity: enter a number from 25 to 100.";
            MarkDraft("Fix chat outline intensity to a number from 25 to 100.");
            return;
        }

        int clamped = ChatReadabilityStylePolicy.ClampOutlineIntensityPercent(requested);
        Plugin.ChatOutlineIntensity.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RefreshVisibleChat();
        _chatOutlineIntensityText = clamped.ToString();
        _message = "Chat outline intensity saved: " + clamped + "%.";
        MarkSaved("Chat outline intensity saved.");
    }

    private void SaveChatUiScale()
    {
        if (Plugin.ChatUiScalePercent == null)
        {
            return;
        }

        string raw = (_chatUiScaleText ?? string.Empty).Trim();
        if (!ChatAccessibilityScalePolicy.TryParseScaleToken(raw, out int clamped))
        {
            _message = "Whole chat UI scale: enter 75-200, decimal scale like 1.25, or reset. Use Chat font size for text-only growth.";
            MarkDraft("Fix whole chat UI scale to a number from 75 to 200.");
            return;
        }

        Plugin.ChatUiScalePercent.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
        _chatUiScaleText = clamped.ToString();
        _message = "Whole chat UI scale saved: " + clamped + "%. This grows the panel, input, backdrop, text, and outlines together.";
        MarkSaved("Whole chat UI scale saved.");
    }

    private void SaveToastHold()
    {
        if (Plugin.ToastHoldSeconds == null)
        {
            return;
        }

        if (!float.TryParse((_toastHoldText ?? string.Empty).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            _message = "Toast reading time must be a number from 2 to 15 seconds.";
            MarkDraft("Fix toast reading time to a number from 2 to 15.");
            return;
        }

        Plugin.ToastHoldSeconds.Value = ToastPolicy.ClampHoldSeconds(value);
        Plugin.Instance?.SaveConfigFromQolMenu();
        _toastHoldText = Plugin.LockedToastHoldSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        _message = "Toast reading time saved: " + _toastHoldText + " seconds.";
        Plugin.Instance?.ShowToast("Toasts now stay for " + _toastHoldText + " seconds.");
        MarkSavedQuiet("Toast reading time saved.");
    }

    private void SaveToastScale()
    {
        if (Plugin.ToastScalePercent == null)
        {
            return;
        }

        string raw = (_toastScaleText ?? string.Empty).Trim();
        int requested;
        if (string.Equals(raw, "auto", StringComparison.OrdinalIgnoreCase) || raw == "0")
        {
            requested = ToastPolicy.AutoScalePercent;
        }
        else if (!int.TryParse(raw.TrimEnd('%'), out requested))
        {
            _message = "Toast size: use Auto or a number from 75 to 200.";
            MarkDraft("Fix toast size to Auto or 75-200.");
            return;
        }

        Plugin.ToastScalePercent.Value = requested == 0 ? 0 : Mathf.Clamp(requested, ToastPolicy.MinScalePercent, ToastPolicy.MaxScalePercent);
        Plugin.Instance?.SaveConfigFromQolMenu();
        _toastScaleText = Plugin.ToastScalePercent.Value == 0 ? "Auto" : Plugin.ToastScalePercent.Value.ToString();
        _message = "Toast size saved: " + _toastScaleText + ".";
        Plugin.Instance?.ShowToast("Toast size: " + _toastScaleText);
        MarkSavedQuiet("Toast size saved.");
    }

    private void SaveMenuUiScale()
    {
        if (Plugin.MenuUiScalePercent == null)
        {
            return;
        }

        string raw = (_menuUiScaleText ?? string.Empty).Trim();
        int requested;
        if (string.Equals(raw, "auto", StringComparison.OrdinalIgnoreCase) || raw == "0")
        {
            requested = MenuDisplayScalePolicy.AutoScalePercent;
        }
        else if (!int.TryParse(raw.TrimEnd('%'), out requested))
        {
            _message = "BlueSage menu scale: use Auto or a number from 75 to 200.";
            MarkDraft("Fix menu scale to Auto or 75-200.");
            return;
        }

        MenuDisplayScaleResult resolved = MenuDisplayScalePolicy.Resolve(requested, Screen.dpi);
        Plugin.MenuUiScalePercent.Value = requested == 0
            ? MenuDisplayScalePolicy.AutoScalePercent
            : resolved.Percent;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _menuUiScaleText = Plugin.MenuUiScalePercent.Value == 0
            ? "Auto"
            : Plugin.MenuUiScalePercent.Value.ToString();
        _message = "BlueSage menu scale saved: " + resolved.Percent + "% (" + resolved.Source + ").";
        MarkSaved("Menu scale saved; chat scale is unchanged.");
        ClampWindowToScreen();
    }

    private void SaveChatFontSize()
    {
        if (Plugin.ChatFontSizePercent == null)
        {
            return;
        }

        string raw = (_chatFontSizeText ?? string.Empty).Trim();
        if (!ChatFontSizePolicy.TryParseFontSizeToken(raw, out int clamped))
        {
            _message = "Chat font size: enter 75-200, decimal size like 1.25, on/off, or reset. This changes message text only.";
            MarkDraft("Fix chat font size to a number from 75 to 200.");
            return;
        }

        Plugin.ChatFontSizePercent.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
        _chatFontSizeText = clamped.ToString();
        _message = "Chat font size saved: " + clamped + "%. Message text grows without scaling the chat panel.";
        MarkSaved("Chat font size saved.");
    }

    private void SaveChatWindowHeight()
    {
        if (Plugin.ChatWindowHeightPercent == null)
        {
            return;
        }

        string raw = (_chatWindowHeightText ?? string.Empty).Trim();
        if (!ChatWindowHeightPolicy.TryParseHeightToken(raw, out int clamped))
        {
            _message = "Chat window height: enter 100-200, decimal size like 1.5, or reset. This adds vertical log space only.";
            MarkDraft("Fix chat window height to a number from 100 to 200.");
            return;
        }

        Plugin.ChatWindowHeightPercent.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        BlueSage.QoLTweaks.Patches.ChatReadabilityPatch.RequestRefreshSoon();
        _chatWindowHeightText = clamped.ToString();
        _message = "Chat window height saved: " + clamped + "%. Drag the game's chat handle if you want to move it.";
        MarkSaved("Chat window height saved.");
    }

    private void SaveChatHistoryRows()
    {
        if (Plugin.ChatHistoryRows == null)
        {
            return;
        }

        string raw = (_chatHistoryRowsText ?? string.Empty).Trim();
        if (string.Equals(raw, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "default", StringComparison.OrdinalIgnoreCase))
        {
            raw = ChatHistoryLimitPolicy.DefaultRows.ToString();
        }

        if (!int.TryParse(raw, out int requested))
        {
            _message = "Retained chat rows: enter 25-250 or reset. Default 150 is recommended for busy lobbies.";
            MarkDraft("Fix retained chat rows to a number from 25 to 250.");
            return;
        }

        int clamped = ChatHistoryLimitPolicy.ClampRows(requested);
        Plugin.ChatHistoryRows.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _chatHistoryRowsText = clamped.ToString();
        _message = "Retained chat rows saved: " + clamped + ". Higher values use a little more memory; a higher limit set by the game or another mod is never lowered.";
        MarkSaved("Retained chat rows saved.");
    }

    private void SaveLocalChatRange()
    {
        if (Plugin.LocalChatRangeMeters == null)
        {
            return;
        }

        string raw = (_localChatRangeText ?? string.Empty).Trim();
        if (string.Equals(raw, "reset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "default", StringComparison.OrdinalIgnoreCase))
        {
            raw = LocalChatRangePolicy.DefaultMeters.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float requested) ||
            float.IsNaN(requested) || float.IsInfinity(requested))
        {
            _message = "Local chat range: enter 5-25 meters or reset. Default 15; 5 is the game's own range.";
            MarkDraft("Fix local chat range to a number from 5 to 25.");
            return;
        }

        float clamped = LocalChatRangePolicy.ClampMeters(requested);
        Plugin.LocalChatRangeMeters.Value = clamped;
        Plugin.Instance?.SaveConfigFromQolMenu();
        _localChatRangeText = clamped.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        _message = "Local chat range saved: " + _localChatRangeText + " m. This only changes which nearby lines you see.";
        MarkSaved("Local chat range saved.");
    }

    private void SavePingSoundMode()
    {
        string value = (_pingSoundModeText ?? string.Empty).Trim().ToLowerInvariant();
        switch (value)
        {
            case "click":
            case "change":
            case "error":
            case "task":
            case "ticket":
                Plugin.PingSoundMode.Value = value;
                Plugin.EnablePingSound.Value = true;
                Plugin.Instance?.SaveConfigFromQolMenu();
                _pingSoundModeText = value;
                MentionPingController.PlayConfiguredPing();
                _message = "Ping sound mode saved: " + value + ".";
                MarkSaved("Ping sound mode saved.");
                return;
            default:
                _message = "Ping sound mode: use click, change, error, task, or ticket.";
                MarkDraft("Fix ping sound mode to click, change, error, task, or ticket.");
                return;
        }
    }

    private void ClampWindowToScreen()
    {
        WindowFitResult fit = BlueSageWindowCoordinator.ResolveForFrame(
            BlueSagePublicWindow.QolMenu,
            _windowRect,
            MinWindowWidth,
            MinWindowHeight,
            MenuDisplayScaleRuntime.LogicalScreenWidth,
            MenuDisplayScaleRuntime.LogicalScreenHeight);
        _windowRect = new Rect(fit.X, fit.Y, fit.Width, fit.Height);
    }

    private void OnDisable()
    {
        BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.QolMenu, false);
        BlueSageWindowHoverScope.UnregisterWindow(47058);
    }

    private void DrawResizeGrip()
    {
        Rect grip = new Rect(_windowRect.width - ResizeGripSize - 8f, _windowRect.height - ResizeGripSize - 8f, ResizeGripSize, ResizeGripSize);
        GUI.Box(grip, new GUIContent("↘", "Drag this larger corner handle to resize the QoL Menu."), _buttonStyle);
        Event evt = Event.current;
        if (evt == null)
        {
            return;
        }

        if (evt.type == EventType.MouseDown && grip.Contains(evt.mousePosition))
        {
            _isResizing = true;
            evt.Use();
        }
        else if (_isResizing && evt.type == EventType.MouseDrag)
        {
            _windowRect.width += evt.delta.x;
            _windowRect.height += evt.delta.y;
            ClampWindowToScreen();
            evt.Use();
        }
        else if (_isResizing && evt.rawType == EventType.MouseUp)
        {
            _isResizing = false;
            evt.Use();
        }
    }

    private void EnsureStyles()
    {
        _theme = BlueSageUiTheme.Current;
        if (_windowStyle != null &&
            string.Equals(_renderedThemeKey, _theme.Key, StringComparison.Ordinal) &&
            _renderedTextureGeneration == BlueSageUiTheme.RuntimeTextureGeneration &&
            BlueSageUiTheme.AreRuntimeTexturesAlive())
        {
            return;
        }

        _renderedThemeKey = _theme.Key;
        _windowBackgroundTexture = BlueSageUiTheme.GetSolidTexture(_theme.Window);
        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            padding = new RectOffset(16, 16, 46, 14),
            normal = { background = _windowBackgroundTexture, textColor = _theme.LabelText }
        };
        _headerBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = BlueSageUiTheme.GetSolidTexture(_theme.Header) } };
        _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = _theme.HeaderText } };
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = _theme.LabelText } };
        _smallStyle = new GUIStyle(GUI.skin.label) { richText = false, fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = Color.Lerp(_theme.SmallText, _theme.LabelText, 0.35f) } };
        _buttonStyle = BlueSageUiTheme.CreateNeutralButtonStyle(GUI.skin.button, _theme, 13);
        _activeButtonStyle = BlueSageUiTheme.CreateStateButtonStyle(_buttonStyle, _theme.ActiveAccent, _theme.FieldFocusedBorder, _theme.ActiveStateText, 13);
        _enabledButtonStyle = BlueSageUiTheme.CreateStateButtonStyle(_buttonStyle, _theme.EnabledAccent, Color.Lerp(_theme.EnabledAccent, Color.white, 0.28f), _theme.EnabledButtonText, 13);
        _disabledButtonStyle = BlueSageUiTheme.CreateStateButtonStyle(_buttonStyle, _theme.DisabledAccent, Color.Lerp(_theme.DisabledAccent, Color.white, 0.20f), _theme.DisabledButtonText, 13);
        _experimentalButtonStyle = BlueSageUiTheme.CreateStateButtonStyle(_buttonStyle, _theme.ExperimentalAccent, Color.Lerp(_theme.ExperimentalAccent, Color.black, 0.25f), _theme.ExperimentalButtonText, 13);
        _boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10), normal = { background = BlueSageUiTheme.GetSolidTexture(_theme.Panel), textColor = _theme.LabelText } };
        _sectionBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(14, 14, 12, 12),
            margin = new RectOffset(0, 0, 0, 0),
            border = new RectOffset(1, 1, 1, 1),
            normal = { background = BlueSageUiTheme.GetPanelTexture(_theme.SectionPanel, _theme.SectionBorder), textColor = _theme.LabelText }
        };
        _comfortSectionBoxStyle = CreateAccentedSectionStyle(_theme.ComfortAccent);
        _chatSectionBoxStyle = CreateAccentedSectionStyle(_theme.ChatAccent);
        _identitySectionBoxStyle = CreateAccentedSectionStyle(_theme.IdentityAccent);
        _personalizationSectionBoxStyle = CreateAccentedSectionStyle(_theme.PersonalizationAccent);
        _accessibilitySectionBoxStyle = CreateAccentedSectionStyle(_theme.AccessibilityAccent);
        _experimentalBoxStyle = new GUIStyle(_sectionBoxStyle)
        {
            normal = { background = BlueSageUiTheme.GetPanelTexture(_theme.SectionPanel, _theme.ExperimentalBorder), textColor = _theme.LabelText }
        };
        _sectionHeaderStyle = new GUIStyle(_labelStyle)
        {
            fontSize = 17,
            normal = { textColor = _theme.LabelText }
        };
        _navigationHintStyle = new GUIStyle(_smallStyle)
        {
            richText = true
        };
        _footerBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(14, 56, 10, 10),
            border = new RectOffset(1, 1, 1, 1),
            normal = { background = BlueSageUiTheme.GetPanelTexture(_theme.FooterBackground, _theme.FooterBorder), textColor = _theme.FooterText }
        };
        _footerTextStyle = new GUIStyle(GUI.skin.label)
        {
            richText = false,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = _theme.FooterText }
        };
        _textFieldStyle = BlueSageUiTheme.CreateTextInputStyle(GUI.skin.textField, _theme, 14, wordWrap: true);
        _renderedTextureGeneration = BlueSageUiTheme.RuntimeTextureGeneration;
    }

    private GUIStyle CreateAccentedSectionStyle(Color accent)
    {
        return new GUIStyle(_sectionBoxStyle)
        {
            normal = { background = BlueSageUiTheme.GetPanelTexture(_theme.SectionPanel, accent), textColor = _theme.LabelText }
        };
    }

    private static void SetAllStateTextColors(GUIStyle style, Color text)
    {
        style.normal.textColor = text;
        style.hover.textColor = text;
        style.active.textColor = text;
        style.focused.textColor = text;
        style.onNormal.textColor = text;
        style.onHover.textColor = text;
        style.onActive.textColor = text;
        style.onFocused.textColor = text;
    }

    private enum FeatureSectionTone
    {
        Comfort,
        Chat,
        Identity,
        Personalization,
        Experimental,
        Accessibility
    }
}
