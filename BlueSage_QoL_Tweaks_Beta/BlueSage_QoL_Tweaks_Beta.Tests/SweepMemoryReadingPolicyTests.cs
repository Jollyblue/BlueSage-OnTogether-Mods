using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 Stage D: a zero Profiler read is "n/a", never a measurement.
[TestFixture]
internal sealed class SweepMemoryReadingPolicyTests
{
    [Test]
    public void Zero_or_negative_reads_are_not_available()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepMemoryReadingPolicy.FormatMiB(0), Is.EqualTo("n/a"));
            Assert.That(SweepMemoryReadingPolicy.FormatMiB(-5), Is.EqualTo("n/a"));
            Assert.That(SweepMemoryReadingPolicy.FormatDeltaMiB(0, 1048576), Is.EqualTo("n/a"));
            Assert.That(SweepMemoryReadingPolicy.FormatDeltaMiB(1048576, 0), Is.EqualTo("n/a"));
        });
    }

    [Test]
    public void Real_reads_format_in_mebibytes_with_signed_delta()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SweepMemoryReadingPolicy.FormatMiB(1048576 * 3), Is.EqualTo("3.0"));
            Assert.That(SweepMemoryReadingPolicy.FormatMiB(1572864), Is.EqualTo("1.5"));
            Assert.That(SweepMemoryReadingPolicy.FormatDeltaMiB(1048576 * 4, 1048576 * 3), Is.EqualTo("-1.0"));
            Assert.That(SweepMemoryReadingPolicy.FormatDeltaMiB(1048576, 1048576 * 2), Is.EqualTo("+1.0"));
            Assert.That(SweepMemoryReadingPolicy.FormatDeltaMiB(1048576, 1048576), Is.EqualTo("0.0"));
        });
    }
}
