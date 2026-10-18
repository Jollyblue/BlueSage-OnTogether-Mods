using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks.Patches;

internal sealed class BlueSagePlayerBadgeMarker :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const int TooltipMinimumFontSize = 16;
    private const float TooltipBackgroundAlpha = 0.97f;

    private static Texture2D _tooltipBackground;
    private static GUIStyle _tooltipStyle;

    internal string BoundSteamId = string.Empty;
    internal bool IsRetired;
    internal PlayerItemController OwnerRow;
    internal int OwnerRowInstanceId;
    internal int NameAnchorInstanceId;
    internal int Generation;
    internal TextMeshProUGUI Label;
    internal TextMeshProUGUI HostLabel;
    internal TMP_Text NativeName;
    internal string TooltipText = string.Empty;

    private bool _hovered;
    private bool _nativeNameReplacementActive;
    private string _nativeNameBaseline = string.Empty;
    private string _nativeNameApplied = string.Empty;
    private readonly PlayerBadgeNativeTextPresentationLease
        _nativePresentationLease =
            new PlayerBadgeNativeTextPresentationLease();
    private readonly PlayerBadgeLayoutMeasurementCache
        _layoutCache =
            new PlayerBadgeLayoutMeasurementCache();
    private readonly PlayerRowGeometryReadinessPolicy
        _geometryReadiness =
            new PlayerRowGeometryReadinessPolicy();

    internal bool HasLivePresentation()
    {
        return NativeName != null &&
               Label != null &&
               HostLabel != null;
    }

    internal bool HasLivePresentation(
        TMP_Text expectedNativeName)
    {
        return expectedNativeName != null &&
               HasLivePresentation() &&
               ReferenceEquals(
                   NativeName,
                   expectedNativeName);
    }

    internal bool TryGetCachedLayout(
        PlayerBadgeLayoutCacheKey key,
        out PlayerBadgeLayout layout)
    {
        if (!_layoutCache.TryGet(key, out layout))
        {
            return false;
        }

        if (layout.NativeNameReservation <= 0f)
        {
            return !_nativePresentationLease.HasActiveLease;
        }

        return NativeName != null &&
               _nativePresentationLease.MatchesApplied(
                   CaptureNativePresentation(NativeName));
    }

    internal void StoreCachedLayout(
        PlayerBadgeLayoutCacheKey key,
        PlayerBadgeLayout layout)
    {
        _layoutCache.Store(key, layout);
    }

    internal void BeginRowLifecycle()
    {
        _geometryReadiness.Reset();
    }

    internal bool ObserveGeometry(
        float width,
        int geometryFingerprint)
    {
        return _geometryReadiness.Observe(
            width,
            geometryFingerprint);
    }

    internal void ResetGeometryReadiness()
    {
        _geometryReadiness.Reset();
    }

    internal void BindNativeName(TMP_Text nativeName)
    {
        if (ReferenceEquals(NativeName, nativeName))
        {
            return;
        }

        RestoreNativePresentation();
        RestoreNativeName();
        NativeName = nativeName;
    }

    internal void ApplyNativePresentationReservation(
        float rightReservation)
    {
        if (NativeName == null || rightReservation <= 0f)
        {
            RestoreNativePresentation();
            return;
        }

        PlayerBadgeNativeTextPresentation applied =
            _nativePresentationLease.Apply(
                CaptureNativePresentation(NativeName),
                rightReservation,
                (int)TextWrappingModes.NoWrap,
                (int)TextOverflowModes.Ellipsis);
        ApplyNativePresentation(
            NativeName,
            applied);
    }

    internal void RestoreNativePresentation()
    {
        if (NativeName == null)
        {
            _nativePresentationLease.Restore(default);
            return;
        }

        PlayerBadgeNativeTextPresentation restored =
            _nativePresentationLease.Restore(
                CaptureNativePresentation(NativeName));
        ApplyNativePresentation(
            NativeName,
            restored);
    }

    internal string GetNativeNameSourceText()
    {
        if (NativeName == null)
        {
            return string.Empty;
        }

        string current = NativeName.text ?? string.Empty;
        if (!_nativeNameReplacementActive)
        {
            return current;
        }

        if (string.Equals(
                current,
                _nativeNameApplied,
                System.StringComparison.Ordinal))
        {
            return _nativeNameBaseline;
        }

        ClearNativeNameReplacementState();
        return current;
    }

    internal void ApplyNativeNameReplacement(string replacement)
    {
        if (NativeName == null ||
            string.IsNullOrWhiteSpace(replacement))
        {
            RestoreNativeName();
            return;
        }

        string source = GetNativeNameSourceText();
        if (string.Equals(
                source,
                replacement,
                System.StringComparison.Ordinal))
        {
            return;
        }

        _nativeNameBaseline = source;
        _nativeNameApplied = replacement;
        _nativeNameReplacementActive = true;
        NativeName.text = replacement;
    }

    internal void RestoreNativeName()
    {
        if (_nativeNameReplacementActive &&
            NativeName != null &&
            string.Equals(
                NativeName.text,
                _nativeNameApplied,
                System.StringComparison.Ordinal))
        {
            NativeName.text = _nativeNameBaseline;
        }

        ClearNativeNameReplacementState();
    }

    private void ClearNativeNameReplacementState()
    {
        _nativeNameReplacementActive = false;
        _nativeNameBaseline = string.Empty;
        _nativeNameApplied = string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = !string.IsNullOrWhiteSpace(TooltipText);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
    }

    private void OnDisable()
    {
        _hovered = false;
        ResetGeometryReadiness();
        RestoreNativePresentation();
        RestoreNativeName();
    }

    private void OnGUI()
    {
        if (!_hovered || string.IsNullOrWhiteSpace(TooltipText))
        {
            return;
        }

        GUIContent content = new GUIContent(TooltipText);
        GUIStyle style = GetTooltipStyle();
        float scale = Mathf.Clamp(Screen.height / 1080f, 1f, 1.35f);
        float requestedWidth = Mathf.Clamp(340f * scale, 300f, 520f);
        float width = BadgeTooltipLayoutPolicy.ResolveWidth(
            Screen.width,
            requestedWidth);
        float measuredHeight = style.CalcHeight(content, width);
        float pointerY = Screen.height - Input.mousePosition.y;
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            Screen.width,
            Screen.height,
            Input.mousePosition.x,
            pointerY,
            width,
            measuredHeight,
            scale);
        GUI.Box(
            new Rect(layout.X, layout.Y, layout.Width, layout.Height),
            content,
            style);
    }

    private static GUIStyle GetTooltipStyle()
    {
        if (_tooltipBackground == null)
        {
            _tooltipBackground = new Texture2D(1, 1);
            _tooltipBackground.name = "BlueSage_RoleTooltip_Background";
            _tooltipBackground.SetPixel(
                0,
                0,
                new Color(0.025f, 0.045f, 0.08f, TooltipBackgroundAlpha));
            _tooltipBackground.Apply();
        }

        if (_tooltipStyle == null)
        {
            _tooltipStyle = new GUIStyle
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(16, 16, 12, 12),
                border = new RectOffset(1, 1, 1, 1)
            };
            _tooltipStyle.normal.background = _tooltipBackground;
            _tooltipStyle.normal.textColor = Color.white;
        }

        float scale = Mathf.Clamp(Screen.height / 1080f, 1f, 1.35f);
        _tooltipStyle.fontSize = Mathf.Clamp(
            Mathf.RoundToInt(TooltipMinimumFontSize * scale),
            TooltipMinimumFontSize,
            22);
        return _tooltipStyle;
    }

    private void OnDestroy()
    {
        ResetGeometryReadiness();
        RestoreNativePresentation();
        RestoreNativeName();
        PlayerBadgeController.UnregisterOwnedMarker(this);
    }

    private static PlayerBadgeNativeTextPresentation
        CaptureNativePresentation(TMP_Text nativeName)
    {
        Vector4 margin = nativeName.margin;
        return new PlayerBadgeNativeTextPresentation(
            margin.x,
            margin.y,
            margin.z,
            margin.w,
            (int)nativeName.textWrappingMode,
            (int)nativeName.overflowMode);
    }

    private static void ApplyNativePresentation(
        TMP_Text nativeName,
        PlayerBadgeNativeTextPresentation presentation)
    {
        nativeName.margin = new Vector4(
            presentation.MarginLeft,
            presentation.MarginTop,
            presentation.MarginRight,
            presentation.MarginBottom);
        nativeName.textWrappingMode =
            (TextWrappingModes)presentation.WrappingMode;
        nativeName.overflowMode =
            (TextOverflowModes)presentation.OverflowMode;
    }
}
