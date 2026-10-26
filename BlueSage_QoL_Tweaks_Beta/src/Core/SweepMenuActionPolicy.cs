namespace BlueSage.QoLTweaks.Core;

internal readonly struct SweepMenuActionState
{
    internal SweepMenuActionState(bool enabled, string label, string tooltip)
    {
        Enabled = enabled;
        Label = label;
        Tooltip = tooltip;
    }

    internal bool Enabled { get; }
    internal string Label { get; }
    internal string Tooltip { get; }
}

internal static class SweepMenuActionPolicy
{
    // Voice pass (2026-10-23): memory cleanup is "cleanup" in player text. A cleanup you asked for always answers,
    // and a refusal answers with the specific reason the plugin already gave, never a generic either/or.
    internal const string StartedMessage = "Cleanup started.";
    internal const string PausedNotice = "Cleanup is resting because another cleanup mod is already doing it.";
    internal const string BusyNotice = "Cleanup is already running.";
    internal const string StartFailedNotice = "Cleanup couldn't start this time. Try again in a moment.";
    internal const string RunFailedNotice = "Cleanup couldn't finish this time. Try again in a moment.";
    internal const string CompletedNotice = "BlueSage cleanup is done.";
    internal const string GameCleanupToast = "The game is running its own cleanup; a short pause is normal.";

    internal static SweepMenuActionState Resolve(bool manualSweepAllowed)
    {
        return manualSweepAllowed
            ? new SweepMenuActionState(
                enabled: true,
                label: "Run Cleanup Now",
                tooltip: "Runs one local unused-asset cleanup now, even when Automatic Cleanup is off. It never deletes files, profiles, or mods and does not change the game's timer.")
            : new SweepMenuActionState(
                enabled: false,
                label: "BlueSage Cleanup: Standby",
                tooltip: "Another cleanup mod is already doing this, so BlueSage cleanup is resting. Automatic Cleanup and the game's own cleanup settings are unchanged.");
    }

    internal static string BuildCompletionMessage(bool started, string refusalNotice)
    {
        if (started)
        {
            return StartedMessage;
        }

        return string.IsNullOrEmpty(refusalNotice) ? StartFailedNotice : refusalNotice;
    }

    // The game's own 30-minute cleanup is quiet by default, like BlueSage's automatic one; the toast shows only
    // when the player turned cleanup notices on, and never for BlueSage's own cleanup (which announces itself).
    internal static bool ShouldToastGameCleanup(bool qolCleanupCreatingRoutine, bool notifyOnCleanup)
    {
        return notifyOnCleanup && !qolCleanupCreatingRoutine;
    }

    // Only a cleanup the player asked for answers a failure; the automatic timer stays quiet and logs instead.
    internal static bool ShouldAnnounceRunFailure(string trigger)
    {
        return !string.Equals(trigger, "automatic timer", System.StringComparison.Ordinal);
    }
}
