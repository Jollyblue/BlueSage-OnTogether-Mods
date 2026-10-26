using NUnit.Framework;
using System.IO;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 Stage A wiring: the runtime folds once at load, folds again when the UTC day rolls, and the model
// merges through the policy with correction-targeted rows frozen.
[TestFixture]
internal sealed class WorkRelaxRoundFiveContractTests
{
    private static string ReadSource(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
        return File.ReadAllText(Path.Combine(root, "src", relative));
    }

    [Test]
    public void Runtime_coalesces_once_at_load_and_compacts_on_the_day_roll()
    {
        string runtime = ReadSource("WorkRelaxRuntimeController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(runtime, Does.Contain("int coalesced = _ledger.CoalesceHistory();"));
            Assert.That(runtime, Does.Contain("WorkRelaxLedgerCompactionSchedulePolicy.IsDue(_lastCompactionUtcDay, nowUtcTicks)"));
            Assert.That(runtime, Does.Contain("MarkDirty(prompt: false, \"day-boundary compaction\");"));
            Assert.That(runtime, Does.Contain("MarkDirty(prompt: true, \"history coalescing\");"));
        });
    }

    [Test]
    public void Model_merges_through_the_policy_and_keeps_the_copy_contract()
    {
        string model = ReadSource(Path.Combine("Core", "WorkRelaxLedgerModels.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(model, Does.Contain("WorkRelaxLedgerCoalescingPolicy.FindMergeIndex(_events, ledgerEvent, IsTargetedByCorrection)"));
            Assert.That(model, Does.Contain("JoinStretch(_events, mergeIndex, ledgerEvent);"));
            Assert.That(model, Does.Contain("events.RemoveAt(index);"), "a joined stretch moves to the end so the list stays ordered by last tick");
            Assert.That(model, Does.Contain("public int CoalesceHistory("));
        });
    }
}
