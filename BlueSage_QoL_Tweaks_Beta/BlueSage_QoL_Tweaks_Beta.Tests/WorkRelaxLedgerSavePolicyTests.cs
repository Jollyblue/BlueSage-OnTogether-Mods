using System;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage G (Marbles, 2026-10-20): the ledger used to be serialized, fsynced and replaced inside the game's
// autosave frame. Now the autosave postfix only marks the ledger dirty; a later frame decides when to write, and
// the write itself runs off the main thread. These rules decide "when".
public sealed class WorkRelaxLedgerSavePolicyTests
{
    private static readonly long Second = TimeSpan.TicksPerSecond;

    [Test]
    public void Routine_writes_wait_at_least_five_minutes_and_prompt_writes_one_second()
    {
        // Round 5 Stage A: 60 s -> 300 s (the ledger was rewritten every minute for 37.5 h).
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerSavePolicy.RoutineIntervalTicks, Is.EqualTo(300 * Second));
            Assert.That(WorkRelaxLedgerSavePolicy.PromptDelayTicks, Is.EqualTo(1 * Second));
        });
    }

    [Test]
    public void Clean_ledger_never_writes()
    {
        Assert.That(WorkRelaxLedgerSavePolicy.IsDue(dirty: false, prompt: true, dirtySinceTicks: 0, lastWriteTicks: 0, nowTicks: 1000 * Second, writeInFlight: false), Is.False);
    }

    [Test]
    public void Never_writes_on_the_frame_that_marked_it_dirty()
    {
        long now = 500 * Second;
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerSavePolicy.IsDue(dirty: true, prompt: false, dirtySinceTicks: now, lastWriteTicks: 0, nowTicks: now, writeInFlight: false), Is.False);
            Assert.That(WorkRelaxLedgerSavePolicy.IsDue(dirty: true, prompt: true, dirtySinceTicks: now, lastWriteTicks: 0, nowTicks: now, writeInFlight: false), Is.False);
        });
    }

    [Test]
    public void Routine_dirty_ledger_writes_once_the_routine_interval_since_the_last_write_passed()
    {
        long lastWrite = 100 * Second;
        long dirtySince = 101 * Second;
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerSavePolicy.IsDue(true, false, dirtySince, lastWrite, 160 * Second, false), Is.False, "60 s after the last write: too soon");
            Assert.That(WorkRelaxLedgerSavePolicy.IsDue(true, false, dirtySince, lastWrite, 399 * Second, false), Is.False, "299 s after the last write: too soon");
            Assert.That(WorkRelaxLedgerSavePolicy.IsDue(true, false, dirtySince, lastWrite, 400 * Second, false), Is.True, "300 s after the last write: due");
        });
    }

    [Test]
    public void Prompt_dirty_ledger_writes_one_second_after_being_marked()
    {
        long lastWrite = 100 * Second;
        long dirtySince = 101 * Second;
        Assert.Multiple(() =>
        {
            Assert.That(WorkRelaxLedgerSavePolicy.IsDue(true, true, dirtySince, lastWrite, dirtySince + Second / 2, false), Is.False);
            Assert.That(WorkRelaxLedgerSavePolicy.IsDue(true, true, dirtySince, lastWrite, dirtySince + Second, false), Is.True);
        });
    }

    [Test]
    public void A_write_in_flight_blocks_the_next_one()
    {
        Assert.That(WorkRelaxLedgerSavePolicy.IsDue(true, true, 10 * Second, 0, 1000 * Second, writeInFlight: true), Is.False);
    }

    [Test]
    public void A_clock_that_went_backwards_does_not_lock_the_ledger_forever()
    {
        // lastWrite in the "future" (session restart, clock change): treat the interval as elapsed.
        Assert.That(WorkRelaxLedgerSavePolicy.IsDue(true, false, 5 * Second, 10_000 * Second, 10 * Second, false), Is.True);
    }
}
