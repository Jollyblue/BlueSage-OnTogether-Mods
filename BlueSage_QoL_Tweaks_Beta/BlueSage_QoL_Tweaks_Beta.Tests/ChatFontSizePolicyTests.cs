using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatFontSizePolicyTests
{
    [TestCase(40, 75)]
    [TestCase(75, 75)]
    [TestCase(100, 100)]
    [TestCase(150, 150)]
    [TestCase(240, 200)]
    public void ClampFontSizePercent_KeepsReadableRange(int configured, int expected)
    {
        Assert.That(ChatFontSizePolicy.ClampFontSizePercent(configured), Is.EqualTo(expected));
    }

    [TestCase("reset", 100)]
    [TestCase("default", 100)]
    [TestCase("off", 100)]
    [TestCase("on", 125)]
    [TestCase("1.5", 150)]
    [TestCase("150%", 150)]
    [TestCase("240", 200)]
    public void TryParseFontSizeToken_AcceptsFriendlyInputs(string token, int expected)
    {
        bool success = ChatFontSizePolicy.TryParseFontSizeToken(token, out int percent);

        Assert.That(success, Is.True);
        Assert.That(percent, Is.EqualTo(expected));
    }
}
