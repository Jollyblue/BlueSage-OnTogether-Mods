using System.Reflection;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityCoverageLogPolicyTests
{
    [Test]
    public void Coverage_log_state_ignores_count_churn_but_keeps_safety_transitions_visible()
    {
        Type? policy = typeof(LargeLobbyRefreshPolicy).Assembly.GetType(
            "BlueSage.QoLTweaks.Core.IdentityCoverageLogPolicy");
        Assert.That(policy, Is.Not.Null,
            "Large-lobby roster counts need a dedicated log-state policy instead of emitting a warning for every harmless count wobble.");

        MethodInfo? buildStateKey = policy?.GetMethod(
            "BuildStateKey",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(buildStateKey, Is.Not.Null);
        if (buildStateKey == null)
        {
            return;
        }

        string incompleteAtFifty = Invoke(buildStateKey,
            9001UL, false, "", 47, 51, 50, 50, 50, 1, 0, 0);
        string incompleteAtSixty = Invoke(buildStateKey,
            9001UL, false, "", 56, 62, 60, 61, 60, 2, 1, 1);
        string complete = Invoke(buildStateKey,
            9001UL, true, "", 60, 60, 60, 60, 60, 0, 0, 0);
        string fatal = Invoke(buildStateKey,
            9001UL, false, "native roster unavailable", 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(incompleteAtSixty, Is.EqualTo(incompleteAtFifty),
                "Changing transient counts must not create a new warning state every roster refresh.");
            Assert.That(complete, Is.Not.EqualTo(incompleteAtFifty),
                "Fail-closed to ready remains a visible transition.");
            Assert.That(fatal, Is.Not.EqualTo(incompleteAtFifty),
                "A new capture failure remains a visible transition.");
        });
    }

    // Round 4 Stage D, log diet #6: the controller used to append confirmed/transport/attested counts to the policy's
    // key, which put the count churn back (20+ lines per big-lobby join wave). The log key now adds only two
    // booleans (transport unavailable, any Steam-ID mismatch); the fine, count-bearing key drives the journal status
    // row instead, so the counts still land somewhere, just not in LogOutput every refresh.
    [Test]
    public void Coverage_warning_fires_on_state_change_only_and_the_counts_go_to_the_journal()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string controller = File.ReadAllText(Path.Combine(root, "src", "PlayerIdentityEvidenceController.cs")).Replace("\r\n", "\n");
        int start = controller.IndexOf("private static void LogCoverageTransition(RosterSnapshot snapshot)", StringComparison.Ordinal);
        string body = controller.Substring(start, controller.IndexOf("\n    }\n", start, StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("string logKey = IdentityCoverageLogPolicy.BuildStateKey("));
            Assert.That(body, Does.Contain("\"|\" + (snapshot.SteamIdMismatchCount > 0) +"));
            Assert.That(body, Does.Contain("\"|\" + snapshot.TransportUnavailable;"));
            Assert.That(body, Does.Contain("string journalKey = logKey +"), "The journal row keys on the counts.");
            int journalKeyStart = body.IndexOf("string journalKey = logKey +", StringComparison.Ordinal);
            Assert.That(body.IndexOf("\"|\" + snapshot.IdentityConfirmed.Length +", StringComparison.Ordinal), Is.GreaterThan(journalKeyStart), "Counts belong to the journal key, not the log key.");
            Assert.That(body.IndexOf("\"|\" + snapshot.TransportVerifiedCount +", StringComparison.Ordinal), Is.GreaterThan(journalKeyStart));
            // Round 5 F: the log-key early return is the hysteresis policy (a flip must hold 60 s; fail-closed reminds hourly).
            Assert.That(body, Does.Contain("StableStateLogPolicy.Observe("));
            Assert.That(body, Does.Contain("remindWhileHeld: !snapshot.CoverageComplete"));
            Assert.That(body, Does.Not.Contain("_lastCoverageTransitionKey"));
            Assert.That(body.IndexOf("HostJournalController.RecordCoverage(", StringComparison.Ordinal), Is.LessThan(body.IndexOf("StableStateLogPolicy.Observe(", StringComparison.Ordinal)), "The journal row is written before the log hysteresis early return.");
        });
    }

    private static string Invoke(MethodInfo method, params object[] arguments) =>
        (string)(method.Invoke(null, arguments) ?? string.Empty);
}
