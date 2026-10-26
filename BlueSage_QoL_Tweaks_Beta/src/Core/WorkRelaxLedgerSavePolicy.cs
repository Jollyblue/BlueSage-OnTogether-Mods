using System;

namespace BlueSage.QoLTweaks.Core;

// Round 4 Stage G (Marbles, 2026-10-20). The ledger used to be serialized, fsynced and replaced inside the game's
// autosave frame. Now the autosave postfix only marks it dirty and a later frame asks this rule whether to hand a
// snapshot to the background writer. Routine changes (reconciliation rows) wait a minute; prompt changes (a mode
// switch, a report correction) go out a second later; nothing is written on the frame that marked it dirty.
internal static class WorkRelaxLedgerSavePolicy
{
    // Round 5 Stage A: 60 s -> 300 s. A crash loses at most five minutes of focus statistics; the file is
    // rewritten about 100x less (it was 5.3 MB every minute, ~10 GB over Blue's 37.5 h host).
    internal const long RoutineIntervalTicks = TimeSpan.TicksPerSecond * 300;
    internal const long PromptDelayTicks = TimeSpan.TicksPerSecond;

    internal static bool IsDue(bool dirty, bool prompt, long dirtySinceTicks, long lastWriteTicks, long nowTicks, bool writeInFlight)
    {
        if (!dirty || writeInFlight || nowTicks <= dirtySinceTicks)
        {
            return false;
        }

        if (prompt && nowTicks - dirtySinceTicks >= PromptDelayTicks)
        {
            return true;
        }

        // A last-write stamp in the future (clock change, restored session) counts as elapsed.
        return lastWriteTicks > nowTicks || nowTicks - lastWriteTicks >= RoutineIntervalTicks;
    }
}
