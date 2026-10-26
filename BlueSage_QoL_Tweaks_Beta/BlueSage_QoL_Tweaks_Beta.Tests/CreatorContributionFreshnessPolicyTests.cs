using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 C2 step 1: the F6 host role label refusal must name the predicate that failed.
[TestFixture]
internal sealed class CreatorContributionFreshnessPolicyTests
{
    private static string Describe(
        bool truthResolved = true,
        string truthError = "",
        bool lobbyKeyMatches = true,
        bool steamIdMatches = true,
        bool purrNetKnown = true,
        bool epochCurrent = true,
        bool generationCurrent = true,
        bool stillFresh = true)
    {
        return CreatorContributionFreshnessPolicy.Describe(
            truthResolved, truthError, lobbyKeyMatches, steamIdMatches, purrNetKnown, epochCurrent, generationCurrent, stillFresh);
    }

    [Test]
    public void All_good_is_empty()
    {
        Assert.That(Describe(), Is.Empty);
    }

    [Test]
    public void Unresolved_truth_carries_the_inner_error()
    {
        string reason = Describe(truthResolved: false, truthError: "public QoL could not resolve one exact current native row: x");
        Assert.That(reason, Does.Contain("did not resolve"));
        Assert.That(reason, Does.Contain("public QoL could not resolve one exact current native row: x"));
    }

    [Test]
    public void Unresolved_truth_without_detail_still_says_so()
    {
        Assert.That(Describe(truthResolved: false, truthError: "  "), Does.Contain("no detail"));
    }

    [Test]
    public void Reasons_follow_the_runtime_order()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Describe(lobbyKeyMatches: false, steamIdMatches: false), Does.Contain("different lobby"));
            Assert.That(Describe(steamIdMatches: false, purrNetKnown: false), Does.Contain("different Steam id"));
            Assert.That(Describe(purrNetKnown: false, epochCurrent: false), Does.Contain("PurrNet id"));
            Assert.That(Describe(epochCurrent: false, generationCurrent: false), Does.Contain("earlier lobby epoch"));
            Assert.That(Describe(generationCurrent: false, stillFresh: false), Does.Contain("earlier evidence generation"));
            Assert.That(Describe(stillFresh: false), Does.Contain("lease expired"));
        });
    }
}
