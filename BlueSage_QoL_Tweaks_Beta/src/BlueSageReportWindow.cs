#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks;

internal sealed class BlueSageReportWindow : MonoBehaviour
{
    private const int WindowId = 47061;
    private const float DefaultWindowWidth = 1180f;
    private const float DefaultWindowHeight = 900f;
    private const float MinWindowWidth = 920f;
    private const float MinWindowHeight = 640f;
    private const float HeaderHeight = 42f;
    private const float FooterHeight = 72f;
    private const float ResizeGripSize = 42f;
    private const int AchievementBatchSize = 12;
    private Rect _window = new(135f, 75f, DefaultWindowWidth, DefaultWindowHeight);
    private Vector2 _scroll;
    private bool _visible;
    private bool _hasOpened;
    private bool _isResizing;
    private bool _hasPendingResize;
    private float _pendingResizeWidth;
    private float _pendingResizeHeight;
    private bool _detailsExpanded;
    private BlueSageReportFilter _filter;
    private BlueSageReportProjection? _projection;
    private readonly BlueSageReportStatusPresenter _status = new();
    private int _allocationMetricIndex;
    private string _allocationAmount = "1";
    private GUIStyle _windowStyle = null!;
    private GUIStyle _headerBoxStyle = null!;
    private GUIStyle _headerStyle = null!;
    private GUIStyle _labelStyle = null!;
    private GUIStyle _smallStyle = null!;
    private GUIStyle _buttonStyle = null!;
    private GUIStyle _activeButtonStyle = null!;
    private GUIStyle _sectionStyle = null!;
    private GUIStyle _footerStyle = null!;
    private GUIStyle _footerTextStyle = null!;
    private GUIStyle _textFieldStyle = null!;
    private Texture2D _windowBackgroundTexture = null!;
    private string _renderedThemeKey = string.Empty;
    private int _renderedTextureGeneration;
    private BlueSageUiThemePalette _theme = null!;
    private Coroutine? _refreshCoroutine;

    internal void Open()
    {
        if (!_hasOpened)
        {
            _window = GetInitialWindowRect();
            _hasOpened = true;
        }

        ClampWindowToScreen();
        _visible = true;
        RequestRefresh();
    }

