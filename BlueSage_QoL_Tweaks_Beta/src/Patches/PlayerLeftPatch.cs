using System;
using System.Text;
using HarmonyLib;
using PurrNet;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerPanelController))]
internal static class PlayerLeftPatch
{
    [HarmonyPatch("DespawnHandle")]
    [HarmonyPrefix]
    private static void DespawnHandlePrefix(NetworkTransform netTransform, PlayerPanelController __instance)
    {
        PlayerIdentityEvidenceController.InvalidateRosterCache();
        if (!Plugin.ShouldApplyLeaveNotifications || __instance == null || netTransform == null)
        {
            return;
        }

        try
        {
            int count = Math.Min(__instance.PlayerTransforms.Count, __instance.IDInfos.Count);
            for (int i = 0; i < count; i++)
            {
                if (__instance.PlayerTransforms[i] != netTransform)
                {
                    continue;
                }

                byte[] rawName = __instance.IDInfos[i].Name;
                string playerName = rawName == null ? "Someone" : Encoding.Unicode.GetString(rawName);
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    playerName = "Someone";
                }

                Plugin.AddLocalNotification($"{playerName} has left the server.");
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Leave notification skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
