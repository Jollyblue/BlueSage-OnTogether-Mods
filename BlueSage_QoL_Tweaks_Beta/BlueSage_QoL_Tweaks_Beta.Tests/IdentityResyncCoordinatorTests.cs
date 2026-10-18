using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityResyncCoordinatorTests
{
    [Test]
    public void Generation_waits_for_500ms_quiet_window_before_one_transaction()
    {
        var coordinator = new IdentityResyncCoordinator();
        long generation = coordinator.ObserveScope("lobby-a", "owner-a", 1_000);
        generation = coordinator.ObserveTrigger(
            "lobby-a",
            "owner-a",
            IdentityResyncTrigger.RowInitialized,
            1_100);

        Assert.Multiple(() =>
        {
            Assert.That(
                coordinator.TryBegin("lobby-a", generation, 1_599, out _, out IdentityResyncBeginDecision early),
                Is.False);
            Assert.That(early.Reason, Is.EqualTo(IdentityResyncBeginReason.QuietWindow));
            Assert.That(
                coordinator.TryBegin("lobby-a", generation, 1_600, out IdentityResyncLease lease, out _),
                Is.True);
            Assert.That(coordinator.TryBegin("lobby-a", generation, 1_600, out _, out IdentityResyncBeginDecision concurrent), Is.False);
            Assert.That(concurrent.Reason, Is.EqualTo(IdentityResyncBeginReason.SingleFlight));
            coordinator.Complete(lease, 1_610);
            Assert.That(coordinator.TryBegin("lobby-a", generation, 7_000, out _, out IdentityResyncBeginDecision duplicate), Is.False);
            Assert.That(duplicate.Reason, Is.EqualTo(IdentityResyncBeginReason.GenerationAlreadyCompleted));
        });
    }

    [Test]
    public void Global_cooldown_applies_across_generations()
    {
        var coordinator = new IdentityResyncCoordinator();
        long first = coordinator.ObserveScope("lobby-a", "owner-a", 0);
        Assert.That(coordinator.TryBegin("lobby-a", first, 500, out IdentityResyncLease lease, out _), Is.True);
        coordinator.Complete(lease, 510);

        long second = coordinator.ObserveTrigger(
            "lobby-a",
            "owner-a",
            IdentityResyncTrigger.RowDisabledOrDespawned,
            1_000);

        Assert.Multiple(() =>
        {
            Assert.That(coordinator.TryBegin("lobby-a", second, 1_500, out _, out IdentityResyncBeginDecision throttled), Is.False);
            Assert.That(throttled.Reason, Is.EqualTo(IdentityResyncBeginReason.GlobalCooldown));
            Assert.That(throttled.RetryAfterMilliseconds, Is.EqualTo(4_000));
            Assert.That(coordinator.TryBegin("lobby-a", second, 5_500, out _, out _), Is.True);
        });
    }

    [Test]
    public void Migrated_lobby_owner_or_stale_generation_is_refused()
    {
        var coordinator = new IdentityResyncCoordinator();
        long oldGeneration = coordinator.ObserveScope("lobby-a", "owner-a", 100);
        long newGeneration = coordinator.ObserveScope("lobby-b", "owner-b", 200);

        Assert.Multiple(() =>
        {
            Assert.That(coordinator.TryBegin("lobby-a", oldGeneration, 700, out _, out IdentityResyncBeginDecision migrated), Is.False);
            Assert.That(migrated.Reason, Is.EqualTo(IdentityResyncBeginReason.ScopeMigrated));
            Assert.That(coordinator.TryBegin("lobby-b", oldGeneration, 700, out _, out IdentityResyncBeginDecision stale), Is.False);
            Assert.That(stale.Reason, Is.EqualTo(IdentityResyncBeginReason.StaleGeneration));
            Assert.That(newGeneration, Is.GreaterThan(oldGeneration));
        });
    }

    [Test]
    public void Authority_membership_change_reopens_a_completed_generation_after_the_global_cooldown()
    {
        var coordinator = new IdentityResyncCoordinator();
        long first = coordinator.ObserveScope(
            "lobby-a",
            "owner-a",
            0);
        Assert.That(
            coordinator.TryBegin(
                "lobby-a",
                first,
                500,
                out IdentityResyncLease firstLease,
                out _),
            Is.True);
        coordinator.Complete(firstLease, 510);

        long changed = coordinator.ObserveTrigger(
            "lobby-a",
            "owner-a",
            IdentityResyncTrigger.AuthorityMembershipChanged,
            5_500);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.GreaterThan(first));
            Assert.That(
                coordinator.TryBegin(
                    "lobby-a",
                    changed,
                    6_000,
                    out _,
                    out IdentityResyncBeginDecision decision),
                Is.True,
                decision.Reason.ToString());
        });
    }

    [Test]
    public void Temporary_authority_unavailability_then_recovery_advances_beyond_the_completed_generation()
    {
        var coordinator = new IdentityResyncCoordinator();
        long first = coordinator.ObserveScope(
            "lobby-a",
            "owner-a",
            0);
        Assert.That(
            coordinator.TryBegin(
                "lobby-a",
                first,
                500,
                out IdentityResyncLease firstLease,
                out _),
            Is.True);
        coordinator.Complete(firstLease, 510);

        coordinator.ObserveScope(string.Empty, string.Empty, 1_000);
        long recovered = coordinator.ObserveScope(
            "lobby-a",
            "owner-a",
            5_500);

        Assert.Multiple(() =>
        {
            Assert.That(recovered, Is.GreaterThan(first));
            Assert.That(
                coordinator.TryBegin(
                    "lobby-a",
                    recovered,
                    6_000,
                    out _,
                    out IdentityResyncBeginDecision decision),
                Is.True,
                decision.Reason.ToString());
        });
    }
}
