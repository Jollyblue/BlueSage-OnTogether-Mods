using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CompanionExactIdentityFingerprintPolicyTests
{
    private static readonly string[] Members =
    {
        "76561198000000001",
        "76561198000000002",
    };

    [Test]
    public void Exact_verified_subset_builds_without_requiring_unrelated_native_ghost_coverage()
    {
        Assert.That(
            CompanionExactIdentityFingerprintPolicy.TryBuildFingerprint(
                10,
                76561198000000001,
                76561198000000002,
                Members,
                new[] { "76561198000000002" },
                new[] { "76561198000000001", "76561198000000002" },
                new ulong[] { 11, 12 },
                new[] { 101, 102 },
                out string fingerprint),
            Is.True);
        Assert.That(fingerprint, Is.Not.Empty);
    }

    [Test]
    public void Canonical_fingerprint_is_order_independent_but_changes_with_exact_tuple_or_authority()
    {
        Assert.That(TryBuild(
            Members,
            new[] { "76561198000000001", "76561198000000002" },
            new ulong[] { 11, 12 },
            new[] { 101, 102 },
            out string baseline), Is.True);
        Assert.That(TryBuild(
            Members.Reverse().ToArray(),
            new[] { "76561198000000002", "76561198000000001" },
            new ulong[] { 12, 11 },
            new[] { 102, 101 },
            out string reordered), Is.True);
        Assert.That(reordered, Is.EqualTo(baseline));

        Assert.That(TryBuild(
            Members,
            new[] { "76561198000000001", "76561198000000002" },
            new ulong[] { 11, 99 },
            new[] { 101, 102 },
            out string remapped), Is.True);
        Assert.That(remapped, Is.Not.EqualTo(baseline));
    }

    [Test]
    public void Duplicate_or_nonmember_exact_rows_fail_closed()
    {
        Assert.That(TryBuild(
            Members,
            new[] { "76561198000000001", "76561198000000001" },
            new ulong[] { 11, 12 },
            new[] { 101, 102 },
            out _), Is.False);
        Assert.That(TryBuild(
            Members,
            new[] { "76561198000000001", "76561198000000003" },
            new ulong[] { 11, 12 },
            new[] { 101, 102 },
            out _), Is.False);
    }

    [Test]
    public void Rejected_authority_envelope_reports_the_exact_invalid_field()
    {
        Assert.That(
            CompanionExactIdentityFingerprintPolicy.TryBuildFingerprint(
                10,
                76561198000000001,
                76561198000000001,
                new[] { "76561198000000001" },
                new[] { "76561198000000002" },
                new[] { "76561198000000001" },
                new ulong[] { 11 },
                new[] { 101 },
                out _,
                out string reason),
            Is.False);
        Assert.That(reason, Is.EqualTo("published helper is not a current Steam lobby member"));
    }

    private static bool TryBuild(
        IReadOnlyList<string> members,
        IReadOnlyList<string> verifiedSteamIds,
        IReadOnlyList<ulong> verifiedPurrNetIds,
        IReadOnlyList<int> verifiedTransformIds,
        out string fingerprint) =>
        CompanionExactIdentityFingerprintPolicy.TryBuildFingerprint(
            10,
            76561198000000001,
            76561198000000002,
            members,
            new[] { "76561198000000002" },
            verifiedSteamIds,
            verifiedPurrNetIds,
            verifiedTransformIds,
            out fingerprint);
}
