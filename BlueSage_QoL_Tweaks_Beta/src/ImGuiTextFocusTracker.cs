using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

// IMGUI tracks keyboard focus in GUIUtility.keyboardControl, which is non-zero only while a text
// control (TextField/TextArea) owns the keyboard. Reading it straight from Update() is unsafe: it
// can stay set after the window that owned it stops drawing, which would leave every hotkey dead.
// So each BlueSage window reports from inside its own OnGUI, and the report expires the frame that
// window stops drawing. IMGUI also never clears focus when a button is clicked, so focus is blurred
// on clicks (TextFocusBlurPolicy) -- otherwise "type, press Save, press Home" left hotkeys dead.
internal static class ImGuiTextFocusTracker
{
    private static int _lastEditingFrame = -10;

    // Call first in a window's draw callback, before any control sees the event.
    internal static void BlurOnClickInsideWindow()
    {
        Event current = Event.current;
        if (current != null && TextFocusBlurPolicy.ShouldBlurInsideWindow(current.type == EventType.MouseDown))
        {
            GUIUtility.keyboardControl = 0;
        }
    }

    // Call in each window's OnGUI AFTER it registers its physical rect and BEFORE
    // MenuDisplayScaleRuntime.Apply: window rects are physical pixels, and Event.mousePosition is
    // only physical outside the scaled GUI matrix.
    internal static void BlurOnClickOutsideWindows()
    {
        Event current = Event.current;
        if (current != null &&
            TextFocusBlurPolicy.ShouldBlurOutsideWindows(
                current.type == EventType.MouseDown,
                BlueSageWindowHoverScope.IsPointOverAnyWindow(current.mousePosition)))
        {
            GUIUtility.keyboardControl = 0;
        }
    }

    internal static void ReportWindowDrawn()
    {
        if (GUIUtility.keyboardControl != 0)
        {
            _lastEditingFrame = Time.frameCount;
        }
    }

    // OnGUI for frame N runs after Update for frame N, so Update sees the previous frame's report.
    internal static bool IsEditing => Time.frameCount - _lastEditingFrame <= 1;
}
