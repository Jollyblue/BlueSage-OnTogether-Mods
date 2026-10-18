using System;
using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

// Reusable Consumables covers fishing bait once bought (Blue, 2026-10-17). Vanilla StartMiniGame does
// _baitCounts[CurrentBait - 1]-- and saves; the prefix records the count, the postfix puts that one
// bait back and saves again. Any surprise (missing field, changed list) leaves vanilla untouched.
[HarmonyPatch(typeof(FishingManager), "StartMiniGame")]
internal static class ReusableBaitPatch
{
    private static readonly AccessTools.FieldRef<FishingManager, List<int>> BaitCounts =
        TryFieldRef();

    // The slot is captured before vanilla runs, so the postfix restores the bait that was actually
    // spent even if CurrentBait changes during the call (review 2026-10-17).
    internal sealed class SpentBait
    {
        internal int Index = -1;
        internal int CountBefore = -1;
    }

    [HarmonyPrefix]
    private static void Prefix(FishingManager __instance, out SpentBait __state)
    {
        __state = new SpentBait();
        int index = BaitIndex(__instance);
        List<int> counts = BaitCounts != null ? BaitCounts(__instance) : null;
        if (counts != null && index >= 0 && index < counts.Count)
        {
            __state.Index = index;
            __state.CountBefore = counts[index];
        }
    }

    [HarmonyPostfix]
    private static void Postfix(FishingManager __instance, SpentBait __state)
    {
        try
        {
            int index = __state?.Index ?? -1;
            List<int> counts = BaitCounts != null ? BaitCounts(__instance) : null;
            if (counts == null || index < 0 || index >= counts.Count ||
                !UnlimitedConsumablesPolicy.ShouldRestoreBait(Plugin.ShouldApplyUnlimitedConsumables, __state.CountBefore, counts[index]))
            {
                return;
            }

            counts[index] = __state.CountBefore;
            MonoSingleton<DataManager>.I.SavePlayerZipData();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Reusable bait skipped: " + ex.GetType().Name + ".");
        }
    }

    private static int BaitIndex(FishingManager manager)
    {
        return manager == null || manager.CurrentBait == BaitType.None ? -1 : (int)manager.CurrentBait - 1;
    }

    private static AccessTools.FieldRef<FishingManager, List<int>> TryFieldRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<FishingManager, List<int>>("_baitCounts");
        }
        catch (Exception)
        {
            return null;
        }
    }
}
