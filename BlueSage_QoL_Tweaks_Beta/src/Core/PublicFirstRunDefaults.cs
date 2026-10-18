namespace BlueSage.QoLTweaks.Core;

internal sealed class PublicFirstRunDefaults
{
    private PublicFirstRunDefaults() { }

    internal static PublicFirstRunDefaults Current { get; } = new();

    internal bool AutomaticCleanup => true;
    internal bool ManualCleanupWithOverlappingSweepMod => false;
    internal bool CommunitySafetyList => true;
    internal bool PlayerRenderSaver => false; // retired 2026-10-17, see PlayerRenderSaverPolicy.Retired
    // 2026-10-17 (Blue): his live UAT settings are the fresh-install defaults. Existing saved
    // choices are untouched (BepInEx keeps saved values).
    internal bool PerformanceOverlay => true;
    internal float PerformanceOverlayOffsetX => 45f;
    internal float PerformanceOverlayOffsetY => 223f;
    internal int MenuScalePercent => 0;
    internal string Theme => "Midnight Dock";
    internal int ChatTextLimit => InputFieldLimitPolicy.DefaultMaxChatCharacters;
    internal int ProfileTextLimit => 4000;
    internal int RoomNameLimit => 4000;
}
