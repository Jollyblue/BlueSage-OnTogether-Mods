using System;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

// The game runs its own memory cleanup every 30 minutes (MainSceneManager.Update starts
// SweepRoutine, which calls Resources.UnloadUnusedAssets behind a small "sweeping" icon). A
// postfix on the iterator method fires when that coroutine is created, i.e. as the cleanup starts,
// so players know why the game may hitch for a moment. BlueSage's own sweeps also create this
// iterator (by reflection) and announce themselves, so those are skipped here.
[HarmonyPatch(typeof(MainSceneManager), "SweepRoutine")]
internal static class VanillaSweepToastPatch
{
    internal const string VanillaSweepToastText = "The game is doing its own 30-minute tidy-up; a short hitch is normal.";

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (Plugin.IsQolSweepCreatingNativeRoutine)
        {
            return;
        }

        try
        {
            Plugin.Instance?.ShowToast(VanillaSweepToastText);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Vanilla sweep notice skipped: " + ex.GetType().Name + ".");
        }
    }
}
