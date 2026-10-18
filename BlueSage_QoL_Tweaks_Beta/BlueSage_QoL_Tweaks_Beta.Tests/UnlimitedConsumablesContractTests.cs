using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class UnlimitedConsumablesContractTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void RuntimePatch_IsDefaultOnReversibleAndDoesNotPatchTicketWallet()
    {
        string plugin = File.ReadAllText(Path.Combine(RepoRoot, "src", "Plugin.cs"));
        string patch = File.ReadAllText(Path.Combine(RepoRoot, "src", "Patches", "UnlimitedConsumablesPatch.cs"));
        string menu = File.ReadAllText(Path.Combine(RepoRoot, "src", "QolMenuWindow.cs"));

        Assert.That(plugin, Does.Contain("EnableUnlimitedConsumables = Config.Bind(\"Consumables\", \"EnableUnlimitedConsumables\", true"));
        Assert.That(patch, Does.Contain("ShouldApplyUnlimitedConsumables"));
        Assert.That(patch, Does.Contain("InventoryManager.DecreaseItemCount"));
        Assert.That(patch, Does.Not.Contain("EconomyManager"));
        Assert.That(patch, Does.Not.Contain("RemoveTicket"));
        Assert.That(menu, Does.Contain("DrawToggle(\"Reusable Consumables\""));
        Assert.That(menu, Does.Contain("Turn Off to restore normal item use immediately"));
        Assert.That(menu, Does.Contain("chalk color, or fishing bait, using it no longer lowers the count"));
        Assert.That(plugin, Does.Contain("\"unlimitedconsumables\""));

        // Fishing bait (Blue, 2026-10-17) has its own patch; it may only put back the bait count and
        // must never touch tickets, the shop, or catch rewards.
        string baitPatch = File.ReadAllText(Path.Combine(RepoRoot, "src", "Patches", "ReusableBaitPatch.cs"));
        Assert.That(baitPatch, Does.Contain("ShouldApplyUnlimitedConsumables"));
        Assert.That(baitPatch, Does.Not.Contain("EconomyManager"));
        Assert.That(baitPatch, Does.Not.Contain("RemoveTicket"));
        Assert.That(baitPatch, Does.Not.Contain("AddFishTicket"));
        Assert.That(baitPatch, Does.Not.Contain("ShopItemType"));
    }
}
