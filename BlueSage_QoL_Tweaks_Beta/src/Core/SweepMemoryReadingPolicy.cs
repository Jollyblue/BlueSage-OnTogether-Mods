#nullable enable
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

// Round 5 Stage D (advisor, 2026-10-22): the sweep line gains Unity's own memory counters so the value of the
// extra cleanup pass can be measured instead of argued. A zero read means the counter is not available on this
// player build, and the line must say "n/a", never "0.0 MiB" as if it were a measurement. Pure.
internal static class SweepMemoryReadingPolicy
{
    internal static string FormatMiB(long bytes)
    {
        if (bytes <= 0L) return "n/a";
        return (bytes / 1048576d).ToString("0.0", CultureInfo.InvariantCulture);
    }

    internal static string FormatDeltaMiB(long beforeBytes, long afterBytes)
    {
        if (beforeBytes <= 0L || afterBytes <= 0L) return "n/a";
        double delta = (afterBytes - beforeBytes) / 1048576d;
        return delta.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }
}
