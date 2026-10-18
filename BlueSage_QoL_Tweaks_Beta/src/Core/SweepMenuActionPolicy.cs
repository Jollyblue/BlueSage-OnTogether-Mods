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
                tooltip: "A compatibility guard currently blocks manual cleanup. Automatic Cleanup and the game's native cleanup settings are unchanged.");
    }

    internal static string BuildCompletionMessage(bool started)
    {
        return started
            ? "Asset cleanup started."
            : "Asset cleanup did not start because compatibility blocked cleanup or another cleanup is already in progress.";
    }
}