    internal void CloseReport()
    {
        _visible = false;
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = null;
        }
        BlueSageWindowHoverScope.UnregisterWindow(WindowId);
    }

    private void OnGUI()
    {
        if (!_visible) return;
        EnsureStyles();
        ClampWindowToScreen();
        BlueSageWindowHoverScope.RegisterWindow(WindowId, MenuDisplayScaleRuntime.ToPhysicalRect(_window), 0);
        ImGuiTextFocusTracker.BlurOnClickOutsideWindows();
        MenuDisplayScaleRuntime.Apply(out Matrix4x4 previousMatrix);
        try
        {
            Rect guiWindowResult = GUI.Window(WindowId, _window, DrawWindow, string.Empty, _windowStyle);
            ImGuiTextFocusTracker.ReportWindowDrawn();
            WindowFitResult mergedWindow = ReportWindowResizePolicy.MergeGuiWindowResult(
                new WindowFitResult(
                    guiWindowResult.x,
                    guiWindowResult.y,
                    guiWindowResult.width,
                    guiWindowResult.height),
                _hasPendingResize,
                _pendingResizeWidth,
                _pendingResizeHeight);
            _hasPendingResize = false;
            _window = new Rect(
                mergedWindow.X,
                mergedWindow.Y,
                mergedWindow.Width,
                mergedWindow.Height);
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
        GUI.tooltip = string.Empty;
        GUI.DrawTexture(new Rect(0f, 0f, _window.width, _window.height), _windowBackgroundTexture);
        DrawHeader();
        GUILayout.Space(8f);
        DrawFilters();
        GUILayout.Space(8f);
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar,
            GUILayout.Height(GetContentScrollHeight()));
        DrawReportContent();
        GUILayout.EndScrollView();
        GUILayout.FlexibleSpace();
        DrawFooter();
        DrawResizeGrip();
        GUI.DragWindow(new Rect(0f, 0f, Math.Max(1f, _window.width - 150f), HeaderHeight));
    }

    private void DrawHeader()
    {
        GUI.Box(new Rect(0f, 0f, _window.width, HeaderHeight), GUIContent.none, _headerBoxStyle);
        GUI.Label(new Rect(16f, 8f, Math.Max(1f, _window.width - 210f), 26f), "▣ BlueSage Report", _headerStyle);
        if (GUI.Button(new Rect(_window.width - 184f, 6f, 104f, 30f), "Refresh", _buttonStyle)) RequestRefresh();
        if (GUI.Button(new Rect(_window.width - 72f, 6f, 56f, 30f), "Close", _buttonStyle)) Plugin.CloseBlueSageReport();
    }

    private void DrawFilters()
    {
        GUILayout.BeginHorizontal();
        DrawFilter(BlueSageReportFilter.All, "ALL");
        DrawFilter(BlueSageReportFilter.Work, "WORK");
        DrawFilter(BlueSageReportFilter.Relax, "RELAX");
        DrawFilter(BlueSageReportFilter.Unclassified, "UNCLASSIFIED");
        GUILayout.EndHorizontal();
    }

    private void DrawReportContent()
    {
        if (_projection == null)
        {
            GUILayout.BeginVertical(_sectionStyle);
            GUILayout.Label(_status.Text, _smallStyle);
            GUILayout.EndVertical();
        }
        else
        {
            BlueSageReportView view = _projection.Select(_filter);
            DrawMetricSection("Overview", view, "TotalFocusDuration", "TotalSessionCount", "TotalCompleteSessions",
                "LongestStreak", "ToDoCompleted", "SecondsPlayed");
            DrawMetricSection("Activity Snapshot", view, "TodayFocusDuration", "TodaySessionCount", "WeekFocusDuration",
                "WeekFocusCount", "MonthFocusDuration", "MonthFocusCount", "AverageDailyFocus", "CurrentStreak");
            DrawMetricSection("Detailed Analysis", view, "FocusSessionsPerDay", "ArtFocusCount", "ConnectFocusCount",
                "EatFocusCount", "ExerciseFocusCount", "ListenFocusCount", "MeditateFocusCount", "ReadFocusCount",
                "WriteFocusCount", "ChoreFocusCount", "PlayFocusCount", "ArtFocusDuration", "ConnectFocusDuration",
                "EatFocusDuration", "ExerciseFocusDuration", "ListenFocusDuration", "MeditateFocusDuration",
                "ReadFocusDuration", "WriteFocusDuration", "ChoreFocusDuration", "PlayFocusDuration", "SyncedFocus",
                "DailyMaxFocus", "TotalDayOfFocus", "DayOfYear", "CurrentWeek", "CurrentMonth");
            DrawMood(view);
            DrawAchievements(view);
            DrawHistory();
        }
    }

    private void DrawFilter(BlueSageReportFilter filter, string label)
    {
        bool selected = _filter == filter;
        if (GUILayout.Toggle(selected, label, selected ? _activeButtonStyle : _buttonStyle) && !selected) _filter = filter;
    }

    private void DrawMetricSection(string title, BlueSageReportView view, params string[] keys)
    {
        GUILayout.Space(8f);
        GUILayout.BeginVertical(_sectionStyle);
        GUILayout.Label(title, _labelStyle);
        foreach (string key in keys)
        {
            BlueSageReportMetric metric = view.Get(key);
            string value = metric.WeekdayValues.Count > 0
                ? string.Join("  ", metric.WeekdayValues.Select(number => number.ToString("0.##", CultureInfo.InvariantCulture)))
                : metric.Value.ToString("0.##", CultureInfo.InvariantCulture);
            GUILayout.Label(BlueSageMetricPresentation.FriendlyLabel(key) + ": " + value + " " + metric.UnitLabel +
                            (string.IsNullOrEmpty(metric.Qualifier) ? string.Empty : "  -  " + metric.Qualifier), _smallStyle);
        }
        GUILayout.EndVertical();
    }

    private void DrawMood(BlueSageReportView view)
    {
        GUILayout.Space(8f);
        GUILayout.BeginVertical(_sectionStyle);
        GUILayout.Label("Journal Mood Check-ins", _labelStyle);
        GUILayout.Label("These are the five faces chosen in On-Together's Journal.", _smallStyle);
        DrawJournalMoodFaces(view);
        BlueSageMoodUiModel ui = BlueSageMoodUiModel.Build(view.MoodStats);
        GUILayout.Label("Overview distribution", _smallStyle);
        foreach (string line in ui.OverviewLines) GUILayout.Label(line, _smallStyle);
        if (ui.OverviewLines.Count == 0) GUILayout.Label("No mood observations in this view.", _smallStyle);
        GUILayout.Label("Current calendar month", _smallStyle);
        foreach (string line in ui.MonthLines) GUILayout.Label(line, _smallStyle);
        GUILayout.Label("Current-year daily history", _smallStyle);
        foreach (string line in ui.YearLines) GUILayout.Label(line, _smallStyle);
        if (ui.YearLines.Count == 0) GUILayout.Label("No check-ins this year.", _smallStyle);
        GUILayout.Label("Only the Journal's saved face choice and time are read. BlueSage does not guess how you felt.", _smallStyle);
        GUILayout.EndVertical();
    }

    private void DrawJournalMoodFaces(BlueSageReportView view)
    {
        bool nativeFacesReady = BlueSageJournalMoodTextureResolver.TryResolve(out IReadOnlyList<Sprite> faces);
        GUILayout.BeginHorizontal();
        for (int feel = 0; feel <= 4; feel++)
        {
            GUILayout.BeginVertical(GUILayout.Width(150f));
            if (nativeFacesReady && feel < faces.Count && faces[feel] != null)
            {
                Rect faceRect = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(48f), GUILayout.Height(48f));
                DrawSprite(faces[feel], faceRect);
            }
            int count = view.MoodStats.OverviewCounts.TryGetValue(feel, out int value) ? value : 0;
            GUILayout.Label(BlueSageJournalMoodPresentation.Label(feel) + ": " + count, _smallStyle);
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
        if (!nativeFacesReady)
            GUILayout.Label("Native Journal face textures unavailable; plain-English labels remain active.", _smallStyle);
    }

    private static void DrawSprite(Sprite sprite, Rect target)
    {
        Texture2D texture = sprite.texture;
        if (texture == null) return;
        Rect source = sprite.textureRect;
        var uv = new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        GUI.DrawTextureWithTexCoords(target, texture, uv, true);
    }

    private void DrawAchievements(BlueSageReportView view)
    {
        GUILayout.Space(8f);
        GUILayout.BeginVertical(_sectionStyle);
        GUILayout.Label("Achievements", _labelStyle);
        if (view.AchievementProgress.IsOverallNative)
            GUILayout.Label(view.AchievementProgress.Unlocked + " / " + view.AchievementProgress.Total + " unlocked - current native truth", _smallStyle);
        else
            GUILayout.Label(view.Achievements.Count + " unlock observations in this mode. Timing is observational, not proof of cause.", _smallStyle);
        foreach (BlueSageAchievementObservation achievement in view.Achievements)
        {
            GUILayout.Label(achievement.Key + " - " + achievement.ObservationLabel, _smallStyle);
        }
        GUILayout.EndVertical();
    }

    private void DrawHistory()
    {
        GUILayout.Space(8f);
        GUILayout.BeginVertical(_sectionStyle);
        GUILayout.Label("Classification History", _labelStyle);
        GUILayout.Label("Recent sessions first. Corrections change only the BlueSage classification.", _smallStyle);
        _detailsExpanded = GUILayout.Toggle(_detailsExpanded, "Show technical details", _buttonStyle);
        foreach (BlueSageReportHistoryItem item in _projection!.History.Take(30))
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(item.Render(_detailsExpanded), _smallStyle);
            foreach (string line in item.DisplayLines) GUILayout.Label("  " + line, _smallStyle);
            GUILayout.EndVertical();
            if (item.Members.Count > 0)
            {
                if (item.Mode != WorkRelaxAttributionMode.Work && GUILayout.Button("Work", _buttonStyle, GUILayout.Width(62f)))
                    Reclassify(item, WorkRelaxAttributionMode.Work);
                if (item.Mode != WorkRelaxAttributionMode.Relax && GUILayout.Button("Relax", _buttonStyle, GUILayout.Width(62f)))
                    Reclassify(item, WorkRelaxAttributionMode.Relax);
                if (item.Mode != WorkRelaxAttributionMode.Unclassified && GUILayout.Button("Unclassified", _buttonStyle, GUILayout.Width(96f)))
                    Reclassify(item, WorkRelaxAttributionMode.Unclassified);
            }
            GUILayout.EndHorizontal();
        }

        string[] allocationLabels = BlueSageAllocationCatalog.Options.Select(item => item.Label).ToArray();
        float contentWidth = Math.Max(1f, _window.width - ReportWindowLayoutPolicy.HorizontalChromeWidth);
        bool stackedAllocationLayout = ReportWindowLayoutPolicy.UseStackedAllocationLayout(contentWidth);
        if (stackedAllocationLayout)
        {
            GUILayout.Label("Allocate Unclassified", _smallStyle);
            _allocationMetricIndex = GUILayout.SelectionGrid(
                _allocationMetricIndex,
                allocationLabels,
                3,
                _buttonStyle,
                GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
        }
        else
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Allocate Unclassified", _smallStyle, GUILayout.Width(140f));
            _allocationMetricIndex = GUILayout.SelectionGrid(
                _allocationMetricIndex,
                allocationLabels,
                3,
                _buttonStyle,
                GUILayout.Width(570f));
        }
        _allocationAmount = GUILayout.TextField(_allocationAmount, _textFieldStyle, GUILayout.Width(70f));
        if (GUILayout.Button("to Work", _buttonStyle, GUILayout.Width(85f))) Allocate(WorkRelaxAttributionMode.Work);
        if (GUILayout.Button("to Relax", _buttonStyle, GUILayout.Width(85f))) Allocate(WorkRelaxAttributionMode.Relax);
        if (GUILayout.Button("Undo latest", _buttonStyle, GUILayout.Width(100f)))
        {
            _status.ShowActionResult(WorkRelaxRuntimeController.TryUndoLatestReportCorrection(out string reason)
                ? "Latest correction undone."
                : reason);
            RequestRefresh();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void Reclassify(BlueSageReportHistoryItem item, WorkRelaxAttributionMode to)
    {
        _status.ShowActionResult(WorkRelaxRuntimeController.TryApplyReportCorrections(item.Members, to, out string reason)
            ? "Classification updated." : reason);
        RequestRefresh();
    }

    private void Allocate(WorkRelaxAttributionMode to)
    {
        if (!double.TryParse(_allocationAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
        {
            _status.ShowActionResult("Enter a valid amount.");
            return;
        }
        _status.ShowActionResult(WorkRelaxRuntimeController.TryApplyReportCorrection(
            BlueSageAllocationCatalog.Options[_allocationMetricIndex].MetricKey,
            WorkRelaxAttributionMode.Unclassified, to, amount, "aggregate-unclassified", out string reason)
            ? "Unclassified value allocated."
            : reason);
        RequestRefresh();
    }

    private void RequestRefresh()
    {
        if (_refreshCoroutine != null) StopCoroutine(_refreshCoroutine);
        _status.BeginProjectionRefresh();
        _refreshCoroutine = StartCoroutine(RefreshRoutine());
    }

    private IEnumerator RefreshRoutine()
    {
        var total = Stopwatch.StartNew();
        long moodMilliseconds;
        long achievementMilliseconds;
        long projectionMilliseconds;
        int yieldedFrames = 1;
        yield return null;

        var phase = Stopwatch.StartNew();
        BlueSageReportNativeReader.ReadMoods(out NativeMoodObservation[] moods, out bool moodsAvailable,
            out string moodWarning);
        moodMilliseconds = phase.ElapsedMilliseconds;
        yield return null;
        yieldedFrames++;

        phase.Restart();
        BlueSageAchievementReadSession achievementRead = BlueSageReportNativeReader.BeginAchievementRead();
        while (!achievementRead.Complete)
        {
            achievementRead.ReadNextAchievementBatch(AchievementBatchSize);
            if (!achievementRead.Complete)
            {
                yield return null;
                yieldedFrames++;
            }
        }
        achievementMilliseconds = phase.ElapsedMilliseconds;

        phase.Restart();
        bool refreshed = WorkRelaxRuntimeController.TryRefreshReport(moods, moodsAvailable,
            achievementRead.Observations, achievementRead.Available,
            out BlueSageReportProjection? report, out string reason);
        projectionMilliseconds = phase.ElapsedMilliseconds;
        string warning = string.Join(" ", new[] { moodWarning, achievementRead.Warning }
            .Where(item => !string.IsNullOrWhiteSpace(item)));
        if (refreshed) _projection = report;
        _status.CompleteProjectionRefresh(refreshed, warning, reason);
        Plugin.Log?.LogInfo("BlueSage Report refresh trace: total=" + total.ElapsedMilliseconds +
            "ms mood=" + moodMilliseconds + "ms achievements=" + achievementMilliseconds +
            "ms projection=" + projectionMilliseconds + "ms yieldedFrames=" + yieldedFrames +
            " achievementCount=" + achievementRead.Observations.Length + ".");
        _refreshCoroutine = null;
    }

    private void DrawFooter()
    {
        GUILayout.BeginVertical(_footerStyle, GUILayout.MinHeight(FooterHeight));
        GUILayout.Label(_status.Text, _footerTextStyle);
        GUILayout.Label("Vanilla saved data stays unchanged. Use the filters to view the same totals by intent.", _footerTextStyle);
        GUILayout.EndVertical();
    }

    private float GetContentScrollHeight()
    {
        return Mathf.Max(120f, _window.height - HeaderHeight - FooterHeight - 52f);
    }

    private Rect GetInitialWindowRect()
    {
        float screenWidth = MenuDisplayScaleRuntime.LogicalScreenWidth;
        float screenHeight = MenuDisplayScaleRuntime.LogicalScreenHeight;
        float width = Mathf.Min(DefaultWindowWidth, WindowFitPolicy.GetUsableWidth(screenWidth));
        float height = Mathf.Min(DefaultWindowHeight, WindowFitPolicy.GetUsableHeight(screenHeight));
        return new Rect((screenWidth - width) / 2f, (screenHeight - height) / 2f, width, height);
    }

    private void ClampWindowToScreen()
    {
        WindowFitResult fit = WindowFitPolicy.FitToScreen(
            _window.x,
            _window.y,
            _window.width,
            _window.height,
            MinWindowWidth,
            MinWindowHeight,
            MenuDisplayScaleRuntime.LogicalScreenWidth,
            MenuDisplayScaleRuntime.LogicalScreenHeight);
        _window = new Rect(fit.X, fit.Y, fit.Width, fit.Height);
    }

    private void DrawResizeGrip()
    {
        Rect grip = new Rect(_window.width - ResizeGripSize - 8f, _window.height - ResizeGripSize - 8f, ResizeGripSize, ResizeGripSize);
        GUI.Box(grip, new GUIContent("↘", "Drag this corner to resize BlueSage Report."), _buttonStyle);
        Event evt = Event.current;
        if (evt == null) return;
        if (evt.type == EventType.MouseDown && grip.Contains(evt.mousePosition))
        {
            _isResizing = true;
            evt.Use();
        }
        else if (_isResizing && evt.type == EventType.MouseDrag)
        {
            WindowFitResult fit = WindowFitPolicy.FitToScreen(
                _window.x,
                _window.y,
                _window.width + evt.delta.x,
                _window.height + evt.delta.y,
                MinWindowWidth,
                MinWindowHeight,
                MenuDisplayScaleRuntime.LogicalScreenWidth,
                MenuDisplayScaleRuntime.LogicalScreenHeight);
            _pendingResizeWidth = fit.Width;
            _pendingResizeHeight = fit.Height;
            _hasPendingResize = true;
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
            BlueSageUiTheme.AreRuntimeTexturesAlive()) return;

        _renderedThemeKey = _theme.Key;
        _windowBackgroundTexture = BlueSageUiTheme.GetSolidTexture(_theme.Window);
        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            padding = new RectOffset(16, 16, 52, 14),
            normal = { background = _windowBackgroundTexture, textColor = _theme.LabelText }
        };
        _headerBoxStyle = new GUIStyle(_windowStyle) { normal = { background = BlueSageUiTheme.GetSolidTexture(_theme.Header) } };
        _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = _theme.HeaderText } };
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = _theme.LabelText } };
        _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = _theme.LabelText } };
        _buttonStyle = BlueSageUiTheme.CreateNeutralButtonStyle(GUI.skin.button, _theme, 13);
        _activeButtonStyle = BlueSageUiTheme.CreateStateButtonStyle(_buttonStyle, _theme.ActiveAccent, _theme.FieldFocusedBorder, _theme.ActiveStateText, 13);
        _sectionStyle = new GUIStyle(_windowStyle)
        {
            padding = new RectOffset(14, 14, 12, 12),
            border = new RectOffset(1, 1, 1, 1),
            normal = { background = BlueSageUiTheme.GetPanelTexture(_theme.SectionPanel, _theme.SectionBorder), textColor = _theme.LabelText }
        };
        _footerStyle = new GUIStyle(_sectionStyle)
        {
            padding = new RectOffset(14, 56, 10, 10),
            normal = { background = BlueSageUiTheme.GetPanelTexture(_theme.FooterBackground, _theme.FooterBorder), textColor = _theme.FooterText }
        };
        _footerTextStyle = new GUIStyle(_smallStyle) { normal = { textColor = _theme.FooterText } };
        _textFieldStyle = BlueSageUiTheme.CreateTextInputStyle(GUI.skin.textField, _theme, 14, wordWrap: false);
        _renderedTextureGeneration = BlueSageUiTheme.RuntimeTextureGeneration;
    }

    private void OnDisable()
    {
        if (_refreshCoroutine != null)
        {
            StopCoroutine(_refreshCoroutine);
            _refreshCoroutine = null;
        }
        BlueSageWindowHoverScope.UnregisterWindow(WindowId);
    }
}

