using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class TimestampFormatterTests
{
    [Test]
    public void BuildPrefix_UsesShortTwentyFourHourFormatByDefault()
    {
        var now = new DateTime(2026, 6, 17, 9, 5, 0);

        string prefix = TimestampFormatter.BuildPrefix(now, use24HourTime: true, sizePercent: 60, colorHex: "F5EDE1");

        Assert.That(prefix, Is.EqualTo("<size=60%><color=#F5EDE1>09:05</color></size> "));
    }

    [Test]
    public void BuildPrefix_SanitizesColorAndClampsSize()
    {
        var now = new DateTime(2026, 6, 17, 21, 5, 0);

        string prefix = TimestampFormatter.BuildPrefix(now, use24HourTime: false, sizePercent: 5, colorHex: "#BADHEX");

        Assert.That(prefix, Is.EqualTo("<size=40%><color=#F5EDE1>9:05 PM</color></size> "));
    }
}
