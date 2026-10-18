using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicReadmeTrustAndViewportCopyContractTests
{
    private const string RescuePrefix = "- A host or approved Helper can privately assist";

    [Test]
    public void Trusted_host_rescue_copy_is_compact_player_language_without_protocol_versions()
    {
        string rescueBullet = File.ReadLines(ReadmePath)
            .Single(line => line.StartsWith(RescuePrefix, StringComparison.Ordinal));

        const string expected = "- A host or approved Helper can privately assist one confirmed QoL player. If BlueSage is unsure who the request belongs to, nothing happens.";

        Assert.Multiple(() =>
        {
            Assert.That(rescueBullet, Is.EqualTo(expected));
            Assert.That(rescueBullet, Does.Not.Contain("`v2`").And.Not.Contain("`v3`"));
            Assert.That(rescueBullet, Does.Not.Contain("SteamID64").And.Not.Contain("native PlayerID").And.Not.Contain("replay nonces"));
        });
    }

    [Test]
    public void Public_credits_use_the_concise_bluesage_love_line_without_internal_viewport_qa_copy()
    {
        string readme = File.ReadAllText(ReadmePath);

        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Not.Contain("On first open,").And.Not.Contain("1453×759"));
            Assert.That(readme, Does.Not.Contain("Share with Rowan!!!"));
            Assert.That(readme, Does.Contain("Made by Blue + Sage = BlueSage with superpowered neurodivergence ♥"));
            Assert.That(readme, Does.Not.Contain("partner in crime"));
        });
    }

    private static string ReadmePath => Path.Combine(FindProjectRoot(), "README.md");

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
