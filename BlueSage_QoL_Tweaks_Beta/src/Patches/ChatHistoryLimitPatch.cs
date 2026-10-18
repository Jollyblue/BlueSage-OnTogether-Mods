using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(GameSettings))]
internal static class ChatHistoryLimitPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(nameof(GameSettings.GlobalMessageLimitCount), MethodType.Getter)]
    private static void GlobalMessageLimitCount_Postfix(ref int __result)
    {
        __result = ChatHistoryLimitPolicy.ApplyConfiguredRows(__result, Plugin.LockedChatHistoryRows);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(nameof(GameSettings.LocalMessageLimitCount), MethodType.Getter)]
    private static void LocalMessageLimitCount_Postfix(ref int __result)
    {
        __result = ChatHistoryLimitPolicy.ApplyConfiguredRows(__result, Plugin.LockedChatHistoryRows);
    }
}
