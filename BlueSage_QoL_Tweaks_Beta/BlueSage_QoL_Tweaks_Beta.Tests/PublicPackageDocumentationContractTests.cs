using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicPackageDocumentationContractTests
{
    [Test]
    public void Public_opening_explains_the_Work_Relax_flow_in_casual_scan_order()
    {
        string root = FindProjectRoot();
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string opening = readme.Substring(0, Math.Min(2000, readme.Length));
        string[] headings =
        {
            "## What's new",
            "## Quick Start",
            "## Your first lobby",
            "## Work / Relax",
            "## Feature guide",
            "## Keys",
            "## Commands",
            "## Friendly FAQ"
        };

        Assert.Multiple(() =>
        {
            int prior = -1;
            foreach (string heading in headings)
            {
                int current = readme.IndexOf(heading, StringComparison.Ordinal);
                Assert.That(current, Is.GreaterThan(prior), $"{heading} must appear in the casual scan order.");
                prior = current;
            }

            Assert.That(opening, Does.Contain("Version 0.2.5 - October 18, 2026").IgnoreCase);
            Assert.That(opening, Does.Contain("beside FPS / PING / RAM").IgnoreCase);
            Assert.That(opening, Does.Contain("game's statistics stay unchanged").IgnoreCase);
            Assert.That(opening, Does.Contain("BlueSage Report").And.Contain("Work and Relax"));
            Assert.That(opening, Does.Contain("correct").And.Contain("history"));
            Assert.That(opening, Does.Contain("commands are optional").IgnoreCase);
            Assert.That(opening, Does.Contain("With Performance Overlay on").IgnoreCase);
            Assert.That(opening, Does.Contain("commands are optional shortcuts").IgnoreCase);
            Assert.That(opening, Does.Not.Contain("always available").IgnoreCase);
            Assert.That(opening, Does.Contain("WEBSITE").And.Contain("https://bluesagetech.net/ (Coming Soon)"));
        });
    }

    // 2026-10-18 (Blue): one release note per version, grouped by what players care about, detailed and
    // forthcoming about every feature without technical jargon. The 0.2.5 entry used to read like a
    // build diary: Fishing Aid twice with different defaults, toasts added then moved later in the list.
    [Test]
    public void The_0_2_5_entry_is_one_grouped_plain_language_release_note()
    {
        string root = FindProjectRoot();
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md")).Replace("\r\n", "\n");
        int start = changelog.IndexOf("## 0.2.5 — 2026-10-18", StringComparison.Ordinal);
        int end = changelog.IndexOf("## 0.2.4 — 2026-07-30", StringComparison.Ordinal);
        string entry = start >= 0 && end > start ? changelog.Substring(start, end - start) : string.Empty;
        string[] groups =
        {
            "### Open things your way", "### On-screen toasts", "### Fishing Aid", "### For hosts",
            "### Work / Relax and BlueSage Report", "### Improved", "### Retired",
        };

        Assert.Multiple(() =>
        {
            Assert.That(entry, Is.Not.Empty);
            foreach (string group in groups)
            {
                Assert.That(entry, Does.Contain(group), group);
            }

            Assert.That(entry, Does.Contain("**QoL-Menu**").And.Contain("**Styler-Menu**"));
            Assert.That(entry, Does.Contain("Don't show this again"));
            Assert.That(entry, Does.Contain("each time you take the rod out"));
            Assert.That(entry, Does.Contain("0.7 s").And.Contain("3.5 s"));
            Assert.That(entry, Does.Contain("/motd").And.Contain("Host Tools"));
            Assert.That(entry, Does.Contain("Steam ID"));
            Assert.That(CountOf(entry, "Adds an accessibility Fishing Aid"), Is.Zero, "Old diary wording is gone.");
            Assert.That(CountOf(entry, "### "), Is.EqualTo(groups.Length), "No duplicate or stray groups.");
            Assert.That(entry, Does.Not.Contain("Harmony").And.Not.Contain("IMGUI").And.Not.Contain("PurrNet").And.Not.Contain("RPC"),
                "Players read this; no engine jargon.");
        });
    }

    [Test]
    public void Readme_names_the_overlay_menu_buttons_and_where_hosts_set_the_welcome_message()
    {
        string readme = File.ReadAllText(Path.Combine(FindProjectRoot(), "README.md"));

        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Contain("**QoL-Menu**").And.Contain("**Styler-Menu**"));
            Assert.That(readme, Does.Contain("Host Tools").And.Contain("/motd"));
            Assert.That(readme, Does.Contain("2–15 seconds"));
            Assert.That(readme, Does.Contain("all around you"));
        });
    }

    [Test]
    public void Thunderstore_description_names_the_0_2_5_headline_features_within_its_limit()
    {
        string manifest = File.ReadAllText(Path.Combine(FindProjectRoot(), "manifest.json"));
        string description = System.Text.Json.JsonDocument.Parse(manifest).RootElement.GetProperty("description").GetString() ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(description.Length, Is.LessThanOrEqualTo(250), "Thunderstore's own limit.");
            Assert.That(description, Does.Contain("Fishing Aid").And.Contain("hotkeys").And.Contain("welcome messages"));
            Assert.That(description, Does.Not.Contain("private").IgnoreCase, "The public package boundary refuses that word.");
        });
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // Blue, 2026-10-18: the published history lost its depth at 0.2.4 (commit 44c7ac1 condensed it; the
    // live 0.2.4 page shows only 0.2.4). It is restored from the notes Thunderstore still serves for
    // 0.0.6-0.2.3 plus Blue's recovered July notes, cleaned of internal words and personal names.
    [Test]
    public void Restored_history_keeps_the_published_depth_without_private_or_personal_details()
    {
        string changelog = File.ReadAllText(Path.Combine(FindProjectRoot(), "CHANGELOG.md")).Replace("\r\n", "\n");
        int historyStart = changelog.IndexOf("## 0.2.4 — 2026-07-30", StringComparison.Ordinal);
        string history = historyStart >= 0 ? changelog.Substring(historyStart) : string.Empty;
        string[] publishedDepth =
        {
            "five-argument", "fail open", "single-flight", "### Reconnect Recovery", "### Identity, Styling, and Readability",
            "Clone Shield", "### Chat Layout Upgrade", "### Highlights since 0.0.10", "/chatvanilla", "Midnight Dock",
            "First Early Access release of BlueSage QoL Tweaks - Beta.", "Chalkboard", "/idsync", "Performance Overlay",
        };
        string[] neverPublic =
        {
            "Steward", "Inspector", "companion extension", "private companion", "authorized companion", "+2026",
            "release candidate", "Rowan", "Nell", "Marbles", "bloo_bamboo", "TinySav", "Ko-fi supporter",
        };

        Assert.Multiple(() =>
        {
            Assert.That(history.Length, Is.GreaterThan(40000), "The restored history carries the published depth again.");
            foreach (string phrase in publishedDepth)
            {
                Assert.That(history, Does.Contain(phrase), phrase);
            }

            foreach (string word in neverPublic)
            {
                Assert.That(changelog, Does.Not.Contain(word), word);
            }

            Assert.That(changelog, Does.Contain("- Updated QoL and PLL with love as Blue + Sage = BlueSage ♥"), "The live 0.2.4 entry is kept verbatim.");
        });
    }

    [Test]
    public void Public_changelog_preserves_every_prior_version_heading()
    {
        string root = FindProjectRoot();
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        string[] priorHeadings =
        {
            "## 0.2.4 — 2026-07-30", "## 0.2.3 — 2026-07-23", "## 0.2.2 — 2026-07-17",
            "## 0.2.1 — 2026-07-17", "## 0.2.0 — 2026-07-15", "## 0.1.9 — 2026-07-11",
            "## 0.1.8 — 2026-07-11", "## 0.1.7 — 2026-07-06", "## 0.1.6 — 2026-07-06",
            "## 0.1.5 — 2026-07-06", "## 0.1.4 — 2026-07-06", "## 0.1.3 — 2026-07-05",
            "## 0.1.2 — 2026-07-04", "## 0.1.1 — 2026-07-03", "## 0.1.0 — 2026-07-03",
            "## 0.0.10 — 2026-07-02", "## 0.0.9 — 2026-07-02", "## 0.0.8 — 2026-06-29",
            "## 0.0.7 — 2026-06-28", "## 0.0.6 — 2026-06-28", "## 0.0.5 — 2026-06-28",
            "## 0.0.4 — 2026-06-28", "## 0.0.3 — 2026-06-18", "## 0.0.2 — 2026-06-18",
            "## 0.0.1 — 2026-06-18"
        };

        Assert.Multiple(() =>
        {
            foreach (string heading in priorHeadings)
            {
                Assert.That(changelog, Does.Contain(heading), $"Missing historical heading: {heading}");
            }
        });

    }

    [Test]
    public void Public_package_docs_are_concise_consumer_guides_without_private_candidate_history()
    {
        string root = FindProjectRoot();
        string readmePath = Path.Combine(root, "README.md");
        string changelogPath = Path.Combine(root, "CHANGELOG.md");
        string readme = File.ReadAllText(readmePath);
        string changelog = File.ReadAllText(changelogPath);

        Assert.Multiple(() =>
        {
            // 2026-10-18: raised again (Blue: "the byte caps are moot, we make them increase"). The caps
            // only guard against runaway walls of text; completeness and plain language come first.
            Assert.That(new FileInfo(readmePath).Length, Is.LessThanOrEqualTo(40000));
            Assert.That(new FileInfo(changelogPath).Length, Is.LessThanOrEqualTo(120000));
            Assert.That(readme, Does.Contain("Quick Start"));
            Assert.That(readme, Does.Contain("Your first lobby"));
            Assert.That(readme, Does.Contain("Friendly FAQ"));
            Assert.That(readme, Does.Contain("Start with the defaults"));
            Assert.That(readme, Does.Contain("one setting at a time"));
            Assert.That(readme, Does.Contain("Compatibility"));
            Assert.That(readme, Does.Contain("Privacy"));
            Assert.That(readme, Does.Contain("Troubleshooting"));
            Assert.That(readme, Does.Contain("Support"));
            Assert.That(readme, Does.Contain("host-only").IgnoreCase);
            Assert.That(readme, Does.Contain("Made by Blue + Sage = BlueSage with superpowered neurodivergence ♥"));
            Assert.That(readme, Does.Not.Contain("Neonicalle").And.Not.Contain("Bochi").And.Not.Contain("partner in crime"));
            Assert.That(readme, Does.Contain("Focus Pulse"));
            Assert.That(readme, Does.Contain("names, status, and Spoons tag").IgnoreCase);
            Assert.That(readme, Does.Contain("[U]").And.Contain("Compatible QoL User"));
            Assert.That(readme, Does.Contain("never grants host or moderation powers"));
            Assert.That(readme, Does.Contain("first open after someone joins").IgnoreCase);
            Assert.That(readme, Does.Contain("QoL menu").And.Contain("/assetsweep").And.Contain("F8"));
            Assert.That(readme, Does.Contain("remain available whenever you want to run it yourself").IgnoreCase);
            Assert.That(readme, Does.Contain("leaves the game's own cleanup alone").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("fallback only when the game does not own cleanup").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("developer beta").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("advanced testers").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("tested at 1453").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("migrate once").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("build 24389833").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("sha256").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("candidate").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("runtime-green lane").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("loader-eligible").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("UAT").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("runtime promotion").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("behavioral and rollback baseline").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("PurrNet").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("7BF6BA50FAC5"));
            Assert.That(readme, Does.Not.Contain("BlueExclusive"));
            Assert.That(readme, Does.Not.Contain("SteamID64"));
            Assert.That(readme, Does.Not.Contain("native PlayerID"));
            Assert.That(changelog, Does.Not.Contain("failed UAT"));
            Assert.That(changelog, Does.Not.Contain("heartbeat").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("publication").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("cadence").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("7BF6BA50FAC5"));
            Assert.That(changelog, Does.Not.Contain("BlueExclusive"));
            Assert.That(changelog, Does.Not.Contain("UAT").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("candidate").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("build 24389833").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("advanced testers").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("corrected July 25 UTC dates").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("individual support amounts").IgnoreCase);
            Assert.That(changelog, Does.Not.Contain("observation modes").IgnoreCase);
            Assert.That(changelog, Does.Contain("Focus Pulse"));
            Assert.That(changelog.Replace("\r\n", "\n"), Does.StartWith("# Changelog\n\nEvery previous public release is kept below so the complete BlueSage history stays available on Thunderstore.\n\n## 0.2.5 — 2026-10-18"));
            Assert.That(changelog, Does.Contain("## 0.2.4 — 2026-07-30"));
            Assert.That(changelog, Does.Contain("## 0.2.3 — 2026-07-23"));
            Assert.That(changelog, Does.Contain("## 0.1.0 — 2026-07-03"));
            Assert.That(changelog, Does.Contain("## 0.0.1 — 2026-06-18"));
            Assert.That(changelog, Does.Contain("Updated QoL and PLL with love as Blue + Sage = BlueSage ♥"));
            Assert.That(changelog, Does.Contain("names, status, and Spoons tag").IgnoreCase);
            Assert.That(changelog, Does.Contain("QoL menu").And.Contain("sweep slash commands").And.Contain("F8"));
        });
    }

    // The chat welcome and the welcome overlay both send players to the README FAQ,
    // so every hotkey, rebind path, toast, and overlay surface they mention must be answered there.
    [Test]
    public void Readme_faq_answers_what_the_welcome_message_and_overlay_point_players_to()
    {
        string root = FindProjectRoot();
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        int faqStart = readme.IndexOf("## Friendly FAQ", StringComparison.Ordinal);
        int faqEnd = readme.IndexOf("## Compatibility", StringComparison.Ordinal);
        Assert.That(faqStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(faqEnd, Is.GreaterThan(faqStart));
        string faq = readme.Substring(faqStart, faqEnd - faqStart);

        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Contain("`Home`").And.Contain("`Insert`").And.Contain("`End`").And.Contain("`F8`"));
            Assert.That(faq, Does.Contain("/rebind"));
            Assert.That(faq, Does.Contain("Hotkeys section"));
            Assert.That(faq, Does.Contain("/rebind reset"));
            Assert.That(faq, Does.Contain("/keys"));
            Assert.That(faq, Does.Contain("/welcome"));
            Assert.That(faq, Does.Contain("/faq"));
            Assert.That(faq, Does.Contain("every time you join a lobby").IgnoreCase);
            Assert.That(faq, Does.Contain("Don't show this again"));
            Assert.That(faq, Does.Contain("toast").IgnoreCase);
            Assert.That(faq, Does.Contain("Toast Pop-ups"));
            Assert.That(faq, Does.Contain("Welcome Overlay"));
            Assert.That(faq, Does.Contain("Tab"));
        });
    }

    [Test]
    public void Public_docs_explain_Host_Tools_and_large_lobby_chat_in_plain_language()
    {
        string root = FindProjectRoot();
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        string playerLimitRoot = Path.GetFullPath(
            Path.Combine(root, "..", "BlueSagePatched_PlayerLimitLift"));
        string playerLimitReadme = File.ReadAllText(
            Path.Combine(playerLimitRoot, "README.md"));
        string playerLimitChangelog = File.ReadAllText(
            Path.Combine(playerLimitRoot, "CHANGELOG.md"));

        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Contain("Host Tools"));
            Assert.That(readme, Does.Contain("Refresh Player List").And.Contain("Recheck Player Matches"));
            Assert.That(readme, Does.Contain("Checking this lobby").And.Contain("rejoin").And.Contain("recreate the lobby"));
            Assert.That(readme, Does.Contain("Check game moderation buttons").And.Contain("read-only").IgnoreCase);
            Assert.That(readme, Does.Contain("In-game large-lobby chat").And.Contain("not a Discord bridge").IgnoreCase);
            Assert.That(readme, Does.Not.Contain("24/7 Degraded"));
            Assert.That(readme, Does.Not.Contain("Verified roster is not converged"));
            Assert.That(readme, Does.Not.Contain("Audit Native UI"));
            Assert.That(changelog, Does.Contain("Host Tools").And.Contain("step-by-step player-matching repair path"));
            Assert.That(playerLimitReadme, Does.Contain("in-game large-lobby chat").IgnoreCase);
            Assert.That(playerLimitReadme, Does.Contain("not a Discord bridge").IgnoreCase);
            Assert.That(playerLimitChangelog, Does.Contain("in-game large-lobby chat").IgnoreCase);
            Assert.That(playerLimitChangelog, Does.Contain("Discord bridge").IgnoreCase);
        });
    }

    [Test]
    public void Concise_public_docs_satisfy_the_non_packaging_release_verifier()
    {
        string root = FindProjectRoot();
        string verifierPath = Path.Combine(root, "tools", "Verify-BlueSageQoLTweaks.ps1");

        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            WorkingDirectory = root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(verifierPath);
        process.StartInfo.ArgumentList.Add("-SkipTests");
        process.StartInfo.ArgumentList.Add("-SkipPackage");

        Assert.That(process.Start(), Is.True, "Could not start the QoL release verifier.");
        // Bounded on purpose (2026-10-17). Stalls seen that day were indefinite (the "~5 min" was
        // our own command timeout). Known contributors: (1) reading stdout to EOF before stderr
        // deadlocks when a failing verifier fills the stderr pipe; (2) without -NonInteractive and a
        // closed stdin, anything in the child that prompts waits forever on testhost's stdin.
        // Root cause of every stall is NOT fully proven, so this test can never hang the suite.
        process.StandardInput.Close();
        System.Threading.Tasks.Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        System.Threading.Tasks.Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(90_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("QoL release verifier did not finish within 90s and was killed. Partial stdout:"
                + Environment.NewLine + (standardOutputTask.Wait(2_000) ? standardOutputTask.Result : "(unavailable)"));
        }

        string standardOutput = standardOutputTask.Result;
        string standardError = standardErrorTask.Result;

        Assert.That(
            process.ExitCode,
            Is.EqualTo(0),
            $"QoL non-packaging release verifier failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
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
