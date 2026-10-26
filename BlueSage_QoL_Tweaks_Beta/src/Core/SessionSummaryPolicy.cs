#nullable enable
using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

// Round 5 Stage S (advisor, 2026-10-22): one line that answers "how did this session go" without a morning of
// grep. Written at quit and every 6 hours. No ids, no lobby name (it can carry an invite link), nothing per
// player. Host-only numbers read "n/a" for a guest so the line never claims a zero that was never counted.
// This line is the Thunderstore stability bar for 0.2.7. Pure: the net8 tests pin every field.
internal static class SessionSummaryPolicy
{
    internal const long IntervalTicks = TimeSpan.TicksPerHour * 6;
    internal const string Prefix = "[BlueSageSession]";

    internal static bool IsDue(long lastWriteUtcTicks, long nowUtcTicks)
    {
        return lastWriteUtcTicks > 0 && nowUtcTicks - lastWriteUtcTicks >= IntervalTicks;
    }

    internal static string Format(
        string trigger,
        double uptimeMinutes,
        bool hosted,
        int peakPlayers,
        int joins,
        int sweeps,
        long ledgerBytes,
        int ledgerWrites,
        int journalActionRows,
        int journalStatusRows,
        double heapStartMiB,
        double heapNowMiB,
        int warnings,
        int errors,
        long journalBytes = -1,
        long folderBytes = -1)
    {
        string hostOnly(int value) => hosted ? value.ToString(CultureInfo.InvariantCulture) : "n/a";
        return Prefix +
               " trigger=" + Sanitize(trigger) +
               "; uptimeMin=" + Math.Max(0d, uptimeMinutes).ToString("0", CultureInfo.InvariantCulture) +
               "; hosted=" + (hosted ? "yes" : "no") +
               "; peakPlayers=" + hostOnly(peakPlayers) +
               "; joins=" + hostOnly(joins) +
               "; sweeps=" + sweeps.ToString(CultureInfo.InvariantCulture) +
               "; ledgerKB=" + (Math.Max(0L, ledgerBytes) / 1024L).ToString(CultureInfo.InvariantCulture) +
               "; ledgerWrites=" + ledgerWrites.ToString(CultureInfo.InvariantCulture) +
               "; journalActions=" + hostOnly(journalActionRows) +
               "; journalStatus=" + hostOnly(journalStatusRows) +
               "; heapStartMiB=" + heapStartMiB.ToString("0.0", CultureInfo.InvariantCulture) +
               "; heapNowMiB=" + heapNowMiB.ToString("0.0", CultureInfo.InvariantCulture) +
               "; warnings=" + warnings.ToString(CultureInfo.InvariantCulture) +
               "; errors=" + errors.ToString(CultureInfo.InvariantCulture) +
               (journalBytes >= 0 ? "; journalKB=" + (journalBytes / 1024L).ToString(CultureInfo.InvariantCulture) : string.Empty) +
               (folderBytes >= 0 ? "; folderMB=" + (folderBytes / (1024L * 1024L)).ToString(CultureInfo.InvariantCulture) : string.Empty) + ".";
    }

    private static string Sanitize(string trigger)
    {
        string value = (trigger ?? string.Empty).Trim();
        if (value.Length == 0) return "unknown";
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '-');
        }

        return builder.ToString();
    }
}
