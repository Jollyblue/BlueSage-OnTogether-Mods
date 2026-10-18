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

    private static string Invoke(MethodInfo method, params object[] arguments) =>
        (string)(method.Invoke(null, arguments) ?? string.Empty);
}
