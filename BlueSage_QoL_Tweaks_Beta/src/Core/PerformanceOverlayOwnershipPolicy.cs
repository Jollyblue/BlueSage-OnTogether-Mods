namespace BlueSage.QoLTweaks.Core;

internal static class PerformanceOverlayOwnershipPolicy
{
    public static bool IsActive(
        bool pluginReady,
        bool pluginEnabled,
        bool observationOnly,
        bool controllerStarted,
        bool overlayEnabled)
    {
        return pluginReady &&
               pluginEnabled &&
               !observationOnly &&
               controllerStarted &&
               overlayEnabled;
    }
}
