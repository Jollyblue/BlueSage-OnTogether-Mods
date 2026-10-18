using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal static class UnlimitedConsumablesPolicy
{
    private static readonly HashSet<string> ConsumableItemNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "CottonCandyPink",
        "LemonZestYellow",
        "BlueBerrySplashBlue",
        "GrapePopPurple",
        "SourAppleGreen",
        "ColaTurbo",
        "OrangeBlast",
        "LimeShock",
        "Boba1",
        "IceCream",
        "CottonCandy",
        "RainbowPopcorn",
        "Apple",
        "CottonGreen",
        "CottonPurple",
        "CottonLemon",
        "CottonBlue",
        "Water",
        "Boba2",
        "Boba3",
        "Chalk0",
        "Chalk1",
        "Chalk2",
        "Chalk3",
        "Chalk4"
    };

    // Fishing bait (Blue, 2026-10-17): vanilla spends one bait in FishingManager.StartMiniGame. Put back
    // exactly that one, and only bait the player owned; never create bait from nothing.
    internal static bool ShouldRestoreBait(bool applied, int countBefore, int countAfter)
    {
        return applied && countBefore > 0 && countAfter == countBefore - 1;
    }

    internal static bool IsUnlimitedConsumable(string itemTypeName)
    {
        return !string.IsNullOrWhiteSpace(itemTypeName) && ConsumableItemNames.Contains(itemTypeName.Trim());
    }
}
