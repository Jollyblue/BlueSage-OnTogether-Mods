using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatAccessibilityScalePolicyTests
{
    [TestCase(1, 75)]
    [TestCase(75, 75)]
    [TestCase(100, 100)]
    [TestCase(150, 150)]
    [TestCase(200, 200)]
    [TestCase(250, 200)]
    public void ClampScalePercent_KeepsAccessibilityScaleInStableRange(int configured, int expected)
    {
        Assert.That(ChatAccessibilityScalePolicy.ClampScalePercent(configured), Is.EqualTo(expected));
    }

    [Test]
    public void NormalizeScale_UsesClampedPercent()
    {
        Assert.That(ChatAccessibilityScalePolicy.NormalizeScale(100), Is.EqualTo(1.00f).Within(0.001f));
        Assert.That(ChatAccessibilityScalePolicy.NormalizeScale(150), Is.EqualTo(1.50f).Within(0.001f));
        Assert.That(ChatAccessibilityScalePolicy.NormalizeScale(250), Is.EqualTo(2.00f).Within(0.001f));
    }

    [TestCase("125", true, 125)]
    [TestCase("125%", true, 125)]
    [TestCase("1.25", true, 125)]
    [TestCase("2.5", true, 200)]
    [TestCase("reset", true, 100)]
    [TestCase("status", false, 100)]
    [TestCase("huge", false, 100)]
    public void TryParseScaleToken_AcceptsPercentDecimalAndReset(string token, bool expectedSuccess, int expectedPercent)
    {
        bool success = ChatAccessibilityScalePolicy.TryParseScaleToken(token, out int percent);

        Assert.That(success, Is.EqualTo(expectedSuccess));
        Assert.That(percent, Is.EqualTo(expectedPercent));
    }

    [Test]
    public void TryCalculateScreenClampDelta_NudgesScaledPanelBackInsideScreen()
    {
        bool changed = ChatAccessibilityScalePolicy.TryCalculateScreenClampDelta(
            left: -80f,
            right: 420f,
            bottom: 20f,
            top: 520f,
            screenWidth: 1920f,
            screenHeight: 1080f,
            margin: 8f,
            out float deltaX,
            out float deltaY);

        Assert.That(changed, Is.True);
        Assert.That(deltaX, Is.EqualTo(88f).Within(0.001f));
        Assert.That(deltaY, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void TryCalculateScreenClampDelta_PrefersKeepingDragHandleVisibleWhenPanelIsHuge()
    {
        bool changed = ChatAccessibilityScalePolicy.TryCalculateScreenClampDelta(
            left: -160f,
            right: 2300f,
            bottom: -70f,
            top: 1220f,
            screenWidth: 1920f,
            screenHeight: 1080f,
            margin: 8f,
            out float deltaX,
            out float deltaY);

        Assert.That(changed, Is.True);
        Assert.That(deltaX, Is.EqualTo(168f).Within(0.001f));
        Assert.That(deltaY, Is.EqualTo(78f).Within(0.001f));
    }
}
