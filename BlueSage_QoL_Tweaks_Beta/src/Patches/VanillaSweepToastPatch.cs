using System;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

// The game runs its own memory cleanup every 30 minutes (MainSceneManager.Update starts
// SweepRoutine, which calls Resources.UnloadUnusedAssets behind a small "sweeping" icon). A
// postfix on the iterator method fires when that coroutine is created, i.e. as the cleanup starts,
// so players know why the game may hitch for a moment. BlueSage's own sweeps also create this
// iterator (by reflection) and announce themselves, so those are skipped here.
// Voice pass (2026-10-23): automatic cleanups are quiet by default, so this toast shows only when
// NotifyOnSweep (cleanup notices) is on.
[HarmonyPatch(typeof(MainSceneManager), "SweepRoutine")]
internal static class VanillaSweepToastPatch
{
    internal const string VanillaSweepToastText = SweepMenuActionPolicy.GameCleanupToast;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!SweepMenuActionPolicy.ShouldToastGameCleanup(Plugin.IsQolSweepCreatingNativeRoutine, Plugin.NotifyOnSweep?.Value == true))
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
