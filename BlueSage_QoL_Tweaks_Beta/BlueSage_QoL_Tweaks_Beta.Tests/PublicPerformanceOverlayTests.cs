using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicPerformanceOverlayTests
{
    [Test]
    public void Remote_label_is_a_single_straight_bar()
    {
        var state = new PerformanceOverlayState();
        state.SampleFrame(1d / 30d);

        string label = state.BuildLabel(33, 1535450808L);

        Assert.Multiple(() =>
        {
            Assert.That(label, Is.EqualTo("FPS 30  PING 33 ms  RAM 1.43 GB"));
            Assert.That(label, Does.Not.Contain("\r").And.Not.Contain("\n"));
        });
    }

    [Test]
    public void Host_or_unavailable_latency_is_honest()
    {
        var state = new PerformanceOverlayState();
        state.SampleFrame(1d / 30d);

        Assert.That(
            state.BuildLabel(null, 1535450808L),
            Is.EqualTo("FPS 30  PING --  RAM 1.43 GB"));
    }

    [Test]
    public void Settings_keep_the_bar_readable_and_finite()
    {
        PerformanceOverlaySettings settings =
            PerformanceOverlaySettingsPolicy.Normalize(
                double.NaN,
                0,
                double.PositiveInfinity,
                double.NegativeInfinity,
                12d);

        Assert.Multiple(() =>
        {
            Assert.That(settings.RefreshSeconds, Is.EqualTo(0.25d));
            Assert.That(settings.FontSize, Is.EqualTo(13));
            Assert.That(settings.OffsetX, Is.EqualTo(12d));
            Assert.That(settings.OffsetY, Is.EqualTo(174d));
            Assert.That(settings.Width, Is.EqualTo(320d));
        });
    }

    [Test]
    public void Rtt_conversion_rejects_fake_or_impossible_values()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TickManagerPingPolicy.FromRttSeconds(0.033d), Is.EqualTo(33));
            Assert.That(TickManagerPingPolicy.FromRttSeconds(0d), Is.Null);
            Assert.That(TickManagerPingPolicy.FromRttSeconds(double.NaN), Is.Null);
        });
    }

    [Test]
    public void Runtime_memory_uses_native_fallback_when_managed_process_counter_is_zero()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ProcessRuntimeMetricsCapture.SelectPositiveBytes(0L, 1535450808L),
                Is.EqualTo(1535450808L));
            Assert.That(
                ProcessRuntimeMetricsCapture.SelectPositiveBytes(1535450808L, 2048L),
                Is.EqualTo(1535450808L));
            Assert.That(
                ProcessRuntimeMetricsCapture.SelectPositiveBytes(0L, 0L),
                Is.Null);
        });
    }
}
