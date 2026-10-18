using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatHistoryLimitPolicyTests
{
    [TestCase(25, 100, 100)]
    [TestCase(50, 50, 50)]
    [TestCase(125, 100, 125)]
    [TestCase(250, 250, 250)]
    [TestCase(300, 100, 300)]
    public void ApplyConfiguredRows_RaisesLowerLimitsWithoutLoweringFutureOrOtherModLimits(int originalLimit, int configured, int expected)
    {
        Assert.That(ChatHistoryLimitPolicy.ApplyConfiguredRows(originalLimit, configured), Is.EqualTo(expected));
    }

    [TestCase(0, 25)]
    [TestCase(100, 100)]
    [TestCase(999, 250)]
    public void ConfiguredRows_AreClampedForPredictableUiCost(int value, int expected)
    {
        Assert.That(ChatHistoryLimitPolicy.ClampRows(value), Is.EqualTo(expected));
        Assert.That(ChatHistoryLimitPolicy.DefaultRows, Is.EqualTo(150));
    }
}
