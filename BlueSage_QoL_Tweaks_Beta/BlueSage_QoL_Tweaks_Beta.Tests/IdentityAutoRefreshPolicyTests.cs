using System;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage E #5 (Blue, 2026-10-19 audit): force-refresh button plus a 15-minute check while hosting. The check
// rescans and relabels from already-available state and reports in the host journal; it never moderates.
[TestFixture]
public sealed class IdentityAutoRefreshPolicyTests
{
    private const long T0 = 638_000_000_000_000_000L;

    [Test]
    public void Automatic_refresh_arms_on_the_first_host_tick_and_runs_every_fifteen_minutes()
    {
        long interval = IdentityAutoRefreshPolicy.IntervalTicks;
        Assert.Multiple(() =>
        {
            Assert.That(interval, Is.EqualTo(TimeSpan.FromMinutes(15).Ticks));
            Assert.That(IdentityAutoRefreshPolicy.ShouldRun(false, 0, T0, out long notHost), Is.False, "Guests never run it.");
            Assert.That(notHost, Is.EqualTo(T0), "A guest keeps re-arming so a later host session waits a full interval.");
            Assert.That(IdentityAutoRefreshPolicy.ShouldRun(true, 0, T0, out long armed), Is.False, "The first host tick arms, it does not refresh a fresh lobby.");
            Assert.That(armed, Is.EqualTo(T0));
            Assert.That(IdentityAutoRefreshPolicy.ShouldRun(true, T0, T0 + interval - 1, out long waiting), Is.False);
            Assert.That(waiting, Is.EqualTo(T0), "Waiting keeps the original arm time.");
            Assert.That(IdentityAutoRefreshPolicy.ShouldRun(true, T0, T0 + interval, out long ran), Is.True);
            Assert.That(ran, Is.EqualTo(T0 + interval), "The run re-arms from now.");
            Assert.That(IdentityAutoRefreshPolicy.ShouldRun(true, T0 + interval, T0, out long reset), Is.False, "A clock that went backwards re-arms instead of running.");
            Assert.That(reset, Is.EqualTo(T0));
        });
    }

    [Test]
    public void Automatic_refresh_is_wired_as_a_local_relabel_with_a_journal_row_and_the_menu_says_so()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string controller = File.ReadAllText(Path.Combine(root, "src", "IdentityResyncController.cs")).Replace("\r\n", "\n");
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        int start = controller.IndexOf("private static void RunAutomaticRefreshIfDue(", StringComparison.Ordinal);
        string body = start < 0 ? string.Empty : controller.Substring(start, controller.IndexOf("\n    }\n", start, StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(start, Is.GreaterThan(0));
            Assert.That(controller, Does.Contain("RunAutomaticRefreshIfDue(nowMilliseconds);"), "Driven from Tick.");
            Assert.That(body, Does.Contain("IdentityAutoRefreshPolicy.ShouldRun(isHost, _lastAutoRefreshUtcTicks, nowTicks, out _lastAutoRefreshUtcTicks)"));
            Assert.That(body, Does.Contain("RefreshLocalSurfaces();"), "Rescan and relabel from already-available state only.");
            Assert.That(body, Does.Not.Contain("TryExecuteRepair(").And.Not.Contain("TryExecuteHostAction(").And.Not.Contain("BanRPC"), "Never a moderation action or an authoritative repair on its own.");
            // Round 5 Stage B: the journal records decisions and changes, not heartbeats. In Blue's 37.5 h host all
            // 150 action rows were this tick; real moderation rows would have scrolled out of the box after ~25 h.
            Assert.That(body, Does.Not.Contain("HostJournalController.BeginSurface("), "A maintenance tick writes no journal row.");
            Assert.That(body, Does.Not.Contain("HostJournalController.RecordAction("), "A maintenance tick writes no journal row.");
            Assert.That(body, Does.Not.Contain("HostJournalController.RecordHostHealth("), "No status heartbeat from the tick either.");
            Assert.That(menu, Does.Contain("Runs by itself every 15 minutes while you host"));
        });
    }
}
