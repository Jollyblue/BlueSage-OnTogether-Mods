using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(InventoryManager), nameof(InventoryManager.DecreaseItemCount))]
internal static class UnlimitedConsumablesPatch
{
    private static bool Prefix(ShopItemType itemType)
    {
        if (!Plugin.ShouldApplyUnlimitedConsumables)
        {
            return true;
        }

        // Keep only the reviewed canteen consumables and Chalk0-Chalk4 from decrementing.
        // Clothing, rods, floaters, and unknown future items remain vanilla. Fishing bait is spent
        // outside the inventory and handled separately by ReusableBaitPatch.
        return !UnlimitedConsumablesPolicy.IsUnlimitedConsumable(itemType.ToString());
    }
}
