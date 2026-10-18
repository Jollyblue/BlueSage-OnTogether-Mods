using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityResyncPlanPolicyTests
{
    [TestCase("idsync")]
    [TestCase("identitysync")]
    [TestCase("rostersync")]
    public void Bare_command_and_legacy_aliases_are_read_only_status(string commandName)
    {
        IdentityResyncPlan plan = IdentityResyncPlanPolicy.Plan(
            commandName,
            string.Empty,
            IdentityResyncAccess.Ordinary);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Allowed, Is.True);
            Assert.That(plan.Action, Is.EqualTo(IdentityResyncAction.Status));
            Assert.That(plan.ReadOnly, Is.True);
            Assert.That(plan.RequiresAuthoritativeCapture, Is.False);
        });
    }

    [Test]
    public void Ordinary_refresh_is_local_presentation_only()
    {
        IdentityResyncPlan plan = IdentityResyncPlanPolicy.Plan(
            "idsync",
            "refresh",
            IdentityResyncAccess.Ordinary);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Allowed, Is.True);
            Assert.That(plan.Action, Is.EqualTo(IdentityResyncAction.RefreshLocalSurfaces));
            Assert.That(plan.ReadOnly, Is.False);
            Assert.That(plan.RequiresAuthoritativeCapture, Is.False);
            Assert.That(plan.MayWriteNativeIdentity, Is.False);
        });
    }

    [TestCase((int)IdentityResyncAccess.Ordinary)]
    [TestCase((int)IdentityResyncAccess.HelperReady)]
    [TestCase((int)IdentityResyncAccess.HelperWaitingForHost)]
    public void Only_the_exact_host_can_plan_direct_repair(int accessValue)
    {
        var access = (IdentityResyncAccess)accessValue;
        IdentityResyncPlan plan = IdentityResyncPlanPolicy.Plan(
            "idsync",
            "repair",
            access);

        Assert.That(plan.Allowed, Is.False);
        Assert.That(plan.Action, Is.EqualTo(IdentityResyncAction.Denied));
    }

    [Test]
    public void Host_direct_repair_is_one_authoritative_capture()
    {
        IdentityResyncPlan plan = IdentityResyncPlanPolicy.Plan(
            "idsync",
            "repair",
            IdentityResyncAccess.Host);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Allowed, Is.True);
            Assert.That(plan.Action, Is.EqualTo(IdentityResyncAction.RepairIncompleteRows));
            Assert.That(plan.RequiresAuthoritativeCapture, Is.True);
            Assert.That(plan.MayWriteNativeIdentity, Is.False);
        });
    }

    [Test]
    public void Only_a_fresh_authenticated_helper_can_plan_a_request()
    {
        IdentityResyncPlan ready = IdentityResyncPlanPolicy.Plan(
            "idsync",
            "request",
            IdentityResyncAccess.HelperReady);
        IdentityResyncPlan ordinary = IdentityResyncPlanPolicy.Plan(
            "idsync",
            "request",
            IdentityResyncAccess.Ordinary);
        IdentityResyncPlan waiting = IdentityResyncPlanPolicy.Plan(
            "idsync",
            "request",
            IdentityResyncAccess.HelperWaitingForHost);

        Assert.Multiple(() =>
        {
            Assert.That(ready.Allowed, Is.True);
            Assert.That(ready.Action, Is.EqualTo(IdentityResyncAction.PublishHostRequest));
            Assert.That(ordinary.Allowed, Is.False);
            Assert.That(waiting.Allowed, Is.False);
        });
    }

    [Test]
    public void Private_caller_has_no_ambiguity_bypass_or_extra_access_state()
    {
        string[] accessStates = Enum.GetNames<IdentityResyncAccess>();
        IdentityResyncPlan ordinaryRepair = IdentityResyncPlanPolicy.Plan(
            "idsync",
            "repair",
            IdentityResyncAccess.Ordinary);

        Assert.Multiple(() =>
        {
            Assert.That(accessStates, Does.Not.Contain("Blue"));
            Assert.That(accessStates, Does.Not.Contain("Private"));
            Assert.That(accessStates, Does.Not.Contain("Admin"));
            Assert.That(ordinaryRepair.Allowed, Is.False);
        });
    }

    [Test]
    public void Crowded_lobby_native_and_purrnet_ahead_of_steam_remains_fail_closed()
    {
        var coverage = new IdentityResyncCoverage(
            verifiedRows: 8,
            nativeRows: 9,
            purrNetRows: 9,
            steamMembers: 8,
            liveNativeOnlyRows: 1,
            leftStaleRows: 0,
            ambiguousRows: 0,
            captureAvailable: true,
            errorReason: string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(coverage.Ready, Is.False);
            Assert.That(coverage.LockedRows, Is.EqualTo(1));
            Assert.That(coverage.Reason, Does.Contain("native/PurrNet rows exceed current Steam membership"));
        });
    }

    [Test]
    public void Purrnet_confirmed_stale_native_row_is_excluded_without_blocking_current_coverage()
    {
        var coverage = new IdentityResyncCoverage(
            verifiedRows: 13,
            nativeRows: 13,
            rawNativeRows: 14,
            purrNetRows: 13,
            steamMembers: 13,
            liveNativeOnlyRows: 0,
            leftStaleRows: 1,
            ambiguousRows: 0,
            captureAvailable: true,
            errorReason: string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(coverage.Ready, Is.True);
            Assert.That(coverage.LockedRows, Is.Zero);
            Assert.That(coverage.ExcludedNativeRows, Is.EqualTo(1));
            Assert.That(coverage.Reason, Does.Contain("stale native row excluded"));
        });
    }
}
