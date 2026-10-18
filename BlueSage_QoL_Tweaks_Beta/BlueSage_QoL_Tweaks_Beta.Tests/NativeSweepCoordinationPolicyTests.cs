using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class NativeSweepCoordinationPolicyTests
{
    [Test]
    public void Automatic_cleanup_runs_when_due_and_vanilla_is_outside_its_boundary_window()
    {
        NativeSweepCoordinationDecision decision = NativeAssetSweepPolicy.DecideAutomaticSweep(
            automaticDue: true,
            blueSageSweepRunning: false,
            vanillaStateAvailable: true,
            vanillaTimerSeconds: 600f,
            vanillaDurationSeconds: 1800f,
            vanillaSweepActive: false);

        Assert.That(decision, Is.EqualTo(NativeSweepCoordinationDecision.Run));
    }

    [TestCase(1755f, false)]
    public void Automatic_cleanup_defers_before_vanilla_cleanup(
        float vanillaTimerSeconds,
        bool vanillaSweepActive)
    {
        NativeSweepCoordinationDecision decision = NativeAssetSweepPolicy.DecideAutomaticSweep(
            automaticDue: true,
            blueSageSweepRunning: false,
            vanillaStateAvailable: true,
            vanillaTimerSeconds: vanillaTimerSeconds,
            vanillaDurationSeconds: 1800f,
            vanillaSweepActive: vanillaSweepActive);

        Assert.That(decision, Is.EqualTo(NativeSweepCoordinationDecision.Defer));
    }

    [TestCase(15f, false)]
    [TestCase(600f, true)]
    public void Recent_or_active_vanilla_cleanup_satisfies_the_pending_BlueSage_interval(
        float vanillaTimerSeconds,
        bool vanillaSweepActive)
    {
        NativeSweepCoordinationDecision decision = NativeAssetSweepPolicy.DecideAutomaticSweep(
            automaticDue: true,
            blueSageSweepRunning: false,
            vanillaStateAvailable: true,
            vanillaTimerSeconds: vanillaTimerSeconds,
            vanillaDurationSeconds: 1800f,
            vanillaSweepActive: vanillaSweepActive);

        Assert.That(decision, Is.EqualTo(NativeSweepCoordinationDecision.SatisfiedByVanilla));
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public void Automatic_cleanup_skips_when_not_due_or_already_single_flight(
        bool automaticDue,
        bool blueSageSweepRunning)
    {
        NativeSweepCoordinationDecision decision = NativeAssetSweepPolicy.DecideAutomaticSweep(
            automaticDue,
            blueSageSweepRunning,
            vanillaStateAvailable: true,
            vanillaTimerSeconds: 600f,
            vanillaDurationSeconds: 1800f,
            vanillaSweepActive: false);

        Assert.That(decision, Is.EqualTo(NativeSweepCoordinationDecision.Skip));
    }

    [Test]
    public void Automatic_cleanup_defers_when_read_only_vanilla_state_is_unavailable()
    {
        NativeSweepCoordinationDecision decision = NativeAssetSweepPolicy.DecideAutomaticSweep(
            automaticDue: true,
            blueSageSweepRunning: false,
            vanillaStateAvailable: false,
            vanillaTimerSeconds: 0f,
            vanillaDurationSeconds: 0f,
            vanillaSweepActive: false);

        Assert.That(decision, Is.EqualTo(NativeSweepCoordinationDecision.Defer));
    }

    [Test]
    public void Automatic_cleanup_defers_when_vanilla_timer_values_are_invalid()
    {
        NativeSweepCoordinationDecision decision = NativeAssetSweepPolicy.DecideAutomaticSweep(
            automaticDue: true,
            blueSageSweepRunning: false,
            vanillaStateAvailable: true,
            vanillaTimerSeconds: float.NaN,
            vanillaDurationSeconds: 1800f,
            vanillaSweepActive: false);

        Assert.That(decision, Is.EqualTo(NativeSweepCoordinationDecision.Defer));
    }

    [Test]
    public void Fresh_default_on_and_existing_explicit_off_are_both_preserved()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SweepSettingsPolicy.MigrateAutomaticCleanupOptIn(
                    configuredEnabled: true,
                    alreadyMigrated: false),
                Is.True,
                "A fresh bind default of true must remain enabled.");
            Assert.That(
                SweepSettingsPolicy.MigrateAutomaticCleanupOptIn(
                    configuredEnabled: false,
                    alreadyMigrated: false),
                Is.False,
                "An existing explicit off value must never be forced on.");
            Assert.That(SweepSettingsPolicy.DefaultIntervalMinutes, Is.EqualTo(15));
        });
    }

    [Test]
    public void Manual_cleanup_permission_is_independent_of_native_automatic_coordination()
    {
        Assert.That(
            NativeAssetSweepPolicy.AllowBlueSageManualSweep(
                nativeSweepPresent: true,
                compatibilityAllows: true),
            Is.True);
    }
}
