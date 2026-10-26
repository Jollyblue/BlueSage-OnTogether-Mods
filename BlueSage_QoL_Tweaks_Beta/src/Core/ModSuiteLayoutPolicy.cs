#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

// Round 5d (Blue, 2026-10-23): "everything needs to live in a singular BlueSageQoLModSuite type dir … Files will be
// named as timestamp-date-relatedlogname or trackerfunction … if multiple sessions OR reasonable file size limit hit,
// after so many days whichever wins first … compress … still accessible". One folder under BepInEx for the PUBLIC QoL
// mod's own files only. The game's saves, BepInEx's own log, every .cfg file, other mods' files, avatar slots and
// chalkboards are never moved or deleted (Blue: "we are only touching our MOD related files").
//
//   BepInEx/BlueSageQoLModSuite/
//     README.txt                    plain-words guide (written by the mod)
//     layout-v1.marker              "this folder is authoritative" (written before any old file is removed)
//     trackers/                     live files that keep their name: work-relax-p_<id>.json, host-journal.jsonl,
//                                   session-run-marker.v1, community-ban-attestation.json (+ .bak rescue copies)
//     logs/<yyyy-MM-dd_HHmmss>-session/   optional local audit, one folder per game session (local time)
//     archive/<yyyy-MM-dd_HHmmss>-<what>.zip   older sessions, rotated journals, the pre-0.2.7 layout
internal static class ModSuiteLayoutPolicy
{
    internal const string RootFolderName = "BlueSageQoLModSuite";
    internal const string TrackersFolderName = "trackers";
    internal const string LogsFolderName = "logs";
    internal const string ArchiveFolderName = "archive";
    internal const string MarkerFileName = "layout-v1.marker";
    internal const string ReadmeFileName = "README.txt";

    internal const string WorkRelaxFilePrefix = "work-relax-";
    internal const string HostJournalFileName = "host-journal.jsonl";
    internal const string RunMarkerFileName = "session-run-marker.v1";
    internal const string AttestationFileName = "community-ban-attestation.json";

    internal const string SessionFolderSuffix = "-session";
    internal const string SessionLogsArchiveKind = "session-logs";
    internal const string HostJournalArchiveKind = "host-journal";
    internal const string PreLayoutArchiveKind = "pre-0.2.7-layout";

    // Raw session logs: whichever comes first of 3 sessions, 3 days, 24 MiB moves a session into the archive.
    internal const int RawSessionsKept = 3;
    internal const int RawSessionDays = 3;
    internal const long RawLogsBudgetBytes = 24L * 1024L * 1024L;

    // Archive: zips are kept 14 days (Blue 2026-10-23: "keeping the last 14 days (2 weeks) of playtime"); beyond 48 MiB
    // the oldest go first. The pre-0.2.7 layout zip (the rollback lifeline) leaves only by age.
    internal const int ArchiveDays = 14;

    // Final safeguard (Blue 2026-10-23: "should not result in a related modsuite folder that grows larger than 1gb"):
    // whatever the other limits say, the whole BlueSageQoLModSuite folder is brought back under 1 GiB at every
    // tidy-up, oldest archive zip or finished log session first. Live trackers and the session being written stay.
    internal const long FolderCeilingBytes = 1024L * 1024L * 1024L;

    internal static IList<string> FolderCeilingEvictions(IList<ModSuiteItem>? evictable, long trackersBytes, long ceilingBytes = FolderCeilingBytes)
    {
        List<string> evict = new List<string>();
        if (evictable == null || evictable.Count == 0)
        {
            return evict;
        }

        long total = Math.Max(0L, trackersBytes) + evictable.Sum(item => Math.Max(0L, item.Bytes));
        foreach (ModSuiteItem item in evictable.OrderBy(item => item.CreatedUtcTicks))
        {
            if (total <= ceilingBytes)
            {
                break;
            }

            if (item.IsCurrent)
            {
                continue;
            }

            evict.Add(item.Name);
            total -= Math.Max(0L, item.Bytes);
        }

        return evict;
    }
    internal const long ArchiveBudgetBytes = 48L * 1024L * 1024L;

    // Local time: the folder is read by players, not machines.
    internal static string Stamp(DateTime localTime)
    {
        return localTime.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
    }

    internal static string SessionFolderName(DateTime localTime) => Stamp(localTime) + SessionFolderSuffix;

    internal static string ArchiveFileName(DateTime localTime, string kind) => Stamp(localTime) + "-" + kind + ".zip";

    internal static string ChatFileStem(string sessionStamp) => sessionStamp + "-chat";

    internal static string GameLogFileStem(string sessionStamp) => sessionStamp + "-game-log";

    internal static string SnapshotFileName(DateTime localTime) => Stamp(localTime) + "-snapshot-game-log.log";

    internal static bool IsSessionFolderName(string? name)
    {
        return name != null && name.EndsWith(SessionFolderSuffix, StringComparison.Ordinal) && name.Length == 17 + SessionFolderSuffix.Length && TryParseStamp(name.Substring(0, 17), out _);
    }

