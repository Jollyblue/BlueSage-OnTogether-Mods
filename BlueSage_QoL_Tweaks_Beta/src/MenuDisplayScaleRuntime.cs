using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal static class MenuDisplayScaleRuntime
{
    internal static MenuDisplayScaleResult Current =>
        MenuDisplayScalePolicy.Resolve(
            Plugin.MenuUiScalePercent?.Value ?? MenuDisplayScalePolicy.AutoScalePercent,
            Screen.dpi);

    internal static float LogicalScreenWidth => Current.ToLogicalPixels(Screen.width);

    internal static float LogicalScreenHeight => Current.ToLogicalPixels(Screen.height);

    internal static Rect ToPhysicalRect(Rect logicalRect)
    {
        float scale = Current.Scale;
        return new Rect(
            logicalRect.x * scale,
            logicalRect.y * scale,
            logicalRect.width * scale,
            logicalRect.height * scale);
    }

    internal static void Apply(out Matrix4x4 previous)
    {
        previous = GUI.matrix;
        float scale = Current.Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
    }

    internal static void Restore(Matrix4x4 previous)
    {
        GUI.matrix = previous;
    }
}
