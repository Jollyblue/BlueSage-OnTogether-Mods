using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerHostLabelDisplayPolicyTests
{
    [TestCase(true, true, false, false, false, "ordinary owner host")]
    [TestCase(true, false, true, true, false, "identity generation unavailable")]
    [TestCase(false, true, true, true, false, "not the exact owned host")]
    [TestCase(true, true, true, false, true, "fresh private creator contribution")]
    [TestCase(true, true, false, true, true, "fresh approved private fleet host authority")]
    public void Host_publication_requires_exact_owned_host_identity_and_creator_or_private_fleet_truth(
        bool exactOwnedHost,
        bool exactIdentityGenerationFresh,
        bool creatorContributionFresh,
        bool privateHostAuthorityFresh,
        bool expected,
        string reason)
    {
        Assert.That(CreatorHostLabelPublicationPolicy.CanPublish(
            exactOwnedHost,
            exactIdentityGenerationFresh,
            creatorContributionFresh,
            privateHostAuthorityFresh), Is.EqualTo(expected), reason);
    }

    [Test]
    public void Active_creator_label_clears_and_stops_when_the_provider_disappears()
    {
        Assert.That(CreatorHostLabelPublicationPolicy.DecideHeartbeat(
            labelActive: true,
            heartbeatDue: true,
            exactOwnedHost: true,
            exactIdentityGenerationFresh: false,
            creatorContributionFresh: false),
            Is.EqualTo(CreatorHostLabelHeartbeatDecision.ClearAndStop));
    }

    [Test]
    public void Active_private_fleet_label_clears_on_host_transfer_or_stale_authority()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CreatorHostLabelPublicationPolicy.DecideHeartbeat(
                labelActive: true,
                heartbeatDue: true,
                exactOwnedHost: false,
                exactIdentityGenerationFresh: true,
                creatorContributionFresh: false,
                privateHostAuthorityFresh: true),
                Is.EqualTo(CreatorHostLabelHeartbeatDecision.ClearAndStop));
            Assert.That(CreatorHostLabelPublicationPolicy.DecideHeartbeat(
                labelActive: true,
                heartbeatDue: true,
                exactOwnedHost: true,
                exactIdentityGenerationFresh: true,
                creatorContributionFresh: false,
                privateHostAuthorityFresh: false),
                Is.EqualTo(CreatorHostLabelHeartbeatDecision.ClearAndStop));
        });
    }

    [Test]
    public void Authorized_creator_label_refreshes_only_when_the_heartbeat_is_due()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CreatorHostLabelPublicationPolicy.DecideHeartbeat(
                labelActive: true,
                heartbeatDue: true,
                exactOwnedHost: true,
                exactIdentityGenerationFresh: true,
                creatorContributionFresh: true),
                Is.EqualTo(CreatorHostLabelHeartbeatDecision.Publish));
            Assert.That(CreatorHostLabelPublicationPolicy.DecideHeartbeat(
                labelActive: true,
                heartbeatDue: false,
                exactOwnedHost: true,
                exactIdentityGenerationFresh: true,
                creatorContributionFresh: true),
                Is.EqualTo(CreatorHostLabelHeartbeatDecision.None));
        });
    }

    [Test]
    public void Fresh_set_from_exact_owner_is_renderable()
    {
        const long now = 1_900_000_000;
        Assert.That(RoleLabelProtocolPolicy.TryBuildSet(
            "<color=#70D6FF><b>♪Host♪</b></color>",
            now,
            "Abcd1234",
            out string payload,
            out _), Is.True);

        Assert.That(PlayerHostLabelDisplayPolicy.TryResolve(
            payload,
            now,
            isExactCurrentOwner: true,
            out string label), Is.True);
        Assert.That(label, Is.EqualTo("<color=#70D6FF><b>♪Host♪</b></color>"));
    }

    [TestCase(false, false, "non-owner")]
    [TestCase(true, false, "stale")]
    public void Non_owner_and_stale_payloads_fail_closed(
        bool exactOwner,
        bool fresh,
        string reason)
    {
        const long issued = 1_900_000_000;
        Assert.That(RoleLabelProtocolPolicy.TryBuildSet(
            "♪Host♪",
            issued,
            "Abcd1234",
            out string payload,
            out _), Is.True);

        long now = fresh ? issued : issued + RoleLabelProtocolPolicy.FreshnessSeconds + 1;
        Assert.That(PlayerHostLabelDisplayPolicy.TryResolve(
            payload,
            now,
            exactOwner,
            out string label), Is.False, reason);
        Assert.That(label, Is.Empty);
    }

    [Test]
    public void Clear_payload_never_renders()
    {
        const long now = 1_900_000_000;
        Assert.That(RoleLabelProtocolPolicy.TryBuildClear(
            now,
            "Abcd1234",
            out string payload,
            out _), Is.True);

        Assert.That(PlayerHostLabelDisplayPolicy.TryResolve(
            payload,
            now,
            isExactCurrentOwner: true,
            out string label), Is.False);
        Assert.That(label, Is.Empty);
    }

    [TestCase("Blue (Host)", "Blue")]
    [TestCase("<color=#70D6FF>Blue</color> (Host)", "<color=#70D6FF>Blue</color>")]
    [TestCase("Blue (Code) (Host)", "Blue (Code)")]
    public void Fresh_exact_creator_host_label_suppresses_only_terminal_vanilla_suffix(
        string renderedName,
        string expected)
    {
        Assert.That(PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
            renderedName: renderedName,
            nativeHostLabel: "(Host)",
            uniqueExactRow: true,
            ownerMatchesRow: true,
            nativeHostAgreement: true,
            customHostLabelActive: true,
            replacement: out string replacement), Is.True);
        Assert.That(replacement, Is.EqualTo(expected));
    }

    [TestCase(false, true, true, true, "ordinary public host")]
    [TestCase(true, false, true, true, "non-owner row")]
    [TestCase(true, true, false, true, "native host disagreement")]
    [TestCase(true, true, true, false, "custom label inactive")]
    public void Suppression_fails_closed_without_every_exact_owner_condition(
        bool uniqueExactRow,
        bool ownerMatchesRow,
        bool nativeHostAgreement,
        bool customHostLabelActive,
        string reason)
    {
        Assert.That(PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
            renderedName: "Blue (Host)",
            nativeHostLabel: "(Host)",
            uniqueExactRow: uniqueExactRow,
            ownerMatchesRow: ownerMatchesRow,
            nativeHostAgreement: nativeHostAgreement,
            customHostLabelActive: customHostLabelActive,
            replacement: out string replacement), Is.False, reason);
        Assert.That(replacement, Is.Empty);
    }

    [TestCase("Blue")]
    [TestCase("Blue (Host) (AFK)")]
    [TestCase("Blue(Host)")]
    [TestCase("")]
    public void Non_terminal_or_missing_vanilla_suffix_is_never_rewritten(
        string renderedName)
    {
        Assert.That(PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
            renderedName: renderedName,
            nativeHostLabel: "(Host)",
            uniqueExactRow: true,
            ownerMatchesRow: true,
            nativeHostAgreement: true,
            customHostLabelActive: true,
            replacement: out string replacement), Is.False);
        Assert.That(replacement, Is.Empty);
    }

    [Test]
    public void Remote_exact_owner_label_replaces_localized_native_suffix_without_private_provider()
    {
        Assert.That(PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
            "Blue (Host)", "(Host)", true, true, true, true,
            out string replacement), Is.True);
        Assert.That(replacement, Is.EqualTo("Blue"));
    }

    [Test]
    public void Localized_host_suffix_is_not_hard_coded_to_english()
    {
        Assert.That(PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
            "Blue (Gastgeber)", "(Gastgeber)", true, true, true, true,
            out string replacement), Is.True);
        Assert.That(replacement, Is.EqualTo("Blue"));
    }
}
