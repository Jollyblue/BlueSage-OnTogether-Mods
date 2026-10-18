using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatisticsPulsePresentationPolicyTests
{
    [TestCase(2, 2, "🔥 2  •  Best 2")]
    [TestCase(0, 5, "🔥 0  •  Best 5")]
    [TestCase(-1, -3, "🔥 0  •  Best 0")]
    public void Streak_value_is_lively_compact_and_fail_open(int current, int longest, string expected)
    {
        Assert.That(StatisticsPulsePresentationPolicy.BuildStreakValue(current, longest), Is.EqualTo(expected));
    }
}
