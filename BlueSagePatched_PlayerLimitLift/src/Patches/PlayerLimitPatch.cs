using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BlueSagePatched.PlayerLimitLift.Patches;

[HarmonyPatch(typeof(MainMenuUIController))]
internal static class PlayerLimitPatch
{
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    private static void AwakePatch(ref TextMeshProUGUI ____filterPlayerText)
    {
        MonoSingleton<MultiplayerManager>.I.FilterPlayerValue = Plugin.EffectiveDefaultLobbySize;
        if (____filterPlayerText != null)
        {
            ____filterPlayerText.text = Plugin.EffectiveDefaultLobbySize.ToString();
        }
    }

    [HarmonyPatch("ButtonChangeFilterPlayer")]
    [HarmonyPrefix]
    private static bool ButtonChangeFilterPlayerPatch(int value, ref TextMeshProUGUI ____filterPlayerText)
    {
        int step = IsShiftHeld() ? Plugin.EffectiveShiftSkipRate : 1;
        int delta = value == 1 ? step : -step;
        int next = Plugin.WrapLobbySize(MonoSingleton<MultiplayerManager>.I.FilterPlayerValue, delta);

        MonoSingleton<MultiplayerManager>.I.FilterPlayerValue = next;
        if (____filterPlayerText != null)
        {
            ____filterPlayerText.text = next.ToString();
        }

        return false;
    }

    [HarmonyPatch("ButtonChangeFilterListPlayer")]
    [HarmonyPrefix]
    private static bool ButtonChangeFilterListPlayerPatch(int value, ref TextMeshProUGUI ____filterListPlayerText)
    {
        int step = IsShiftHeld() ? Plugin.EffectiveShiftSkipRate : 1;
        int delta = value == 1 ? step : -step;
        int next = Plugin.WrapLobbyListFilter(MonoSingleton<MultiplayerManager>.I.FilterListPlayerValue, delta);

        MonoSingleton<MultiplayerManager>.I.FilterListPlayerValue = next;
        if (____filterListPlayerText != null)
        {
            ____filterListPlayerText.text = next == 0
                ? MonoSingleton<SettingsController>.I.AllString.String
                : next.ToString();
        }

        return false;
    }

    private static bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
