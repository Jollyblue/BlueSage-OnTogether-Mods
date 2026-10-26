using NUnit.Framework;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityResyncHelpPolicyTests
{
    [Test]
    public void Healthy_matching_says_no_action_is_needed_and_explains_the_repair_order()
    {
        var coverage = new IdentityResyncCoverage(
            verifiedRows: 4,
            nativeRows: 4,
            purrNetRows: 4,
            steamMembers: 4,
            liveNativeOnlyRows: 0,
            leftStaleRows: 0,
            ambiguousRows: 0,
            captureAvailable: true,
            errorReason: string.Empty);

        IdentityResyncHelpPresentation presentation =
            IdentityResyncHelpPolicy.Describe(coverage, isHost: true);

        Assert.Multiple(() =>
        {
            Assert.That(presentation.Healthy, Is.True);
            Assert.That(presentation.StatusHeading, Is.EqualTo("Player matching is healthy"));
            Assert.That(presentation.WhatToDo, Does.Contain("Nothing"));
            Assert.That(presentation.WhatToDo, Does.Contain("automatically"));
            Assert.That(presentation.RefreshButtonLabel, Is.EqualTo("1. Refresh Player List"));
            Assert.That(presentation.RecheckButtonLabel, Is.EqualTo("2. Recheck Player Matches"));
            Assert.That(presentation.RecoveryAdvice, Does.Contain("start with Refresh Player List"));
        });
    }

    [Test]
    public void Unresolved_matching_explains_fail_closed_safety_and_a_casual_host_recovery_ladder()
    {
        var coverage = new IdentityResyncCoverage(
            verifiedRows: 2,
            nativeRows: 3,
            purrNetRows: 3,
            steamMembers: 3,
            liveNativeOnlyRows: 0,
            leftStaleRows: 0,
            ambiguousRows: 1,
            captureAvailable: true,
            errorReason: string.Empty);

        IdentityResyncHelpPresentation presentation =
            IdentityResyncHelpPolicy.Describe(coverage, isHost: true);

        Assert.Multiple(() =>
        {
            Assert.That(presentation.Healthy, Is.False);
            Assert.That(presentation.StatusHeading, Is.EqualTo("Some player rows are still being matched"));
            Assert.That(presentation.WhatThisMeans, Does.Contain("keeps host actions locked on the rows it can't match"), "Round 5e: per-row, not lobby-wide.");
            Assert.That(presentation.WhatThisMeans, Does.Contain("matched players can still be managed"));
            Assert.That(presentation.WhatToDo, Does.Contain("already checking again"));
            Assert.That(presentation.RefreshExplanation, Does.Contain("does not kick, mute, report, ban, or delete"));
            Assert.That(presentation.RecheckExplanation, Does.Contain("compares each visible row"));
            Assert.That(presentation.ExpectedResult, Does.Contain("affected rows stay locked"));
            Assert.That(presentation.RecoveryAdvice, Does.Contain("leave and rejoin"));
            Assert.That(presentation.RecoveryAdvice, Does.Contain("recreate the lobby"));
            Assert.That(presentation.RecoveryAdvice, Does.Contain("refuses to guess"));
        });
    }

    [Test]
    public void Helper_copy_asks_the_host_without_suggesting_extra_authority()
    {
        var coverage = new IdentityResyncCoverage(
            verifiedRows: 0,
            nativeRows: 1,
            purrNetRows: 1,
            steamMembers: 1,
            liveNativeOnlyRows: 0,
            leftStaleRows: 0,
            ambiguousRows: 0,
            captureAvailable: true,
            errorReason: string.Empty);

        IdentityResyncHelpPresentation presentation =
            IdentityResyncHelpPolicy.Describe(coverage, isHost: false);

        Assert.Multiple(() =>
        {
            Assert.That(presentation.RecheckButtonLabel, Is.EqualTo("Ask Host to Recheck Player List"));
            Assert.That(presentation.RecheckExplanation, Does.Contain("asks the host"));
            Assert.That(presentation.RecheckExplanation, Does.Not.Contain("grants"));
        });
    }

    [Test]
    public void Successful_menu_actions_return_plain_language_instead_of_runtime_counters()
    {
        var coverage = new IdentityResyncCoverage(
            verifiedRows: 1,
            nativeRows: 1,
            purrNetRows: 1,
            steamMembers: 1,
            liveNativeOnlyRows: 0,
            leftStaleRows: 0,
            ambiguousRows: 0,
            captureAvailable: true,
            errorReason: string.Empty);

        string refresh = IdentityResyncHelpPolicy.FormatActionResult(
            "refresh",
            "Player list refreshed. No player action was taken.",
            coverage);
        string repair = IdentityResyncHelpPolicy.FormatActionResult(
            "repair",
            "Player matching recheck finished. Every visible player is matched safely. No player action was taken.",
            coverage);

        Assert.Multiple(() =>
        {
            Assert.That(refresh, Is.EqualTo("Player list refreshed. Every visible player is still matched safely. No player action was taken."));
            Assert.That(repair, Is.EqualTo("Player matching recheck finished. Every visible player is matched safely. No player action was taken."));
            Assert.That(refresh, Does.Not.Contain("surface"));
            Assert.That(repair, Does.Not.Contain("targetedRows"));
        });
    }

    [Test]
    public void Failed_or_blocked_menu_actions_return_a_plain_safe_next_step()
    {
        var coverage = new IdentityResyncCoverage(
            verifiedRows: 0,
            nativeRows: 1,
            purrNetRows: 1,
            steamMembers: 1,
            liveNativeOnlyRows: 0,
            leftStaleRows: 0,
            ambiguousRows: 0,
            captureAvailable: false,
            errorReason: "Steam lobby owner unavailable");
        const string blocked = "Only the lobby host can recheck player matches. Nothing changed.";

        string result = IdentityResyncHelpPolicy.FormatActionResult("repair", blocked, coverage);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Only the current lobby host"));
            Assert.That(result, Does.Contain("No player action was taken"));
            Assert.That(result, Does.Not.Contain("PurrNet"));
            Assert.That(result, Does.Not.Contain("exact current Steam lobby owner"));
        });
    }
}
