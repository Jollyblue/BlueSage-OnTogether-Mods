using System;

namespace BlueSage.QoLTweaks.Core;

internal enum KickFollowUp
{
    Wait,
    Unban,
    FailTowardBan,
    Cancelled
}

// Round 4 Stage E #3 (Blue, 2026-10-19): kick = ban through the game's own flow, wait until the player's row is gone,
// then unban. There is no vanilla kick, and a connection drop would only make a reconnect mod rejoin at once; the
// ban flow tells the client it was removed on purpose. Fable amendment: a kick that does not finish fails TOWARD
// the ban (the ban stays, a journal row is written, the host is told). An explicit ban for the same target while
// the kick is pending promotes it: the follow-up unban is cancelled.
internal static class KickFollowUpPolicy
{
    internal const long TimeoutTicks = TimeSpan.TicksPerSecond * 20;
    internal const string IncompleteMessage = "Kick incomplete: the player is still banned. Unban manually if you want.";

    internal static KickFollowUp Decide(int liveRows, long elapsedTicks, bool promotedToBan)
    {
        if (promotedToBan)
        {
            return KickFollowUp.Cancelled;
        }

        if (liveRows == 0)
        {
            return KickFollowUp.Unban;
        }

        return elapsedTicks >= TimeoutTicks ? KickFollowUp.FailTowardBan : KickFollowUp.Wait;
    }
}