internal static class BlueSageReportNativeReader
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static void ReadMoods(out NativeMoodObservation[] moods, out bool moodsAvailable, out string warning)
    {
        moods = Array.Empty<NativeMoodObservation>();
        moodsAvailable = false;
        warning = string.Empty;
        Assembly? game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item =>
            string.Equals(item.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
        try
        {
            Type? dataManagerType = game?.GetType("DataManager", false);
            if (dataManagerType == null)
            {
                warning = "Native mood observations are unavailable on this game build.";
            }
            else
            {
                UnityEngine.Object? dataManager = NativeManagerCache.FindLiveObject(dataManagerType);
                object? playerData = dataManager == null ? null : Read(dataManager, dataManagerType, "PlayerData");
                object? journal = playerData == null ? null : Read(playerData, playerData.GetType(), "JournalDatas2");
                var moodList = new List<NativeMoodObservation>();
                if (journal is IEnumerable entries)
                {
                    foreach (object? entry in entries)
                    {
                        if (entry == null) continue;
                        object? feel = Read(entry, entry.GetType(), "Feel");
                        object? time = Read(entry, entry.GetType(), "Time");
                        if (feel == null || time == null) continue;
                        moodList.Add(new NativeMoodObservation(Convert.ToInt32(feel, CultureInfo.InvariantCulture), ToTicks(time)));
                    }
                    moods = moodList.ToArray();
                    moodsAvailable = true;
                }
                else warning = "Native mood history is temporarily unavailable.";
            }
        }
        catch (Exception exception) { warning = "Native mood refresh stayed safe: " + exception.GetType().Name; }
    }

    internal static BlueSageAchievementReadSession BeginAchievementRead() => new(Flags);
    private static object? Read(object target, Type type, string name) =>
        BlueSageReportPrivacyPolicy.IsAllowedJournalMember(name)
            ? type.GetField(name, Flags)?.GetValue(target) ?? type.GetProperty(name, Flags)?.GetValue(target, null)
            : ReadNonJournalMember(target, type, name);
    private static object? ReadNonJournalMember(object target, Type type, string name)
    {
        if (string.Equals(type.Name, "JournalData2", StringComparison.Ordinal) ||
            string.Equals(type.Name, "JournalData", StringComparison.Ordinal)) return null;
        return type.GetField(name, Flags)?.GetValue(target) ?? type.GetProperty(name, Flags)?.GetValue(target, null);
    }
    private static long ToTicks(object value)
    {
        if (value is DateTime time) return time.ToUniversalTime().Ticks;
        if (value is long ticks) return ticks;
        if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out DateTime parsed)) return parsed.Ticks;
        return 0L;
    }
}

