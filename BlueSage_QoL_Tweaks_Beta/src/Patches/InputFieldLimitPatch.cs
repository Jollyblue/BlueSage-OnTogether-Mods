using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TMPro.TMP_InputField))]
internal static class InputFieldLimitPatch
{
    [HarmonyPatch("OnEnable")]
    [HarmonyPostfix]
    private static void OnEnablePostfix(TMPro.TMP_InputField __instance)
    {
        if (__instance == null)
        {
            return;
        }

        int? limit = InputFieldLimitPolicy.GetLimit(
            __instance.name,
            Plugin.LockedMaxIdCardCharacters,
            Plugin.LockedMaxSessionNameCharacters,
            Plugin.LockedMaxChatCharacters);

        if (limit.HasValue)
        {
            __instance.characterLimit = limit.Value;
        }
    }
}
