using BlueSage.QoLTweaks.Core;
using PurrNet;
using PurrNet.Modules;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class PerformanceOverlayController : MonoBehaviour
{
    private const float OverlayHeight = 28f;
    private readonly PerformanceOverlayState _state = new PerformanceOverlayState();
    private string _label = "FPS --  PING --  RAM --";
    private float _nextRefreshAt;
    private Rect _rect;
    private bool _dragging;
    private Vector2 _dragOffset;
    private GUIStyle _style;
    private GUIStyle _buttonStyle;
    private GUIStyle _selectedButtonStyle;
    private Texture2D _background;

    private const string ModeHelp = "Vanilla statistics keep running. BlueSage classifies new activity as Work or Relax.";

    private void Update()
    {
        _state.SampleFrame(Time.unscaledDeltaTime);
        if (Plugin.EnablePerformanceOverlay?.Value != true || Time.unscaledTime < _nextRefreshAt)
        {
            return;
        }

        PerformanceOverlaySettings settings = ReadSettings();
        _label = _state.BuildLabel(ReadActiveSessionPing(), ProcessRuntimeMetricsCapture.ReadWorkingSetBytes());
        _nextRefreshAt = Time.unscaledTime + (float)settings.RefreshSeconds;
    }

    private void OnGUI()
    {
        if (Plugin.EnablePerformanceOverlay?.Value != true)
        {
            return;
        }

        PerformanceOverlaySettings settings = ReadSettings();
        EnsureStyle(settings.FontSize);
        WorkRelaxOverlayLayout layout = WorkRelaxOverlayLayoutPolicy.ResolveForViewport(
            (float)settings.Width,
            Screen.width,
            OverlayHeight);
        if (!_dragging)
        {
            PerformanceOverlayPosition position = PerformanceOverlayPositionPolicy.ClampToScreen(
                settings.OffsetX,
                settings.OffsetY,
                layout.Width,
                layout.Height,
                Screen.width,
                Screen.height);
            _rect = new Rect((float)position.OffsetX, (float)position.OffsetY, layout.Width, layout.Height);
        }
        else
        {
            _rect.width = layout.Width;
            _rect.height = layout.Height;
        }

        Rect qolMenuRect = ToScreenRect(layout.QolMenu);
        Rect styleMenuRect = ToScreenRect(layout.StyleMenu);
        Rect labelRect = ToScreenRect(layout.Metrics);
        Rect workRect = ToScreenRect(layout.Work);
        Rect relaxRect = ToScreenRect(layout.Relax);
        Rect closeRect = ToScreenRect(layout.Close);
        HandleDrag(settings, labelRect);
        if (GUI.Button(qolMenuRect, new GUIContent(WorkRelaxOverlayLayoutPolicy.QolMenuLabel, "Open or close the QoL menu (same as its hotkey)."), _buttonStyle))
        {
            Plugin.Instance?.ToggleQolMenuFromOverlayButton();
        }
        if (GUI.Button(styleMenuRect, new GUIContent(WorkRelaxOverlayLayoutPolicy.StyleMenuLabel, "Open or close the Style Helper (same as its hotkey)."), _buttonStyle))
        {
            Plugin.Instance?.ToggleStyleUiFromOverlayButton();
        }
        GUI.Label(labelRect, _label, _style);
        WorkRelaxMode currentMode = WorkRelaxRuntimeController.TryGetCurrentMode(out WorkRelaxMode selectedMode)
            ? selectedMode
            : WorkRelaxMode.Work;
        if (GUI.Button(workRect, new GUIContent("WORK", ModeHelp),
                currentMode == WorkRelaxMode.Work ? _selectedButtonStyle : _buttonStyle))
        {
            WorkRelaxRuntimeController.SetMode(WorkRelaxMode.Work);
        }
        if (GUI.Button(relaxRect, new GUIContent("RELAX", ModeHelp),
                currentMode == WorkRelaxMode.Relax ? _selectedButtonStyle : _buttonStyle))
        {
            WorkRelaxRuntimeController.SetMode(WorkRelaxMode.Relax);
        }
        if (GUI.Button(closeRect, "×"))
        {
            Plugin.EnablePerformanceOverlay.Value = false;
            Plugin.Instance?.SaveConfigFromQolMenu();
            _dragging = false;
            Plugin.Log?.LogInfo("Public QoL performance overlay hidden from its in-bar shortcut; reopen it from QoL Menu > Toggles > Personalization.");
        }
    }

    private static int? ReadActiveSessionPing()
    {
        try
        {
            NetworkManager manager = NetworkManager.main;
            if (manager == null ||
                !manager.TryGetModule<TickManager>(false, out TickManager tickManager) ||
                tickManager == null)
            {
                return null;
            }

            return TickManagerPingPolicy.FromRttSeconds(tickManager.rtt);
        }
        catch
        {
            return null;
        }
    }

    private static PerformanceOverlaySettings ReadSettings() =>
        PerformanceOverlaySettingsPolicy.Normalize(
            Plugin.PerformanceOverlayRefreshSeconds?.Value ?? 0.25f,
            Plugin.PerformanceOverlayFontSize?.Value ?? 13,
            Plugin.PerformanceOverlayOffsetX?.Value ?? PublicFirstRunDefaults.Current.PerformanceOverlayOffsetX,
            Plugin.PerformanceOverlayOffsetY?.Value ?? PublicFirstRunDefaults.Current.PerformanceOverlayOffsetY,
            Plugin.PerformanceOverlayWidth?.Value ?? 320f);

    private void EnsureStyle(int fontSize)
    {
        if (_style == null)
        {
            _background = new Texture2D(1, 1);
            _background.name = "BlueSagePublicPerformanceOverlayBackground";
            _background.hideFlags = HideFlags.HideAndDontSave;
            _background.SetPixel(0, 0, new Color(0.025f, 0.055f, 0.075f, 0.82f));
            _background.Apply(false, true);
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { background = _background, textColor = new Color(0.72f, 0.92f, 0.94f, 1f) }
            };
            _style.padding = new RectOffset(6, 6, 2, 2);
        }

        _style.fontSize = fontSize;
        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            _selectedButtonStyle = new GUIStyle(_buttonStyle);
            _selectedButtonStyle.normal = GUI.skin.button.onNormal;
            _selectedButtonStyle.hover = GUI.skin.button.onHover;
            _selectedButtonStyle.active = GUI.skin.button.onActive;
            _selectedButtonStyle.focused = GUI.skin.button.onFocused;
        }

        int buttonFontSize = Mathf.Max(10, fontSize - 2);
        _buttonStyle.fontSize = buttonFontSize;
        _selectedButtonStyle.fontSize = buttonFontSize;
    }

    private Rect ToScreenRect(WorkRelaxOverlayRect relative) =>
        new Rect(_rect.x + relative.X, _rect.y + relative.Y, relative.Width, relative.Height);

    private void HandleDrag(PerformanceOverlaySettings settings, Rect dragRect)
    {
        Event current = Event.current;
        if (current == null)
        {
            return;
        }

        if (current.type == EventType.MouseDown && current.button == 0 && dragRect.Contains(current.mousePosition))
        {
            _dragging = true;
            _dragOffset = current.mousePosition - _rect.position;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && current.button == 0 && _dragging)
        {
            MoveTo(current.mousePosition - _dragOffset, settings);
            current.Use();
        }
        else if (current.type == EventType.MouseUp && current.button == 0 && _dragging)
        {
            MoveTo(_rect.position, settings);
            Plugin.PerformanceOverlayOffsetX.Value = _rect.x;
            Plugin.PerformanceOverlayOffsetY.Value = _rect.y;
            Plugin.Instance?.SaveConfigFromQolMenu();
            _dragging = false;
            current.Use();
        }
    }

    private void MoveTo(Vector2 requested, PerformanceOverlaySettings settings)
    {
        PerformanceOverlayPosition position = PerformanceOverlayPositionPolicy.ClampToScreen(
            requested.x, requested.y, _rect.width, _rect.height, Screen.width, Screen.height);
        _rect.position = new Vector2((float)position.OffsetX, (float)position.OffsetY);
    }

    private void OnDestroy()
    {
        if (_background != null)
        {
            Destroy(_background);
            _background = null;
        }
    }
}
