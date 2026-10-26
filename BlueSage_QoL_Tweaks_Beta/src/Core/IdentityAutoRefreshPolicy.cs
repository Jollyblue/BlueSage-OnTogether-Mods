using System;

namespace BlueSage.QoLTweaks.Core;

// Round 4 Stage E #5 (Blue, 2026-10-19): a button plus a 15-minute check while hosting. "Auto-remediation" means
// rescan, relabel and clear stale presentation only; it never takes a moderation action on its own. The first run
// comes 15 minutes after hosting starts (arming on the first host tick), so a fresh lobby is not refreshed at once.
internal static class IdentityAutoRefreshPolicy
{
    internal const long IntervalTicks = TimeSpan.TicksPerMinute * 15;

    internal static bool ShouldRun(bool isNetworkHost, long lastRunUtcTicks, long nowUtcTicks, out long nextLastRunUtcTicks)
    {
        if (!isNetworkHost || lastRunUtcTicks <= 0 || nowUtcTicks < lastRunUtcTicks)
        {
            // Not hosting, first host tick, or a clock that went backwards: arm and wait a full interval.
            nextLastRunUtcTicks = nowUtcTicks;
            return false;
        }

        if (nowUtcTicks - lastRunUtcTicks < IntervalTicks)
        {
            nextLastRunUtcTicks = lastRunUtcTicks;
            return false;
        }

        nextLastRunUtcTicks = nowUtcTicks;
        return true;
    }
}
