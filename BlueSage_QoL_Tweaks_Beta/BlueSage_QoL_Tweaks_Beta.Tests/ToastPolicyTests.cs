using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ToastPolicyTests
{
    [Test]
    public void Alpha_is_zero_before_the_toast_is_shown()
    {
        Assert.That(ToastPolicy.ComputeAlpha(-0.1f), Is.EqualTo(0f));
    }

    [Test]
    public void Alpha_ramps_up_linearly_during_fade_in()
    {
        Assert.That(ToastPolicy.ComputeAlpha(0f), Is.EqualTo(0f));
        Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.FadeInSeconds / 2f), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.FadeInSeconds), Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void Alpha_is_full_throughout_the_hold_window()
    {
        Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.FadeInSeconds + 0.1f), Is.EqualTo(1f));
        Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.FadeInSeconds + ToastPolicy.HoldSeconds - 0.001f), Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void Alpha_ramps_down_linearly_during_fade_out()
    {
        float fadeOutStart = ToastPolicy.FadeInSeconds + ToastPolicy.HoldSeconds;
        Assert.That(ToastPolicy.ComputeAlpha(fadeOutStart), Is.EqualTo(1f).Within(0.001f));
        Assert.That(ToastPolicy.ComputeAlpha(fadeOutStart + ToastPolicy.FadeOutSeconds / 2f), Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void Alpha_is_zero_after_the_toast_has_fully_expired()
    {
        Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.TotalLifetimeSeconds), Is.EqualTo(0f).Within(0.001f));
        Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.TotalLifetimeSeconds + 10f), Is.EqualTo(0f));
    }

    [Test]
    public void IsExpired_matches_the_total_lifetime_boundary()
    {
        Assert.That(ToastPolicy.IsExpired(ToastPolicy.TotalLifetimeSeconds - 0.01f), Is.False);
        Assert.That(ToastPolicy.IsExpired(ToastPolicy.TotalLifetimeSeconds), Is.True);
    }
}
