using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal static class BlueSageHelpFrame
{
    internal static HelpFrameLayout Resolve(BlueSageHelpFrameKind kind, Rect window) =>
        HelpFramePolicy.Resolve(kind, window.width, window.height);

    internal static void BeginFooter(GUIStyle footerStyle, HelpFrameLayout layout)
    {
        GUILayout.BeginVertical(
            footerStyle,
            GUILayout.Height(layout.FooterHeight),
            GUILayout.MaxHeight(layout.FooterHeight));
    }

    internal static void DrawStateAndHelp(
        GUIStyle textStyle,
        HelpFrameLayout layout,
        string stateLine,
        string helpLine)
    {
        GUILayout.Label(
            stateLine ?? string.Empty,
            textStyle,
            GUILayout.Height(layout.StateLineHeight),
            GUILayout.MaxHeight(layout.StateLineHeight));
        GUILayout.Label(
            helpLine ?? string.Empty,
            textStyle,
            GUILayout.Height(layout.HelpLineHeight),
            GUILayout.MaxHeight(layout.HelpLineHeight));
    }

    internal static void EndFooter()
    {
        GUILayout.EndVertical();
    }
}
