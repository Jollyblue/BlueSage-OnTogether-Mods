using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class StyleHelperWindow : MonoBehaviour
{
    // Theme palettes supply 0.99f near-opaque window backgrounds so the helper stays readable in bright lobbies.
    private const string PlaceholderName = "YOURNAMEHERE";
    private const float MinWindowWidth = 920f;
    private const float MinWindowHeight = 640f;
    private const float ResizeGripSize = 42f;
    private const float FooterHeight = 156f;
    private const float CompactFooterHeight = 198f;
    private const float ContentChromeHeight = 78f;
    private const string DefaultColorA = "0E6EED";
    private const string DefaultColorB = "A59";
    private const string DefaultColorC = "D85";
    private const string NameStylerDescription = "Build styled name/chat text with colors, bold, italic, and gradients.";
    private const string StatusStylerDescription = "Set your activity tag and Spoons together without overwriting your styled name.";
    private const string TemplatesDescription = "Save or load five local name-style templates. Status and Spoons stay separate.";
    private static readonly string[] PresetColors =
    {
        "0E6EED", "1E90FF", "5AC8FA", "00C7BE", "70FFBD", "34C759",
        "FFD45D", "FFB347", "FF9500", "FF6B8A", "FF2D55", "FF3B30",
        "AF52DE", "9B59B6", "8833EE", "A550A5", "D88555", "C7A17A",
        "FFFFFF", "F7E7CE", "D8CAB8", "9B8E7E", "7B5B63", "2D1B2F",
        "FFFD82", "B8FF70", "70D6FF", "B388FF", "FFAFCC", "CDB4DB",
        "000000", "808080", "C0C0C0", "800000", "FF0000", "FFA500",
        "FFFF00", "808000", "008000", "00FF00", "008080", "00FFFF",
        "000080", "0000FF", "800080", "FF00FF", "4B0082", "F5DEB3",
        "CD853F", "D2691E", "8B4513", "2F4F4F", "4682B4", "DA70D6",
        "FF69B4", "FF1493", "DB7093", "E6E6FA", "DDA0DD", "BA55D3",
        "7FFFD4", "40E0D0", "48D1CC", "20B2AA", "66CDAA", "00FA9A",
        "ADFF2F", "7CFC00", "32CD32", "228B22", "556B2F", "6B8E23",
        "FFFACD", "FFE4B5", "FFDAB9", "F4A460", "DEB887", "A0522D",
        "B0E0E6", "87CEEB", "6495ED", "4169E1", "191970", "708090"
    };

    private Rect _windowRect = new Rect(80f, 80f, 960f, 720f);
    private bool _visible;
    private bool _hasOpened;
    internal bool IsVisible => _visible;
    private string _text = PlaceholderName;
    private string _colorA = DefaultColorA;
    private string _colorB = DefaultColorB;
    private string _colorC = DefaultColorC;
    private string _status = "BRB";
    private string _statusColor = "FFD45D";
    private string _spoonCountText = "5";
    private string _spoonLabelText = "sp";
    private string _preview = string.Empty;
    private string _message = "Pick a tab, choose colors, then Apply or Copy.";
    private bool _previewIsValid;
    private bool _bold;
    private bool _italic;
    private int _mode = 3;
    private int _tab;
    private int _activeColorSlot;
    private string _detectedNamePreview = string.Empty;
    private Vector2 _scroll;
    private Vector2 _contentScroll;
    private GUIStyle _windowStyle;
    private GUIStyle _headerBoxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _smallStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _activeButtonStyle;
    private GUIStyle _boxStyle;
    private GUIStyle _footerBoxStyle;
    private GUIStyle _footerTextStyle;
    private GUIStyle _textFieldStyle;
    private GUIStyle _generatedTextStyle;
    private GUIStyle _swatchButtonStyle;
    private GUIStyle _previewStyle;
    private Texture2D _windowBackgroundTexture;
    private bool _isResizing;
    private string _lastThemeKey = string.Empty;
    private int _renderedTextureGeneration;
    private BlueSageUiThemePalette _theme;
    private readonly Stack<StyleHelperSnapshot> _undoHistory = new Stack<StyleHelperSnapshot>();
    private readonly Stack<StyleHelperSnapshot> _redoHistory = new Stack<StyleHelperSnapshot>();
    private bool _hasUndo => _undoHistory.Count > 0;
    private string _stateLabel = "Saved";
    private string _stateDetail = "Detected current in-game values.";
    private string _lastOwnedFooterTooltip = string.Empty;

    public void ToggleVisible()
    {
        _visible = !_visible;
        BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.StyleHelper, _visible);
        if (!_visible)
        {
            BlueSageWindowHoverScope.UnregisterWindow(47057);
        }
        if (_visible)
        {
            _lastOwnedFooterTooltip = string.Empty;
            WindowFitResult fitted = BlueSageWindowCoordinator.ResolveForOpen(
                BlueSagePublicWindow.StyleHelper,
                _hasOpened,
                _windowRect,
                MinWindowWidth,
                MinWindowHeight,
                MenuDisplayScaleRuntime.LogicalScreenWidth,
                MenuDisplayScaleRuntime.LogicalScreenHeight);
            _windowRect = new Rect(fitted.X, fitted.Y, fitted.Width, fitted.Height);
            _hasOpened = true;
            ResetTransientEditorStateForLiveSync(resetTab: true);
            DetectedDisplayNameState detected = Plugin.Instance != null ? Plugin.Instance.GetCurrentDisplayNameStateForStyleUi() : null;
            HydrateCurrentName(force: true, detected);
            HydrateCurrentStatus(detected);
            HydrateSpoons(detected);
            RebuildPreview();
            MarkSaved(detected?.DriftSummary ?? "Detected current in-game values. Edit a field to start a draft.");
        }
    }

    private void OnGUI()
    {
        if (!_visible || Plugin.EnableStyleUi == null || !Plugin.EnableStyleUi.Value)
        {
            BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.StyleHelper, false);
            return;
        }

        BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.StyleHelper, true);
        EnsureStyles();
        ClampWindowToScreen();
        BlueSageWindowHoverScope.RegisterWindow(47057, MenuDisplayScaleRuntime.ToPhysicalRect(_windowRect), 10);
        ImGuiTextFocusTracker.BlurOnClickOutsideWindows();
        MenuDisplayScaleRuntime.Apply(out Matrix4x4 previousMatrix);
        try
        {
            _windowRect = GUI.Window(47057, _windowRect, DrawWindow, string.Empty, _windowStyle);
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
        // QoL Menu and Style Helper may overlap. GUI.tooltip is global, so
        // reject the inherited value while preserving help from this window.
        string inheritedTooltip = GUI.tooltip;
        GUI.tooltip = string.Empty;
        if (_windowBackgroundTexture != null)
        {
            GUI.DrawTexture(new Rect(0f, 0f, _windowRect.width, _windowRect.height), _windowBackgroundTexture);
        }

        DrawHeader();
        GUILayout.Space(10f);
        DrawTabs();
        GUILayout.Space(8f);
        _contentScroll = GUILayout.BeginScrollView(_contentScroll, false, true, GUILayout.Height(GetContentScrollHeight()));

        if (_tab == 0)
        {
            DrawStyleTab();
        }
        else if (_tab == 1)
        {
            DrawStatusTab();
        }
        else
        {
            DrawTemplatesTab();
        }

        GUILayout.EndScrollView();
        DrawFooter(inheritedTooltip);
        DrawResizeGrip();
        HeaderActionLayout headerLayout = HeaderActionLayoutPolicy.Resolve(_windowRect.width);
        GUI.DragWindow(ToUnityRect(headerLayout.Drag));
    }

    private void DrawHeader()
    {
        Rect header = new Rect(0f, 0f, _windowRect.width, 38f);
        GUI.Box(header, GUIContent.none, _headerBoxStyle);
        HeaderActionLayout layout = HeaderActionLayoutPolicy.Resolve(_windowRect.width);
        GUI.Label(ToUnityRect(layout.Title), $"▣ BlueSage Style Helper  v{Plugin.PluginDisplayVersion}", _headerStyle);
        if (GUI.Button(ToUnityRect(layout.Website), new GUIContent(layout.WebsiteLabel, HeaderActionLayoutPolicy.WebsiteTooltip), _buttonStyle))
        {
            Plugin.Instance?.ShowToast(HeaderActionLayoutPolicy.WebsiteComingSoonNotice);
        }
        if (GUI.Button(ToUnityRect(layout.Thunderstore), new GUIContent(layout.ThunderstoreLabel, "Open the BlueSage QoL Tweaks Thunderstore page."), _buttonStyle))
        {
            Application.OpenURL("https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/");
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
            BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.StyleHelper, false);
            BlueSageWindowHoverScope.UnregisterWindow(47057);
        }
    }

    private static Rect ToUnityRect(HeaderActionRect rect) =>
        new Rect(rect.X, rect.Y, rect.Width, rect.Height);

    private float GetContentScrollHeight()
    {
        HelpFrameLayout frame = BlueSageHelpFrame.Resolve(
            BlueSageHelpFrameKind.StyleHelper,
            _windowRect);
        return Mathf.Max(36f, frame.ContentHeight);
    }

    private float GetFooterHeight()
    {
        return BlueSageHelpFrame.Resolve(
            BlueSageHelpFrameKind.StyleHelper,
            _windowRect).FooterHeight;
    }

    private void DrawTabs()
    {
        GUILayout.BeginHorizontal();
        DrawTabButton(0, "Name Styler", NameStylerDescription);
        DrawTabButton(1, "Status Styler", StatusStylerDescription);
        DrawTabButton(2, "Templates", TemplatesDescription);
        GUILayout.EndHorizontal();
    }

    private void DrawTabButton(int index, string label, string tooltip)
    {
        bool active = _tab == index;
        if (GUILayout.Button(new GUIContent(label, tooltip), active ? _activeButtonStyle : _buttonStyle, GUILayout.Height(32f)))
        {
            if (_tab != index)
            {
                _tab = index;
                _contentScroll = Vector2.zero;
            }
            RebuildPreview();
        }
    }

    private void DrawStyleTab()
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label("NAME STYLER: type the name text players should see, choose color/bold/italic/gradient, then use Apply Name Styler or Copy Name Style Text.", _smallStyle);
        GUILayout.Label("Tip: choose Color A/B/C first, then tap a swatch. The highlighted slot is the one the shared color library will change.", _smallStyle);
        GUILayout.Label("Detected rich-text tags stay visible and editable here, including nested <b>, <rotate>, and <size>. Undo/Redo keeps up to 50 steps.", _smallStyle);
        GUILayout.Space(4f);
        GUILayout.Label("Text", _labelStyle);
        // Keep the complete draft in the editor. Apply/copy validates the configured
        // profile limit without letting Unity cut a closing rich-text tag mid-edit.
        string nextText = GUILayout.TextArea(_text ?? string.Empty, _textFieldStyle, GUILayout.MinHeight(54f), GUILayout.MaxHeight(92f));
        if (nextText != _text)
        {
            CaptureUndo("Text edit");
            _text = nextText;
            ClearDetectedNamePreview();
            RebuildPreview();
            MarkDraft("Name draft changed. Apply Name Styler or Copy Name Style Text when ready.");
        }

        GUILayout.Space(6f);
        DrawColorRow();
        GUILayout.Space(6f);
        DrawModeRow();
        if ((_text ?? string.Empty).IndexOf('<') >= 0)
        {
            GUILayout.Label("Custom Rich Text mode: the exact balanced markup is authoritative. BlueSage verifies tag structure; the game decides whether each tag/value renders. Presets do not rewrite custom markup unless you remove it first.", _smallStyle);
        }
        GUILayout.Space(6f);
        DrawPreview();
        GUILayout.EndVertical();
    }

    private void DrawStatusTab()
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label("STATUS STYLER: set the short activity tag after your name, like Sam (Testing). Spoons is the optional compact [3/5sp] tag.", _smallStyle);
        GUILayout.Label("Tip: Status Styler changes only the activity tag. Name Styler colors stay separate; the color library here updates the status color.", _smallStyle);
        GUILayout.Label("Clear Status removes the live status. Undo tries to restore the last Style Helper apply/clear/edit.", _smallStyle);
        GUILayout.Space(4f);
        GUILayout.Label("Status", _labelStyle);
        string nextStatus = GUILayout.TextArea(_status ?? string.Empty, _textFieldStyle, GUILayout.MinHeight(54f), GUILayout.MaxHeight(92f));
        if (nextStatus != _status)
        {
            CaptureUndo("Status edit");
            _status = nextStatus;
            RebuildPreview();
            MarkDraft("Status draft changed. Apply Status Styler when ready.");
        }

        GUILayout.Space(6f);
        DrawStatusColorRow();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Clear Status", "Remove only the activity/status text from your display name. Your styled base name and Spoons setting remain separate."), _buttonStyle, GUILayout.Height(32f)))
        {
            CaptureUndo("Clear Status", restoreStatus: true);
            bool applied = false;
            _message = Plugin.Instance != null ? Plugin.Instance.TryClearStatusFromStyleUi(out applied) : "Style UI: plugin is not ready yet.";
            MarkApplyOutcome(applied, "Live status cleared. Name and Spoons settings were left alone.", "Status was not cleared; fix the reported problem and try again.");
        }
        GUILayout.EndHorizontal();
        DrawSpoonsControls();
        DrawPreview();
        GUILayout.EndVertical();
    }

    private void DrawSpoonsControls()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Spoons", _labelStyle);
        GUILayout.Label("Optional compact social-battery tag, like [3/5sp]. Default is hidden; Save Spoons or Show Tag opts in, Hide Tag removes it from your name.", _smallStyle);
        DrawSpoonCustomLabelRow();
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Spoons 0-5", "Set your optional spoon tag from 0 to 5. Lower numbers can mean lower social battery."), _labelStyle, GUILayout.Width(120f));
        string previousSpoonText = _spoonCountText ?? string.Empty;
        string nextSpoonText = GUILayout.TextField(previousSpoonText, 3, _textFieldStyle, GUILayout.Width(80f), GUILayout.Height(28f));
        if (!string.Equals(nextSpoonText, previousSpoonText, StringComparison.Ordinal))
        {
            CaptureUndo("Spoons edit");
            _spoonCountText = nextSpoonText;
            MarkDraft("Spoons draft changed. Save Spoons or Show Tag to apply it.");
        }
        if (GUILayout.Button(new GUIContent("Save Spoons", "Save the compact [0/5sp]-[5/5sp] tag and apply it to your display name."), _buttonStyle, GUILayout.Height(28f)))
        {
            CaptureUndo("Save Spoons", restoreName: true, restoreSpoons: true);
            SaveSpoonsFromStyleUi();
        }
        if (GUILayout.Button(new GUIContent("Show Tag", "Show your saved [0/5sp]-[5/5sp] tag in your display name."), _buttonStyle, GUILayout.Height(28f)))
        {
            CaptureUndo("Show Spoon Tag", restoreName: true, restoreSpoons: true);
            if (Plugin.Instance != null)
            {
                _message = Plugin.Instance.TrySetSpoonVisibilityFromMenu(true, out bool applied);
                MarkApplyOutcome(applied, "Spoons tag is visible in your name.", "Spoons stayed unchanged; fix the reported problem and try again.");
            }
        }
        if (GUILayout.Button(new GUIContent("Hide Tag", "Hide the optional [spoons] tag from your display name. Your saved spoon number stays here for later."), _buttonStyle, GUILayout.Height(28f)))
        {
            CaptureUndo("Hide Spoon Tag", restoreName: true, restoreSpoons: true);
            if (Plugin.Instance != null)
            {
                _message = Plugin.Instance.TrySetSpoonVisibilityFromMenu(false, out bool applied);
                MarkApplyOutcome(applied, "Spoons tag is hidden. Saved spoon number remains here.", "Spoons stayed unchanged; fix the reported problem and try again.");
            }
        }
        if (GUILayout.Button(new GUIContent("Undo Spoons", "Undo only the last Spoons save/show/hide action. Name, status, and template undo still live in the main footer Undo."), _buttonStyle, GUILayout.Width(104f), GUILayout.Height(28f)))
        {
            UndoSpoonsLastAction();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSpoonCustomLabelRow()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Custom label", "Optional 1-12 character label after 0/5. Default 'sp' makes [3/5sp]; try energy or spoons."), _labelStyle, GUILayout.Width(120f));
        string nextLabel = GUILayout.TextField(_spoonLabelText ?? string.Empty, 12, _textFieldStyle, GUILayout.Width(140f), GUILayout.Height(28f));
        if (!string.Equals(nextLabel, _spoonLabelText, StringComparison.Ordinal))
        {
            CaptureUndo("Mood label edit");
            _spoonLabelText = nextLabel;
            MarkDraft("Mood label changed. Save Spoons applies it; /setmood does the same from chat.");
        }
        GUILayout.Label("Default is 'sp'. /setmood default restores it.", _smallStyle);
        GUILayout.EndHorizontal();
    }

    private void DrawStatusColorRow()
    {
        DrawStatusColorPicker();
        GUILayout.Label("Status color library: tap a swatch to color only the status tag.", _labelStyle);
        DrawPresetColorLibrary(
            preset => new GUIContent("#" + preset, "Use #" + preset + " for only your status tag. It will not recolor your styled name."),
            preset =>
            {
                CaptureUndo("Status color swatch");
                _statusColor = preset;
                _message = "Status color set to #" + preset + ".";
                RebuildPreview();
                MarkDraft("Status color draft changed. Apply Status Styler when ready.");
            });
    }

    private void DrawStatusColorPicker()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent("Status color", "This color belongs only to Status Styler. It will not change Name Styler Color A."), _labelStyle, GUILayout.Width(120f));
        string value = _statusColor ?? string.Empty;
        string next = GUILayout.TextField(value, 8, _textFieldStyle, GUILayout.Width(112f), GUILayout.Height(28f));
        if (next != value)
        {
            CaptureUndo("Status color edit");
            _statusColor = next;
            RebuildPreview();
            MarkDraft("Status color draft changed. Apply Status Styler when ready.");
        }

        Color oldBackground = GUI.backgroundColor;
        GUI.backgroundColor = HexToColor(_statusColor, oldBackground);
        GUILayout.Button(new GUIContent(" ", "Current color preview for your status tag only."), _swatchButtonStyle, GUILayout.Width(38f), GUILayout.Height(28f));
        GUI.backgroundColor = oldBackground;
        GUILayout.EndHorizontal();
    }

    private void DrawTemplatesTab()
    {
        GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));
        GUILayout.Label("Tip: Templates save name styling only: text, colors, style mode, bold, and italic. Load applies the saved name style right away; Status and Spoons stay separate.", _smallStyle);
        for (int i = 1; i <= 5; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Template " + i, _labelStyle, GUILayout.Width(110f));
            if (GUILayout.Button(new GUIContent("Save " + i, "Save the current Style tab text/colors/mode into this local template slot."), _buttonStyle, GUILayout.Height(30f)))
            {
                SaveTemplate(i);
            }
            if (GUILayout.Button(new GUIContent("Undo", "Undo the last template save/load or other Style Helper action."), _buttonStyle, GUILayout.Height(30f)))
            {
                UndoLastAction();
            }
            if (GUILayout.Button(new GUIContent("Load " + i, "Load and apply this saved name-style template now. Use Undo if it was not what you wanted."), _buttonStyle, GUILayout.Height(30f)))
            {
                LoadTemplate(i);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void DrawColorRow()
    {
        DrawColorPicker("Color A", 0);
        DrawColorPicker("Color B", 1);
        DrawColorPicker("Color C", 2);
        GUILayout.Label("Color library: tap Color A/B/C first, then tap a swatch. Color mode uses A, Gradient uses A/B, Gradient 3 uses A/B/C.", _labelStyle);
        DrawPresetColorLibrary(
            preset => new GUIContent("#" + preset, "Apply #" + preset + " to Color " + (char)('A' + _activeColorSlot) + " in the editor preview."),
            preset =>
            {
                CaptureUndo("Color swatch");
                SetColorSlot(_activeColorSlot, preset);
                RebuildPreview();
                MarkDraft("Color draft changed. Apply Name Styler or Copy Name Style Text when ready.");
            });
    }

    private void DrawPresetColorLibrary(Func<string, GUIContent> buildContent, Action<string> applyPreset)
    {
        int columns = Mathf.Clamp(Mathf.FloorToInt((_windowRect.width - 72f) / 96f), 4, 9);
        int rows = Mathf.CeilToInt(PresetColors.Length / (float)columns);
        for (int row = 0; row < rows; row++)
        {
            GUILayout.BeginHorizontal();
            for (int index = row * columns; index < Math.Min(PresetColors.Length, row * columns + columns); index++)
            {
                string preset = PresetColors[index];
                Color oldBackground = GUI.backgroundColor;
                Color oldContent = GUI.contentColor;
                Color swatch = HexToColor(preset, oldBackground);
                GUI.backgroundColor = swatch;
                GUI.contentColor = BlueSageUiTheme.BestTextColor(swatch, _theme);
                if (GUILayout.Button(buildContent(preset), _swatchButtonStyle, GUILayout.MinWidth(84f), GUILayout.Height(28f)))
                {
                    applyPreset(preset);
                }
                GUI.contentColor = oldContent;
                GUI.backgroundColor = oldBackground;
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawColorPicker(string label, int slot)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent(label, "Choose which color slot the swatch library changes. A is used by every style mode."), _activeColorSlot == slot ? _activeButtonStyle : _buttonStyle, GUILayout.Width(92f), GUILayout.Height(28f)))
        {
            _activeColorSlot = slot;
        }
        string value = GetColorSlot(slot);
        string next = GUILayout.TextField(value ?? string.Empty, 8, _textFieldStyle, GUILayout.Width(112f), GUILayout.Height(28f));
        if (next != value)
        {
            CaptureUndo(label + " edit");
            SetColorSlot(slot, next);
            RebuildPreview();
            MarkDraft(label + " draft changed. Apply when ready.");
        }
        Color oldBackground = GUI.backgroundColor;
        GUI.backgroundColor = HexToColor(value, oldBackground);
        GUILayout.Button(new GUIContent(" ", "Current color preview for this slot."), _swatchButtonStyle, GUILayout.Width(38f), GUILayout.Height(28f));
        GUI.backgroundColor = oldBackground;
        GUILayout.EndHorizontal();
    }

    private void DrawModeRow()
    {
        GUILayout.Label("Style toggles", _labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent(_bold ? "Bold On" : "Bold Off", "Adds bold tags when using Color mode. Gradients ignore bold for cleaner output."), _bold ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
        {
            CaptureUndo("Bold toggle");
            _bold = !_bold;
            ClearDetectedNamePreview();
            RebuildPreview();
            MarkDraft("Bold style draft changed. Apply Name Styler when ready.");
        }
        if (GUILayout.Button(new GUIContent(_italic ? "Italic On" : "Italic Off", "Adds italic tags when using Color mode. Gradients ignore italic for cleaner output."), _italic ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
        {
            CaptureUndo("Italic toggle");
            _italic = !_italic;
            ClearDetectedNamePreview();
            RebuildPreview();
            MarkDraft("Italic style draft changed. Apply Name Styler when ready.");
        }
        if (GUILayout.Button(new GUIContent(_mode == 1 ? "Color On" : "Color", "Use Color A across the whole text. Best for simple readable names."), _mode == 1 ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
        {
            CaptureUndo("Color mode");
            _mode = 1;
            ClearDetectedNamePreview();
            RebuildPreview();
            MarkDraft("Color mode draft changed. Apply Name Styler when ready.");
        }
        if (GUILayout.Button(new GUIContent(_mode == 2 ? "Gradient On" : "Gradient", "Blend from Color A to Color B across the text."), _mode == 2 ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
        {
            CaptureUndo("Gradient mode");
            _mode = 2;
            ClearDetectedNamePreview();
            RebuildPreview();
            MarkDraft("Gradient mode draft changed. Apply Name Styler when ready.");
        }
        if (GUILayout.Button(new GUIContent(_mode == 3 ? "Gradient 3 On" : "Gradient 3", "Blend from Color A to B to C. Best for playful names, but can create longer hidden text."), _mode == 3 ? _activeButtonStyle : _buttonStyle, GUILayout.Height(30f)))
        {
            CaptureUndo("Gradient 3 mode");
            _mode = 3;
            ClearDetectedNamePreview();
            RebuildPreview();
            MarkDraft("Gradient 3 mode draft changed. Apply Name Styler when ready.");
        }
        GUILayout.EndHorizontal();
    }

    private void DrawPreview()
    {
        GUILayout.Space(6f);
        int displayedLength = _previewIsValid
            ? (_preview ?? string.Empty).Length
            : (_tab == 1 ? (_status ?? string.Empty).Length : (_text ?? string.Empty).Length);
        GUILayout.Label($"Rich-text length: {displayedLength}/{Plugin.LockedMaxIdCardCharacters} characters (tags count too)", _smallStyle);
        GUILayout.Label(_message ?? string.Empty, _smallStyle);
        GUILayout.Box(_previewIsValid ? _preview : "Build a style to preview it here.", _previewStyle, GUILayout.Height(70f), GUILayout.ExpandWidth(true));
        GUILayout.Label("Copy/paste text", _labelStyle);
        DrawGeneratedTextArea();
    }

    private void DrawGeneratedTextArea()
    {
        _scroll = GUILayout.BeginScrollView(_scroll, true, true, GUILayout.Height(_mode == 3 ? 116f : 90f));
        GUILayout.TextArea(_preview ?? string.Empty, _generatedTextStyle, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        GUILayout.EndScrollView();
    }

    private void DrawFooter(string inheritedTooltip)
    {
        HelpFrameLayout frame = BlueSageHelpFrame.Resolve(
            BlueSageHelpFrameKind.StyleHelper,
            _windowRect);
        BlueSageHelpFrame.BeginFooter(_footerBoxStyle, frame);
        bool wrapFooterActions = _windowRect.width < 1100f;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Detect Current", "Re-detect your current in-game name, status, and Spoons into this helper. It does not apply anything by itself."), _buttonStyle, GUILayout.Height(34f)))
        {
            CaptureUndo("Detect Current");
            ResetToCurrentPlayerState();
        }
        if (GUILayout.Button(new GUIContent("Undo", _hasUndo ? "Undo the last Style Helper apply, clear, template save/load, color pick, or local edit." : "No Style Helper action to undo yet."), _buttonStyle, GUILayout.Height(34f)))
        {
            UndoLastAction();
        }
        if (GUILayout.Button(new GUIContent("Redo", _redoHistory.Count > 0 ? "Redo the last undone Style Helper action." : "No Style Helper action to redo yet."), _buttonStyle, GUILayout.Height(34f)))
        {
            RedoLastAction();
        }
        if (wrapFooterActions)
        {
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
        }
        string applyLabel = _tab == 1 ? "Apply Status Styler" : "Apply Name Styler";
        string applyTip = _tab == 1 ? "Apply only the activity/status tag after your current name. Use Save Spoons for the Spoons tag." : "Apply the styled preview to your display name. Status and Spoons stay separate.";
        if (GUILayout.Button(new GUIContent(applyLabel, applyTip), _activeButtonStyle, GUILayout.Height(34f)))
        {
            if (_tab == 1 && _previewIsValid)
            {
                CaptureUndo("Apply Status Styler", restoreStatus: true);
                bool applied = false;
                _message = Plugin.Instance != null ? Plugin.Instance.TryApplyStatusFromStyleUi(null, _status, CleanColor(_statusColor), out applied) : "Style UI: plugin is not ready yet.";
                MarkApplyOutcome(applied, "Status Styler applied to your live name.", "Status Styler did not apply; fix the reported problem and try again.");
            }
            else if (_tab != 1 && _previewIsValid)
            {
                CaptureUndo("Apply Name Styler", restoreName: true);
                bool applied = false;
                _message = Plugin.Instance != null ? Plugin.Instance.TryApplyStyledNameFromStyleUi(_preview, out applied) : "Style UI: plugin is not ready yet.";
                MarkApplyOutcome(applied, "Name Styler applied to your live name.", "Name Styler did not apply; fix the reported problem and try again.");
            }
            else
            {
                _message = "Build a valid style before applying it to your name.";
                MarkDraft("Fix the draft before applying.");
            }
        }
        string copyLabel = _tab == 1 ? "Copy Status Style Text" : "Copy Name Style Text";
        if (GUILayout.Button(new GUIContent(copyLabel, "Copy the generated rich text to clipboard so you can paste it into chat, ID card, room, or profile fields."), _buttonStyle, GUILayout.Height(34f)))
        {
            if (_previewIsValid)
            {
                CaptureUndo(copyLabel);
                GUIUtility.systemCopyBuffer = _preview ?? string.Empty;
                _message = "Copied. Paste it into your name, ID card, room, status, or chat field.";
                Plugin.AddLocalNotificationWithToast("Style UI: copied styled text.");
                MarkAction("Copied current style text to clipboard.");
            }
            else
            {
                _message = "Build a valid style before copying.";
                MarkDraft("Fix the draft before copying.");
            }
        }
        if (GUILayout.Button(new GUIContent("Clear Draft", "Clear only the current editor draft. It does not change your live name/status; use Apply Status Styler or Clear Status for live status changes."), _buttonStyle, GUILayout.Height(34f)))
        {
            CaptureUndo("Clear Draft");
            if (_tab == 1)
            {
                _status = string.Empty;
            }
            else
            {
                _text = PlaceholderName;
                ClearDetectedNamePreview();
            }
            _preview = string.Empty;
            _previewIsValid = false;
            _message = _tab == 1 ? "Status draft cleared only. Use Clear Status to remove your live status." : "Draft cleared. Type your name or load a template.";
            MarkDraft("Draft cleared locally. Live name/status is unchanged until you apply.");
        }
        GUILayout.EndHorizontal();
        string stateLine = "State: " + _stateLabel + " — " + _stateDetail;
        BlueSageHelpFrame.DrawStateAndHelp(
            _footerTextStyle,
            frame,
            stateLine,
            BuildActiveTabFooterDescription(inheritedTooltip));
        BlueSageHelpFrame.EndFooter();
    }

    private string BuildActiveTabFooterDescription(string inheritedTooltip)
    {
        string activeDescription = _tab == 1 ? StatusStylerDescription
            : _tab == 2 ? TemplatesDescription
            : NameStylerDescription;
        string scopedTooltip = GUI.tooltip;
        bool inheritedFromThisWindow = string.Equals(inheritedTooltip, _lastOwnedFooterTooltip, StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(scopedTooltip))
        {
            _lastOwnedFooterTooltip = string.Empty;
            return activeDescription;
        }
        if (string.Equals(scopedTooltip, inheritedTooltip, StringComparison.Ordinal) && !inheritedFromThisWindow)
        {
            _lastOwnedFooterTooltip = string.Empty;
            return activeDescription;
        }

        _lastOwnedFooterTooltip = scopedTooltip;
        return scopedTooltip;
    }

    private void RebuildPreview()
    {
        if (_tab != 1 && UseDetectedNamePreviewIfAvailable())
        {
            return;
        }

        string text = GetUsableName();
        string richDraft = _tab == 1 ? (_status ?? string.Empty) : text;
        if (!RichTextDraftPolicy.TryValidate(richDraft, out string validationMessage))
        {
            _previewIsValid = false;
            _preview = validationMessage;
            _message = validationMessage;
            return;
        }
        if (_tab != 1 && text.IndexOf('<') >= 0)
        {
            _previewIsValid = text.Length <= Plugin.LockedMaxIdCardCharacters;
            _preview = text;
            _message = _previewIsValid
                ? $"Ready: {_preview.Length}/{Plugin.LockedMaxIdCardCharacters} chars. Custom Rich Text mode preserves exact balanced markup; verify uncommon TMP tags in preview/game."
                : $"Styled text is too long ({text.Length}/{Plugin.LockedMaxIdCardCharacters}).";
            return;
        }
        if (_tab == 1)
        {
            _preview = StatusSuffixPolicy.Build(
                _status ?? string.Empty,
                CleanColor(_statusColor),
                Plugin.StatusBrackets?.Value ?? "()");
            _previewIsValid = _preview.Length <= Plugin.LockedMaxIdCardCharacters;
            _message = _previewIsValid
                ? $"Ready: {_preview.Length}/{Plugin.LockedMaxIdCardCharacters} chars. Preview, Copy, and Apply use your saved status brackets."
                : $"Styled status is too long ({_preview.Length}/{Plugin.LockedMaxIdCardCharacters}).";
            return;
        }

        string args;
        if (_mode == 1)
        {
            string mode = _bold && _italic ? "bolditalic" : _bold ? "bold" : _italic ? "italic" : "color";
            args = mode + " #" + CleanColor(_colorA) + " " + text;
        }
        else if (_mode == 2)
        {
            args = "gradient #" + CleanColor(_colorA) + " #" + CleanColor(_colorB) + " " + text;
        }
        else
        {
            args = "gradient3 #" + CleanColor(_colorA) + " #" + CleanColor(_colorB) + " #" + CleanColor(_colorC) + " " + text;
        }

        RichTextStyleResult result = RichTextStyleBuilder.TryBuild(args, Plugin.LockedMaxIdCardCharacters);
        _previewIsValid = result.Success;
        _preview = result.Success ? result.GeneratedText : result.Message;
        _message = result.Success
            ? $"Ready: {_preview.Length}/{Plugin.LockedMaxIdCardCharacters} chars. Apply Name Styler or Copy Name Style Text."
            : result.Message;
    }

    private void HydrateCurrentName()
    {
        HydrateCurrentName(force: false);
    }

    private void HydrateCurrentName(bool force, DetectedDisplayNameState detectedState = null)
    {
        if (!force && !string.Equals(_text, PlaceholderName, StringComparison.Ordinal))
        {
            return;
        }

        string current = detectedState != null
            ? RichTextStyleBuilder.ToPlainStyleEditorText(detectedState.BaseName)
            : Plugin.Instance != null ? Plugin.Instance.GetCurrentPlayerPlainNameForStyleUi() : string.Empty;
        _detectedNamePreview = detectedState?.BaseName ?? (Plugin.Instance != null ? Plugin.Instance.GetCurrentPlayerStyledNameForStyleUi() : string.Empty);
        if (!string.IsNullOrWhiteSpace(_detectedNamePreview))
        {
            _text = _detectedNamePreview;
            HydrateDetectedStyleControls(_detectedNamePreview);
        }
        else if (!string.IsNullOrWhiteSpace(current))
        {
            _text = current;
        }
    }

    private void HydrateDetectedStyleControls(string styledName)
    {
        MatchCollection matches = Regex.Matches(styledName ?? string.Empty, @"<(?:color\s*=\s*#?|#)([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\s*>", RegexOptions.IgnoreCase);
        List<string> colors = new List<string>();
        foreach (Match match in matches)
        {
            if (RichTextStyleBuilder.TryNormalizeHex(match.Groups[1].Value, out string hex) && !colors.Contains(hex))
            {
                colors.Add(hex);
            }
        }
        if (colors.Count > 0)
        {
            _colorA = colors[0];
            _colorB = colors[Math.Min(colors.Count - 1, Math.Max(1, colors.Count / 2))];
            _colorC = colors[colors.Count - 1];
            _mode = DetectColorMode(colors);
        }
        _bold = Regex.IsMatch(styledName ?? string.Empty, @"<\s*b(?:\s|>)", RegexOptions.IgnoreCase);
        _italic = Regex.IsMatch(styledName ?? string.Empty, @"<\s*i(?:\s|>)", RegexOptions.IgnoreCase);
    }

    private void ResetToCurrentPlayerState()
    {
        ResetTransientEditorStateForLiveSync(resetTab: false, clearHistory: false);
        DetectedDisplayNameState detected = Plugin.Instance != null ? Plugin.Instance.GetCurrentDisplayNameStateForStyleUi() : null;
        HydrateCurrentName(force: true, detected);
        HydrateCurrentStatus(detected);
        HydrateSpoons(detected);
        _message = _tab == 1
            ? "Detected current status and Spoons. " + (detected?.DriftSummary ?? "No live player state was available yet.")
            : "Detected current name. " + (detected?.DriftSummary ?? "No live player state was available yet.");
        RebuildPreview();
        MarkSaved(detected?.DriftSummary ?? "Detected current in-game values. Edit a field to start a draft.");
    }

    private void ResetTransientEditorStateForLiveSync(bool resetTab, bool clearHistory = true)
    {
        if (resetTab)
        {
            _tab = 0;
        }

        _activeColorSlot = 0;
        _scroll = Vector2.zero;
        _contentScroll = Vector2.zero;
        if (clearHistory)
        {
            _undoHistory.Clear();
            _redoHistory.Clear();
        }
        _text = PlaceholderName;
        _colorA = DefaultColorA;
        _colorB = DefaultColorB;
        _colorC = DefaultColorC;
        _bold = false;
        _italic = false;
        _mode = 3;
        _detectedNamePreview = string.Empty;
        _preview = string.Empty;
        _previewIsValid = false;
    }

    private void HydrateCurrentStatus(DetectedDisplayNameState detectedState = null)
    {
        _status = detectedState?.StatusMessage ?? Plugin.StatusMessage?.Value ?? string.Empty;
        _statusColor = detectedState?.StatusColor ?? Plugin.StatusColor?.Value ?? "FFD45D";
    }

    private void HydrateSpoons(DetectedDisplayNameState detectedState = null)
    {
        _spoonCountText = detectedState != null
            ? detectedState.SpoonCount.ToString()
            : Plugin.SpoonCount?.Value.ToString() ?? "5";
        _spoonLabelText = detectedState?.SpoonLabel ?? Plugin.SpoonLabel?.Value ?? "sp";
    }

    private string GetUsableName()
    {
        string value = (_text ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? PlaceholderName : value;
    }

    private void SaveTemplate(int index)
    {
        CaptureUndo("Save Template " + index);
        string prefix = TemplatePrefix(index);
        PlayerPrefs.SetString(prefix + "Text", _text ?? string.Empty);
        PlayerPrefs.SetString(prefix + "ColorA", _colorA ?? string.Empty);
        PlayerPrefs.SetString(prefix + "ColorB", _colorB ?? string.Empty);
        PlayerPrefs.SetString(prefix + "ColorC", _colorC ?? string.Empty);
        PlayerPrefs.SetInt(prefix + "Mode", _mode);
        PlayerPrefs.SetInt(prefix + "Bold", _bold ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "Italic", _italic ? 1 : 0);
        PlayerPrefs.Save();
        _message = "Saved template " + index + ".";
        MarkSaved("Template " + index + " saved locally.");
    }

    private void LoadTemplate(int index)
    {
        CaptureUndo("Load Template " + index, restoreName: true);
        string prefix = TemplatePrefix(index);
        if (!PlayerPrefs.HasKey(prefix + "Text"))
        {
            _message = "Template " + index + " is empty. Detect Current loads live player data; Save " + index + " stores this slot.";
            MarkDraft("Template " + index + " is empty; live player data was left unchanged.");
            return;
        }

        _text = PlayerPrefs.GetString(prefix + "Text", PlaceholderName);
        ClearDetectedNamePreview();
        _colorA = PlayerPrefs.GetString(prefix + "ColorA", DefaultColorA);
        _colorB = PlayerPrefs.GetString(prefix + "ColorB", DefaultColorB);
        _colorC = PlayerPrefs.GetString(prefix + "ColorC", DefaultColorC);
        _mode = PlayerPrefs.GetInt(prefix + "Mode", 3);
        _bold = PlayerPrefs.GetInt(prefix + "Bold", 0) == 1;
        _italic = PlayerPrefs.GetInt(prefix + "Italic", 0) == 1;
        RebuildPreview();
        if (!_previewIsValid)
        {
            _message = "Template " + index + " loaded, but it cannot apply yet: " + _message;
            MarkDraft("Template " + index + " loaded into a draft that needs fixing.");
            return;
        }

        bool applied = false;
        string applyMessage = Plugin.Instance != null
            ? Plugin.Instance.TryApplyStyledNameFromStyleUi(_preview, out applied)
            : "Style UI: plugin is not ready yet.";
        _message = (applied ? "Loaded and applied template " : "Loaded template but could not apply it ") + index + ". " + applyMessage;
        MarkApplyOutcome(applied, "Template " + index + " loaded and applied to your live name.", "Template " + index + " remains a draft because the live apply failed.");
    }

    private void SaveSpoonsFromStyleUi()
    {
        if (Plugin.SpoonCount == null || Plugin.EnableSpoons == null)
        {
            _message = "Spoons config is not ready yet.";
            MarkDraft("Spoons could not save because config is not ready.");
            return;
        }

        if (!int.TryParse((_spoonCountText ?? string.Empty).Trim(), out int requested))
        {
            _message = "Spoons must be a number from 0 to 5.";
            MarkDraft("Fix Spoons to a number from 0 to 5.");
            return;
        }

        int clamped = Mathf.Clamp(requested, 0, 5);
        string label = new string((_spoonLabelText ?? string.Empty).Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').Take(12).ToArray());
        if (string.IsNullOrWhiteSpace(label))
        {
            _message = "Custom label must use 1-12 letters/numbers, '-' or '_'. Use 'sp' for the default.";
            MarkDraft("Fix the custom Spoons/mood label before saving.");
            return;
        }
        bool applied = false;
        _message = Plugin.Instance != null
            ? Plugin.Instance.TryApplySpoonsFromStyleUi(clamped, label, true, DateTime.UtcNow.ToString("O"), out applied)
            : "Spoons config is not ready yet.";
        if (applied)
        {
            _spoonCountText = clamped.ToString();
        }
        MarkApplyOutcome(applied, "Spoons saved and applied to your live name.", "Spoons were not saved; fix the reported problem and try again.");
    }

    private void CaptureUndo(string action, bool restoreName = false, bool restoreStatus = false, bool restoreSpoons = false)
    {
        _undoHistory.Push(CaptureSnapshot(action, restoreName, restoreStatus, restoreSpoons));
        while (_undoHistory.Count > 50)
        {
            StyleHelperSnapshot[] snapshots = _undoHistory.ToArray();
            _undoHistory.Clear();
            for (int i = Math.Min(49, snapshots.Length - 1); i >= 0; i--)
            {
                _undoHistory.Push(snapshots[i]);
            }
        }
        _redoHistory.Clear();
    }

    private StyleHelperSnapshot CaptureSnapshot(string action, bool restoreName = false, bool restoreStatus = false, bool restoreSpoons = false)
    {
        return new StyleHelperSnapshot
        {
            Action = action ?? "Action",
            Text = _text,
            ColorA = _colorA,
            ColorB = _colorB,
            ColorC = _colorC,
            Status = _status,
            StatusColorDraft = _statusColor,
            Preview = _preview,
            Message = _message,
            PreviewIsValid = _previewIsValid,
            DetectedNamePreview = _detectedNamePreview,
            Bold = _bold,
            Italic = _italic,
            Mode = _mode,
            Tab = _tab,
            ActiveColorSlot = _activeColorSlot,
            RestoreName = restoreName,
            RestoreStatus = restoreStatus,
            PlayerName = Plugin.Instance != null ? Plugin.Instance.GetCurrentPlayerDisplayNameForStyleUi() : string.Empty,
            PlayerStatus = Plugin.StatusMessage?.Value ?? string.Empty,
            PlayerStatusColor = Plugin.StatusColor?.Value ?? "FFD45D",
            SpoonCountText = _spoonCountText,
            SpoonLabelText = _spoonLabelText,
            StateLabel = _stateLabel,
            StateDetail = _stateDetail,
            RestoreSpoons = restoreSpoons,
            SpoonsEnabled = Plugin.EnableSpoons != null && Plugin.EnableSpoons.Value,
            SpoonCount = Plugin.SpoonCount?.Value ?? 5,
            SavedSpoonLabel = Plugin.SpoonLabel?.Value ?? "sp",
            SpoonsUpdatedUtc = Plugin.SpoonsUpdatedUtc?.Value ?? string.Empty
        };
    }

    private bool UndoLastAction()
    {
        if (!_hasUndo)
        {
            _message = "Nothing to undo yet.";
            return false;
        }

        _redoHistory.Push(CaptureSnapshot("Redo " + _undoHistory.Peek().Action, _undoHistory.Peek().RestoreName, _undoHistory.Peek().RestoreStatus, _undoHistory.Peek().RestoreSpoons));
        StyleHelperSnapshot snapshot = _undoHistory.Pop();
        return RestoreSnapshot(snapshot, "Undid ");
    }

    private bool RedoLastAction()
    {
        if (_redoHistory.Count == 0)
        {
            _message = "Nothing to redo yet.";
            return false;
        }

        StyleHelperSnapshot pending = _redoHistory.Peek();
        _undoHistory.Push(CaptureSnapshot("Undo " + pending.Action, pending.RestoreName, pending.RestoreStatus, pending.RestoreSpoons));
        return RestoreSnapshot(_redoHistory.Pop(), "Redid ");
    }

    private bool RestoreSnapshot(StyleHelperSnapshot snapshot, string verb)
    {
        _text = snapshot.Text;
        _colorA = snapshot.ColorA;
        _colorB = snapshot.ColorB;
        _colorC = snapshot.ColorC;
        _status = snapshot.Status;
        _statusColor = snapshot.StatusColorDraft;
        _preview = snapshot.Preview;
        _detectedNamePreview = snapshot.DetectedNamePreview;
        _message = snapshot.Message;
        _previewIsValid = snapshot.PreviewIsValid;
        _bold = snapshot.Bold;
        _italic = snapshot.Italic;
        _mode = snapshot.Mode;
        _tab = snapshot.Tab;
        _activeColorSlot = snapshot.ActiveColorSlot;
        _spoonCountText = snapshot.SpoonCountText;
        _spoonLabelText = snapshot.SpoonLabelText;
        _stateLabel = snapshot.StateLabel;
        _stateDetail = snapshot.StateDetail;

        bool restored = true;
        string restoreMessage = verb + snapshot.Action + ".";
        if (snapshot.RestoreSpoons && Plugin.Instance != null)
        {
            string spoonMessage = Plugin.Instance.TryApplySpoonsFromStyleUi(
                snapshot.SpoonCount,
                snapshot.SavedSpoonLabel ?? "sp",
                snapshot.SpoonsEnabled,
                snapshot.SpoonsUpdatedUtc,
                out bool spoonsApplied);
            restoreMessage += " " + spoonMessage;
            restored &= spoonsApplied;
            if (spoonsApplied)
            {
                _spoonCountText = Mathf.Clamp(snapshot.SpoonCount, 0, 5).ToString();
                _spoonLabelText = snapshot.SavedSpoonLabel ?? "sp";
            }
        }

        if (snapshot.RestoreName && Plugin.Instance != null && !string.IsNullOrWhiteSpace(snapshot.PlayerName))
        {
            restoreMessage += " " + Plugin.Instance.TryApplyStyledNameFromStyleUi(snapshot.PlayerName, out bool nameApplied);
            restored &= nameApplied;
        }

        if (snapshot.RestoreStatus && Plugin.Instance != null)
        {
            string statusMessage = string.IsNullOrWhiteSpace(snapshot.PlayerStatus)
                ? Plugin.Instance.TryClearStatusFromStyleUi(out bool statusCleared)
                : Plugin.Instance.TryApplyStatusFromStyleUi(null, snapshot.PlayerStatus, snapshot.PlayerStatusColor, out statusCleared);
            restoreMessage += " " + statusMessage;
            restored &= statusCleared;
        }

        RebuildPreview();
        _message = restoreMessage;
        MarkApplyOutcome(restored, verb.Trim() + " restored the saved/live state for that action.", verb.Trim() + " could not restore every live value; rejected values stayed unchanged.");
        return restored;
    }

    private void UndoSpoonsLastAction()
    {
        if (!_hasUndo || !_undoHistory.Peek().RestoreSpoons)
        {
            _message = "Nothing to undo for Spoons yet.";
            MarkDraft("Spoons undo needs a prior Save Spoons, Show Tag, or Hide Tag action.");
            return;
        }

        bool restored = UndoLastAction();
        MarkApplyOutcome(restored, "Spoons undo restored only the last Spoons state.", "Spoons undo could not apply; saved/live values stayed unchanged.");
    }

    private static string TemplatePrefix(int index)
    {
        return "BlueSageQoL.StyleTemplate." + index + ".";
    }

    private string GetColorSlot(int slot)
    {
        if (slot == 1)
        {
            return _colorB;
        }

        if (slot == 2)
        {
            return _colorC;
        }

        return _colorA;
    }

    private void MarkDraft(string detail)
    {
        _stateLabel = "Draft";
        _stateDetail = detail ?? "Changes are local until you apply or save.";
    }

    private void MarkSaved(string detail)
    {
        _stateLabel = "Saved";
        _stateDetail = detail ?? "Ready.";
    }

    private void MarkApplyOutcome(bool applied, string savedDetail, string failedDetail)
    {
        if (applied)
        {
            MarkSaved(savedDetail);
        }
        else
        {
            MarkDraft(failedDetail);
        }
    }

    private void MarkAction(string detail)
    {
        _stateLabel = "Done";
        _stateDetail = detail ?? "Done.";
    }

    private void SetColorSlot(int slot, string value)
    {
        if (slot == 1)
        {
            ClearDetectedNamePreview();
            _colorB = value;
        }
        else if (slot == 2)
        {
            ClearDetectedNamePreview();
            _colorC = value;
        }
        else
        {
            ClearDetectedNamePreview();
            _colorA = value;
        }
    }

    private bool UseDetectedNamePreviewIfAvailable()
    {
        if (string.IsNullOrWhiteSpace(_detectedNamePreview))
        {
            return false;
        }

        string detectedPlain = RichTextStyleBuilder.ToPlainStyleEditorText(_detectedNamePreview);
        if (!string.Equals(_detectedNamePreview, GetUsableName(), StringComparison.Ordinal) &&
            !string.Equals(detectedPlain, GetUsableName(), StringComparison.Ordinal))
        {
            return false;
        }

        _preview = _detectedNamePreview;
        string validationMessage = string.Empty;
        _previewIsValid = _preview.Length <= Plugin.LockedMaxIdCardCharacters && RichTextDraftPolicy.TryValidate(_preview, out validationMessage);
        _message = _previewIsValid
            ? "Detected current live name style."
            : (_preview.Length > Plugin.LockedMaxIdCardCharacters
                ? $"Detected current live name style is too long ({_preview.Length}/{Plugin.LockedMaxIdCardCharacters}). Shorten before applying."
                : validationMessage);
        return true;
    }

    private static int DetectColorMode(IReadOnlyList<string> colors)
    {
        if (colors == null || colors.Count <= 1)
        {
            return 1;
        }

        if (colors.Count == 2)
        {
            return 2;
        }

        if (!TryParseRgb(colors[0], out int startR, out int startG, out int startB) ||
            !TryParseRgb(colors[colors.Count - 1], out int endR, out int endG, out int endB))
        {
            return 3;
        }

        for (int index = 1; index < colors.Count - 1; index++)
        {
            if (!TryParseRgb(colors[index], out int actualR, out int actualG, out int actualB))
            {
                return 3;
            }

            double ratio = index / (double)(colors.Count - 1);
            int expectedR = (int)Math.Round(startR + ((endR - startR) * ratio));
            int expectedG = (int)Math.Round(startG + ((endG - startG) * ratio));
            int expectedB = (int)Math.Round(startB + ((endB - startB) * ratio));
            if (Math.Abs(actualR - expectedR) > 1 || Math.Abs(actualG - expectedG) > 1 || Math.Abs(actualB - expectedB) > 1)
            {
                return 3;
            }
        }

        return 2;
    }

    private static bool TryParseRgb(string value, out int red, out int green, out int blue)
    {
        red = green = blue = 0;
        if (!RichTextStyleBuilder.TryNormalizeHex(value, out string hex))
        {
            return false;
        }

        red = Convert.ToInt32(hex.Substring(0, 2), 16);
        green = Convert.ToInt32(hex.Substring(2, 2), 16);
        blue = Convert.ToInt32(hex.Substring(4, 2), 16);
        return true;
    }

    private void ClearDetectedNamePreview()
    {
        _detectedNamePreview = string.Empty;
    }

    private struct StyleHelperSnapshot
    {
        public string Action;
        public string Text;
        public string ColorA;
        public string ColorB;
        public string ColorC;
        public string Status;
        public string StatusColorDraft;
        public string Preview;
        public string Message;
        public bool PreviewIsValid;
        public string DetectedNamePreview;
        public bool Bold;
        public bool Italic;
        public int Mode;
        public int Tab;
        public int ActiveColorSlot;
        public bool RestoreName;
        public bool RestoreStatus;
        public string PlayerName;
        public string PlayerStatus;
        public string PlayerStatusColor;
        public string SpoonCountText;
        public string SpoonLabelText;
        public string StateLabel;
        public string StateDetail;
        public bool RestoreSpoons;
        public bool SpoonsEnabled;
        public int SpoonCount;
        public string SavedSpoonLabel;
        public string SpoonsUpdatedUtc;
    }

    private static string CleanColor(string value)
    {
        string cleaned = (value ?? string.Empty).Trim().TrimStart('#');
        return string.IsNullOrWhiteSpace(cleaned) ? "0E6EED" : cleaned;
    }

    private static Color HexToColor(string value, Color fallback)
    {
        if (!RichTextStyleBuilder.TryNormalizeHex(value, out string hex))
        {
            return fallback;
        }

        float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
        float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
        float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
        return new Color(r, g, b, 1f);
    }

    private void ClampWindowToScreen()
    {
        WindowFitResult fit = BlueSageWindowCoordinator.ResolveForFrame(
            BlueSagePublicWindow.StyleHelper,
            _windowRect,
            MinWindowWidth,
            MinWindowHeight,
            MenuDisplayScaleRuntime.LogicalScreenWidth,
            MenuDisplayScaleRuntime.LogicalScreenHeight);
        _windowRect = new Rect(fit.X, fit.Y, fit.Width, fit.Height);
    }

    private void OnDisable()
    {
        BlueSageWindowCoordinator.SetVisible(BlueSagePublicWindow.StyleHelper, false);
        BlueSageWindowHoverScope.UnregisterWindow(47057);
    }

    private void DrawResizeGrip()
    {
        Rect grip = new Rect(_windowRect.width - ResizeGripSize - 8f, _windowRect.height - ResizeGripSize - 8f, ResizeGripSize, ResizeGripSize);
        GUI.Box(grip, new GUIContent("↘", "Drag this larger corner handle to resize the Style Helper."), _buttonStyle);
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
            string.Equals(_lastThemeKey, _theme.Key, StringComparison.Ordinal) &&
            _renderedTextureGeneration == BlueSageUiTheme.RuntimeTextureGeneration &&
            BlueSageUiTheme.AreRuntimeTexturesAlive())
        {
            return;
        }

        _lastThemeKey = _theme.Key;
        Texture2D windowTexture = BlueSageUiTheme.GetSolidTexture(_theme.Window);
        Texture2D panelTexture = BlueSageUiTheme.GetSolidTexture(_theme.Panel);
        _windowBackgroundTexture = windowTexture;

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            padding = new RectOffset(16, 16, 46, 14),
            normal = { background = windowTexture, textColor = _theme.LabelText },
            onNormal = { background = windowTexture, textColor = _theme.LabelText }
        };
        _headerBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = BlueSageUiTheme.GetSolidTexture(_theme.Header) } };
        _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = _theme.HeaderText } };
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = _theme.LabelText } };
        _smallStyle = new GUIStyle(GUI.skin.label) { richText = false, fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = Color.Lerp(_theme.SmallText, _theme.LabelText, 0.35f) } };
        _buttonStyle = BlueSageUiTheme.CreateNeutralButtonStyle(GUI.skin.button, _theme, 14);
        _activeButtonStyle = BlueSageUiTheme.CreateStateButtonStyle(_buttonStyle, _theme.ActiveAccent, _theme.FieldFocusedBorder, _theme.ActiveStateText, 14);
        _boxStyle = new GUIStyle(GUI.skin.box) { stretchHeight = false, padding = new RectOffset(12, 12, 10, 10), normal = { background = panelTexture, textColor = _theme.LabelText } };
        _footerBoxStyle = new GUIStyle(GUI.skin.box) { stretchHeight = false, padding = new RectOffset(12, 12, 8, 8), normal = { background = BlueSageUiTheme.GetSolidTexture(_theme.FooterBackground), textColor = _theme.FooterText } };
        _footerTextStyle = new GUIStyle(GUI.skin.label) { richText = false, fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = _theme.FooterText } };
        _textFieldStyle = BlueSageUiTheme.CreateTextInputStyle(GUI.skin.textField, _theme, 15, wordWrap: true);
        _generatedTextStyle = new GUIStyle(_textFieldStyle) { wordWrap = false };
        _previewStyle = new GUIStyle(GUI.skin.box) { richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter, fontSize = 17, normal = { background = BlueSageUiTheme.GetSolidTexture(_theme.PreviewBackground), textColor = _theme.PreviewText } };
        _swatchButtonStyle = BlueSageUiTheme.CreateSwatchButtonStyle(GUI.skin.button, 13);
        _renderedTextureGeneration = BlueSageUiTheme.RuntimeTextureGeneration;
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

}
