namespace BlueSage.QoLTweaks.Core;

// Unity's Input.GetKeyDown is global: a key pressed while a text box has focus also reaches
// BlueSage hotkeys, and End/Home/Delete/arrows are caret keys in every text box. So hotkeys and
// rebind capture both stand down while any text input is focused (game chat, a game menu field,
// or a BlueSage IMGUI field).
internal static class HotkeyInputGatePolicy
{
    internal const float RebindCaptureTimeoutSeconds = 15f;

    internal static bool ShouldDispatchHotkeys(bool textInputFocused) => !textInputFocused;

    internal static bool ShouldEvaluateRebindCapture(bool textInputFocused) => !textInputFocused;

    // The timeout clock starts the first frame capture can actually hear a key, not when capture
    // was requested: after "/rebind fly" + Enter the chat box can keep focus for a while.
    internal static float? AdvanceCaptureClock(float? activeSince, bool textInputFocused, float now)
    {
        if (activeSince.HasValue)
        {
            return activeSince;
        }

        return textInputFocused ? (float?)null : now;
    }

    // Backstop: even if the capture never becomes active (text focus never released), it ends this
    // long after it was requested, so hotkeys can never stay off indefinitely.
    internal const float RebindCaptureHardCapSeconds = 60f;

    internal static bool IsRebindCaptureExpired(float? activeSince, float requestedAt, float now)
    {
        if (now - requestedAt >= RebindCaptureHardCapSeconds)
        {
            return true;
        }

        return activeSince.HasValue && now - activeSince.Value >= RebindCaptureTimeoutSeconds;
    }

    internal static bool ShouldAnnounceRejection(string rejectedKey, string lastAnnouncedKey)
    {
        return !string.Equals(rejectedKey, lastAnnouncedKey, System.StringComparison.Ordinal);
    }
}
