namespace BlueSage.QoLTweaks.Core;

internal readonly struct ToastEntry
{
    internal ToastEntry(string text, float shownAt, bool pinned = false)
    {
        Text = text;
        ShownAt = shownAt;
        Pinned = pinned;
    }

    internal string Text { get; }
    internal float ShownAt { get; }
    // Host announcements: ordinary toasts never push them off screen before they fade.
    internal bool Pinned { get; }
}

internal static class ToastPolicy
{
    internal const float FadeInSeconds = 0.25f;
    internal const float HoldSeconds = 3.5f;
    internal const float FadeOutSeconds = 0.75f;
    internal const float TotalLifetimeSeconds = FadeInSeconds + HoldSeconds + FadeOutSeconds;
    internal const int MaxVisible = 4;
    // Round 3 (Blue, 2026-10-17): slower readers choose how long a toast stays fully visible.
    internal const float DefaultHoldSeconds = HoldSeconds;
    internal const float MinHoldSeconds = 2f;
    internal const float MaxHoldSeconds = 15f;
    // Toast size: 0 = Auto (grows with screen height from 1080p, never below 100%), else 75-200%.
    internal const int AutoScalePercent = 0;
    internal const int MinScalePercent = 75;
    internal const int MaxScalePercent = 200;
    private const float BaselineScreenHeight = 1080f;

    internal static float ComputeAlpha(float elapsedSeconds) => ComputeAlpha(elapsedSeconds, HoldSeconds);

    internal static float ComputeAlpha(float elapsedSeconds, float holdSeconds)
    {
        float hold = ClampHoldSeconds(holdSeconds);
        if (elapsedSeconds < 0f) return 0f;
        if (elapsedSeconds < FadeInSeconds) return elapsedSeconds / FadeInSeconds;
        if (elapsedSeconds < FadeInSeconds + hold) return 1f;

        float fadeOutElapsed = elapsedSeconds - FadeInSeconds - hold;
        if (fadeOutElapsed < FadeOutSeconds) return 1f - (fadeOutElapsed / FadeOutSeconds);

        return 0f;
    }

    internal static bool IsExpired(float elapsedSeconds) => IsExpired(elapsedSeconds, HoldSeconds);

    internal static bool IsExpired(float elapsedSeconds, float holdSeconds) =>
        elapsedSeconds >= FadeInSeconds + ClampHoldSeconds(holdSeconds) + FadeOutSeconds;

    internal static float ClampHoldSeconds(float seconds) =>
        float.IsNaN(seconds) ? DefaultHoldSeconds : System.Math.Max(MinHoldSeconds, System.Math.Min(MaxHoldSeconds, seconds));

    internal static float ResolveScale(int configuredPercent, float screenHeight)
    {
        if (configuredPercent == AutoScalePercent)
        {
            float auto = screenHeight > 0f ? screenHeight / BaselineScreenHeight : 1f;
            return System.Math.Max(1f, System.Math.Min(MaxScalePercent / 100f, auto));
        }

        int clamped = System.Math.Max(MinScalePercent, System.Math.Min(MaxScalePercent, configuredPercent));
        return clamped / 100f;
    }

    // Oldest unpinned toast goes first; only when every toast is pinned does the oldest pinned one go.
    internal static int IndexToEvict(System.Collections.Generic.IReadOnlyList<ToastEntry> toasts)
    {
        if (toasts == null || toasts.Count == 0) return -1;
        for (int i = 0; i < toasts.Count; i++)
        {
            if (!toasts[i].Pinned) return i;
        }
        return 0;
    }

    internal const float MaxWidth = 360f;
    // 16px gutter on each side so a toast never runs off a small laptop screen.
    internal const float ScreenGutter = 16f;

    internal static float ComputeWidth(float screenWidth) => ComputeWidth(screenWidth, 1f);

    internal static float ComputeWidth(float screenWidth, float scale)
    {
        return System.Math.Min(MaxWidth * scale, screenWidth - (2f * ScreenGutter));
    }

    // Repeated events (several cleanups, reconnect retries) refresh nothing visible, so an
    // identical toast still on screen is not stacked again; small screens fit only a few.
    internal static bool IsDuplicateOfVisible(
        string text,
        System.Collections.Generic.IReadOnlyList<ToastEntry> visible,
        float now)
    {
        if (visible == null) return false;
        for (int i = 0; i < visible.Count; i++)
        {
            if (string.Equals(visible[i].Text, text, System.StringComparison.Ordinal) &&
                !IsExpired(now - visible[i].ShownAt))
            {
                return true;
            }
        }
        return false;
    }
}
