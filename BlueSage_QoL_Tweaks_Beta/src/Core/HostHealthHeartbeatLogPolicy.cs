using System;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

// Round 5 F (audit of Blue's 37.5 h host): "Host Health H1: refreshed Steam lobby heartbeat metadata" was 451
// of the 690 QoL Info lines, one per 5-minute heartbeat, while the journal status row already carries the lobby
// count. The line is now written only when what it says has changed, and the lobby count is not part of
// "what it says": it changes on every join and leave and lives in the journal. Pure.
internal static class HostHealthHeartbeatLogPolicy
{
    internal static string BuildKey(bool heartbeatCompatOk, int refreshedSurfaceKeys, string playerLimitText)
    {
        var key = new StringBuilder();
        key.Append(heartbeatCompatOk ? "compat-ok" : "compat-failed");
        key.Append('|').Append(refreshedSurfaceKeys);
        foreach (string token in (playerLimitText ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("lobby=", StringComparison.Ordinal))
            {
                continue;
            }

            key.Append('|').Append(token);
        }

        return key.ToString();
    }

    internal static bool ShouldLog(string lastKey, string key)
    {
        return !string.Equals(lastKey, key, StringComparison.Ordinal);
    }
}
