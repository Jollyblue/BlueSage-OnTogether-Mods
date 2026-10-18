#nullable enable

using System;

namespace BlueSage.QoLTweaks.Core;

// Fly-mode comfort: finer ALT precision, rotate with a chosen mouse button, and a wider zoom range.
// Vanilla's own limits (GameSettings.ZoomMin/ZoomMax) are restored the moment fly ends.
internal static class FlyCameraPolicy
{
    internal const int DefaultPrecisionPercent = 15;
    internal const int MinPrecisionPercent = 5;
    internal const int MaxPrecisionPercent = 50;
    internal const float ZoomInFactor = 0.2f;
    internal const float ZoomOutFactor = 4f;
    internal const string DefaultRotateButton = "Mouse2";

    // Unity KeyCode names; Mouse1 (right) is vanilla's own rotate button and stays last.
    private static readonly string[] RotateButtons = { "Mouse2", "Mouse3", "Mouse4", "Mouse1" };

    internal static int ClampPrecisionPercent(int percent)
    {
        return Math.Max(MinPrecisionPercent, Math.Min(MaxPrecisionPercent, percent));
    }

    internal static float PrecisionMultiplier(int percent)
    {
        return ClampPrecisionPercent(percent) / 100f;
    }

    internal static float MinFlyZoom(float vanillaMin) => vanillaMin * ZoomInFactor;

    internal static float MaxFlyZoom(float vanillaMax) => vanillaMax * ZoomOutFactor;

    internal static float ComputeFlyZoom(
        float current,
        float scrollY,
        float zoomSpeed,
        float vanillaMin,
        float vanillaMax,
        bool shiftHeld)
    {
        if (shiftHeld)
        {
            return current;
        }

        // Steps grow with distance so the wide range stays a handful of scroll notches.
        float scale = Math.Max(1f, current / Math.Max(0.0001f, vanillaMax));
        float next = current - (scrollY * zoomSpeed * scale);
        return Math.Max(MinFlyZoom(vanillaMin), Math.Min(MaxFlyZoom(vanillaMax), next));
    }

    internal static float ClampToVanilla(float current, float vanillaMin, float vanillaMax)
    {
        return Math.Max(vanillaMin, Math.Min(vanillaMax, current));
    }

    internal static string NextRotateButton(string? current)
    {
        int index = Array.IndexOf(RotateButtons, current ?? string.Empty);
        return index < 0 ? DefaultRotateButton : RotateButtons[(index + 1) % RotateButtons.Length];
    }

    // Only the offered mouse buttons are ever parsed into a KeyCode; anything else falls back.
    internal static string NormalizeRotateButton(string? button)
    {
        return Array.IndexOf(RotateButtons, button ?? string.Empty) >= 0 ? button! : DefaultRotateButton;
    }

    internal static string FriendlyRotateButtonName(string? button)
    {
        switch (button)
        {
            case "Mouse1": return "Right Mouse";
            case "Mouse2": return "Middle Mouse";
            case "Mouse3": return "Mouse 4 (back)";
            case "Mouse4": return "Mouse 5 (forward)";
            default: return "Middle Mouse";
        }
    }

    internal static bool ShouldDriveRotation(bool flying, bool buttonHeld, string? button)
    {
        return flying && buttonHeld && !string.Equals(button, "Mouse1", StringComparison.Ordinal);
    }
}
