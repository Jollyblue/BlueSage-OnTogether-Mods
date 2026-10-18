using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

// Roster counts are diagnostic detail, not a new safety state. During a busy
// join/leave wave they change frequently even though the fail-closed decision
// has not changed, so they must not turn every bounded snapshot into a warning.
internal static class IdentityCoverageLogPolicy
{
    internal static string BuildStateKey(
        ulong lobbyId,
        bool coverageComplete,
        string captureError,
        int verifiedPlayerCount,
        int nativeRowCount,
        int activeNativeRowCount,
        int purrNetRowCount,
        int lobbyMemberCount,
        int liveSteamCheckCount,
        int leftStaleCount,
        int ambiguousCount)
    {
        // Counts stay in the emitted message below, but they are intentionally
        // not part of the transition identity: replication catch-up changes
        // them frequently without changing the safety decision.
        return lobbyId.ToString(CultureInfo.InvariantCulture) + "|" +
               coverageComplete + "|" +
               (string.IsNullOrWhiteSpace(captureError)
                   ? "none"
                   : captureError.Trim());
    }
}
