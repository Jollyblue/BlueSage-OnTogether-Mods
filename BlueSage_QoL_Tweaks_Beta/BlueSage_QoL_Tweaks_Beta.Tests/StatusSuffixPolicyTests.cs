using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatusSuffixPolicyTests
{
    [TestCase("Reading", "FFD45D", "()", "<#FFD45D>(Reading)</color>")]
    [TestCase("Reading", "#70FFBD", "[]", "<#70FFBD>[Reading]</color>")]
    [TestCase(" Reading ", "83E", "{}", "<#8833EE>{Reading}</color>")]
    [TestCase("Reading", "bad", "()", "<#BBAADD>(Reading)</color>")]
    public void Preview_copy_and_apply_share_the_same_status_suffix(
        string status,
        string color,
        string brackets,
        string expected)
    {
        Assert.That(StatusSuffixPolicy.Build(status, color, brackets), Is.EqualTo(expected));
    }

    [Test]
    public void Empty_status_has_no_suffix()
    {
        Assert.That(StatusSuffixPolicy.Build("  ", "FFD45D", "[]"), Is.Empty);
    }
}
