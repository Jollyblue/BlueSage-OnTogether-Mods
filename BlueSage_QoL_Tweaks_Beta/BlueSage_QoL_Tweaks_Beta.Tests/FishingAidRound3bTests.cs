using System;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 3b (Blue, live UAT 2026-10-18): the Fishing Aid toasts only showed the first time, because the
// "already told you" flags were cleared only when the Fishing Aid toggle changed. Putting the rod away
// ends a fishing session; taking it out again starts a fresh one that announces itself again.
public sealed class FishingAidRound3bTests
{
    [Test]
    public void Only_the_out_to_away_edge_ends_a_fishing_session()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.IsRodPutAway(rodWasOut: true, rodOut: false), Is.True);
            Assert.That(FishingAidPolicy.IsRodPutAway(rodWasOut: true, rodOut: true), Is.False, "Still fishing.");
            Assert.That(FishingAidPolicy.IsRodPutAway(rodWasOut: false, rodOut: false), Is.False, "Already away: reset once, not every frame.");
            Assert.That(FishingAidPolicy.IsRodPutAway(rodWasOut: false, rodOut: true), Is.False, "Taking the rod out is not a put-away.");
        });
    }

    [Test]
    public void Putting_the_rod_away_clears_every_announcement_and_the_cast_in_flight()
    {
        string patches = ReadSource(Path.Combine("Patches", "FishingAidPatches.cs"));
        string session = Slice(patches, "private static void ResetSession()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(session, Is.Not.Empty, "A session reset exists.");
            foreach (string flag in new[]
            {
                "_announcedTakeover = false;", "_announcedNoBait = false;", "_announcedNoWater = false;",
                "_announcedRefused = false;", "_lastAttemptFoundNoWater = false;", "_refusedCasts = 0;",
                "_castAttemptPending = false;", "_aidCasting = false;", "_idleSince = -1f;",
                "_biteSince = -1f;", "_biteAnswered = false;",
            })
            {
                Assert.That(session, Does.Contain(flag), flag);
            }

            // The hand-cast spot survives a put-away; walking away still forgets it (ShouldForgetCast).
            Assert.That(session, Does.Not.Contain("_hasRecordedCast"));
        });
    }

    [Test]
    public void The_loop_resets_the_session_on_the_put_away_edge_and_the_toggle_still_resets_everything()
    {
        string patches = ReadSource(Path.Combine("Patches", "FishingAidPatches.cs"));
        string loop = Slice(patches, "internal static void DriveFishingLoop(", "switch (manager.FishingState)");
        string reset = Slice(patches, "internal static void Reset()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(loop, Does.Contain("if (FishingAidPolicy.IsRodPutAway(_rodWasOut, rodOut))"));
            Assert.That(loop, Does.Contain("ResetSession();"));
            Assert.That(loop, Does.Contain("_rodWasOut = rodOut;"));
            Assert.That(reset, Does.Contain("ResetSession();"));
            Assert.That(reset, Does.Contain("_hasRecordedCast = false;"));
            Assert.That(reset, Does.Contain("_rodWasOut = false;"));
        });
    }

    // Blue, UAT 2026-10-18: the toasts on every equip and recast are wanted, the chat lines are spam.
    // Fishing Aid notices are toast-only.
    [Test]
    public void Fishing_Aid_notices_are_toasts_only_never_chat_lines()
    {
        string patches = ReadSource(Path.Combine("Patches", "FishingAidPatches.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(patches, Does.Not.Contain("AddLocalNotificationWithToast("), "No chat line from Fishing Aid.");
            Assert.That(patches, Does.Not.Contain("AddLocalNotification("));
            foreach (string notice in new[] { "Fishing Aid is fishing for you.", "Fishing Aid is waiting: no bait equipped.", "Fishing Aid can't cast right now", "Fishing Aid: no water found nearby." })
            {
                Assert.That(patches, Does.Contain("Plugin.Instance?.ShowToast(\"" + notice), notice);
            }
        });
    }

    private static string ReadSource(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", relative)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
