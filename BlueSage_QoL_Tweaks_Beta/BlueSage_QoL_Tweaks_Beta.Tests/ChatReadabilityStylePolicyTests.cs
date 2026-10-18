using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatReadabilityStylePolicyTests
{
    [TestCase(1, 25)]
    [TestCase(25, 25)]
    [TestCase(70, 70)]
    [TestCase(100, 100)]
    [TestCase(250, 100)]
    public void ClampOutlineIntensityPercent_KeepsCommunitySettingInReadableRange(int configured, int expected)
    {
        Assert.That(ChatReadabilityStylePolicy.ClampOutlineIntensityPercent(configured), Is.EqualTo(expected));
    }

    [Test]
    public void NormalizeOutlineIntensity_UsesClampedPercent()
    {
        Assert.That(ChatReadabilityStylePolicy.NormalizeOutlineIntensity(70), Is.EqualTo(0.70f).Within(0.001f));
        Assert.That(ChatReadabilityStylePolicy.NormalizeOutlineIntensity(250), Is.EqualTo(1.00f).Within(0.001f));
    }

    [Test]
    public void ShouldUseLightOutline_OnlyForAbsoluteBlackText()
    {
        Assert.That(ChatReadabilityStylePolicy.ShouldUseLightOutline(0f, 0f, 0f), Is.True);
        Assert.That(ChatReadabilityStylePolicy.ShouldUseLightOutline(0.001f, 0f, 0f), Is.False);
        Assert.That(ChatReadabilityStylePolicy.ShouldUseLightOutline(0.06f, 0.06f, 0.10f), Is.False);
        Assert.That(ChatReadabilityStylePolicy.ShouldUseLightOutline(1f, 1f, 1f), Is.False);
        Assert.That(ChatReadabilityStylePolicy.ShouldUseLightOutline(0.3f, 0.8f, 1f), Is.False);
    }

    [TestCase("<#000000>gothic</color>: yippieee", true)]
    [TestCase("<color=#000000>voltzar</color>: where to", true)]
    [TestCase("<color=#000000FF>black with alpha</color>", true)]
    [TestCase("<color=black>named black</color>", true)]
    [TestCase("<#000080>navy is not true black</color>", false)]
    [TestCase("<color=#000001>near black still uses normal outline</color>", false)]
    [TestCase("<#101010>dark gray still uses normal outline</color>", false)]
    [TestCase("plain chat text", false)]
    public void ContainsTrueBlackRichTextColorTag_DetectsOnlyExactBlackRichText(string text, bool expected)
    {
        Assert.That(ChatReadabilityStylePolicy.ContainsTrueBlackRichTextColorTag(text), Is.EqualTo(expected));
    }

    [TestCase("#000000", "000000")]
    [TestCase("ffffff", "FFFFFF")]
    [TestCase(" 8e35ef ", "8E35EF")]
    [TestCase("#000000ff", "ABCDEF")]
    [TestCase("blue", "ABCDEF")]
    [TestCase("", "ABCDEF")]
    public void NormalizeHexColor_OnlyAcceptsSixDigitRgb(string value, string expected)
    {
        Assert.That(ChatReadabilityStylePolicy.NormalizeHexColor(value, "ABCDEF"), Is.EqualTo(expected));
    }
}
