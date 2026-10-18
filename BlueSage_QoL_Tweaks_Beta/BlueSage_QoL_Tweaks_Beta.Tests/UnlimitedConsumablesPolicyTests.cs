using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class UnlimitedConsumablesPolicyTests
{
    [TestCase("CottonCandyPink")]
    [TestCase("LemonZestYellow")]
    [TestCase("BlueBerrySplashBlue")]
    [TestCase("GrapePopPurple")]
    [TestCase("SourAppleGreen")]
    [TestCase("ColaTurbo")]
    [TestCase("OrangeBlast")]
    [TestCase("LimeShock")]
    [TestCase("Boba1")]
    [TestCase("Boba2")]
    [TestCase("Boba3")]
    [TestCase("IceCream")]
    [TestCase("CottonCandy")]
    [TestCase("CottonGreen")]
    [TestCase("CottonPurple")]
    [TestCase("CottonLemon")]
    [TestCase("CottonBlue")]
    [TestCase("RainbowPopcorn")]
    [TestCase("Apple")]
    [TestCase("Water")]
    [TestCase("Chalk0")]
    [TestCase("Chalk1")]
    [TestCase("Chalk2")]
    [TestCase("Chalk3")]
    [TestCase("Chalk4")]
    public void IsUnlimitedConsumable_RecognizesCurrentTicketConsumables(string itemName)
    {
        Assert.That(UnlimitedConsumablesPolicy.IsUnlimitedConsumable(itemName), Is.True);
    }

    [TestCase("None")]
    [TestCase("Bait")]
    [TestCase("Rod")]
    [TestCase("Floater")]
    [TestCase("FutureUnknownItem")]
    [TestCase("")]
    public void IsUnlimitedConsumable_LeavesNonConsumablesAndUnknownFutureItemsVanilla(string itemName)
    {
        Assert.That(UnlimitedConsumablesPolicy.IsUnlimitedConsumable(itemName), Is.False);
    }
}
