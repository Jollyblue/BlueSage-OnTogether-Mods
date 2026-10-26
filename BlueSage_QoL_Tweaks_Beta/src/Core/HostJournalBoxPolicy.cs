using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum HostJournalBoxFilter
{
    Actions,
    Status,
    RefusedOnly
}

// Round 4 Stage H (Blue, 2026-10-20): the Host Tools journal box for hosts of 60-100 slots. Actions first (they are
// the record a host looks for after an incident), status as its own view, refused actions on their own, one-glance
// rows with the full line on hover and on copy. The file format (HostJournalFormat v1) is untouched.
internal static class HostJournalBoxPolicy
{
    internal const int CopyRowCount = 20;

    internal static IReadOnlyList<HostJournalRow> Select(
        IReadOnlyList<HostJournalRow> actionsNewestFirst,
        IReadOnlyList<HostJournalRow> statusNewestFirst,
        HostJournalBoxFilter filter)
    {
        if (filter == HostJournalBoxFilter.Status)
        {
            return statusNewestFirst ?? Array.Empty<HostJournalRow>();
        }

        IReadOnlyList<HostJournalRow> actions = actionsNewestFirst ?? Array.Empty<HostJournalRow>();
        if (filter == HostJournalBoxFilter.Actions)
        {
            return actions;
        }

        List<HostJournalRow> refused = new List<HostJournalRow>();
        foreach (HostJournalRow row in actions)
        {
            if (!row.Success)
            {
                refused.Add(row);
            }
        }

        return refused;
    }

    // "19:42  BAN  HELPER -> Cozy (…000002)  RECONNECTED  refused": what a host scans for. The Steam ID and the
    // reason live on hover (the full display line) and in the copied text.
    internal static string ToShortLine(HostJournalRow row)
    {
        string clock = new DateTime(row.UtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        if (row.Kind != HostJournalRowKind.Action)
        {
            return clock + "  status  " + row.SteamCount.ToString(CultureInfo.InvariantCulture) + "/" +
                   row.SteamLimit.ToString(CultureInfo.InvariantCulture) + "  live " +
                   row.LiveRoster.ToString(CultureInfo.InvariantCulture) + "  " + row.HostHealth;
        }

        StringBuilder line = new StringBuilder(96);
        line.Append(clock).Append("  ").Append(row.Action.ToUpperInvariant()).Append("  ").Append(row.ActorRole).Append(" -> ");
        if (!string.IsNullOrEmpty(row.TargetName))
        {
            line.Append(row.TargetName).Append(' ');
        }

        line.Append('(').Append(Suffix(row.TargetSteamId)).Append(')');
        if (!string.IsNullOrEmpty(row.Label))
        {
            line.Append("  ").Append(row.Label);
        }

        line.Append("  ").Append(row.Success ? "ok" : "refused");
        return line.ToString();
    }

    internal static string PinnedStatusLine(HostJournalRow latestStatus, int statusChangeCount)
    {
        if (latestStatus == null)
        {
            return "No status yet this session.";
        }

        // Round 5d: joins and leaves no longer count as a change, so the label says what it counts.
        string changes = statusChangeCount == 1
            ? "Lobby or health changed once this session."
            : "Lobby or health changed " + statusChangeCount.ToString(CultureInfo.InvariantCulture) + " times this session.";
        // Round 5d voice pass: player words only. The live-row and transport counts stay in the journal file.
        return "Now: " + (latestStatus.IsHost ? "you're hosting" : "you're a guest") + ", lobby " + (latestStatus.LobbyValid ? "OK" : "not ready") + ", " +
               latestStatus.SteamCount.ToString(CultureInfo.InvariantCulture) + " of " + latestStatus.SteamLimit.ToString(CultureInfo.InvariantCulture) +
               " players, health " + latestStatus.HostHealth + ". " + changes;
    }

    internal static string CopyBlock(IReadOnlyList<HostJournalRow> rowsNewestFirst, int maximumRows)
    {
        if (rowsNewestFirst == null || rowsNewestFirst.Count == 0 || maximumRows <= 0)
        {
            return string.Empty;
        }

        StringBuilder block = new StringBuilder();
        int count = Math.Min(maximumRows, rowsNewestFirst.Count);
        for (int index = 0; index < count; index++)
        {
            if (index > 0)
            {
                block.Append('\n');
            }

            block.Append(HostJournalFormat.ToDisplayLine(rowsNewestFirst[index]));
        }

        return block.ToString();
    }

    private static string Suffix(string steamId)
    {
        if (string.IsNullOrEmpty(steamId))
        {
            return "no id";
        }

        return steamId.Length > 6 ? "…" + steamId.Substring(steamId.Length - 6) : steamId;
    }
}
