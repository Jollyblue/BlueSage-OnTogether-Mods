using System;
using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UiOutline = UnityEngine.UI.Outline;
using UiShadow = UnityEngine.UI.Shadow;
using UnityObject = UnityEngine.Object;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TextChannelManager))]
internal static class ChatReadabilityPatch
{
    private static readonly Color DarkOutlineColor = new Color(0f, 0f, 0f, 0.95f);
    private static readonly Color LightOutlineColor = new Color(1f, 1f, 1f, 0.92f);
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.72f);
    private const float MinTmpOutlineWidth = 0.08f;
    private const float MaxTmpOutlineWidth = 0.22f;
    private const float ScreenClampMargin = 8f;
    private const float MinChatWindowBaselineSize = 24f;
    private static readonly Vector2 MaxOutlineDistance = new Vector2(1.65f, -1.65f);
    private static readonly Vector2 MaxShadowDistance = new Vector2(2.25f, -2.25f);
    private static readonly Dictionary<int, BackdropState> OriginalBackdropColors = new Dictionary<int, BackdropState>();
    private static readonly Dictionary<int, GameObject> OwnedBackdropOverlays = new Dictionary<int, GameObject>();
    private static readonly Dictionary<int, TextReadabilityState> OriginalTextStates = new Dictionary<int, TextReadabilityState>();
    private static readonly Dictionary<int, ChatSurfaceScaleState> OriginalSurfaceScales = new Dictionary<int, ChatSurfaceScaleState>();
    private static readonly Dictionary<int, ChatTextFontSizeState> OriginalFontSizes = new Dictionary<int, ChatTextFontSizeState>();
    private static readonly Dictionary<int, ChatSurfaceWindowSizeState> OriginalSurfaceWindowSizes = new Dictionary<int, ChatSurfaceWindowSizeState>();
    private static readonly List<Transform> ScratchChatSurfaces = new List<Transform>(6);
    private static int _refreshRequestVersion;
    private static int _lastLoggedWindowPercent = -1;
    private static int _lastLoggedWindowApplied = -1;

    internal static int RefreshRequestVersion => _refreshRequestVersion;

    private sealed class TextReadabilityState
    {
        public TextMeshProUGUI Text;
        public bool HadOutline;
        public Color OutlineColor;
        public Vector2 OutlineDistance;
        public bool OutlineUseGraphicAlpha;
        public bool HadShadow;
        public Color ShadowColor;
        public Vector2 ShadowDistance;
        public bool ShadowUseGraphicAlpha;
        public Material OriginalFontMaterial;
        public Material AppliedFontMaterial;
        public int LastAppliedSignature;
    }

    private sealed class BackdropState
    {
        public Image Image;
        public Color OriginalColor;
    }

    private sealed class ChatSurfaceScaleState
    {
        public Transform Transform;
        public Vector3 OriginalScale;
        public bool HadRectTransform;
        public Vector2 OriginalAnchoredPosition;
        public Vector3 OriginalPosition;
    }

    private sealed class ChatTextFontSizeState
    {
        public TextMeshProUGUI Text;
        public float OriginalFontSize;
    }

    private sealed class ChatSurfaceWindowSizeState
    {
        public RectTransform Transform;
        public Vector2 OriginalSizeDelta;
        public Vector2 OriginalAnchoredPosition;
        public Vector3 OriginalPosition;
        public float OriginalWidth;
        public float OriginalHeight;
    }

    [HarmonyPostfix]
    [HarmonyPatch("AddMessageUI", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]
    private static void AddMessageUI_Postfix(string userName, string text, bool isLocal, int senderIndex, bool sendByPlayer)
    {
        try
        {
            RefreshNewestRows();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Chat Readability could not update chat UI: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void RequestRefreshSoon()
    {
        unchecked
        {
            _refreshRequestVersion++;
        }

    }

    internal static void RefreshNewestRows()
    {
        try
        {
            UIManager ui = MonoSingleton<UIManager>.I;
            RefreshNewestRow(ui?.TextContentLocalTransform);
            RefreshNewestRow(ui?.TextContentGlobalTransform);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Chat Readability row refresh skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RefreshNewestRow(Transform content)
    {
        if (content == null || content.childCount == 0)
        {
            return;
        }

        // AddMessageUI appends one row. Touch only that row; configuration and
        // topology changes are reconciled by the controller's deferred full pass.
        Transform newest = content.GetChild(content.childCount - 1);
        if (newest == null)
        {
            return;
        }

        ApplyChatFontSizeToContent(newest, ChatFontSizePolicy.NormalizeFontSize(Plugin.LockedChatFontSizePercent));
        if (Plugin.ShouldApplyChatReadability)
        {
            ApplyReadability(newest.gameObject);
        }
    }

    internal static void RefreshVisibleChat()
    {
        try
        {
            UIManager ui = MonoSingleton<UIManager>.I;
            ApplyChatWindowHeight(ui);
            ApplyChatAccessibilityScale(ui);
            ApplyChatFontSize(ui);
            RefreshChatContents(ui);
            PruneDestroyedState();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Chat Readability refresh skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RefreshChatContents(UIManager ui)
    {
        Transform local = ui?.TextContentLocalTransform;
        Transform global = ui?.TextContentGlobalTransform;

        // Content roots overlap through their ancestors. Process each message tree
        // once and handle the shared panel/backdrop topology separately.
        RefreshContent(local);
        if (global != local)
        {
            RefreshContent(global);
        }

        RefreshBackdropTopology(local, global);
    }

    private static void ApplyChatAccessibilityScale(UIManager ui)
    {
        ScratchChatSurfaces.Clear();
        AddChatPanelSurface(ui?.TextContentLocalTransform, ScratchChatSurfaces);
        AddChatPanelSurface(ui?.TextContentGlobalTransform, ScratchChatSurfaces);

        float scale = ChatAccessibilityScalePolicy.NormalizeScale(Plugin.LockedChatUiScalePercent);
        if (Mathf.Approximately(scale, 1f))
        {
            RestoreChatAccessibilityScale();
            return;
        }

        foreach (Transform surface in ScratchChatSurfaces)
        {
            if (surface == null)
            {
                continue;
            }

            int id = surface.GetInstanceID();
            if (!OriginalSurfaceScales.TryGetValue(id, out ChatSurfaceScaleState state))
            {
                state = new ChatSurfaceScaleState
                {
                    Transform = surface,
                    OriginalScale = surface.localScale,
                    HadRectTransform = surface is RectTransform,
                    OriginalAnchoredPosition = surface is RectTransform rect ? rect.anchoredPosition : Vector2.zero,
                    OriginalPosition = surface.position
                };
                OriginalSurfaceScales[id] = state;
            }

            surface.localScale = new Vector3(state.OriginalScale.x * scale, state.OriginalScale.y * scale, state.OriginalScale.z);
            if (surface is RectTransform rectTransform)
            {
                ClampScaledChatSurfaceToScreen(rectTransform);
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            }
        }
    }

    private static void ApplyChatWindowHeight(UIManager ui)
    {
        ScratchChatSurfaces.Clear();
        AddChatWindowSurfaces(ui?.TextContentLocalTransform, ScratchChatSurfaces);
        AddChatWindowSurfaces(ui?.TextContentGlobalTransform, ScratchChatSurfaces);

        float height = ChatWindowHeightPolicy.NormalizeHeight(Plugin.LockedChatWindowHeightPercent);
        if (Mathf.Approximately(height, 1f))
        {
            RestoreChatWindowHeight();
            return;
        }

        int applied = 0;
        foreach (Transform surface in ScratchChatSurfaces)
        {
            applied += ResizeChatWindowRect(surface as RectTransform, height, clampToScreen: true) ? 1 : 0;
        }

        int percent = Plugin.LockedChatWindowHeightPercent;
        if (percent != _lastLoggedWindowPercent || applied != _lastLoggedWindowApplied)
        {
            _lastLoggedWindowPercent = percent;
            _lastLoggedWindowApplied = applied;
            string readiness = applied > 0 ? "ready" : "waiting for visible layout";
            Plugin.Log?.LogInfo($"Chat window height {percent}% {readiness}; applied to {applied} surface rect(s); {ScratchChatSurfaces.Count} primary surface(s) checked.");
        }
    }

    private static void ApplyChatFontSize(UIManager ui)
    {
        float scale = ChatFontSizePolicy.NormalizeFontSize(Plugin.LockedChatFontSizePercent);
        if (Mathf.Approximately(scale, 1f))
        {
            RestoreChatFontSize();
            return;
        }

        int applied = 0;
        applied += ApplyChatFontSizeToContent(ui?.TextContentLocalTransform, scale);
        applied += ApplyChatFontSizeToContent(ui?.TextContentGlobalTransform, scale);

        if (applied == 0)
        {
            return;
        }
    }

    private static int ApplyChatFontSizeToContent(Transform content, float scale)
    {
        if (content == null)
        {
            return 0;
        }

        int applied = 0;
        foreach (TextMeshProUGUI text in content.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
            {
                continue;
            }

            int id = text.GetInstanceID();
            if (!OriginalFontSizes.TryGetValue(id, out ChatTextFontSizeState state))
            {
                state = new ChatTextFontSizeState
                {
                    Text = text,
                    OriginalFontSize = Mathf.Max(1f, text.fontSize)
                };
                OriginalFontSizes[id] = state;
            }

            float targetSize = Mathf.Max(1f, state.OriginalFontSize * scale);
            if (!Mathf.Approximately(text.fontSize, targetSize))
            {
                text.fontSize = targetSize;
                text.SetLayoutDirty();
                text.SetVerticesDirty();
                applied++;
            }
        }

        return applied;
    }

    private static bool ResizeChatWindowRect(RectTransform rectTransform, float heightScale, bool clampToScreen)
    {
        if (rectTransform == null)
        {
            return false;
        }

        float currentWidth = Mathf.Max(0f, rectTransform.rect.width);
        float currentHeight = Mathf.Max(0f, rectTransform.rect.height);
        if (!IsValidChatWindowBaseline(currentWidth, currentHeight))
        {
            OriginalSurfaceWindowSizes.Remove(rectTransform.GetInstanceID());
            return false;
        }

        int id = rectTransform.GetInstanceID();
        if (!OriginalSurfaceWindowSizes.TryGetValue(id, out ChatSurfaceWindowSizeState state))
        {
            state = new ChatSurfaceWindowSizeState
            {
                Transform = rectTransform,
                OriginalSizeDelta = rectTransform.sizeDelta,
                OriginalAnchoredPosition = rectTransform.anchoredPosition,
                OriginalPosition = rectTransform.position,
                OriginalWidth = Mathf.Max(1f, rectTransform.rect.width),
                OriginalHeight = Mathf.Max(1f, rectTransform.rect.height)
            };
            OriginalSurfaceWindowSizes[id] = state;
        }
        else if (!IsValidChatWindowBaseline(state.OriginalWidth, state.OriginalHeight))
        {
            state.OriginalSizeDelta = rectTransform.sizeDelta;
            state.OriginalAnchoredPosition = rectTransform.anchoredPosition;
            state.OriginalPosition = rectTransform.position;
            state.OriginalWidth = Mathf.Max(1f, rectTransform.rect.width);
            state.OriginalHeight = Mathf.Max(1f, rectTransform.rect.height);
        }

        float currentBottom = rectTransform.anchoredPosition.y - currentHeight * Mathf.Clamp01(rectTransform.pivot.y);
        float targetHeight = Mathf.Max(1f, state.OriginalHeight * heightScale);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        rectTransform.anchoredPosition = new Vector2(
            rectTransform.anchoredPosition.x,
            currentBottom + targetHeight * Mathf.Clamp01(rectTransform.pivot.y));

        if (clampToScreen)
        {
            ClampScaledChatSurfaceToScreen(rectTransform);
        }

        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        return true;
    }

    private static void ClampScaledChatSurfaceToScreen(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        Camera camera = GetUiCamera(rectTransform);
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float left = float.MaxValue;
        float right = float.MinValue;
        float bottom = float.MaxValue;
        float top = float.MinValue;
        for (int index = 0; index < corners.Length; index++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
            left = Mathf.Min(left, screenPoint.x);
            right = Mathf.Max(right, screenPoint.x);
            bottom = Mathf.Min(bottom, screenPoint.y);
            top = Mathf.Max(top, screenPoint.y);
        }

        if (!ChatAccessibilityScalePolicy.TryCalculateScreenClampDelta(
                left,
                right,
                bottom,
                top,
                Screen.width,
                Screen.height,
                ScreenClampMargin,
                out float deltaX,
                out float deltaY))
        {
            return;
        }

        if (rectTransform.parent is RectTransform parent &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Vector2.zero, camera, out Vector2 localBefore) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(deltaX, deltaY), camera, out Vector2 localAfter))
        {
            rectTransform.anchoredPosition += localAfter - localBefore;
            return;
        }

        rectTransform.position += new Vector3(deltaX, deltaY, 0f);
    }

    private static Camera GetUiCamera(RectTransform rectTransform)
    {
        Canvas canvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private static void AddChatPanelSurface(Transform content, List<Transform> surfaces)
    {
        Transform surface = ResolveChatPanelSurface(content);
        if (surface == null || surfaces.Contains(surface))
        {
            return;
        }

        surfaces.Add(surface);
    }

    private static void AddChatWindowSurfaces(Transform content, List<Transform> surfaces)
    {
        // Keep vertical expansion on the message scroll viewport. Resizing the outer
        // vanilla shell can drift over Global/Local tabs or the input drag handle.
        AddUniqueSurface(surfaces, ResolveChatScrollView(content));
    }

    private static void AddUniqueSurface(List<Transform> surfaces, Transform surface)
    {
        if (surface == null || surfaces.Contains(surface))
        {
            return;
        }

        surfaces.Add(surface);
    }

    private static Transform ResolveChatPanelSurface(Transform content)
    {
        Transform scrollView = ResolveChatScrollView(content);
        return scrollView?.parent ?? content;
    }

    private static Transform ResolveChatScrollView(Transform content)
    {
        return content?.parent?.parent;
    }

    private static bool IsValidChatWindowBaseline(float width, float height)
    {
        return width >= MinChatWindowBaselineSize && height >= MinChatWindowBaselineSize;
    }

    internal static void RestoreChatWindowHeight()
    {
        if (OriginalSurfaceWindowSizes.Count == 0)
        {
            return;
        }

        foreach (int id in new List<int>(OriginalSurfaceWindowSizes.Keys))
        {
            ChatSurfaceWindowSizeState state = OriginalSurfaceWindowSizes[id];
            if (state == null || state.Transform == null)
            {
                OriginalSurfaceWindowSizes.Remove(id);
                continue;
            }

            RectTransform rectTransform = state.Transform;
            rectTransform.sizeDelta = state.OriginalSizeDelta;
            rectTransform.anchoredPosition = state.OriginalAnchoredPosition;
            rectTransform.position = state.OriginalPosition;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            OriginalSurfaceWindowSizes.Remove(id);
        }
    }

    internal static void RestoreChatFontSize()
    {
        if (OriginalFontSizes.Count == 0)
        {
            return;
        }

        foreach (int id in new List<int>(OriginalFontSizes.Keys))
        {
            ChatTextFontSizeState state = OriginalFontSizes[id];
            if (state == null || state.Text == null)
            {
                OriginalFontSizes.Remove(id);
                continue;
            }

            state.Text.fontSize = state.OriginalFontSize;
            state.Text.SetLayoutDirty();
            state.Text.SetVerticesDirty();
            OriginalFontSizes.Remove(id);
        }
    }

    internal static void RestoreChatAccessibilityScale()
    {
        if (OriginalSurfaceScales.Count == 0)
        {
            return;
        }

        foreach (int id in new List<int>(OriginalSurfaceScales.Keys))
        {
            ChatSurfaceScaleState state = OriginalSurfaceScales[id];
            if (state == null || state.Transform == null)
            {
                OriginalSurfaceScales.Remove(id);
                continue;
            }

            state.Transform.localScale = state.OriginalScale;
            if (state.Transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = state.OriginalAnchoredPosition;
                rectTransform.position = state.OriginalPosition;
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            }
            else
            {
                state.Transform.position = state.OriginalPosition;
            }

            OriginalSurfaceScales.Remove(id);
        }
    }

    private static void RefreshContent(Transform container)
    {
        if (container == null)
        {
            return;
        }

        if (Plugin.ShouldApplyChatReadability)
        {
            ApplyReadability(container.gameObject);
        }
        else
        {
            RestoreReadability(container.gameObject);
        }
    }

    private static void RefreshBackdropTopology(Transform localContent, Transform globalContent)
    {
        if (Plugin.ShouldApplyPersistentChatBackdrop)
        {
            ApplyPersistentBackdrop(localContent);
            if (globalContent != localContent)
            {
                ApplyPersistentBackdrop(globalContent);
            }
        }
        else
        {
            RestorePersistentBackdrop(localContent);
            RestorePersistentBackdrop(globalContent);
        }
    }

    internal static void PruneDestroyedState()
    {
        foreach (int id in new List<int>(OriginalTextStates.Keys))
        {
            TextReadabilityState state = OriginalTextStates[id];
            if (state == null || state.Text == null)
            {
                DestroyOwnedMaterial(state?.AppliedFontMaterial);
                OriginalTextStates.Remove(id);
            }
        }

        foreach (int id in new List<int>(OriginalFontSizes.Keys))
        {
            if (OriginalFontSizes[id]?.Text == null)
            {
                OriginalFontSizes.Remove(id);
            }
        }

        foreach (int id in new List<int>(OriginalBackdropColors.Keys))
        {
            if (OriginalBackdropColors[id]?.Image == null)
            {
                OriginalBackdropColors.Remove(id);
            }
        }

        foreach (int id in new List<int>(OwnedBackdropOverlays.Keys))
        {
            if (OwnedBackdropOverlays[id] == null)
            {
                OwnedBackdropOverlays.Remove(id);
            }
        }

        foreach (int id in new List<int>(OriginalSurfaceScales.Keys))
        {
            ChatSurfaceScaleState state = OriginalSurfaceScales[id];
            if (state == null || state.Transform == null)
            {
                OriginalSurfaceScales.Remove(id);
            }
        }

        foreach (int id in new List<int>(OriginalSurfaceWindowSizes.Keys))
        {
            ChatSurfaceWindowSizeState state = OriginalSurfaceWindowSizes[id];
            if (state == null || state.Transform == null)
            {
                OriginalSurfaceWindowSizes.Remove(id);
            }
        }
    }

    internal static string GetDiagnosticStateSummary()
    {
        return "text=" + OriginalTextStates.Count +
               ",font=" + OriginalFontSizes.Count +
               ",backdrop=" + OriginalBackdropColors.Count +
               ",overlay=" + OwnedBackdropOverlays.Count +
               ",scale=" + OriginalSurfaceScales.Count +
               ",window=" + OriginalSurfaceWindowSizes.Count;
    }

    private static void ApplyReadability(GameObject messageRow)
    {
        if (messageRow == null)
        {
            return;
        }

        foreach (TextMeshProUGUI text in messageRow.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
            {
                continue;
            }

            TextReadabilityState state = CaptureOriginalState(text);
            float intensity = ChatReadabilityStylePolicy.NormalizeOutlineIntensity(Plugin.LockedChatOutlineIntensityPercent);
            Color outlineColor = GetOutlineColorForText(text);
            int signature = ComputeReadabilitySignature(text, outlineColor, intensity);
            if (state.LastAppliedSignature == signature &&
                state.AppliedFontMaterial != null &&
                text.fontMaterial == state.AppliedFontMaterial)
            {
                continue;
            }
            outlineColor.a *= Mathf.Lerp(0.58f, 1f, intensity);
            Color shadowColor = ShadowColor;
            shadowColor.a *= Mathf.Lerp(0.42f, 1f, intensity);
            Vector2 outlineDistance = MaxOutlineDistance * Mathf.Lerp(0.52f, 1f, intensity);
            Vector2 shadowDistance = MaxShadowDistance * Mathf.Lerp(0.46f, 1f, intensity);
            UiOutline outline = text.gameObject.GetComponent<UiOutline>() ?? text.gameObject.AddComponent<UiOutline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = outlineDistance;
            outline.useGraphicAlpha = false;

            UiShadow shadow = text.gameObject.GetComponent<UiShadow>() ?? text.gameObject.AddComponent<UiShadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = shadowDistance;
            shadow.useGraphicAlpha = false;

            ApplyTmpMaterialOutline(text, state, outlineColor, intensity);
            text.SetMaterialDirty();
            text.SetVerticesDirty();
            state.LastAppliedSignature = signature;
        }
    }

    private static int ComputeReadabilitySignature(TextMeshProUGUI text, Color outlineColor, float intensity)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + outlineColor.GetHashCode();
            hash = hash * 31 + intensity.GetHashCode();
            hash = hash * 31 + (text?.color.GetHashCode() ?? 0);
            hash = hash * 31 + (ChatReadabilityStylePolicy.ContainsTrueBlackRichTextColorTag(text?.text) ? 1 : 0);
            return hash;
        }
    }

    private static void ApplyTmpMaterialOutline(TextMeshProUGUI text, TextReadabilityState state, Color outlineColor, float intensity)
    {
        if (text == null || state == null)
        {
            return;
        }

        Material material = state.AppliedFontMaterial;
        if (material == null)
        {
            material = text.fontMaterial;
            if (material == null)
            {
                return;
            }

            state.AppliedFontMaterial = new Material(material)
            {
                name = material.name + " BlueSage Chat Outline"
            };
            material = state.AppliedFontMaterial;
        }

        float outlineWidth = Mathf.Lerp(MinTmpOutlineWidth, MaxTmpOutlineWidth, intensity);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        text.fontMaterial = material;
        text.UpdateMeshPadding();
    }

    private static TextReadabilityState CaptureOriginalState(TextMeshProUGUI text)
    {
        int id = text.GetInstanceID();
        if (OriginalTextStates.TryGetValue(id, out TextReadabilityState state))
        {
            return state;
        }

        UiOutline outline = text.gameObject.GetComponent<UiOutline>();
        UiShadow shadow = text.gameObject.GetComponent<UiShadow>();
        state = new TextReadabilityState
        {
            Text = text,
            HadOutline = outline != null,
            OutlineColor = outline != null ? outline.effectColor : Color.clear,
            OutlineDistance = outline != null ? outline.effectDistance : Vector2.zero,
            OutlineUseGraphicAlpha = outline != null && outline.useGraphicAlpha,
            HadShadow = shadow != null,
            ShadowColor = shadow != null ? shadow.effectColor : Color.clear,
            ShadowDistance = shadow != null ? shadow.effectDistance : Vector2.zero,
            ShadowUseGraphicAlpha = shadow != null && shadow.useGraphicAlpha,
            OriginalFontMaterial = text.fontMaterial
        };
        OriginalTextStates[id] = state;
        return state;
    }

    private static Color GetOutlineColorForText(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return DarkOutlineColor;
        }

        Color textColor = text.color;
        bool useBlackOutlinePath =
            Plugin.ShouldApplyBlackNamesOutline &&
            (ChatReadabilityStylePolicy.ShouldUseLightOutline(textColor.r, textColor.g, textColor.b) ||
             ChatReadabilityStylePolicy.ContainsTrueBlackRichTextColorTag(text.text));

        return useBlackOutlinePath
            ? ColorFromHex(Plugin.LockedBlackChatOutlineColorHex, LightOutlineColor)
            : ColorFromHex(Plugin.LockedChatOutlineColorHex, DarkOutlineColor);
    }

    private static Color ColorFromHex(string value, Color fallback)
    {
        string normalized = ChatReadabilityStylePolicy.NormalizeHexColor(value, string.Empty);
        if (string.IsNullOrEmpty(normalized))
        {
            return fallback;
        }

        try
        {
            byte r = Convert.ToByte(normalized.Substring(0, 2), 16);
            byte g = Convert.ToByte(normalized.Substring(2, 2), 16);
            byte b = Convert.ToByte(normalized.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, fallback.a);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static void RestoreReadability(GameObject messageRoot)
    {
        if (messageRoot == null || OriginalTextStates.Count == 0)
        {
            return;
        }

        foreach (TextMeshProUGUI text in messageRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text == null)
            {
                continue;
            }

            int id = text.GetInstanceID();
            if (!OriginalTextStates.TryGetValue(id, out TextReadabilityState state))
            {
                continue;
            }

            RestoreUiEffect(text.gameObject.GetComponent<UiOutline>(), state.HadOutline, state.OutlineColor, state.OutlineDistance, state.OutlineUseGraphicAlpha);
            RestoreUiEffect(text.gameObject.GetComponent<UiShadow>(), state.HadShadow, state.ShadowColor, state.ShadowDistance, state.ShadowUseGraphicAlpha);
            if (state.OriginalFontMaterial != null)
            {
                text.fontMaterial = state.OriginalFontMaterial;
                text.UpdateMeshPadding();
                text.SetMaterialDirty();
                text.SetVerticesDirty();
            }

            DestroyOwnedMaterial(state.AppliedFontMaterial);
            state.AppliedFontMaterial = null;
            OriginalTextStates.Remove(id);
        }
    }

    private static void DestroyOwnedMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        UnityObject.Destroy(material);
    }

    private static void RestoreUiEffect(UiShadow outline, bool hadEffect, Color color, Vector2 distance, bool useGraphicAlpha)
    {
        if (outline == null)
        {
            return;
        }

        if (!hadEffect)
        {
            UnityObject.Destroy(outline);
            return;
        }

        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = useGraphicAlpha;
    }

    private static void ApplyPersistentBackdrop(Transform content)
    {
        RectTransform scrollView = ResolveChatScrollView(content) as RectTransform;
        if (scrollView == null)
        {
            return;
        }

        Image image = scrollView.GetComponent<Image>();
        if (image == null)
        {
            int scrollId = scrollView.GetInstanceID();
            if (!OwnedBackdropOverlays.TryGetValue(scrollId, out GameObject overlay) || overlay == null)
            {
                overlay = new GameObject("BlueSage Chat Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                RectTransform overlayRect = overlay.GetComponent<RectTransform>();
                overlayRect.SetParent(scrollView, false);
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                overlayRect.SetAsFirstSibling();
                overlay.GetComponent<CanvasGroup>().blocksRaycasts = false;
                OwnedBackdropOverlays[scrollId] = overlay;
            }

            image = overlay.GetComponent<Image>();
            image.color = new Color(0.10f, 0.09f, 0.11f, 0.62f);
            return;
        }

        Color color = image.color;
        int id = image.GetInstanceID();
        if (!OriginalBackdropColors.ContainsKey(id))
        {
            OriginalBackdropColors[id] = new BackdropState
            {
                Image = image,
                OriginalColor = color
            };
        }

        image.color = new Color(
            Mathf.Min(color.r, 0.22f),
            Mathf.Min(color.g, 0.20f),
            Mathf.Min(color.b, 0.20f),
            Mathf.Max(color.a, 0.72f));
    }

    private static void RestorePersistentBackdrop(Transform content)
    {
        RectTransform scrollView = ResolveChatScrollView(content) as RectTransform;
        if (scrollView != null && OwnedBackdropOverlays.TryGetValue(scrollView.GetInstanceID(), out GameObject overlay))
        {
            OwnedBackdropOverlays.Remove(scrollView.GetInstanceID());
            if (overlay != null)
            {
                UnityObject.Destroy(overlay);
            }
        }

        if (OriginalBackdropColors.Count == 0)
        {
            return;
        }

        foreach (int id in new List<int>(OriginalBackdropColors.Keys))
        {
            BackdropState state = OriginalBackdropColors[id];
            if (state?.Image != null)
            {
                state.Image.color = state.OriginalColor;
            }

            OriginalBackdropColors.Remove(id);
        }
    }
}
