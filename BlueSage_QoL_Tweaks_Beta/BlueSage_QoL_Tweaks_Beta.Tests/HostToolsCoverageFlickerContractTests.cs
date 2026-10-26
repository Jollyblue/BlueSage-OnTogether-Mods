using System;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4b (Blue, live UAT 2026-10-20): "Host Tools flickers every 2-3 s in the Player matching section". The
// section read GetCachedIdentityResyncCoverage, which reports "the cached identity snapshot is stale" the moment
// the 2 s roster snapshot lifetime passes, while the roster box below it re-captures a frame later. So every
// 2 s one frame showed the stale/fail-closed help text, then the fresh one. The menu now reads a refreshing
// coverage that re-captures (cache allowed) before judging staleness; the cached reader stays for the console
// and resync controllers, which want fail-closed semantics.
public sealed class HostToolsCoverageFlickerContractTests
{
    [Test]
    public void Host_tools_player_matching_reads_refreshing_coverage_not_the_stale_cache()
    {
        string menu = ReadSource("src", "QolMenuWindow.cs");
        string evidence = ReadSource("src", "PlayerIdentityEvidenceController.cs");
        string draw = MethodBody(menu, "private void DrawIdentityResyncControls(");
        string fresh = MethodBody(evidence, "internal static IdentityResyncCoverage GetIdentityResyncCoverage()");

        Assert.Multiple(() =>
        {
            Assert.That(draw, Does.Contain("PlayerIdentityEvidenceController.GetIdentityResyncCoverage()"));
            Assert.That(draw, Does.Not.Contain("GetCachedIdentityResyncCoverage()"), "the draw must never see the one-frame stale state");
            Assert.That(fresh, Does.Contain("CaptureRosterSnapshot(cacheAllowed: true)"), "re-capture within the 2 s lifetime rule before judging");
            Assert.That(fresh, Does.Contain("Plugin.CanUseLobbySafety(out _)"), "same gate as GetVerifiedRoster");
            Assert.That(fresh, Does.Not.Contain("is stale"), "a reader that just refreshed cannot be stale");
            Assert.That(LargeLobbyRefreshPolicy.RosterSnapshotLifetime, Is.EqualTo(TimeSpan.FromSeconds(2)), "the lifetime itself is unchanged; the fix is where the menu reads");
        });
    }

    private static string MethodBody(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
        int open = source.IndexOf('{', source.IndexOf(')', start));
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] == '}' && --depth == 0)
            {
                return source.Substring(open, index - open + 1);
            }
        }

        throw new InvalidOperationException("Unbalanced method body for " + signature);
    }

    private static string ReadSource(params string[] pathParts)
    {
        string path = FindProjectRoot();
        foreach (string part in pathParts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
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
