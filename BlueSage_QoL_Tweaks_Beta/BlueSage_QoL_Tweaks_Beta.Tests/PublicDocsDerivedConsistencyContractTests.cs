using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5b (2026-10-23, Blue: "refresh the gate and build checks so we stop correcting ourselves later").
// The older documentation pins are literal strings that a release-identity script has to move by hand; a missed
// pin passes silently once the literal is moved. These checks derive the expected text from the code itself:
// the plugin's version and release date, and the policy constants behind every number in the README settings
// table. If a default changes in code and the README still quotes the old number, the gate fails.
public sealed class PublicDocsDerivedConsistencyContractTests
{
    [Test]
    public void Readme_and_changelog_carry_the_plugin_version_and_release_date_from_the_code()
    {
        string plugin = ReadRootFile(Path.Combine("src", "Plugin.cs"));
        string version = Capture(plugin, "public const string PluginVersion = \"([^\"]+)\";");
        string releaseDate = Capture(plugin, "public const string PluginReleaseDate = \"([0-9]{4}-[0-9]{2}-[0-9]{2})\";");
        DateTime date = DateTime.ParseExact(releaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        string longDate = date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

        string readme = ReadRootFile("README.md");
        string changelog = ReadRootFile("CHANGELOG.md").Replace("\r\n", "\n");
        string manifest = ReadRootFile("manifest.json");
        string verifier = ReadRootFile(Path.Combine("tools", "Verify-BlueSageQoLTweaks.ps1"));

        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Contain($"**Version {version} - {longDate}.**"), "README version line must match PluginVersion and PluginReleaseDate.");
            Assert.That(changelog, Does.StartWith("# Changelog\n\nEvery previous public release is kept below so the complete BlueSage history stays available on Thunderstore.\n\n## " + version + " — " + releaseDate), "CHANGELOG must lead with the current version and the code's release date.");
            Assert.That(manifest, Does.Contain($"\"version_number\": \"{version}\""), "manifest.json must carry PluginVersion.");
            Assert.That(verifier, Does.Contain($"PluginReleaseDate = \"{releaseDate}\""), "The verifier's release-date pin must follow the code.");
            Assert.That(verifier, Does.Contain($"## {version} — {releaseDate}"), "The verifier's CHANGELOG heading pin must follow the code.");
            Assert.That(plugin, Does.Contain($"PluginBuildIdentity = \"{version}+{date:yyyyMMdd}."), "The build identity must start with the version and the release date.");
        });
    }

    [Test]
    public void Readme_settings_table_and_feature_text_quote_the_real_defaults()
    {
        string readme = ReadRootFile("README.md");
        string plugin = ReadRootFile(Path.Combine("src", "Plugin.cs"));
        string toastSeconds = ToastPolicy.DefaultHoldSeconds.ToString("0.#", CultureInfo.InvariantCulture);
        string reaction = FishingAidPolicy.DefaultReactionSeconds.ToString("0.#", CultureInfo.InvariantCulture);
        string release = FishingAidPolicy.DefaultReleaseSeconds.ToString("0.#", CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Contain($"| Automatic Cleanup | On, every {SweepSettingsPolicy.DefaultIntervalMinutes} min |"));
            Assert.That(readme, Does.Contain($"Automatic Cleanup starts on and runs every {SweepSettingsPolicy.DefaultIntervalMinutes} minutes"));
            Assert.That(readme, Does.Contain($"| Toast Pop-ups | On, {toastSeconds} s |"));
            Assert.That(readme, Does.Contain($"| Chat rows | {ChatHistoryLimitPolicy.DefaultRows} |"));
            Assert.That(readme, Does.Contain($"| Local chat range | {LocalChatRangePolicy.DefaultMeters.ToString("0.#", CultureInfo.InvariantCulture)} m |"));
            Assert.That(readme, Does.Contain($"default {ChatHistoryLimitPolicy.DefaultRows})"), "The /chatrows command line must quote the real default.");
            Assert.That(readme, Does.Contain($"| Reconnect Guard | On, {ReconnectRejoinHoldPolicy.DefaultHoldSeconds} s wait |"));
            Assert.That(readme, Does.Contain($"stops after {ReconnectGuardInput.DefaultMaxGenerationsPerLobby} in a row on the same lobby"));
            Assert.That(readme, Does.Contain($"waits {ReconnectRejoinHoldPolicy.DefaultHoldSeconds} seconds after a real disconnect"));
            Assert.That(readme, Does.Contain($"The wait is {ReconnectRejoinHoldPolicy.DefaultHoldSeconds} seconds by default"));
            Assert.That(readme, Does.Contain($"| Fishing Aid | Off | Reaction {reaction} s, 7 clicks per second, release {release} s |"));
            Assert.That(plugin, Does.Contain("Config.Bind(\"Asset Sweep\", \"NotifyOnSweep\", false,"), "The automatic cleanup is quiet by default since 0.2.7.");
            Assert.That(readme, Does.Not.Contain("Posts a quiet done note in chat"), "The README must not claim the automatic cleanup posts a chat note (NotifyOnSweep defaults to off).");
            Assert.That(readme, Does.Contain("The automatic run stays quiet"), "The README must say the automatic cleanup is quiet.");
        });
    }

    [Test]
    public void Readme_whats_new_leads_with_the_current_release_not_an_older_one()
    {
        string readme = ReadRootFile("README.md");
        string plugin = ReadRootFile(Path.Combine("src", "Plugin.cs"));
        string version = Capture(plugin, "public const string PluginVersion = \"([^\"]+)\";");
        string whatsNew = Slice(readme, "## What's new", "## Quick Start");

        Assert.Multiple(() =>
        {
            Assert.That(whatsNew, Does.Contain($"**New in {version}:**"), "The What's new section must open with the current version's own items.");
            // Headline words for the current release; move these with each release (the identity script owns the literals).
            foreach (string headline in new[] { "qol v", "reconnect", "faces" })
            {
                Assert.That(whatsNew.ToLowerInvariant(), Does.Contain(headline), $"{version} headline missing from What's new: {headline}");
            }

            Assert.That(whatsNew.IndexOf($"**New in {version}:**", StringComparison.Ordinal), Is.LessThan(whatsNew.IndexOf("**Also recent", StringComparison.Ordinal)), "Current items come before the older ones.");
        });
    }

    [Test]
    public void Player_facing_text_keeps_engineering_words_out()
    {
        string readme = ReadRootFile("README.md");
        string changelog = ReadRootFile("CHANGELOG.md");
        string top = Slice(changelog.Replace("\r\n", "\n"), "## 0.2.7", "## 0.2.6");
        string[] banned = { "PurrNet", "Harmony", "IMGUI", "RPC", "generation", "identity-coverage", "identity coverage", "roster-alignment", "lobby-metadata", "Mono heap", "atlas", "sprite", "sha256", "SHA-256", "worktree", "UAT", "candidate" };

        Assert.Multiple(() =>
        {
            foreach (string word in banned)
            {
                Assert.That(readme.IndexOf(word, StringComparison.OrdinalIgnoreCase), Is.LessThan(0), $"README leaks an engineering word: {word}");
                Assert.That(top.IndexOf(word, StringComparison.OrdinalIgnoreCase), Is.LessThan(0), $"Newest CHANGELOG section leaks an engineering word: {word}");
            }
        });
    }

    // Blue (2026-10-23): the public icon band says "EARLY ACCESS - BETA" with no version until an official 1.0.0,
    // because a version baked into a picture goes stale the moment the version bumps (it read 0.2.5 at 0.2.7).
    [Test]
    public void Public_icon_band_carries_no_version_until_a_1_0_0_release()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string script = File.ReadAllText(Path.Combine(Directory.GetParent(root)!.FullName, "tools", "New-BlueSagePackageIcons.ps1"));
        string qolSpec = Slice(script, "Folder = 'BlueSage_QoL_Tweaks_Beta'", "Folder = 'BlueSagePatched_PlayerLimitLift'");

        Assert.Multiple(() =>
        {
            Assert.That(qolSpec, Does.Contain("Band = 'EARLY ACCESS - BETA'"));
            Assert.That(qolSpec, Does.Not.Contain("{0}"), "No version placeholder in the public band.");
        });
    }

    private static string Capture(string text, string pattern)
    {
        Match match = Regex.Match(text, pattern);
        Assert.That(match.Success, Is.True, $"Pattern not found in Plugin.cs: {pattern}");
        return match.Groups[1].Value;
    }

    private static string ReadRootFile(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, relative));
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, StringComparison.Ordinal);
        return e < 0 ? source.Substring(s) : source.Substring(s, e - s);
    }
}
