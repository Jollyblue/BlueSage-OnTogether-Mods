using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerItemController), "Init")]
internal static class EnhancedPlayerPanelPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        PlayerItemController __instance,
        string playerSteamId,
        bool isSelf)
    {
        PlayerPanelSurfaceController.RefreshRow(
            __instance,
            playerSteamId,
            isSelf,
            Plugin.ShouldApplyEnhancedPlayerPanel);
    }
}
