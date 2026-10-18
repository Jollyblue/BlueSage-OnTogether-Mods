using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerIdentityEvidencePolicyTests
{
    private const string Target = "76561198000000001";

    [Test]
    public void Exact_unique_aligned_steam_id_resolves_even_when_names_can_be_cloned()
    {
        string[] roster = { "76561198000000000", Target, "76561198000000002" };

        bool resolved = PlayerIdentityEvidencePolicy.TryResolveExactRosterIndex(Target, roster, 3, 3, out int index, out string error);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(error, Is.Empty);
        });
    }

    [Test]
    public void Duplicate_or_missing_steam_id_fails_closed()
    {
        string[] duplicate = { Target, Target };
        string[] missing = { "76561198000000002" };

        Assert.That(PlayerIdentityEvidencePolicy.TryResolveExactRosterIndex(Target, duplicate, 2, 2, out _, out string duplicateError), Is.False);
        Assert.That(duplicateError, Does.Contain("more than once"));
        Assert.That(PlayerIdentityEvidencePolicy.TryResolveExactRosterIndex(Target, missing, 1, 1, out _, out string missingError), Is.False);
        Assert.That(missingError, Does.Contain("no longer"));
    }

    [Test]
    public void Misaligned_native_lists_fail_closed()
    {
        string[] roster = { "76561198000000000", Target };

        Assert.That(PlayerIdentityEvidencePolicy.TryResolveExactRosterIndex(Target, roster, 1, 2, out _, out string error), Is.False);
        Assert.That(error, Does.Contain("not aligned"));
    }

    [Test]
    public void Current_lobby_membership_must_be_unique()
    {
        Assert.That(PlayerIdentityEvidencePolicy.IsUniqueCurrentLobbyMember(Target, new[] { Target }), Is.True);
        Assert.That(PlayerIdentityEvidencePolicy.IsUniqueCurrentLobbyMember(Target, new[] { Target, Target }), Is.False);
        Assert.That(PlayerIdentityEvidencePolicy.IsUniqueCurrentLobbyMember(Target, Array.Empty<string>()), Is.False);
    }

    [Test]
    public void Active_rows_verify_individually_while_departed_history_rows_are_excluded()
    {
        string helper = "76561198000000002";
        string stale = "76561198000000003";
        string[] panelSteamIds = { Target, helper, stale };
        string[] panelPlayerIds = { "p-host", "p-helper", "p-stale" };
        string[] currentLobby = { Target, helper };

        Assert.Multiple(() =>
        {
            Assert.That(PlayerIdentityEvidencePolicy.IsVerifiedActiveRow(Target, panelSteamIds, "p-host", panelPlayerIds, true, currentLobby), Is.True);
            Assert.That(PlayerIdentityEvidencePolicy.IsVerifiedActiveRow(helper, panelSteamIds, "p-helper", panelPlayerIds, true, currentLobby), Is.True);
            Assert.That(PlayerIdentityEvidencePolicy.IsVerifiedActiveRow(stale, panelSteamIds, "p-stale", panelPlayerIds, true, currentLobby), Is.False);
        });
    }

    [Test]
    public void Live_native_row_missing_only_from_steam_is_not_misreported_as_departed()
    {
        string live = "76561198000000003";
        string error = PlayerIdentityEvidencePolicy.DescribeUnresolvedExactRow(
            live,
            new[] { Target, "76561198000000002", "76561198000000004" },
            "1 current Steam member is still waiting for one unique live player row",
            nativeSteamMatches: 1,
            hasUniquePlayerId: true,
            hasLiveTransform: true);

        Assert.That(error, Does.StartWith("live/steam-check"));
    }

    [Test]
    public void Departed_or_history_row_requires_missing_live_native_evidence()
    {
        string departed = "76561198000000003";

        Assert.Multiple(() =>
        {
            Assert.That(PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                departed, new[] { Target }, 0, false, false),
                Is.EqualTo(PlayerIdentityEvidencePolicy.UnresolvedRowState.LeftStale));
            Assert.That(PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                departed, new[] { Target }, 1, true, false),
                Is.EqualTo(PlayerIdentityEvidencePolicy.UnresolvedRowState.LeftStale));
        });
    }

    [Test]
    public void Duplicate_native_or_player_identity_remains_ambiguous_and_locked()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                Target, new[] { Target }, 2, true, true),
                Is.EqualTo(PlayerIdentityEvidencePolicy.UnresolvedRowState.Checking));
            Assert.That(PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                Target, new[] { Target }, 1, false, true),
                Is.EqualTo(PlayerIdentityEvidencePolicy.UnresolvedRowState.Checking));
        });
    }

    [Test]
    public void Native_row_absent_from_both_live_authorities_is_stale_not_a_lobby_lock()
    {
        Assert.That(
            PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                Target,
                Array.Empty<string>(),
                nativeSteamMatches: 1,
                hasUniquePlayerId: true,
                hasLiveTransform: true,
                isPurrNetConnected: false),
            Is.EqualTo(PlayerIdentityEvidencePolicy.UnresolvedRowState.LeftStale));
    }

    [Test]
    public void Steam_and_purrnet_disagreement_remains_fail_closed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                    Target,
                    Array.Empty<string>(),
                    nativeSteamMatches: 1,
                    hasUniquePlayerId: true,
                    hasLiveTransform: true,
                    isPurrNetConnected: true),
                Is.EqualTo(PlayerIdentityEvidencePolicy.UnresolvedRowState.LiveSteamCheck));
            Assert.That(
                PlayerIdentityEvidencePolicy.ClassifyUnresolvedRow(
                    Target,
                    new[] { Target },
                    nativeSteamMatches: 1,
                    hasUniquePlayerId: true,
                    hasLiveTransform: true,
                    isPurrNetConnected: false),
                Is.EqualTo(PlayerIdentityEvidencePolicy.UnresolvedRowState.Checking));
        });
    }

    [Test]
    public void Duplicate_sid_player_id_or_missing_transform_stays_fail_closed_per_row()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlayerIdentityEvidencePolicy.IsVerifiedActiveRow(Target, new[] { Target, Target }, "p1", new[] { "p1", "p2" }, true, new[] { Target }), Is.False);
            Assert.That(PlayerIdentityEvidencePolicy.IsVerifiedActiveRow(Target, new[] { Target }, "p1", new[] { "p1", "p1" }, true, new[] { Target }), Is.False);
            Assert.That(PlayerIdentityEvidencePolicy.IsVerifiedActiveRow(Target, new[] { Target }, "p1", new[] { "p1" }, false, new[] { Target }), Is.False);
        });
    }

    [Test]
    public void Coverage_is_bidirectional_while_exact_verified_rows_remain_individually_usable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlayerIdentityEvidencePolicy.IsCoverageComplete(
                captureReady: true,
                missingCurrentSteamMembers: 0,
                liveNativeOnlyRows: 0,
                ambiguousRows: 0), Is.True);
            Assert.That(PlayerIdentityEvidencePolicy.IsCoverageComplete(true, 0, 8, 0), Is.False,
                "Eight live PurrNet/native rows absent from Steam must not report complete coverage.");
            Assert.That(PlayerIdentityEvidencePolicy.IsCoverageComplete(true, 1, 0, 0), Is.False);
            Assert.That(PlayerIdentityEvidencePolicy.IsCoverageComplete(true, 0, 0, 1), Is.False);
            Assert.That(PlayerIdentityEvidencePolicy.IsCoverageComplete(false, 0, 0, 0), Is.False);
        });
    }

    [Test]
    public void Manual_identity_resync_is_one_shot_and_rate_limited()
    {
        long cooldown = TimeSpan.FromSeconds(5).Ticks;
        long at100Seconds = TimeSpan.FromSeconds(100).Ticks;
        bool firstAllowed = PlayerIdentityEvidencePolicy.TryAcquireManualResync(
            at100Seconds,
            0L,
            cooldown,
            out long firstAcquiredAt,
            out int firstRetryAfter);
        bool secondAllowed = PlayerIdentityEvidencePolicy.TryAcquireManualResync(
            TimeSpan.FromSeconds(103).Ticks,
            firstAcquiredAt,
            cooldown,
            out long secondAcquiredAt,
            out int secondRetryAfter);
        bool thirdAllowed = PlayerIdentityEvidencePolicy.TryAcquireManualResync(
            TimeSpan.FromSeconds(105).Ticks,
            firstAcquiredAt,
            cooldown,
            out long thirdAcquiredAt,
            out int thirdRetryAfter);

        Assert.Multiple(() =>
        {
            Assert.That(firstAllowed, Is.True);
            Assert.That(firstAcquiredAt, Is.EqualTo(at100Seconds));
            Assert.That(firstRetryAfter, Is.EqualTo(0));
            Assert.That(secondAllowed, Is.False);
            Assert.That(secondAcquiredAt, Is.EqualTo(firstAcquiredAt));
            Assert.That(secondRetryAfter, Is.EqualTo(2));
            Assert.That(thirdAllowed, Is.True);
            Assert.That(thirdAcquiredAt, Is.EqualTo(TimeSpan.FromSeconds(105).Ticks));
            Assert.That(thirdRetryAfter, Is.EqualTo(0));
        });
    }

    [Test]
    public void Manual_identity_resync_targets_only_unverified_native_or_missing_lobby_rows()
    {
        string verifiedA = "76561198000000001";
        string verifiedB = "76561198000000002";
        string nativeOnly = "76561198000000003";
        string missingNative = "76561198000000004";

        IReadOnlyList<string> targets = PlayerIdentityEvidencePolicy.SelectManualResyncTargets(
            new[] { verifiedA, verifiedB, nativeOnly, "not-a-steam-id" },
            new[] { verifiedA, verifiedB, missingNative, string.Empty },
            new[] { verifiedA, verifiedB });

        Assert.Multiple(() =>
        {
            Assert.That(targets, Is.EquivalentTo(new[] { nativeOnly, missingNative }));
            Assert.That(targets, Does.Not.Contain(verifiedA));
            Assert.That(targets, Does.Not.Contain(verifiedB));
            Assert.That(targets, Does.Not.Contain("not-a-steam-id"));
        });
    }
}
