using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

internal static class CreateSessionNameInputLimitPatch
{
    [HarmonyPatch(typeof(MainMenuUIController), "Start")]
    [HarmonyPostfix]
    private static void StartPostfix(MainMenuUIController __instance)
    {
        Apply(__instance);
    }

    [HarmonyPatch(typeof(MainMenuUIController), nameof(MainMenuUIController.ButtonPlay))]
    [HarmonyPostfix]
    private static void ButtonPlayPostfix(MainMenuUIController __instance)
    {
        Apply(__instance);
    }

    private static void Apply(MainMenuUIController controller)
    {
        TMPro.TMP_InputField input = controller?.CreateSessionNameInputField;
        if (input == null)
        {
            return;
        }

        input.characterLimit = InputFieldLimitPolicy.ResolveCreateSessionNameLimit(
            input.characterLimit,
            Plugin.LockedMaxSessionNameCharacters);
    }
}
