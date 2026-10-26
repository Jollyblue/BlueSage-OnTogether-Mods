using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HiddenControlLinePolicyTests
{
    [TestCase("<size=0>GT1|P|steam|hello</size>")]
    [TestCase("  <size=0>GT1|H|x</size>")]
    [TestCase("\t<size=0>GT1|")]
    public void Hidden_control_lines_are_recognised(string text)
    {
        Assert.That(HiddenControlLinePolicy.IsHiddenControlLine(text), Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("hello there")]
    [TestCase("GT1|looks like it but has no size tag")]
    [TestCase("<size=0>hidden but not a control line</size>")]
    [TestCase("a normal line that mentions <size=0>GT1| later")]
    [TestCase("<size=80%>12:30</size> <size=0>GT1|P|x</size>")]
    [TestCase("<color=#fff>GT1|</color>")]
    public void Normal_chat_is_never_mistaken_for_a_control_line(string text)
    {
        Assert.That(HiddenControlLinePolicy.IsHiddenControlLine(text), Is.False);
    }

    [Test]
    public void The_marker_is_the_exact_size_zero_prefix()
    {
        Assert.That(HiddenControlLinePolicy.Marker, Is.EqualTo("<size=0>GT1|"));
    }
}
