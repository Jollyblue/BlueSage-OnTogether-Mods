using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class BlueSageUiThemePalette
{
    public BlueSageUiThemePalette(
        string key,
        string displayName,
        Color window,
        Color panel,
        Color header,
        Color headerText,
        Color labelText,
        Color smallText,
        Color buttonText,
        Color activeButtonText,
        Color fieldText,
        Color previewBackground)
    {
        Key = key;
        DisplayName = displayName;
        Window = window;
        Panel = panel;
        Header = header;
        HeaderText = headerText;
        LabelText = labelText;
        SmallText = smallText;
        ButtonText = buttonText;
        ActiveButtonText = activeButtonText;
        FieldText = fieldText;
        PreviewBackground = previewBackground;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public Color Window { get; }
    public Color Panel { get; }
    public Color Header { get; }
    public Color HeaderText { get; }
    public Color LabelText { get; }
    public Color SmallText { get; }
    public Color ButtonText { get; }
    public Color ActiveButtonText { get; }
    public Color FieldText { get; }
    public Color PreviewBackground { get; }

    public bool IsMidnightDock => string.Equals(Key, "Midnight Dock", StringComparison.OrdinalIgnoreCase);

    public bool UsesWarmContent => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(Key, "Vanilla Cream OT", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(Key, "OG", StringComparison.OrdinalIgnoreCase);

    public Color SectionPanel => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
        ? new Color(0.975f, 0.945f, 0.885f, 1f)
        : Color.Lerp(Panel, Window, 0.16f);

    public Color SectionBorder => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
        ? new Color(0.65f, 0.52f, 0.39f, 1f)
        : string.Equals(Key, "Vanilla Cream OT", StringComparison.OrdinalIgnoreCase)
            ? Color.Lerp(Header, FieldText, 0.40f)
            : Color.Lerp(Header, LabelText, 0.30f);

    public Color FieldBackground => IsMidnightDock
        ? new Color(0.045f, 0.065f, 0.105f, 1f)
        : Color.Lerp(SectionPanel, Color.white, UsesWarmContent ? 0.44f : 0.10f);

    public Color FieldHoverBackground => IsMidnightDock
        ? new Color(0.075f, 0.115f, 0.165f, 1f)
        : Color.Lerp(FieldBackground, Header, 0.10f);

    public Color FieldFocusedBackground => IsMidnightDock
        ? new Color(0.055f, 0.135f, 0.165f, 1f)
        : Color.Lerp(FieldBackground, Color.white, 0.16f);

    public Color FieldBorder => IsMidnightDock
        ? new Color(0.28f, 0.42f, 0.56f, 1f)
        : SectionBorder;

    public Color FieldFocusedBorder => IsMidnightDock
        ? new Color(0.24f, 0.72f, 0.78f, 1f)
        : Color.Lerp(ActiveAccent, DarkStateText, 0.18f);

    public Color ActiveAccent => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
        ? new Color(0.91f, 0.49f, 0.39f, 1f)
        : Header;

    public Color EnabledAccent => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
        ? new Color(0.39f, 0.55f, 0.24f, 1f)
        : Color.Lerp(Header, new Color(0.45f, 0.80f, 0.36f, 1f), 0.58f);

    public string NavigationKeywordHex => IsMidnightDock ? "70D6FF" : "005A85";

    public Color DisabledAccent => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
        ? new Color(0.68f, 0.65f, 0.61f, 1f)
        : IsMidnightDock
            ? new Color(0.26f, 0.31f, 0.39f, 1f)
            : Color.Lerp(ButtonText, Window, 0.58f);

    public Color DarkStateText => IsMidnightDock
        ? Window
        : string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
            ? Header
            : FieldText;

    public Color ActiveStateText => IsMidnightDock ? ActiveButtonText : DarkStateText;
    public Color EnabledButtonText => DarkStateText;
    public Color DisabledButtonText => IsMidnightDock ? new Color(0.84f, 0.89f, 0.94f, 1f) : DarkStateText;
    public Color ExperimentalButtonText => DarkStateText;

    public Color NeutralButtonAccent => IsMidnightDock
        ? Color.Lerp(Panel, Color.white, 0.12f)
        : Color.Lerp(Panel, ButtonText, 0.30f);

    public Color NeutralButtonText => IsMidnightDock ? ButtonText : DarkStateText;
    public Color PreviewText => string.Equals(Key, "OG", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Key, "Vanilla Cream OT", StringComparison.OrdinalIgnoreCase)
        ? Color.white
        : LabelText;

    public Color PopupHeaderText => IsMidnightDock ? ActiveButtonText : DarkStateText;
    public Color PopupBodyText => IsMidnightDock ? LabelText : DarkStateText;
    public Color PopupHintText => IsMidnightDock ? Color.Lerp(SmallText, LabelText, 0.35f) : DarkStateText;

    public Color ComfortAccent => IsMidnightDock
        ? new Color(0.42f, 0.76f, 0.49f, 1f)
        : new Color(0.39f, 0.55f, 0.24f, 1f);

    public Color ChatAccent => IsMidnightDock
        ? new Color(0.32f, 0.72f, 0.82f, 1f)
        : new Color(0.10f, 0.56f, 0.62f, 1f);

    public Color IdentityAccent => IsMidnightDock
        ? new Color(0.55f, 0.63f, 0.91f, 1f)
        : new Color(0.47f, 0.40f, 0.68f, 1f);

    public Color PersonalizationAccent => IsMidnightDock
        ? new Color(0.82f, 0.55f, 0.73f, 1f)
        : new Color(0.72f, 0.39f, 0.50f, 1f);

    // Round 3 (Blue, 2026-10-17): the Accessibility section has its own purple border.
    public Color AccessibilityAccent => IsMidnightDock
        ? new Color(0.66f, 0.45f, 0.98f, 1f)
        : new Color(0.50f, 0.30f, 0.80f, 1f);

    public Color ExperimentalAccent => new Color(1f, 0.42f, 0.08f, 1f);
    public Color ExperimentalBorder => IsMidnightDock
        ? ExperimentalAccent
        : Color.Lerp(ExperimentalAccent, DarkStateText, 0.35f);

    public Color FooterBackground => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
        ? new Color(0.055f, 0.075f, 0.115f, 1f)
        : Color.Lerp(Header, Window, 0.30f);

    public Color FooterText => string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
        ? new Color(0.84f, 0.93f, 0.98f, 1f)
        : HeaderText;

    public Color FooterBorder => IsMidnightDock
        ? FieldFocusedBorder
        : string.Equals(Key, "BlueSage Harbor", StringComparison.OrdinalIgnoreCase)
            ? SectionBorder
            : DarkStateText;
}

internal static class BlueSageUiTheme
{
    public const string DefaultThemeKey = "BlueSage Harbor";

    private static readonly Dictionary<string, Texture2D> SolidTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D> PanelTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

    public static int RuntimeTextureGeneration { get; private set; } = 1;

    private static readonly BlueSageUiThemePalette[] Palettes =
    {
        new BlueSageUiThemePalette(
            "BlueSage Harbor",
            "BlueSage Harbor",
            new Color(0.93f, 0.875f, 0.79f, 0.995f),
            new Color(0.985f, 0.95f, 0.88f, 0.995f),
            new Color(0.055f, 0.075f, 0.115f, 1f),
            new Color(0.84f, 0.95f, 0.98f, 1f),
            new Color(0.20f, 0.16f, 0.15f, 1f),
            new Color(0.35f, 0.29f, 0.27f, 1f),
            new Color(0.22f, 0.18f, 0.17f, 1f),
            new Color(1f, 0.96f, 0.88f, 1f),
            new Color(0.12f, 0.10f, 0.10f, 1f),
            new Color(0.985f, 0.95f, 0.88f, 0.98f)),
        new BlueSageUiThemePalette(
            "OG",
            "OG",
            new Color(0.93f, 0.90f, 0.83f, 0.99f),
            new Color(0.84f, 0.80f, 0.72f, 0.98f),
            new Color(0.55f, 0.52f, 0.47f, 1f),
            new Color(0.12f, 0.09f, 0.10f, 1f),
            new Color(0.22f, 0.14f, 0.17f, 1f),
            new Color(0.34f, 0.26f, 0.29f, 1f),
            new Color(0.24f, 0.17f, 0.20f, 1f),
            Color.white,
            new Color(0.14f, 0.11f, 0.12f, 1f),
            new Color(0.28f, 0.27f, 0.25f, 0.96f)),
        new BlueSageUiThemePalette(
            "Vanilla Cream OT",
            "Vanilla Cream OT",
            new Color(0.98f, 0.93f, 0.84f, 0.99f),
            new Color(0.91f, 0.82f, 0.69f, 0.98f),
            new Color(0.91f, 0.49f, 0.42f, 1f),
            new Color(0.23f, 0.14f, 0.16f, 1f),
            new Color(0.32f, 0.22f, 0.25f, 1f),
            new Color(0.43f, 0.31f, 0.33f, 1f),
            new Color(0.31f, 0.22f, 0.25f, 1f),
            Color.white,
            new Color(0.15f, 0.11f, 0.12f, 1f),
            new Color(0.39f, 0.31f, 0.27f, 0.96f)),
        new BlueSageUiThemePalette(
            "Midnight Dock",
            "Midnight Dock",
            new Color(0.08f, 0.10f, 0.16f, 0.99f),
            new Color(0.13f, 0.16f, 0.24f, 0.98f),
            new Color(0.08f, 0.36f, 0.42f, 1f),
            new Color(0.88f, 0.98f, 1f, 1f),
            new Color(0.91f, 0.96f, 1f, 1f),
            new Color(0.72f, 0.82f, 0.90f, 1f),
            new Color(0.89f, 0.94f, 1f, 1f),
            new Color(1f, 0.91f, 0.42f, 1f),
            new Color(0.96f, 0.98f, 1f, 1f),
            new Color(0.04f, 0.05f, 0.08f, 0.97f))
    };

    public static BlueSageUiThemePalette Current => Get(Plugin.UiThemePreset?.Value);
    public static string[] Names => Array.ConvertAll(Palettes, palette => palette.DisplayName);

    public static BlueSageUiThemePalette Get(string requested)
    {
        string normalized = Normalize(requested);
        foreach (BlueSageUiThemePalette palette in Palettes)
        {
            if (string.Equals(palette.Key, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(palette.DisplayName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }
        }

        return GetDefaultPalette();
    }

    public static string Normalize(string requested)
    {
        string value = (requested ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return DefaultThemeKey;
        }

        foreach (BlueSageUiThemePalette palette in Palettes)
        {
            if (string.Equals(palette.Key, value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(palette.DisplayName, value, StringComparison.OrdinalIgnoreCase))
            {
                return palette.Key;
            }
        }

        return DefaultThemeKey;
    }

    public static string Next(string requested)
    {
        string normalized = Normalize(requested);
        for (int index = 0; index < Palettes.Length; index++)
        {
            if (string.Equals(Palettes[index].Key, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return Palettes[(index + 1) % Palettes.Length].Key;
            }
        }

        return GetDefaultPalette().Key;
    }

    private static BlueSageUiThemePalette GetDefaultPalette()
    {
        foreach (BlueSageUiThemePalette palette in Palettes)
        {
            if (string.Equals(palette.Key, DefaultThemeKey, StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }
        }

        return Palettes[0];
    }

    public static GUIStyle CreateTextInputStyle(GUIStyle source, BlueSageUiThemePalette palette, int fontSize, bool wordWrap)
    {
        var style = new GUIStyle(source)
        {
            richText = false,
            fontSize = fontSize,
            wordWrap = wordWrap,
            border = new RectOffset(1, 1, 1, 1),
            padding = new RectOffset(8, 8, 5, 5)
        };

        ApplyState(style.normal, palette.FieldBackground, palette.FieldBorder, palette.FieldText);
        ApplyState(style.hover, palette.FieldHoverBackground, palette.FieldBorder, palette.FieldText);
        ApplyState(style.active, palette.FieldFocusedBackground, palette.FieldFocusedBorder, palette.FieldText);
        ApplyState(style.focused, palette.FieldFocusedBackground, palette.FieldFocusedBorder, palette.FieldText);
        ApplyState(style.onNormal, palette.FieldBackground, palette.FieldBorder, palette.FieldText);
        ApplyState(style.onHover, palette.FieldHoverBackground, palette.FieldBorder, palette.FieldText);
        ApplyState(style.onActive, palette.FieldFocusedBackground, palette.FieldFocusedBorder, palette.FieldText);
        ApplyState(style.onFocused, palette.FieldFocusedBackground, palette.FieldFocusedBorder, palette.FieldText);
        return style;
    }

    public static GUIStyle CreateStateButtonStyle(GUIStyle source, Color fill, Color border, Color text, int fontSize)
    {
        var style = new GUIStyle(source)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(1, 1, 1, 1)
        };

        bool lightText = RelativeLuminance(text) >= 0.50f;
        Color hoverFill = Color.Lerp(fill, lightText ? Color.black : Color.white, lightText ? 0.08f : 0.11f);
        Color pressedFill = Color.Lerp(fill, lightText ? Color.black : Color.white, lightText ? 0.18f : 0.20f);
        ApplyState(style.normal, fill, border, text);
        ApplyState(style.hover, hoverFill, Color.Lerp(border, Color.white, 0.16f), text);
        ApplyState(style.active, pressedFill, border, text);
        ApplyState(style.focused, hoverFill, Color.Lerp(border, Color.white, 0.16f), text);
        ApplyState(style.onNormal, fill, border, text);
        ApplyState(style.onHover, hoverFill, Color.Lerp(border, Color.white, 0.16f), text);
        ApplyState(style.onActive, pressedFill, border, text);
        ApplyState(style.onFocused, hoverFill, Color.Lerp(border, Color.white, 0.16f), text);
        return style;
    }

    public static GUIStyle CreateNeutralButtonStyle(GUIStyle source, BlueSageUiThemePalette palette, int fontSize)
    {
        return CreateStateButtonStyle(source, palette.NeutralButtonAccent, palette.FieldBorder, palette.NeutralButtonText, fontSize);
    }

    public static GUIStyle CreateSwatchButtonStyle(GUIStyle source, int fontSize)
    {
        var style = new GUIStyle(source)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(1, 1, 1, 1)
        };

        Color normal = Color.white;
        Color hover = new Color(0.92f, 0.92f, 0.92f, 1f);
        Color pressed = new Color(0.82f, 0.82f, 0.82f, 1f);
        ApplyState(style.normal, normal, Color.black, Color.white);
        ApplyState(style.hover, hover, Color.black, Color.white);
        ApplyState(style.active, pressed, Color.black, Color.white);
        ApplyState(style.focused, hover, Color.black, Color.white);
        ApplyState(style.onNormal, normal, Color.black, Color.white);
        ApplyState(style.onHover, hover, Color.black, Color.white);
        ApplyState(style.onActive, pressed, Color.black, Color.white);
        ApplyState(style.onFocused, hover, Color.black, Color.white);
        return style;
    }

    public static Color BestTextColor(Color background, BlueSageUiThemePalette palette)
    {
        Color dark = palette?.DarkStateText ?? Color.black;
        return ContrastRatio(dark, background) >= ContrastRatio(Color.white, background) ? dark : Color.white;
    }

    private static float ContrastRatio(Color first, Color second)
    {
        float firstLum = RelativeLuminance(first);
        float secondLum = RelativeLuminance(second);
        float lighter = Math.Max(firstLum, secondLum);
        float darker = Math.Min(firstLum, secondLum);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    private static float RelativeLuminance(Color color)
    {
        return 0.2126f * Linearize(color.r) + 0.7152f * Linearize(color.g) + 0.0722f * Linearize(color.b);
    }

    private static float Linearize(float channel)
    {
        return channel <= 0.03928f
            ? channel / 12.92f
            : (float)Math.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    public static Texture2D GetSolidTexture(Color color)
    {
        string key = ColorKey(color);
        if (SolidTextures.TryGetValue(key, out Texture2D existing))
        {
            if (existing != null)
            {
                return existing;
            }

            SolidTextures.Remove(key);
            RuntimeTextureGeneration++;
        }

        var texture = new Texture2D(1, 1)
        {
            name = "BlueSageUi-Solid-" + key,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        SolidTextures[key] = texture;
        return texture;
    }

    public static Texture2D GetPanelTexture(Color fill, Color border)
    {
        string key = ColorKey(fill) + "-" + ColorKey(border);
        if (PanelTextures.TryGetValue(key, out Texture2D existing))
        {
            if (existing != null)
            {
                return existing;
            }

            PanelTextures.Remove(key);
            RuntimeTextureGeneration++;
        }

        var texture = new Texture2D(3, 3)
        {
            name = "BlueSageUi-Panel-" + key,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                bool edge = x == 0 || y == 0 || x == 2 || y == 2;
                texture.SetPixel(x, y, edge ? border : fill);
            }
        }

        texture.Apply();
        PanelTextures[key] = texture;
        return texture;
    }

    public static bool AreRuntimeTexturesAlive()
    {
        foreach (Texture2D texture in SolidTextures.Values)
        {
            if (texture == null) return false;
        }

        foreach (Texture2D texture in PanelTextures.Values)
        {
            if (texture == null) return false;
        }

        return true;
    }

    public static void ReleaseRuntimeTextures()
    {
        foreach (Texture2D texture in SolidTextures.Values)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }

        foreach (Texture2D texture in PanelTextures.Values)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }

        SolidTextures.Clear();
        PanelTextures.Clear();
        RuntimeTextureGeneration++;
    }

    private static string ColorKey(Color color)
    {
        Color32 exact = color;
        return exact.r.ToString("X2") + exact.g.ToString("X2") + exact.b.ToString("X2") + exact.a.ToString("X2");
    }

    private static void ApplyState(GUIStyleState state, Color fill, Color border, Color text)
    {
        state.background = GetPanelTexture(fill, border);
        state.textColor = text;
    }
}
