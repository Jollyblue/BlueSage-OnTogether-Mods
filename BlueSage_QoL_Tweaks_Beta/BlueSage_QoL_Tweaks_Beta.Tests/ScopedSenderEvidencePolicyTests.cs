using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ScopedSenderEvidencePolicyTests
{
    private const string SteamId = "76561198012345678";
    private const string PlayerId = "player-id-0";
    private const string Correlator = "row-a19c34d278ef";

    [Test]
    public void Ordinary_unique_agreement_is_explicit_local_evidence_without_purrnet_disclosure()
    {
        bool built = ScopedSenderEvidencePolicy.TryBuild(
            Valid(privileged: false),
            out string evidence,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(built, Is.True, error);
            Assert.That(evidence, Does.StartWith("BlueSage exact identity evidence - EVIDENCE ONLY"));
            Assert.That(evidence, Does.Contain("Local clipboard only"));
            Assert.That(evidence, Does.Contain("Native row index (zero-based): 0"));
            Assert.That(evidence, Does.Contain("Session row correlator: " + Correlator));
            Assert.That(evidence, Does.Contain("SteamID64: " + SteamId));
            Assert.That(evidence, Does.Not.Contain(PlayerId));
            Assert.That(evidence, Does.Not.Contain("Display name"));
        });
    }

    [Test]
    public void Exact_host_or_fresh_helper_scope_may_include_complete_verified_agreement()
    {
        bool built = ScopedSenderEvidencePolicy.TryBuild(
            Valid(privileged: true),
            out string evidence,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(built, Is.True, error);
            Assert.That(evidence, Does.Contain("SteamID64: " + SteamId));
            Assert.That(evidence, Does.Contain("PurrNet PlayerID: " + PlayerId));
            Assert.That(evidence, Does.Contain("Agreement: unique current Steam/PurrNet/native-row/transform"));
        });
    }

    [TestCase(false, true, true, true)]
    [TestCase(true, false, true, true)]
    [TestCase(true, true, false, true)]
    [TestCase(true, true, true, false)]
    public void Split_duplicate_stale_or_unbound_sender_fails_closed(
        bool unique,
        bool fresh,
        bool tupleMatches,
        bool hasBinding)
    {
        ScopedSenderEvidenceContext context = Valid(privileged: true);
        context.UniqueCurrentAgreement = unique;
        context.FreshAtClick = fresh;
        context.ExactTupleMatches = tupleMatches;
        context.HasSenderBinding = hasBinding;

        bool built = ScopedSenderEvidencePolicy.TryBuild(
            context,
            out string evidence,
            out _);

        Assert.Multiple(() =>
        {
            Assert.That(built, Is.False);
            Assert.That(evidence, Is.Empty);
        });
    }

    [Test]
    public void Native_row_zero_is_valid_only_when_the_exact_tuple_agrees()
    {
        ScopedSenderEvidenceContext context = Valid(privileged: false);
        context.NativeRowIndex = 0;

        Assert.That(
            ScopedSenderEvidencePolicy.TryBuild(context, out _, out _),
            Is.True);

        context.ExactTupleMatches = false;
        Assert.That(
            ScopedSenderEvidencePolicy.TryBuild(context, out _, out _),
            Is.False);
    }

    [TestCase("not-a-steamid", "player-id-0", "row-a19c34d278ef")]
    [TestCase("76561198012345678", "player\nid", "row-a19c34d278ef")]
    [TestCase("76561198012345678", "player-id-0", "row-invalid!")]
    public void Malformed_exact_fields_are_rejected_not_sanitized_into_authority(
        string steamId,
        string playerId,
        string correlator)
    {
        ScopedSenderEvidenceContext context = Valid(privileged: true);
        context.SteamId = steamId;
        context.PlayerId = playerId;
        context.SessionRowCorrelator = correlator;

        Assert.That(
            ScopedSenderEvidencePolicy.TryBuild(context, out _, out _),
            Is.False);
    }

    private static ScopedSenderEvidenceContext Valid(bool privileged)
    {
        return new ScopedSenderEvidenceContext
        {
            HasSenderBinding = true,
            UniqueCurrentAgreement = true,
            FreshAtClick = true,
            ExactTupleMatches = true,
            PrivilegedObserver = privileged,
            SteamId = SteamId,
            PlayerId = PlayerId,
            NativeRowIndex = 0,
            SessionRowCorrelator = Correlator,
        };
    }
}
