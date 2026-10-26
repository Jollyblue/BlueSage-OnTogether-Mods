using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 C1 + F: one hysteresis rule for every "state held" log line (Clone Shield alignment waits, identity
// coverage flips). A flip must hold 60 s before it is announced; a persisting bad state reminds once an hour.
[TestFixture]
internal sealed class StableStateLogPolicyTests
{
    [Test]
    public void First_state_is_announced_at_once()
    {
        var state = new StableStateLogState();
        Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", remindWhileHeld: true, now: 10f), Is.EqualTo(StableStateLogDecision.Announce));
        Assert.That(state.AnnouncedKey, Is.EqualTo("waiting"));
    }

    [Test]
    public void A_flip_shorter_than_the_hold_logs_nothing()
    {
        var state = new StableStateLogState();
        StableStateLogPolicy.Observe(ref state, "aligned", remindWhileHeld: false, now: 0f);
        Assert.Multiple(() =>
        {
            Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 6f), Is.EqualTo(StableStateLogDecision.None));
            Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 30f), Is.EqualTo(StableStateLogDecision.None));
            Assert.That(StableStateLogPolicy.Observe(ref state, "aligned", false, now: 36f), Is.EqualTo(StableStateLogDecision.None));
            Assert.That(state.AnnouncedKey, Is.EqualTo("aligned"));
        });
    }

    [Test]
    public void A_flip_that_holds_is_announced_once_then_reminded_hourly()
    {
        var state = new StableStateLogState();
        StableStateLogPolicy.Observe(ref state, "aligned", false, now: 0f);
        StableStateLogPolicy.Observe(ref state, "waiting", true, now: 6f);
        Assert.Multiple(() =>
        {
            Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 66f), Is.EqualTo(StableStateLogDecision.Announce));
            Assert.That(state.AnnouncedAt, Is.EqualTo(6f), "the announced age counts from when the state began, not from the announcement");
            Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 1000f), Is.EqualTo(StableStateLogDecision.None));
            Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 66f + 3600f), Is.EqualTo(StableStateLogDecision.Reminder));
            Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 66f + 3600f + 10f), Is.EqualTo(StableStateLogDecision.None));
            Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 66f + 7200f), Is.EqualTo(StableStateLogDecision.Reminder));
            Assert.That(state.AnnouncedAgeSeconds(66f + 7200f), Is.EqualTo(7260f));
        });
    }

    [Test]
    public void Recovery_is_announced_only_after_it_holds_and_a_good_state_never_reminds()
    {
        var state = new StableStateLogState();
        StableStateLogPolicy.Observe(ref state, "waiting", true, now: 0f);
        Assert.Multiple(() =>
        {
            Assert.That(StableStateLogPolicy.Observe(ref state, "aligned", false, now: 5f), Is.EqualTo(StableStateLogDecision.None));
            Assert.That(StableStateLogPolicy.Observe(ref state, "aligned", false, now: 65f), Is.EqualTo(StableStateLogDecision.Announce));
            Assert.That(StableStateLogPolicy.Observe(ref state, "aligned", false, now: 65f + 7200f), Is.EqualTo(StableStateLogDecision.None));
        });
    }

    [Test]
    public void Seventeen_hours_of_churn_produce_one_start_and_hourly_reminders_only()
    {
        var state = new StableStateLogState();
        StableStateLogPolicy.Observe(ref state, "aligned", false, now: 0f);
        int announces = 0;
        int reminders = 0;
        // One poll every 6 s for 17.5 h, always waiting (the roster never fully aligned).
        for (float now = 6f; now <= 17.5f * 3600f; now += 6f)
        {
            switch (StableStateLogPolicy.Observe(ref state, "waiting", true, now))
            {
                case StableStateLogDecision.Announce: announces++; break;
                case StableStateLogDecision.Reminder: reminders++; break;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(announces, Is.EqualTo(1));
            Assert.That(reminders, Is.EqualTo(17));
        });
    }

    [Test]
    public void Reset_forgets_everything()
    {
        var state = new StableStateLogState();
        StableStateLogPolicy.Observe(ref state, "waiting", true, now: 0f);
        StableStateLogPolicy.Reset(ref state);
        Assert.That(state.AnnouncedKey, Is.Null);
        Assert.That(StableStateLogPolicy.Observe(ref state, "waiting", true, now: 1f), Is.EqualTo(StableStateLogDecision.Announce));
    }
}
