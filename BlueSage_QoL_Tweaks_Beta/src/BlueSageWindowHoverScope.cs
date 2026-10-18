using UnityEngine;

namespace BlueSage.QoLTweaks;

internal static class BlueSageWindowHoverScope
{
    private static int _hoveredWindowId;
    private static int _hoveredPriority;
    private static int _hoveredFrame = -1;
    private static readonly System.Collections.Generic.Dictionary<int, WindowRectStamp> WindowRects =
        new System.Collections.Generic.Dictionary<int, WindowRectStamp>();

    private readonly struct WindowRectStamp
    {
        internal WindowRectStamp(Rect rect, int frame)
        {
            Rect = rect;
            Frame = frame;
        }

        internal Rect Rect { get; }
        internal int Frame { get; }
    }

    // True when the point is inside any BlueSage window drawn in the last couple of frames.
    // Independent of OnGUI order across windows (each records its own rect every frame).
    internal static bool IsPointOverAnyWindow(Vector2 point)
    {
        int frame = Time.frameCount;
        foreach (WindowRectStamp entry in WindowRects.Values)
        {
            if (frame - entry.Frame <= 2 && entry.Rect.Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    // For Update-time callers (no IMGUI Event): Input.mousePosition is bottom-left origin, the
    // registered rects are IMGUI top-left origin.
    internal static bool IsScreenMouseOverAnyWindow()
    {
        Vector3 mouse = Input.mousePosition;
        return IsPointOverAnyWindow(new Vector2(mouse.x, Screen.height - mouse.y));
    }

    internal static void RegisterWindow(int windowId, Rect screenRect, int priority)
    {
        WindowRects[windowId] = new WindowRectStamp(screenRect, Time.frameCount);
        Event current = Event.current;
        if (current == null || !screenRect.Contains(current.mousePosition))
        {
            return;
        }

        int frame = Time.frameCount;
        if (_hoveredFrame != frame || priority >= _hoveredPriority)
        {
            _hoveredWindowId = windowId;
            _hoveredPriority = priority;
            _hoveredFrame = frame;
        }
    }

    internal static bool IsWindowHovered(int windowId)
    {
        return _hoveredFrame == Time.frameCount && _hoveredWindowId == windowId;
    }

    internal static void UnregisterWindow(int windowId)
    {
        WindowRects.Remove(windowId);
        if (_hoveredFrame == Time.frameCount && _hoveredWindowId == windowId)
        {
            _hoveredWindowId = 0;
            _hoveredPriority = 0;
            _hoveredFrame = -1;
        }
    }
}
