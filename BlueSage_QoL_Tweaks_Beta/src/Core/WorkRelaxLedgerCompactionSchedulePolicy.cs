#nullable enable
using System;

namespace BlueSage.QoLTweaks.Core;

// Round 5 Stage A: CompactHistory used to run only at startup, so a multi-day host never folded anything (7,543
// rows older than the window sat in Blue's file after 37 h). It now also runs when the UTC day rolls over while
// the game is running. Pure.
internal static class WorkRelaxLedgerCompactionSchedulePolicy
{
    internal static long UtcDay(long utcTicks) => utcTicks / TimeSpan.TicksPerDay;

    internal static bool IsDue(long lastCompactionUtcDay, long nowUtcTicks)
    {
        return lastCompactionUtcDay > 0 && UtcDay(nowUtcTicks) != lastCompactionUtcDay;
    }
}
