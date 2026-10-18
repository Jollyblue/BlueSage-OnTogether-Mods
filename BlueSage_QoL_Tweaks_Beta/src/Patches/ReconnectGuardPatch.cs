using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(MainSceneManager))]
internal static class ReconnectGuardPatch
{
    [HarmonyPatch("ConnectionLost")]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore(new[] { "com.andrewlin.ontogether.reconnect" })]
    private static bool ConnectionLostPrefix(NotificationStatus notificationStatus)
    {
        return Plugin.HandleConnectionLostForReconnectGuard(notificationStatus);
    }
}
