using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReleaseCandidateIdentityContractTests
{
    [Test]
    public void August_public_version_has_a_distinct_large_lobby_cadence_uat_identity()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string verifier = File.ReadAllText(Path.Combine(root, "tools", "Verify-BlueSageQoLTweaks.ps1"));
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        // Round 5d (Blue, 2026-10-23): the 0.2.8 badge build folded back into 0.2.7 (0.2.7 was never public); label "round5h-rc7" until Blue's live UAT passes.
        const string expectedIdentity = "0.2.7+20261025.round5h-rc7";
        const string expectedUatIdentity = "0.2.7+20261025.round5h-rc7-uat";

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain($"PluginBuildIdentity = \"{expectedIdentity}\""));
            Assert.That(plugin, Does.Contain($"PluginBuildIdentity = \"{expectedUatIdentity}\""));
            Assert.That(verifier, Does.Contain(expectedIdentity));
            Assert.That(verifier, Does.Contain("[DateTimeOffset]::new(2026, 8, 6, 0, 0, 0, [TimeSpan]::Zero)"));
            Assert.That(plugin, Does.Not.Contain("0.2.0+20260715.6-nell-visibility-rescue"));
            Assert.That(plugin, Does.Not.Contain("0.2.0+20260715.8-release-candidate"));
            Assert.That(plugin, Does.Not.Contain("0.2.0+20260715.9-final-live-polish"));
            Assert.That(changelog.Replace("\r\n", "\n"), Does.StartWith("# Changelog\n\nEvery previous public release is kept below so the complete BlueSage history stays available on Thunderstore.\n\n## 0.2.7 — 2026-10-25"));
            Assert.That(changelog, Does.Contain("## 0.2.4 — 2026-07-30"));
            Assert.That(changelog, Does.Not.Contain("CreatorPresent"));
            Assert.That(changelog, Does.Contain("role-label colors and visibility"));
            Assert.That(changelog, Does.Contain("Focus Pulse"));
            Assert.That(changelog, Does.Contain("exact clicked player's portrait"));
        });
    }

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
