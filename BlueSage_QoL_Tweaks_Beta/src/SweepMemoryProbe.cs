#nullable enable
using System;
using UnityEngine.Profiling;

namespace BlueSage.QoLTweaks;

// Round 5 Stage D (advisor, 2026-10-22): four cheap counter reads around each sweep so the extra cleanup pass is
// measured, not argued. No scans. Fact-checked: this build's Profiler already answers GetRuntimeMemorySizeLong
// for the private Inspector's resource census, so the class is live in the player; a counter that is not
// available reads 0 here and "n/a" on the log line (SweepMemoryReadingPolicy). net46: no ValueTuple, so a class.
internal sealed class SweepMemoryProbe
{
    internal long AllocatedBytes;
    internal long ReservedBytes;
    internal long MonoHeapBytes;
    internal long MonoUsedBytes;

    internal static SweepMemoryProbe Capture()
    {
        var probe = new SweepMemoryProbe();
        probe.AllocatedBytes = SafeRead(Profiler.GetTotalAllocatedMemoryLong);
        probe.ReservedBytes = SafeRead(Profiler.GetTotalReservedMemoryLong);
        probe.MonoHeapBytes = SafeRead(Profiler.GetMonoHeapSizeLong);
        probe.MonoUsedBytes = SafeRead(Profiler.GetMonoUsedSizeLong);
        return probe;
    }

    private static long SafeRead(Func<long> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return 0L;
        }
    }
}
