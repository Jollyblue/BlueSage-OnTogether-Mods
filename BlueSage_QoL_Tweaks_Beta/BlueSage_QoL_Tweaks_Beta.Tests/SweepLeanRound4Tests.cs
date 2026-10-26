using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 WS2 (Blue, 2026-10-19): the deep sweep diagnostics were early iterations that bloat the log
// and cause more hitches than they help. Every sweep scanned all loaded assets twice and enumerated
// every OS thread twice. They are removed. (The existing coordination already keeps BlueSage's
// 15-minute cadence from landing next to the game's own 30-minute cleanup.)
[TestFixture]
public sealed class SweepLeanRound4Tests
{
    private static string Root()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return root;
    }

    private static string Plugin => File.ReadAllText(Path.Combine(Root(), "src", "Plugin.cs")).Replace("\r\n", "\n");

    [Test]
    public void Deep_sweep_evidence_is_gone_from_the_plugin()
    {
        string src = Path.Combine(Root(), "src");
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(src, "SweepResourceEvidenceCapture.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(src, "SweepSessionEvidenceCapture.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(src, "Core", "SweepResourceEvidence.cs")), Is.False);
            Assert.That(File.Exists(Path.Combine(src, "Core", "SweepSessionEvidence.cs")), Is.False);
            Assert.That(Plugin, Does.Not.Contain("EvidenceCapture.Capture()"));
            Assert.That(Plugin, Does.Not.Contain("EvidenceFormatter.Format("));
        });
    }

    [Test]
    public void No_sweep_path_scans_every_loaded_object()
    {
        string[] offenders = Directory.GetFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("StatisticsPulseReader.cs", System.StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("Resources.FindObjectsOfTypeAll"))
            .Select(Path.GetFileName)
            .ToArray();
        Assert.That(offenders, Is.Empty, "StatisticsPulseReader's cached lookup is tracked separately (WS5).");
    }

    [Test]
    public void Completed_sweep_logs_one_short_line_and_leaves_game_state_read_only()
    {
        string plugin = Plugin;
        int start = plugin.IndexOf("private IEnumerator CompleteSweepOperation(", System.StringComparison.Ordinal);
        string body = plugin.Substring(start, plugin.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Not.Contain("SetValue("), "The game's own cleanup timer is read, never written.");
            Assert.That(body, Does.Contain("phase=complete; seq={sequence}"));
            Assert.That(body, Does.Not.Contain("resourceEvidence"));
            Assert.That(body, Does.Not.Contain("sessionEvidence"));
        });
    }
}
