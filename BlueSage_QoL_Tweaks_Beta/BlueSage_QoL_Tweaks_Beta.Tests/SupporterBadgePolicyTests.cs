using System.Security.Cryptography;
using System.Text;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SupporterBadgePolicyTests
{
    private const string SyntheticIdentity = "12345678901234567";

    [Test]
    public void Supporter_badge_requires_exact_current_row_and_hash_match()
    {
        PlayerBadgeEvidence matched = BaseTruth().With(
            supporterIdentityHashMatched: true);

        Assert.Multiple(() =>
        {
            Assert.That(PlayerBadgePolicy.Compose(matched, 7).DisplayText,
                Is.EqualTo("[♥]"));
            Assert.That(PlayerBadgePolicy.Compose(
                matched.With(uniqueExactRow: false), 7).DisplayText,
                Is.Empty);
            Assert.That(PlayerBadgePolicy.Compose(
                matched.With(uniqueCurrentMember: false), 7).DisplayText,
                Is.Empty);
            Assert.That(PlayerBadgePolicy.Compose(
                matched.With(supporterIdentityHashMatched: false), 7).DisplayText,
                Is.Empty);
        });
    }

    [Test]
    public void Compiled_map_accepts_only_sha256_hashes_and_default_map_fails_closed()
    {
        string digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(SyntheticIdentity)));

        Assert.Multiple(() =>
        {
            Assert.That(SupporterBadgeIdentityPolicy.MatchesCompiledIdentityHash(
                SyntheticIdentity, new[] { digest }), Is.True);
            Assert.That(SupporterBadgeIdentityPolicy.MatchesCompiledIdentityHash(
                SyntheticIdentity, new[] { SyntheticIdentity }), Is.False);
            Assert.That(SupporterBadgeIdentityPolicy.IsRecognizedExactIdentity(
                SyntheticIdentity), Is.False,
                "No authoritative local supporter-to-account map exists, so the shipped map must fail closed.");
        });
    }

    [Test]
    public void Supporter_presentation_never_renders_the_identity()
    {
        PlayerBadgePresentation result = PlayerBadgePolicy.Compose(
            BaseTruth().With(supporterIdentityHashMatched: true), 7);

        Assert.Multiple(() =>
        {
            Assert.That(result.DisplayText, Is.EqualTo("[♥]"));
            Assert.That(result.TooltipText,
                Is.EqualTo("[♥] — BlueSage community supporter"));
            Assert.That(result.DisplayText, Does.Not.Contain(SyntheticIdentity));
            Assert.That(result.TooltipText, Does.Not.Contain(SyntheticIdentity));
        });
    }

    [Test]
    public void Runtime_controller_uses_the_hash_boundary_instead_of_rendering_raw_ids()
    {
        string root = FindProjectRoot();
        string controller = File.ReadAllText(Path.Combine(
            root, "src", "Patches", "PlayerBadgeController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain(
                "SupporterBadgeIdentityPolicy.IsRecognizedExactIdentity(marker.BoundSteamId)"));
            Assert.That(controller, Does.Not.Contain("[♥] " + SyntheticIdentity));
        });
    }

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

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