internal sealed class BlueSageAchievementReadSession
{
    private readonly object[] _values = Array.Empty<object>();
    private readonly UnityEngine.Object? _manager;
    private readonly MethodInfo? _getAchievement;
    private readonly List<NativeAchievementObservation> _observations = new();
    private int _next;

    internal BlueSageAchievementReadSession(BindingFlags flags)
    {
        try
        {
            Assembly? game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item =>
                string.Equals(item.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal));
            Type? managerType = game?.GetType("AchievementManager", false);
            Type? achievementType = game?.GetType("Achievement", false);
            if (managerType == null || achievementType == null || !achievementType.IsEnum)
            {
                Warning = "Native achievement observations are unavailable on this game build.";
                Complete = true;
                return;
            }

            _manager = NativeManagerCache.FindLiveObject(managerType);
            _getAchievement = managerType.GetMethod("GetAchievement", flags, null, new[] { achievementType }, null);
            if (_manager == null || _getAchievement == null)
            {
                Warning = "Native achievement history is temporarily unavailable.";
                Complete = true;
                return;
            }

            _values = Enum.GetValues(achievementType).Cast<object>().ToArray();
            Complete = _values.Length == 0;
            Available = true;
        }
        catch (Exception exception)
        {
            Warning = "Native achievement refresh stayed safe: " + exception.GetType().Name;
            Complete = true;
        }
    }

    internal bool Complete { get; private set; }
    internal bool Available { get; private set; }
    internal string Warning { get; private set; } = string.Empty;
    internal NativeAchievementObservation[] Observations => _observations.ToArray();

    internal void ReadNextAchievementBatch(int maximum)
    {
        if (Complete) return;
        int end = Math.Min(_values.Length, _next + Math.Max(1, maximum));
        try
        {
            for (; _next < end; _next++)
            {
                object value = _values[_next];
                bool unlocked = _getAchievement!.Invoke(_getAchievement.IsStatic ? null : _manager,
                    new[] { value }) is bool state && state;
                _observations.Add(new NativeAchievementObservation(value.ToString() ?? string.Empty, unlocked));
            }
            Complete = _next >= _values.Length;
        }
        catch (Exception exception)
        {
            Warning = "Native achievement refresh stayed safe: " + exception.GetType().Name;
            Available = false;
            Complete = true;
            _observations.Clear();
        }
    }
}

internal static class BlueSageJournalMoodTextureResolver
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static Component? _statisticManager;
    private static IReadOnlyList<Sprite>? _cached;

    internal static void ObserveStatisticManager(Component? statisticManager)
    {
        if (statisticManager == null || statisticManager == _statisticManager) return;
        _statisticManager = statisticManager;
        _cached = null;
    }

    internal static bool TryResolve(out IReadOnlyList<Sprite> faces)
    {
        if (_cached != null && _cached.Count == 5 && _cached.All(item => item != null))
        {
            faces = _cached;
            return true;
        }

        faces = Array.Empty<Sprite>();
        if (_statisticManager == null) return false;
        try
        {
            FieldInfo? field = _statisticManager.GetType().GetField("_moodIcons", Flags);
            if (field?.GetValue(_statisticManager) is not IEnumerable values) return false;
            List<Sprite> resolved = values.Cast<object?>().OfType<Sprite>().Take(5).ToList();
            if (resolved.Count != 5 || resolved.Any(item => item == null)) return false;
            _cached = resolved.AsReadOnly();
            faces = _cached;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
