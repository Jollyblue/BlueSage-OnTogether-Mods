using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicReleasePrivacyContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Public_badge_lane_contains_no_private_account_label_digest_or_identity_logging()
    {
        string[] files =
        {
            Read("src", "Core", "BlueSageExtensionContracts.cs"),
            Read("src", "Core", "BlueSageExtensionRegistryCore.cs"),
            Read("src", "BlueSageExtensionRegistry.cs"),
            Read("src", "Patches", "PlayerBadgeController.cs"),
            Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs"),
        };
        string combined = string.Join("\n", files);

        Assert.Multiple(() =>
        {
            Assert.That(combined, Does.Not.Match("[A-Fa-f0-9]{64}"));
            Assert.That(combined, Does.Not.Contain("LogDebug(steam").And.Not.Contain("LogWarning(steam"));
            Assert.That(typeof(BlueSageBadgeContributionResponseV1).GetProperties()
                .Any(property =>
                    property.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)), Is.False);
        });
    }

    [Test]
    public void Public_tests_describe_the_negative_account_only_as_non_creator_private_helper()
    {
        string source = File.ReadAllText(Path.Combine(
            Root,
            "BlueSage_QoL_Tweaks_Beta.Tests",
            "PublicReleasePrivacyContractTests.cs"));

        Assert.That(source, Does.Contain("non-creator private helper"));
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
}