    internal static bool TryParseStamp(string? stamp, out DateTime localTime)
    {
        return DateTime.TryParseExact(stamp ?? string.Empty, "yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out localTime);
    }

    // Newest first: sessions beyond the count, older than the age or beyond the raw budget go to the archive.
    // The session being written now is never moved.
    internal static IList<string> SessionsToArchive(IList<ModSuiteItem>? sessions, long nowUtcTicks)
    {
        List<string> archive = new List<string>();
        if (sessions == null || sessions.Count == 0)
        {
            return archive;
        }

        long cutoff = nowUtcTicks - TimeSpan.TicksPerDay * RawSessionDays;
        List<ModSuiteItem> newestFirst = sessions.OrderByDescending(item => item.CreatedUtcTicks).ToList();
        List<ModSuiteItem> kept = new List<ModSuiteItem>();
        for (int index = 0; index < newestFirst.Count; index++)
        {
            ModSuiteItem item = newestFirst[index];
            if (!item.IsCurrent && (index >= RawSessionsKept || item.CreatedUtcTicks < cutoff))
            {
                archive.Add(item.Name);
            }
            else
            {
                kept.Add(item);
            }
        }

        long total = kept.Sum(item => Math.Max(0L, item.Bytes));
        for (int index = kept.Count - 1; index >= 0 && total > RawLogsBudgetBytes; index--)
        {
            if (kept[index].IsCurrent)
            {
                continue;
            }

            archive.Add(kept[index].Name);
            total -= Math.Max(0L, kept[index].Bytes);
        }

        return archive;
    }

    // Zips older than ArchiveDays go; then, oldest first, ordinary zips go until the archive fits ArchiveBudgetBytes.
    internal static IList<string> ArchivesToDelete(IList<ModSuiteItem>? archives, long nowUtcTicks)
    {
        List<string> delete = new List<string>();
        if (archives == null || archives.Count == 0)
        {
            return delete;
        }

        long cutoff = nowUtcTicks - TimeSpan.TicksPerDay * ArchiveDays;
        List<ModSuiteItem> oldestFirst = archives.OrderBy(item => item.CreatedUtcTicks).ToList();
        List<ModSuiteItem> kept = new List<ModSuiteItem>();
        foreach (ModSuiteItem item in oldestFirst)
        {
            if (item.CreatedUtcTicks < cutoff)
            {
                delete.Add(item.Name);
            }
            else
            {
                kept.Add(item);
            }
        }

        // The pre-0.2.7 copy is the rollback lifeline: it leaves only by age and does not count toward the budget, so it
        // can never push every newer zip out (security review).
        long total = kept.Where(item => !IsPreLayoutArchive(item.Name)).Sum(item => Math.Max(0L, item.Bytes));
        foreach (ModSuiteItem item in kept)
        {
            if (total <= ArchiveBudgetBytes)
            {
                break;
            }

            if (IsPreLayoutArchive(item.Name))
            {
                continue;
            }

            delete.Add(item.Name);
            total -= Math.Max(0L, item.Bytes);
        }

        return delete;
    }

    internal static bool IsPreLayoutArchive(string? name) =>
        name != null && name.EndsWith("-" + PreLayoutArchiveKind + ".zip", StringComparison.Ordinal);

    // The only paths BlueSage may ever delete: inside its own new folder, or one of the exact old-layout locations
    // the 0.2.6-and-older QoL mod created. Anything else (the game, BepInEx, .cfg files, other mods) is refused.
    internal static bool IsDeletableOwnedPath(string candidate, string bepinexRoot, string configRoot)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(bepinexRoot) || string.IsNullOrWhiteSpace(configRoot))
        {
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return false;
        }

        if (full.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (string root in OwnedRoots(bepinexRoot, configRoot))
        {
            if (IsInside(full, root))
            {
                return true;
            }
        }

        return false;
    }

    internal static IEnumerable<string> OwnedRoots(string bepinexRoot, string configRoot)
    {
        yield return Path.Combine(bepinexRoot, RootFolderName);
        yield return Path.Combine(bepinexRoot, LegacyAuditFolderName);
        yield return Path.Combine(configRoot, LegacyAuditFolderName);
        yield return Path.Combine(configRoot, LegacyWorkRelaxFolderName);
    }

    // The 0.2.6-and-older QoL layout.
    internal const string LegacyAuditFolderName = "BlueSageAudit";
    internal const string LegacyWorkRelaxFolderName = "BlueSageWorkRelax";
    internal const string LegacyHostJournalFolderName = "host-journal";
    internal const string LegacyHostJournalFileName = "host-journal-v1.jsonl";
    internal const string LegacyRunMarkerFileName = "run-marker.v1";
    internal const string LegacyAttestationFileName = "community-ban-source-attestation.json";

    internal static bool IsLegacyWorkRelaxFileName(string? name)
    {
        if (name == null)
        {
            return false;
        }

        string stem = name.EndsWith(".json.bak", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9)
            : name.EndsWith(".json", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5)
            : string.Empty;
        return stem.Length == 66 && stem.StartsWith("p_", StringComparison.Ordinal) && stem.Substring(2).All(IsLowerHex);
    }

    private static bool IsLowerHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');

    private static bool IsInside(string full, string root)
    {
        string normalizedRoot;
        try
        {
            normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception)
        {
            return false;
        }

        return string.Equals(full, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ModSuiteItem
{
    internal ModSuiteItem(string name, long createdUtcTicks, long bytes, bool isCurrent = false)
    {
        Name = name ?? string.Empty;
        CreatedUtcTicks = createdUtcTicks;
        Bytes = bytes;
        IsCurrent = isCurrent;
    }

    internal string Name { get; }
    internal long CreatedUtcTicks { get; }
    internal long Bytes { get; }
    internal bool IsCurrent { get; }
}
