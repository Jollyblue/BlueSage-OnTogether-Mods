using NUnit.Framework;
using System;
using System.IO;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 C2 step 1 (Blue, 2026-10-22): the F6 host role label refusal must say which exact-identity predicate
// failed, which the runtime used to discard. These pins keep the reason plumbed from the badge controller to the
// public result string that BlueExclusive prints in its warning.
[TestFixture]
internal sealed class HostRoleLabelRefusalReasonContractTests
{
    private static string ReadSource(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
        return File.ReadAllText(Path.Combine(root, "src", relative));
    }

    [Test]
    public void Badge_controller_exposes_the_freshness_reason_in_runtime_order()
    {
        string source = ReadSource(Path.Combine("Patches", "PlayerBadgeController.cs"));
        int start = source.IndexOf("out bool contributedBadgePresent,\n        out string reason)", StringComparison.Ordinal);
        if (start < 0)
            start = source.IndexOf("out bool contributedBadgePresent,\r\n        out string reason)", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0), "the reason overload must exist");
        string body = source.Substring(start, Math.Min(2600, source.Length - start));

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("CreatorContributionFreshnessPolicy.Describe("));
            Assert.That(body, Does.Contain("lobbyKeyMatches: _lobbyKey == (ulong)expectedLobby"));
            // Round 5e: freshness compares against the generation the registry is keyed to (companion unless locked).
            Assert.That(body, Does.Contain("generationCurrent: truth.EvidenceGeneration ==").And.Contain("HostAuthorityRegistryKeyPolicy.FreshnessGeneration("));
            Assert.That(body, Does.Contain("stillFresh: truth.FreshUntilUtcTicks >= nowTicks"));
            Assert.That(body, Does.Contain("the companion badge contribution read did not answer"));
        });
    }

    [Test]
    public void Role_label_refusal_names_the_failing_input()
    {
        string source = ReadSource(Path.Combine("Patches", "PlayerRoleLabelController.cs"));
        int start = source.IndexOf("internal static bool TrySetLocalHostLabelOverride(", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0));
        string body = source.Substring(start, Math.Min(3200, source.Length - start));

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("out string freshnessReason"));
            Assert.That(body, Does.Contain("fresh exact creator or approved private-fleet host authority is unavailable."));
            Assert.That(body, Does.Contain("Exact-identity check: "));
            Assert.That(body, Does.Contain("creator badge: "));
            Assert.That(body, Does.Contain("private host authority: "));
            Assert.That(body, Does.Contain("\"; companion generation \" + PlayerBadgeController.CompanionEvidenceGenerationForDiagnostics"), "Round 5d: the round 5e input.");
            Assert.That(body, Does.Contain("\"; authority generation \" + PlayerBadgeController.AuthorityEvidenceGenerationForDiagnostics"));
        });
    }
}
