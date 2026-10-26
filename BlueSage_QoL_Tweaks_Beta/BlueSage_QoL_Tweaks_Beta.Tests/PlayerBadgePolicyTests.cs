using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerBadgePolicyTests
{
    [Test]
    public void Complete_truth_is_presented_in_fixed_O_M_U_V_S_creator_order()
    {
        PlayerBadgePresentation result = PlayerBadgePolicy.Compose(AllTruth(), capacity: 6);

        Assert.Multiple(() =>
        {
            Assert.That(PlayerBadgePolicy.PresentationOrder, Is.EqualTo(new[]
            {
                PlayerBadgeKind.Host,
                PlayerBadgeKind.Helper,
                PlayerBadgeKind.CompatibleClient,
                PlayerBadgeKind.ProtectedVictim,
                PlayerBadgeKind.Suspect,
                PlayerBadgeKind.Supporter,
                PlayerBadgeKind.Creator,
            }));
            Assert.That(result.DisplayText, Is.EqualTo("[O] [U] [V] [S] [♪]"));
            Assert.That(result.HiddenCount, Is.Zero);
            Assert.That(result.AllBadges, Is.EqualTo(new[]
            {
                PlayerBadgeKind.Host,
                PlayerBadgeKind.CompatibleClient,
                PlayerBadgeKind.ProtectedVictim,
                PlayerBadgeKind.Suspect,
                PlayerBadgeKind.Creator,
            }));
            Assert.That(result.TooltipText, Is.EqualTo(
                "[O]  Owner / Host\n" +
                "[U]  Pre-indicator QoL User\n" +
                "[V] — Protected clone-incident victim\n" +
                "[S] — Open clone-incident suspect\n" +
                "[♪] — BlueSage creator identity"));
        });
    }

    [Test]
    public void Overflow_reserves_the_compatible_user_signal_and_keeps_the_complete_tooltip()
    {
        PlayerBadgePresentation full = PlayerBadgePolicy.Compose(AllTruth(), capacity: 6);
        PlayerBadgePresentation overflow = PlayerBadgePolicy.Compose(AllTruth(), capacity: 3);
        PlayerBadgePresentation oneSlot = PlayerBadgePolicy.Compose(AllTruth(), capacity: 1);

        Assert.Multiple(() =>
        {
            Assert.That(overflow.DisplayText, Is.EqualTo("[O] [U] [+3]"));
            Assert.That(overflow.HiddenCount, Is.EqualTo(3));
            Assert.That(overflow.TooltipText, Is.EqualTo(full.TooltipText));
            Assert.That(oneSlot.DisplayText, Is.EqualTo("[U]"));
            Assert.That(oneSlot.HiddenCount, Is.EqualTo(4));
            Assert.That(oneSlot.TooltipText, Is.EqualTo(full.TooltipText));
        });
    }

    [Test]
    public void Overflow_keeps_creator_and_verified_supporter_visible_before_lower_priority_status()
    {
        PlayerBadgeEvidence evidence = AllTruth().With(
            supporterIdentityHashMatched: true);

        PlayerBadgePresentation result = PlayerBadgePolicy.Compose(evidence, capacity: 4);

        Assert.Multiple(() =>
        {
            Assert.That(result.DisplayText, Is.EqualTo("[O] [U] [♪] [+3]"));
            Assert.That(result.HiddenCount, Is.EqualTo(3));
            Assert.That(result.TooltipText, Does.Contain("[U]  Pre-indicator QoL User"));
            Assert.That(result.TooltipText, Does.Contain("[♥] — BlueSage community supporter"));
            Assert.That(result.TooltipText, Does.Contain("[♪] — BlueSage creator identity"));
        });
    }

    [Test]
    public void Repeated_layout_collapse_is_deterministic_and_keeps_full_truth_in_the_tooltip()
    {
        PlayerBadgePresentation full = PlayerBadgePolicy.Compose(AllTruth(), capacity: 6);

        PlayerBadgePresentation first = PlayerBadgePolicy.Collapse(full, capacity: 2);
        PlayerBadgePresentation second = PlayerBadgePolicy.Collapse(full, capacity: 2);

        Assert.Multiple(() =>
        {
            Assert.That(first.DisplayText, Is.EqualTo("[O] [U]"));
            Assert.That(second.DisplayText, Is.EqualTo(first.DisplayText));
            Assert.That(first.HiddenCount, Is.EqualTo(3));
            Assert.That(first.AllBadges, Is.EqualTo(full.AllBadges));
            Assert.That(first.TooltipText, Is.EqualTo(full.TooltipText));
            Assert.That(second.TooltipText, Is.EqualTo(full.TooltipText));
        });
    }

    [Test]
    public void Host_requires_unique_exact_current_member_owner_and_native_host_agreement()
    {
        PlayerBadgeEvidence valid = HostTruth();

        Assert.Multiple(() =>
        {
            Assert.That(Has(PlayerBadgePolicy.Compose(valid, 6), PlayerBadgeKind.Host), Is.True);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(uniqueExactRow: false), 6), PlayerBadgeKind.Host), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(uniqueCurrentMember: false), 6), PlayerBadgeKind.Host), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(ownerMatchesRow: false), 6), PlayerBadgeKind.Host), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(nativeHostAgreement: false), 6), PlayerBadgeKind.Host), Is.False);
        });
    }

    [Test]
    public void Helper_requires_fresh_owner_scoped_capability_and_owner_is_suppressed_if_mislisted()
    {
        PlayerBadgeEvidence valid = BaseTruth().With(
            moderationCapabilityFresh: true,
            ownerListsRowAsHelper: true);

        Assert.Multiple(() =>
        {
            Assert.That(Has(PlayerBadgePolicy.Compose(valid, 6), PlayerBadgeKind.Helper), Is.True);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(moderationCapabilityFresh: false), 6), PlayerBadgeKind.Helper), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(ownerListsRowAsHelper: false), 6), PlayerBadgeKind.Helper), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(ownerMatchesRow: true, nativeHostAgreement: true), 6), PlayerBadgeKind.Helper), Is.False);
        });
    }

    [Test]
    public void Helper_and_compatible_user_use_literal_compact_bracketed_tags()
    {
        PlayerBadgeEvidence helper = BaseTruth().With(
            moderationCapabilityFresh: true,
            ownerListsRowAsHelper: true,
            compatibleHeartbeatFresh: true);

        PlayerBadgePresentation result = PlayerBadgePolicy.Compose(helper, capacity: 6);

        Assert.Multiple(() =>
        {
            Assert.That(result.DisplayText, Is.EqualTo("[M] [U]"));
            Assert.That(result.TooltipText, Is.EqualTo(
                "[M] BlueSage Helper (this lobby)\n" +
                "[U]  Pre-indicator QoL User"));
        });
    }

    // Round 5c (Blue, 2026-10-23): the long badge text names the peer's own QoL version; the compact token
    // stays [U] at every width so narrow rows are unchanged.
    [Test]
    public void Compatible_badge_names_the_peer_version_when_it_published_one()
    {
        PlayerBadgeEvidence versioned = AllTruth().With(compatibleClientVersion: "0.2.7");
        PlayerBadgeEvidence presentationOnly = new PlayerBadgeEvidence(
            uniqueExactRow: false,
            uniqueCurrentMember: false,
            ownerMatchesRow: false,
            nativeHostAgreement: false,
            moderationCapabilityFresh: false,
            ownerListsRowAsHelper: false,
            compatibleHeartbeatFresh: true,
            cloneFeatureEnabled: false,
            localSafetyAuthorized: false,
            openVictim: false,
            openSuspect: false,
            creatorContributionFresh: false,
            compatiblePresentationRow: true,
            compatibleClientVersion: "1.0.0");

        PlayerBadgePresentation wide = PlayerBadgePolicy.Compose(versioned, capacity: 6);
        PlayerBadgePresentation narrow = PlayerBadgePolicy.Compose(versioned, capacity: 1);
        PlayerBadgePresentation presentation = PlayerBadgePolicy.Compose(presentationOnly, capacity: 6);
        PlayerBadgePresentation collapsed = PlayerBadgePolicy.Collapse(wide, capacity: 2);

        Assert.Multiple(() =>
        {
            Assert.That(wide.TooltipText, Does.Contain("[U]  QoL v0.2.7 User"));
            Assert.That(wide.TooltipText, Does.Not.Contain("Pre-indicator"));
            Assert.That(wide.DisplayText, Does.Contain("[U]"));
            Assert.That(narrow.DisplayText, Is.EqualTo("[U]"), "Compact token unchanged in narrow rows.");
            Assert.That(narrow.TooltipText, Is.EqualTo(wide.TooltipText));
            Assert.That(presentation.DisplayText, Is.EqualTo("[U]"));
            Assert.That(presentation.TooltipText, Is.EqualTo("[U]  QoL v1.0.0 User"));
            Assert.That(collapsed.TooltipText, Is.EqualTo(wide.TooltipText), "Collapse keeps the versioned tooltip.");
        });
    }

    [Test]
    public void Compatible_incident_and_creator_badges_each_fail_closed_on_missing_exact_truth()
    {
        PlayerBadgeEvidence valid = BaseTruth().With(
            compatibleHeartbeatFresh: true,
            cloneFeatureEnabled: true,
            localSafetyAuthorized: true,
            openVictim: true,
            openSuspect: true,
            creatorContributionFresh: true);

        Assert.Multiple(() =>
        {
            Assert.That(Has(PlayerBadgePolicy.Compose(valid, 6), PlayerBadgeKind.CompatibleClient), Is.True);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid, 6), PlayerBadgeKind.Creator), Is.True);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(uniqueCurrentMember: false), 6), PlayerBadgeKind.CompatibleClient), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(cloneFeatureEnabled: false), 6), PlayerBadgeKind.ProtectedVictim), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(localSafetyAuthorized: false), 6), PlayerBadgeKind.Suspect), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(openVictim: false), 6), PlayerBadgeKind.ProtectedVictim), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(openSuspect: false), 6), PlayerBadgeKind.Suspect), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(creatorContributionFresh: false), 6), PlayerBadgeKind.Creator), Is.False);
            Assert.That(Has(PlayerBadgePolicy.Compose(valid.With(uniqueExactRow: false), 6), PlayerBadgeKind.Creator), Is.False);
        });
    }

    [Test]
    public void Compatible_user_stays_visible_for_a_current_game_bound_row_without_unlocking_authority_badges()
    {
        PlayerBadgeEvidence presentationOnly = BaseTruth().With(
            uniqueExactRow: false,
            uniqueCurrentMember: false,
            moderationCapabilityFresh: true,
            ownerListsRowAsHelper: true,
            compatibleHeartbeatFresh: true,
            cloneFeatureEnabled: true,
            localSafetyAuthorized: true,
            openVictim: true,
            openSuspect: true,
            creatorContributionFresh: true,
            compatiblePresentationRow: true);

        PlayerBadgePresentation result = PlayerBadgePolicy.Compose(
            presentationOnly,
            capacity: 6);

        Assert.Multiple(() =>
        {
            Assert.That(Has(result, PlayerBadgeKind.CompatibleClient), Is.True);
            Assert.That(Has(result, PlayerBadgeKind.Host), Is.False);
            Assert.That(Has(result, PlayerBadgeKind.Helper), Is.False);
            Assert.That(Has(result, PlayerBadgeKind.ProtectedVictim), Is.False);
            Assert.That(Has(result, PlayerBadgeKind.Suspect), Is.False);
            Assert.That(Has(result, PlayerBadgeKind.Creator), Is.False);
        });
    }

    [Test]
    public void Exact_owner_published_creator_title_is_visible_without_local_private_contribution()
    {
        PlayerBadgeEvidence evidence = HostTruth().With(
            compatibleHeartbeatFresh: true,
            creatorContributionFresh: false,
            ownerPublishedCreatorHostLabelFresh: true);

        Assert.That(PlayerBadgePolicy.Compose(evidence, 3).DisplayText,
            Is.EqualTo("[O] [U] [♪]"));
    }

    [Test]
    public void Ordinary_owner_host_cannot_publish_creator_presentation_or_replace_native_suffix()
    {
        bool mayPublish = CreatorHostLabelPublicationPolicy.CanPublish(
            exactOwnedHost: true,
            exactIdentityGenerationFresh: true,
            creatorContributionFresh: false);
        PlayerBadgeEvidence evidence = HostTruth().With(
            ownerPublishedCreatorHostLabelFresh: mayPublish);

        Assert.Multiple(() =>
        {
            Assert.That(PlayerBadgePolicy.Compose(evidence, 3).DisplayText,
                Is.EqualTo("[O]"));
            Assert.That(PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
                "Ordinary Host (Host)",
                "(Host)",
                true,
                true,
                true,
                mayPublish,
                out string replacement), Is.False);
            Assert.That(replacement, Is.Empty);
        });
    }

    [Test]
    public void Authorized_publisher_produces_one_remote_title_and_creator_host_badge()
    {
        const long now = 1_900_000_000;
        bool mayPublish = CreatorHostLabelPublicationPolicy.CanPublish(
            exactOwnedHost: true,
            exactIdentityGenerationFresh: true,
            creatorContributionFresh: true);
        Assert.That(RoleLabelProtocolPolicy.TryBuildSet(
            "♪Host♪",
            now,
            "Abcd1234",
            out string payload,
            out _), Is.True);
        Assert.That(PlayerHostLabelDisplayPolicy.TryResolve(
            payload,
            now,
            isExactCurrentOwner: true,
            out string title), Is.True);

        PlayerBadgeEvidence evidence = HostTruth().With(
            ownerPublishedCreatorHostLabelFresh:
                mayPublish && !string.IsNullOrWhiteSpace(title));

        Assert.Multiple(() =>
        {
            Assert.That(title, Is.EqualTo("♪Host♪"));
            Assert.That(PlayerBadgePolicy.Compose(evidence, 3).DisplayText,
                Is.EqualTo("[O] [♪]"));
            Assert.That(PlayerHostLabelDisplayPolicy.TrySuppressNativeHostSuffix(
                "Creator (Host)",
                "(Host)",
                true,
                true,
                true,
                evidence.OwnerPublishedCreatorHostLabelFresh,
                out string replacement), Is.True);
            Assert.That(replacement, Is.EqualTo("Creator"));
        });
    }

    [Test]
    public void Owner_published_creator_title_requires_owner_match()
    {
        PlayerBadgeEvidence evidence = HostTruth().With(
            ownerMatchesRow: false,
            creatorContributionFresh: false,
            ownerPublishedCreatorHostLabelFresh: true);

        Assert.That(Has(PlayerBadgePolicy.Compose(evidence, 6), PlayerBadgeKind.Creator),
            Is.False);
    }

    [Test]
    public void Owner_published_creator_title_requires_native_host_agreement()
    {
        PlayerBadgeEvidence evidence = HostTruth().With(
            nativeHostAgreement: false,
            creatorContributionFresh: false,
            ownerPublishedCreatorHostLabelFresh: true);

        Assert.That(Has(PlayerBadgePolicy.Compose(evidence, 6), PlayerBadgeKind.Creator),
            Is.False);
    }

    private static bool Has(PlayerBadgePresentation result, PlayerBadgeKind badge) =>
        result.AllBadges.Contains(badge);

    private static PlayerBadgeEvidence BaseTruth() => new(
        uniqueExactRow: true,
        uniqueCurrentMember: true,
        ownerMatchesRow: false,
        nativeHostAgreement: false,
        moderationCapabilityFresh: false,
        ownerListsRowAsHelper: false,
        compatibleHeartbeatFresh: false,
        cloneFeatureEnabled: false,
        localSafetyAuthorized: false,
        openVictim: false,
        openSuspect: false,
        creatorContributionFresh: false);

    private static PlayerBadgeEvidence HostTruth() =>
        BaseTruth().With(ownerMatchesRow: true, nativeHostAgreement: true);

    private static PlayerBadgeEvidence AllTruth() => HostTruth().With(
        moderationCapabilityFresh: true,
        ownerListsRowAsHelper: true,
        compatibleHeartbeatFresh: true,
        cloneFeatureEnabled: true,
        localSafetyAuthorized: true,
        openVictim: true,
        openSuspect: true,
        creatorContributionFresh: true);
}
