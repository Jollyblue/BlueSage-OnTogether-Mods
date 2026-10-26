using BlueSage.QoLTweaks.Core;
using NUnit.Framework;
using System;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 Stage S: the one-line session summary (quit and every 6 h) is the 0.2.7 stability bar.
[TestFixture]
internal sealed class SessionSummaryPolicyTests
{
    [Test]
    public void Host_line_carries_every_field_in_a_fixed_order()
    {
        string line = SessionSummaryPolicy.Format("6h", 2250.4, hosted: true, peakPlayers: 56, joins: 345, sweeps: 51,
            ledgerBytes: 5337848, ledgerWrites: 2250, journalActionRows: 150, journalStatusRows: 1276,
            heapStartMiB: 300.1, heapNowMiB: 5360.7, warnings: 110, errors: 0);

        Assert.That(line, Is.EqualTo(
            "[BlueSageSession] trigger=6h; uptimeMin=2250; hosted=yes; peakPlayers=56; joins=345; sweeps=51; ledgerKB=5212; ledgerWrites=2250; journalActions=150; journalStatus=1276; heapStartMiB=300.1; heapNowMiB=5360.7; warnings=110; errors=0."));
    }

    [Test]
    public void Guest_line_says_n_a_for_host_only_numbers()
    {
        string line = SessionSummaryPolicy.Format("quit", 30, hosted: false, peakPlayers: 0, joins: 0, sweeps: 1,
            ledgerBytes: 1024, ledgerWrites: 3, journalActionRows: 0, journalStatusRows: 0,
            heapStartMiB: 200, heapNowMiB: 250, warnings: 0, errors: 0);

        Assert.Multiple(() =>
        {
            Assert.That(line, Does.Contain("hosted=no"));
            Assert.That(line, Does.Contain("peakPlayers=n/a"));
            Assert.That(line, Does.Contain("joins=n/a"));
            Assert.That(line, Does.Contain("journalActions=n/a"));
            Assert.That(line, Does.Contain("journalStatus=n/a"));
            Assert.That(line, Does.Contain("sweeps=1"));
        });
    }

    [Test]
    public void Trigger_is_sanitized_and_never_empty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SessionSummaryPolicy.Format("", 0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), Does.Contain("trigger=unknown"));
            Assert.That(SessionSummaryPolicy.Format("application quit!", 0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), Does.Contain("trigger=application-quit-"));
        });
    }

    [Test]
    public void Six_hour_cadence()
    {
        long start = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc).Ticks;
        Assert.Multiple(() =>
        {
            Assert.That(SessionSummaryPolicy.IsDue(0, start), Is.False, "nothing is due before the session started");
            Assert.That(SessionSummaryPolicy.IsDue(start, start + TimeSpan.TicksPerHour * 5), Is.False);
            Assert.That(SessionSummaryPolicy.IsDue(start, start + TimeSpan.TicksPerHour * 6), Is.True);
        });
    }

    [Test]
    public void Line_never_carries_ids_or_names()
    {
        string line = SessionSummaryPolicy.Format("6h", 10, true, 3, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Multiple(() =>
        {
            Assert.That(line, Does.Not.Contain("7656"));
            Assert.That(line, Does.Not.Contain("lobby"));
            Assert.That(line, Does.Not.Contain("name"));
        });
    }
}
