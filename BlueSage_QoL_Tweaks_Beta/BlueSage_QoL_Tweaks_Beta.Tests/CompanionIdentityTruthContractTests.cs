using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CompanionIdentityTruthContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Identity_truth_snapshot_is_public_typed_and_contains_only_bounded_primitive_truth()
    {
        long captured = 1_000_000;
        var snapshot = new BlueSageIdentityTruthSnapshotV1(
            steamId: 100,
            purrNetId: 200,
            lobbyEpoch: 7,
            evidenceGeneration: 11,
            capturedUtcTicks: captured,
            freshUntilUtcTicks: captured + TimeSpan.TicksPerSecond);

        Assert.Multiple(() =>
        {
            Assert.That(typeof(BlueSageIdentityTruthSnapshotV1).IsPublic, Is.True);
            Assert.That(snapshot, Is.AssignableTo<IBlueSageIdentityTruthV1>());
            Assert.That(snapshot.SteamId, Is.EqualTo(100));
            Assert.That(snapshot.PurrNetId, Is.EqualTo(200));
            Assert.That(snapshot.LobbyEpoch, Is.EqualTo(7));
            Assert.That(snapshot.EvidenceGeneration, Is.EqualTo(11));
            Assert.That(snapshot.CapturedUtcTicks, Is.EqualTo(captured));
            Assert.That(snapshot.FreshUntilUtcTicks, Is.EqualTo(captured + TimeSpan.TicksPerSecond));
            Assert.That(typeof(BlueSageIdentityTruthSnapshotV1).GetProperties()
                .All(property => property.PropertyType.IsPrimitive), Is.True);
        });
    }

    [Test]
    public void Public_companion_bridge_resolves_fresh_exact_Steam_and_PurrNet_truth_without_reflection()
    {
        string plugin = Read("src", "Plugin.cs");
        string badgeController = Read("src", "Patches", "PlayerBadgeController.cs");
        string identityBridge = ExtractMethodBody(
            badgeController,
            "internal static bool TryResolveFreshIdentityTruth(");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain(
                "public bool TryResolveCompanionIdentityTruthV1("));
            Assert.That(plugin, Does.Contain(
                "PlayerBadgeController.TryResolveFreshIdentityTruth("));
            Assert.That(badgeController, Does.Contain(
                "internal static bool TryResolveFreshIdentityTruth("));
            Assert.That(badgeController, Does.Contain(
                ".TryResolveExactWithCompanionFingerprint("));
            Assert.That(badgeController, Does.Contain(
                "TryGetExactPurrNetId("));
            Assert.That(badgeController, Does.Contain(
                "SteamMatchmaking.GetNumLobbyMembers("));
            Assert.That(identityBridge, Does.Not.Contain(
                "AccessTools").And.Not.Contain("System.Reflection"));
        });
    }

    [Test]
    public void Companion_exact_tuple_lease_is_separate_from_global_complete_roster_authority()
    {
        string badgeController = Read("src", "Patches", "PlayerBadgeController.cs");
        string identityController = Read("src", "PlayerIdentityEvidenceController.cs");
        string companionResolver = ExtractMethodBody(
            badgeController,
            "internal static bool TryResolveFreshIdentityTruthBySteam(");
        string exactResolver = ExtractMethodBody(
            identityController,
            "internal static bool TryResolveExactWithCompanionFingerprint(");
        string cachedCompanionContext = ExtractMethodBody(
            identityController,
            "internal static bool TryCaptureCompanionIdentityContextCached(");

        Assert.Multiple(() =>
        {
            Assert.That(companionResolver, Does.Contain("RefreshCompanionEvidenceGeneration("));
            Assert.That(companionResolver, Does.Not.Contain("RefreshEvidenceGeneration("));
            Assert.That(exactResolver, Does.Contain("TryCaptureCompanionIdentityContextCached("));
            Assert.That(cachedCompanionContext, Does.Contain("CompanionExactIdentityFingerprintPolicy"));
            Assert.That(cachedCompanionContext, Does.Not.Contain("snapshot.CoverageComplete"));
            Assert.That(badgeController, Does.Contain(
                ".TryCaptureCompleteAuthorityEvidenceAndRowsCached("),
                "Global moderation/presentation authority must retain the complete-roster gate.");
        });
    }

    [Test]
    public void Evidence_generation_is_bound_to_changed_complete_roster_truth_not_poll_or_read_count()
    {
        string badgeController = Read(
            "src",
            "Patches",
            "PlayerBadgeController.cs");
        string identityController = Read(
            "src",
            "PlayerIdentityEvidenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                badgeController,
                Does.Contain("_evidenceFingerprint"));
            Assert.That(
                badgeController,
                Does.Contain("RefreshEvidenceGeneration("));
            Assert.That(
                badgeController,
                Does.Contain(
                    ".TryCaptureCompleteAuthorityEvidenceAndRowsCached("));
            Assert.That(
                badgeController,
                Does.Not.Contain("AdvanceEvidenceGeneration();"));
            Assert.That(
                identityController,
                Does.Contain(
                    "internal static bool TryCaptureAuthorityEvidenceFingerprint("));
            Assert.That(
                identityController,
                Does.Contain("snapshot.CoverageComplete"));
            Assert.That(
                identityController,
                Does.Contain(".TryBuildFingerprint("));
        });
    }

    [Test]
    public void Authority_fingerprint_and_exact_tuple_reuse_one_bounded_dirty_aware_atomic_snapshot()
    {
        string badgeController = Read(
            "src",
            "Patches",
            "PlayerBadgeController.cs");
        string identityController = Read(
            "src",
            "PlayerIdentityEvidenceController.cs");
        string exactPurrResolver = ExtractMethodBody(
            badgeController,
            "private static bool TryGetExactPurrNetId(");
        string exactAuthorityResolver = ExtractMethodBody(
            identityController,
            "internal static bool TryResolveExactWithAuthorityFingerprint(");

        Assert.Multiple(() =>
        {
            Assert.That(
                identityController,
                Does.Contain("internal ulong PurrNetId;"));
            Assert.That(
                identityController,
                Does.Contain("internal int TransformInstanceId;"));
            Assert.That(
                exactAuthorityResolver,
                Does.Contain(
                    "CaptureRosterSnapshot(cacheAllowed: true)"));
            Assert.That(
                exactAuthorityResolver,
                Does.Not.Contain(
                    "CaptureRosterSnapshot(cacheAllowed: false)"));
            Assert.That(
                exactAuthorityResolver,
                Does.Contain("TryBuildAuthorityFingerprint("));
            Assert.That(
                exactAuthorityResolver,
                Does.Contain("snapshot.Verified.SingleOrDefault("));
            Assert.That(
                identityController,
                Does.Contain(
                    "the native Steam/PurrNet/transform roster changed while BlueSage was reading it"));
            Assert.That(
                identityController,
                Does.Contain(
                    "the Steam lobby membership changed while BlueSage was reading it"));
            Assert.That(
                identityController,
                Does.Contain(
                    "the local/owner/helper authority context changed while BlueSage was reading it"));
            Assert.That(
                exactPurrResolver,
                Does.Contain("identity?.PurrNetId"));
            Assert.That(
                exactPurrResolver,
                Does.Not.Contain("NetworkSingleton")
                    .And.Not.Contain("PlayerIDs"));
            Assert.That(
                badgeController,
                Does.Contain("InvalidateEvidenceGeneration();"));
        });
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), signature);
        int openBrace = source.IndexOf('{', signatureIndex);
        Assert.That(openBrace, Is.GreaterThan(signatureIndex), signature);

        int depth = 0;
        for (int index = openBrace; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(openBrace, index - openBrace + 1);
                    }
                    break;
            }
        }

        Assert.Fail("Method body was not balanced: " + signature);
        return string.Empty;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
}
