using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Blue (2026-10-17): Reusable Consumables also covers fishing bait once bought. Vanilla spends bait
// in FishingManager.StartMiniGame (_baitCounts[CurrentBait - 1]--, then a save) when a bite turns into
// the reel mini-game; BlueSage puts exactly that one bait back. No bait is ever created from nothing.
public sealed class ReusableBaitTests
{
    [Test]
    public void One_spent_bait_is_put_back_when_reusable_consumables_is_on()
    {
        Assert.That(UnlimitedConsumablesPolicy.ShouldRestoreBait(applied: true, countBefore: 3, countAfter: 2), Is.True);
    }

    [TestCase(false, 3, 2, TestName = "Feature_off_keeps_vanilla_bait_use")]
    [TestCase(true, 0, -1, TestName = "Never_restores_bait_the_player_did_not_own")]
    [TestCase(true, 3, 3, TestName = "Nothing_to_restore_when_vanilla_spent_nothing")]
    [TestCase(true, 3, 1, TestName = "Unexpected_double_spend_is_left_alone")]
    public void Bait_is_only_restored_for_exactly_one_owned_bait_spent(bool applied, int before, int after)
    {
        Assert.That(UnlimitedConsumablesPolicy.ShouldRestoreBait(applied, before, after), Is.False);
    }

    [Test]
    public void Bait_patch_brackets_the_vanilla_spend_and_saves_the_restored_count()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string patch = File.ReadAllText(Path.Combine(root, "src", "Patches", "ReusableBaitPatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(FishingManager), \"StartMiniGame\")]"));
            Assert.That(patch, Does.Contain("[HarmonyPrefix]"));
            Assert.That(patch, Does.Contain("[HarmonyPostfix]"));
            Assert.That(patch, Does.Contain("UnlimitedConsumablesPolicy.ShouldRestoreBait(Plugin.ShouldApplyUnlimitedConsumables"));
            Assert.That(patch, Does.Contain("SavePlayerZipData()"));
            Assert.That(patch, Does.Contain("int index = __state?.Index ?? -1;"), "Restores the slot captured before vanilla ran.");
        });
    }
}
