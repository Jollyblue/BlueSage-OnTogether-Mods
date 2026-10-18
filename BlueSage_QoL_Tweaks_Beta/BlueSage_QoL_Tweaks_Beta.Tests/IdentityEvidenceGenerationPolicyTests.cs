using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityEvidenceGenerationPolicyTests
{
    [Test]
    public void Repeated_identical_authority_evidence_keeps_the_same_generation()
    {
        string fingerprint = BuildFingerprint();

        Assert.That(
            IdentityEvidenceGenerationPolicy.TryObserve(
                string.Empty,
                0,
                acceptedValid: false,
                fingerprint,
                observedValid: true,
                out string accepted,
                out long firstGeneration,
                out bool firstValid,
                out bool firstChanged,
                out bool firstRequiresEpochAdvance),
            Is.True);
        for (int index = 0; index < 1_000; index++)
        {
            Assert.That(
                IdentityEvidenceGenerationPolicy.TryObserve(
                    accepted,
                    firstGeneration,
                    firstValid,
                    fingerprint,
                    observedValid: true,
                    out string repeated,
                    out long repeatedGeneration,
                    out bool repeatedValid,
                    out bool repeatedChanged,
                    out bool repeatedRequiresEpochAdvance),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(repeated, Is.EqualTo(accepted));
                Assert.That(
                    repeatedGeneration,
                    Is.EqualTo(firstGeneration));
                Assert.That(repeatedValid, Is.True);
                Assert.That(repeatedChanged, Is.False);
                Assert.That(
                    repeatedRequiresEpochAdvance,
                    Is.False);
            });
        }

        Assert.Multiple(() =>
        {
            Assert.That(firstChanged, Is.True);
            Assert.That(firstGeneration, Is.EqualTo(1));
            Assert.That(firstValid, Is.True);
            Assert.That(firstRequiresEpochAdvance, Is.False);
        });
    }

    [TestCase("owner")]
    [TestCase("local")]
    [TestCase("helper")]
    [TestCase("native-steam")]
    [TestCase("purrnet")]
    [TestCase("transform")]
    [TestCase("membership")]
    public void Any_authority_evidence_change_advances_generation(string change)
    {
        string baseline = BuildFingerprint();
        string changed = change switch
        {
            "owner" => BuildFingerprint(ownerSteamId: 76561198000000002UL),
            "local" => BuildFingerprint(
                localSteamId: 76561198000000002UL),
            "helper" => BuildFingerprint(
                helperSteamIds: new[] { "76561198000000002" }),
            "native-steam" => BuildFingerprint(
                nativeSteamIds: new[]
                {
                    "76561198000000001",
                    "76561198000000003",
                }),
            "purrnet" => BuildFingerprint(
                nativePurrNetIds: new[] { "41", "99" }),
            "transform" => BuildFingerprint(
                transformInstanceIds: new[] { 501, 999 }),
            "membership" => BuildFingerprint(
                lobbyMembers: new[]
                {
                    "76561198000000001",
                    "76561198000000003",
                }),
            _ => throw new AssertionException("Unhandled test case."),
        };

        Assert.That(changed, Is.Not.EqualTo(baseline));
        Assert.That(
            IdentityEvidenceGenerationPolicy.TryObserve(
                baseline,
                7,
                acceptedValid: true,
                changed,
                observedValid: true,
                out string accepted,
                out long generation,
                out bool valid,
                out bool evidenceChanged,
                out bool requiresEpochAdvance),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(evidenceChanged, Is.True);
            Assert.That(accepted, Is.EqualTo(changed));
            Assert.That(generation, Is.EqualTo(8));
            Assert.That(valid, Is.True);
            Assert.That(requiresEpochAdvance, Is.False);
        });
    }

    [Test]
    public void Lobby_member_order_does_not_create_false_generation_churn()
    {
        string first = BuildFingerprint(
            lobbyMembers: new[]
            {
                "76561198000000001",
                "76561198000000002",
            });
        string reordered = BuildFingerprint(
            lobbyMembers: new[]
            {
                "76561198000000002",
                "76561198000000001",
            });

        Assert.That(reordered, Is.EqualTo(first));
    }

    [Test]
    public void Helper_order_does_not_create_false_generation_churn()
    {
        string first = BuildFingerprint(
            helperSteamIds: new[]
            {
                "76561198000000001",
                "76561198000000002",
            });
        string reordered = BuildFingerprint(
            helperSteamIds: new[]
            {
                "76561198000000002",
                "76561198000000001",
            });

        Assert.That(reordered, Is.EqualTo(first));
    }

    [Test]
    public void Invalid_transition_and_recovery_each_advance_once_without_reusing_old_lease()
    {
        string fingerprint = BuildFingerprint();
        Assert.That(
            IdentityEvidenceGenerationPolicy.TryObserve(
                fingerprint,
                7,
                acceptedValid: true,
                observedFingerprint: string.Empty,
                observedValid: false,
                out string invalidFingerprint,
                out long invalidGeneration,
                out bool invalidValid,
                out bool invalidChanged,
                out bool invalidRequiresEpochAdvance),
            Is.True);
        Assert.That(
            IdentityEvidenceGenerationPolicy.TryObserve(
                invalidFingerprint,
                invalidGeneration,
                invalidValid,
                observedFingerprint: string.Empty,
                observedValid: false,
                out string repeatedFingerprint,
                out long repeatedGeneration,
                out bool repeatedValid,
                out bool repeatedChanged,
                out bool repeatedRequiresEpochAdvance),
            Is.True);
        Assert.That(
            IdentityEvidenceGenerationPolicy.TryObserve(
                repeatedFingerprint,
                repeatedGeneration,
                repeatedValid,
                fingerprint,
                observedValid: true,
                out string recoveredFingerprint,
                out long recoveredGeneration,
                out bool recoveredValid,
                out bool recoveredChanged,
                out bool recoveredRequiresEpochAdvance),
            Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(invalidGeneration, Is.EqualTo(8));
            Assert.That(invalidValid, Is.False);
            Assert.That(invalidChanged, Is.True);
            Assert.That(invalidRequiresEpochAdvance, Is.False);
            Assert.That(repeatedGeneration, Is.EqualTo(8));
            Assert.That(repeatedValid, Is.False);
            Assert.That(repeatedChanged, Is.False);
            Assert.That(
                repeatedRequiresEpochAdvance,
                Is.False);
            Assert.That(recoveredFingerprint, Is.EqualTo(fingerprint));
            Assert.That(recoveredGeneration, Is.EqualTo(9));
            Assert.That(recoveredValid, Is.True);
            Assert.That(recoveredChanged, Is.True);
            Assert.That(
                recoveredRequiresEpochAdvance,
                Is.False);
        });
    }

    [Test]
    public void Generation_overflow_requests_new_epoch_instead_of_recycling()
    {
        bool success =
            IdentityEvidenceGenerationPolicy.TryObserve(
                BuildFingerprint(),
                long.MaxValue,
                acceptedValid: true,
                BuildFingerprint(ownerSteamId: 76561198000000002UL),
                observedValid: true,
                out _,
                out long generation,
                out bool valid,
                out bool changed,
                out bool requiresEpochAdvance);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(generation, Is.EqualTo(long.MaxValue));
            Assert.That(valid, Is.False);
            Assert.That(changed, Is.False);
            Assert.That(requiresEpochAdvance, Is.True);
        });
    }

    [Test]
    public void Invalid_or_incomplete_authority_evidence_fails_closed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                IdentityEvidenceGenerationPolicy.TryBuildFingerprint(
                    0,
                    76561198000000001UL,
                    new[] { "76561198000000001" },
                    new[] { "41" },
                    new[] { 501 },
                    new[] { "76561198000000001" },
                    76561198000000001UL,
                    Array.Empty<string>(),
                    coverageComplete: true,
                    liveSteamCheckCount: 0,
                    leftStaleCount: 0,
                    ambiguousCount: 0,
                    out _),
                Is.False);
            Assert.That(
                IdentityEvidenceGenerationPolicy.TryBuildFingerprint(
                    9001,
                    76561198000000001UL,
                    new[] { "76561198000000001" },
                    Array.Empty<string>(),
                    new[] { 501 },
                    new[] { "76561198000000001" },
                    76561198000000001UL,
                    Array.Empty<string>(),
                    coverageComplete: true,
                    liveSteamCheckCount: 0,
                    leftStaleCount: 0,
                    ambiguousCount: 0,
                    out _),
                Is.False);
            Assert.That(
                IdentityEvidenceGenerationPolicy.TryObserve(
                    "old",
                    4,
                    acceptedValid: true,
                    string.Empty,
                    observedValid: true,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _),
                Is.False);
        });
    }

    [Test]
    public void Unity_negative_instance_ids_are_valid_and_zero_is_fail_closed()
    {
        bool negativeAccepted =
            IdentityEvidenceGenerationPolicy.TryBuildFingerprint(
                9001,
                76561198000000001UL,
                new[] { "76561198000000001", "76561198000000002" },
                new[] { "41", "42" },
                new[] { -501, 502 },
                new[] { "76561198000000001", "76561198000000002" },
                76561198000000001UL,
                Array.Empty<string>(),
                coverageComplete: true,
                liveSteamCheckCount: 0,
                leftStaleCount: 0,
                ambiguousCount: 0,
                out _);
        bool zeroAccepted =
            IdentityEvidenceGenerationPolicy.TryBuildFingerprint(
                9001,
                76561198000000001UL,
                new[] { "76561198000000001", "76561198000000002" },
                new[] { "41", "42" },
                new[] { 0, 502 },
                new[] { "76561198000000001", "76561198000000002" },
                76561198000000001UL,
                Array.Empty<string>(),
                coverageComplete: true,
                liveSteamCheckCount: 0,
                leftStaleCount: 0,
                ambiguousCount: 0,
                out _);

        Assert.Multiple(() =>
        {
            Assert.That(negativeAccepted, Is.True);
            Assert.That(zeroAccepted, Is.False);
        });
    }

    private static string BuildFingerprint(
        ulong ownerSteamId = 76561198000000001UL,
        ulong localSteamId = 76561198000000001UL,
        IReadOnlyList<string>? nativeSteamIds = null,
        IReadOnlyList<string>? nativePurrNetIds = null,
        IReadOnlyList<int>? transformInstanceIds = null,
        IReadOnlyList<string>? lobbyMembers = null,
        IReadOnlyList<string>? helperSteamIds = null)
    {
        Assert.That(
            IdentityEvidenceGenerationPolicy.TryBuildFingerprint(
                9001,
                ownerSteamId,
                nativeSteamIds ?? new[]
                {
                    "76561198000000001",
                    "76561198000000002",
                },
                nativePurrNetIds ?? new[] { "41", "42" },
                transformInstanceIds ?? new[] { 501, 502 },
                lobbyMembers ?? new[]
                {
                    "76561198000000001",
                    "76561198000000002",
                },
                localSteamId,
                helperSteamIds ?? Array.Empty<string>(),
                coverageComplete: true,
                liveSteamCheckCount: 0,
                leftStaleCount: 0,
                ambiguousCount: 0,
                out string fingerprint),
            Is.True);
        return fingerprint;
    }
}
