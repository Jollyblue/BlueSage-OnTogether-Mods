using NUnit.Framework;
using System;
using System.IO;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 Stages S and D: the session summary is fed by the code that already does the work (no polling), and
// the sweep line carries Unity's own memory counters with "n/a" for a counter this build does not provide.
[TestFixture]
internal sealed class SessionSummaryContractTests
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
    public void Summary_is_wired_to_update_quit_log_events_sweeps_journal_ledger_and_roster()
    {
        string plugin = ReadSource("Plugin.cs");
        string journal = ReadSource("HostJournalController.cs");
        string store = ReadSource("WorkRelaxLedgerStore.cs");
        string controller = ReadSource("SessionSummaryController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("Log.LogEvent += SessionSummaryController.OnLogEvent;"));
            Assert.That(plugin, Does.Contain("SessionSummaryController.Tick(DateTime.UtcNow.Ticks);"));
            Assert.That(plugin, Does.Contain("SessionSummaryController.OnShutdown("));
            Assert.That(plugin, Does.Contain("SessionSummaryController.NoteSweep();"));
            Assert.That(journal, Does.Contain("SessionSummaryController.NoteJournalRow(true);"));
            Assert.That(journal, Does.Contain("SessionSummaryController.NoteJournalRow(false);"));
            Assert.That(journal, Does.Contain("SessionSummaryController.NoteRoster(steamCount);"));
            Assert.That(store, Does.Contain("LastWrittenBytes"));
            Assert.That(store, Does.Contain("WriteCount"));
            Assert.That(controller, Does.Contain("SessionSummaryPolicy.Format("));
            Assert.That(controller, Does.Not.Contain("FindObjectsOfTypeAll"));
            Assert.That(controller, Does.Not.Contain("Update()"), "no per-frame work of its own");
        });
    }

    [Test]
    public void Sweep_lines_carry_the_four_profiler_reads_with_n_a_for_missing_counters()
    {
        string plugin = ReadSource("Plugin.cs");
        string probe = ReadSource("SweepMemoryProbe.cs");
        int start = plugin.IndexOf("private IEnumerator CompleteSweepOperation(int sequence, string reason)", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0));
        string completion = plugin.Substring(start, Math.Min(6000, plugin.Length - start));
        Assert.Multiple(() =>
        {
            Assert.That(probe, Does.Contain("Profiler.GetTotalAllocatedMemoryLong"));
            Assert.That(probe, Does.Contain("Profiler.GetTotalReservedMemoryLong"));
            Assert.That(probe, Does.Contain("Profiler.GetMonoHeapSizeLong"));
            Assert.That(probe, Does.Contain("Profiler.GetMonoUsedSizeLong"));
            Assert.That(probe, Does.Not.Contain("FindObjectsOfTypeAll"));
            Assert.That(completion, Does.Contain("SweepMemoryProbe.Capture()"));
            Assert.That(completion, Does.Contain("SweepMemoryReadingPolicy.FormatMiB("));
            Assert.That(completion, Does.Contain("SweepMemoryReadingPolicy.FormatDeltaMiB("));
            Assert.That(completion, Does.Contain("nativeAllocMiB="));
            Assert.That(completion, Does.Contain("monoUsedMiB="));
        });
    }
}
