using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal enum BlueSagePublicWindow
{
    QolMenu,
    StyleHelper
}

internal static class BlueSageWindowCoordinator
{
    private static bool _qolMenuVisible;
    private static bool _styleHelperVisible;

    internal static void SetVisible(BlueSagePublicWindow window, bool visible)
    {
        if (window == BlueSagePublicWindow.QolMenu)
        {
            _qolMenuVisible = visible;
        }
        else
        {
            _styleHelperVisible = visible;
        }
    }

    internal static WindowFitResult ResolveForOpen(
        BlueSagePublicWindow window,
        bool hasOpened,
        Rect current,
        float preferredMinWidth,
        float preferredMinHeight,
        float screenWidth,
        float screenHeight)
    {
        SetVisible(window, true);
        return WindowFitPolicy.FitForOpen(
            window == BlueSagePublicWindow.StyleHelper
                ? BlueSageMenuSurface.StyleHelper
                : BlueSageMenuSurface.QolMenu,
            hasOpened,
            current.x,
            current.y,
            current.width,
            current.height,
            preferredMinWidth,
            preferredMinHeight,
            screenWidth,
            screenHeight);
    }

    internal static WindowFitResult ResolveForFrame(
        BlueSagePublicWindow window,
        Rect current,
        float preferredMinWidth,
        float preferredMinHeight,
        float screenWidth,
        float screenHeight)
    {
        SetVisible(window, true);
        // Pairing is an open-time convenience only. Re-applying the pair on
        // every IMGUI frame overwrites the user's drag and resize choices and
        // can pin the Style Helper partly off-screen on smaller displays.
        return WindowFitPolicy.FitToScreen(
            current.x,
            current.y,
            current.width,
            current.height,
            preferredMinWidth,
            preferredMinHeight,
            screenWidth,
            screenHeight,
            expandToMaximum: false);
    }

}
