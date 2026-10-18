using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class VoicePositionRatePolicyTests
{
    [Test]
    public void First_update_and_channel_changes_are_forwarded()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VoicePositionRatePolicy.ShouldForward(10f, -1f, "lobby", "", 0.25f), Is.True);
            Assert.That(VoicePositionRatePolicy.ShouldForward(10.1f, 10f, "new", "old", 0.25f), Is.True);
        });
    }

    [Test]
    public void Per_frame_updates_are_coalesced_to_four_hertz()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VoicePositionRatePolicy.ShouldForward(10.01f, 10f, "lobby", "lobby", 0.25f), Is.False);
            Assert.That(VoicePositionRatePolicy.ShouldForward(10.249f, 10f, "lobby", "lobby", 0.25f), Is.False);
            Assert.That(VoicePositionRatePolicy.ShouldForward(10.25f, 10f, "lobby", "lobby", 0.25f), Is.True);
        });
    }

    [Test]
    public void Clock_reset_fails_open()
    {
        Assert.That(VoicePositionRatePolicy.ShouldForward(2f, 10f, "lobby", "lobby", 0.25f), Is.True);
    }
}
