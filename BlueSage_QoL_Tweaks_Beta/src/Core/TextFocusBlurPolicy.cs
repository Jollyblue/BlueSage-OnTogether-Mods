namespace BlueSage.QoLTweaks.Core;

// IMGUI never clears GUIUtility.keyboardControl when a button is clicked, so text focus "stuck"
// after typing a value and pressing Save, and the hotkey text-focus guard stayed on. Standard
// blur-on-click: any mouse-down inside a BlueSage window clears focus before its controls run (a
// text field under the click immediately re-takes it), and a mouse-down outside every BlueSage
// window (a click into the game) clears it too.
internal static class TextFocusBlurPolicy
{
    internal static bool ShouldBlurInsideWindow(bool isMouseDown) => isMouseDown;

    internal static bool ShouldBlurOutsideWindows(bool isMouseDown, bool pointerOverBlueSageWindow)
    {
        return isMouseDown && !pointerOverBlueSageWindow;
    }
}
