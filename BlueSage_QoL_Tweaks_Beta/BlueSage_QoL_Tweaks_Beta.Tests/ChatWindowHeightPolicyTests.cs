using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatWindowHeightPolicyTests
{
    [TestCase(1, 100)]
    [TestCase(100, 100)]
    [TestCase(150, 150)]
    [TestCase(200, 200)]
    [TestCase(220, 200)]
    [TestCase(300, 200)]
    public void ClampHeightPercent_KeepsChatStretchInStableRange(int configured, int expected)
    {
        Assert.That(ChatWindowHeightPolicy.ClampHeightPercent(configured), Is.EqualTo(expected));
    }

    [Test]
    public void NormalizeHeight_UsesClampedPercent()
    {
        Assert.That(ChatWindowHeightPolicy.NormalizeHeight(100), Is.EqualTo(1.00f).Within(0.001f));
        Assert.That(ChatWindowHeightPolicy.NormalizeHeight(150), Is.EqualTo(1.50f).Within(0.001f));
        Assert.That(ChatWindowHeightPolicy.NormalizeHeight(300), Is.EqualTo(2.00f).Within(0.001f));
    }

    [TestCase("150", true, 150)]
    [TestCase("150%", true, 150)]
    [TestCase("1.5", true, 150)]
    [TestCase("3.0", true, 200)]
    [TestCase("reset", true, 100)]
    [TestCase("off", true, 100)]
    [TestCase("disable", true, 100)]
    [TestCase("on", true, 125)]
    [TestCase("enable", true, 125)]
    [TestCase("status", false, 100)]
    [TestCase("tall", false, 100)]
    public void TryParseHeightToken_AcceptsPercentDecimalAndReset(string token, bool expectedSuccess, int expectedPercent)
    {
        bool success = ChatWindowHeightPolicy.TryParseHeightToken(token, out int percent);

        Assert.That(success, Is.EqualTo(expectedSuccess));
        Assert.That(percent, Is.EqualTo(expectedPercent));
    }

    [TestCase(60f, 240f, 360f, 0.5f, 120f)]
    [TestCase(60f, 240f, 360f, 1.0f, 180f)]
    [TestCase(60f, 240f, 360f, 0.0f, 60f)]
    public void CalculateAnchoredYForBottomLockedStretch_ExpandsUpFromOriginalBottom(float originalY, float originalHeight, float newHeight, float pivotY, float expectedY)
    {
        float actual = ChatWindowHeightPolicy.CalculateAnchoredYForBottomLockedStretch(originalY, originalHeight, newHeight, pivotY);

        Assert.That(actual, Is.EqualTo(expectedY).Within(0.001f));
    }
}
