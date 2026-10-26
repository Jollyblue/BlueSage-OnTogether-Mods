using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 WS5 (Blue, 2026-10-19): no repeat background calls. Statistics reads are already event-driven (menu
// open, Refresh, the game's own stat events and autosave), but while a native manager is missing (menu,
// loading) every read re-ran Resources.FindObjectsOfTypeAll over all loaded objects. A miss now waits.
[TestFixture]
public sealed class NativeLookupCooldownTests
{
    [TestCase(-1f, 10f, true, TestName = "Never_missed_scans")]
    [TestCase(10f, 12f, false, TestName = "Recent_miss_waits")]
    [TestCase(10f, 15f, true, TestName = "Cooldown_elapsed_scans_again")]
    [TestCase(10f, 9f, true, TestName = "Clock_reset_scans")]
    public void A_missing_manager_is_not_rescanned_every_call(float lastMissAt, float now, bool expected)
    {
        Assert.That(NativeLookupCooldownPolicy.ShouldScan(lastMissAt, now, NativeLookupCooldownPolicy.MissCooldownSeconds), Is.EqualTo(expected));
    }

    [Test]
    public void Cache_records_misses_and_consults_the_cooldown_before_scanning()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string source = File.ReadAllText(Path.Combine(root, "src", "StatisticsPulseReader.cs")).Replace("\r\n", "\n");
        int start = source.IndexOf("internal static UnityEngine.Object FindLiveObject(Type type)", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(body.IndexOf("NativeLookupCooldownPolicy.ShouldScan(", System.StringComparison.Ordinal), Is.GreaterThan(0)
                .And.LessThan(body.IndexOf("Resources.FindObjectsOfTypeAll(type)", System.StringComparison.Ordinal)));
            Assert.That(body, Does.Contain("LastMissAt[type] = now;"));
            Assert.That(body, Does.Contain("LastMissAt.Remove(type);"));
        });
    }
}
