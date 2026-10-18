using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerItemController), "Init")]
internal static class PlayerRoleLabelPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerItemController __instance, string playerSteamId)
    {
        PlayerBadgeController.RegisterRow(__instance, playerSteamId);
    }
}
