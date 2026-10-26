#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

// Round 5d: where every public QoL file lives, the one-time move from the old layout, the zip archive and the
// retention pass. Plain file I/O, no Unity or BepInEx types, so the net8 tests run all of it on temporary folders.
internal sealed class ModSuiteLayout
{
    private ModSuiteLayout(string bepinexRoot, string configRoot, bool legacyMode)
    {
        BepInExRoot = bepinexRoot;
        ConfigRoot = configRoot;
        LegacyMode = legacyMode;
        Root = Path.Combine(bepinexRoot, ModSuiteLayoutPolicy.RootFolderName);
        Trackers = Path.Combine(Root, ModSuiteLayoutPolicy.TrackersFolderName);
        Logs = Path.Combine(Root, ModSuiteLayoutPolicy.LogsFolderName);
        Archive = Path.Combine(Root, ModSuiteLayoutPolicy.ArchiveFolderName);
        string legacyAudit = Path.Combine(bepinexRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName);
        if (legacyMode)
        {
            // The move did not finish: keep reading and writing the old places so nothing looks lost.
            WorkRelaxDirectory = Path.Combine(configRoot, ModSuiteLayoutPolicy.LegacyWorkRelaxFolderName);
            WorkRelaxFilePrefix = string.Empty;
            HostJournalPath = Path.Combine(legacyAudit, ModSuiteLayoutPolicy.LegacyHostJournalFolderName, ModSuiteLayoutPolicy.LegacyHostJournalFileName);
            RunMarkerPath = Path.Combine(legacyAudit, ModSuiteLayoutPolicy.LegacyRunMarkerFileName);
            AttestationPath = Path.Combine(configRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName, ModSuiteLayoutPolicy.LegacyAttestationFileName);
        }
        else
        {
            WorkRelaxDirectory = Trackers;
            WorkRelaxFilePrefix = ModSuiteLayoutPolicy.WorkRelaxFilePrefix;
            HostJournalPath = Path.Combine(Trackers, ModSuiteLayoutPolicy.HostJournalFileName);
            RunMarkerPath = Path.Combine(Trackers, ModSuiteLayoutPolicy.RunMarkerFileName);
            AttestationPath = Path.Combine(Trackers, ModSuiteLayoutPolicy.AttestationFileName);
        }
    }

    internal static ModSuiteLayout For(string bepinexRoot, string configRoot, bool legacyMode) => new ModSuiteLayout(bepinexRoot, configRoot, legacyMode);

    internal string BepInExRoot { get; }
    internal string ConfigRoot { get; }
    internal bool LegacyMode { get; }
    internal string Root { get; }
    internal string Trackers { get; }
    internal string Logs { get; }
    internal string Archive { get; }
    internal string WorkRelaxDirectory { get; }
    internal string WorkRelaxFilePrefix { get; }
    internal string HostJournalPath { get; }
    internal string RunMarkerPath { get; }
    internal string AttestationPath { get; }

    // Rotated journals go into the archive as zips; in legacy mode they keep the old .prev rotation.
    internal string? HostJournalArchiveDirectory => LegacyMode ? null : Archive;
}

internal sealed class ModSuiteMigrationResult
{
    internal ModSuiteMigrationResult(bool layoutReady, bool movedOldFiles, string summary)
    {
        LayoutReady = layoutReady;
        MovedOldFiles = movedOldFiles;
        Summary = summary;
    }

    internal bool LayoutReady { get; }
    internal bool MovedOldFiles { get; }
    internal string Summary { get; }
}

internal static class ModSuiteMigration
{
    internal const string ReadmeText =
        "BlueSage QoL Tweaks keeps all of its own files in this one folder.\r\n" +
        "\r\n" +
        "trackers\\   Your Work / Relax history, the host journal (only when you host) and a few small status files.\r\n" +
        "             Files ending in .bak are automatic rescue copies. Please leave this folder alone while playing.\r\n" +
        "logs\\       The optional local chat and game-log copy (Local Audit Exports, off unless you turn it on).\r\n" +
        "             One folder per game session, named with the date and time it started (your local time).\r\n" +
        "archive\\    Older sessions and older journal pages, packed into .zip files you can open with a double-click.\r\n" +
        "             Zips are kept for 14 days and about 48 MB in total, then the oldest are removed.\r\n" +
        "             Whatever happens, this whole folder never stays over 1 GB: the oldest files go first.\r\n" +
        "\r\n" +
        "Nothing here is ever uploaded. Logs can contain player names and chat, so read them before you share them.\r\n" +
        "The game's own saves, BepInEx's log and every .cfg settings file are not stored here and are never touched.\r\n" +
        "\r\n" +
        "Coming from an older version? Your files were moved here once. A copy of the old folders (with up to your\r\n" +
        "12 newest old log sessions from the last 14 days, about 96 MB at most) is in archive\\<date>-pre-0.2.7-layout.zip\r\n" +
        "for 14 days. If you go back to an older version, it starts a fresh Work / Relax history; unzip that copy into\r\n" +
        "BepInEx to bring the old history back.\r\n";

    // Security review: the move keeps what 0.2.6 itself would still have kept (its 12 newest sessions within 14 days),
    // within about 96 MB so the one-time zip stays a short pause; only sessions past that are removed (README says so).
    internal const int PreLayoutSessionsKept = 12;
    internal const int PreLayoutSessionDays = 14;
    internal const long PreLayoutSessionBudgetBytes = 96L * 1024L * 1024L;

    internal static List<DirectoryInfo> SelectOldSessionsToKeep(IEnumerable<DirectoryInfo> oldSessions, DateTime nowUtc, out int dropped)
    {
        List<DirectoryInfo> kept = new List<DirectoryInfo>();
        dropped = 0;
        long bytes = 0;
        DateTime cutoff = nowUtc.AddDays(-PreLayoutSessionDays);
        foreach (DirectoryInfo session in oldSessions.OrderByDescending(directory => directory.CreationTimeUtc))
        {
            long size = FolderBytes(session);
            if (kept.Count >= PreLayoutSessionsKept || session.CreationTimeUtc < cutoff || bytes + size > PreLayoutSessionBudgetBytes)
            {
                dropped++;
                continue;
            }

            kept.Add(session);
            bytes += size;
        }

        return kept;
    }

    internal static ModSuiteMigrationResult Run(string bepinexRoot, string configRoot, DateTime localNow, Action<string>? log)
    {
        ModSuiteLayout layout = ModSuiteLayout.For(bepinexRoot, configRoot, legacyMode: false);
        string marker = Path.Combine(layout.Root, ModSuiteLayoutPolicy.MarkerFileName);
        List<KeyValuePair<string, string>> copies = BuildCopyList(bepinexRoot, configRoot, layout);
        List<string> extras = BuildExtraZipSources(bepinexRoot);
        List<DirectoryInfo> oldSessions = OldSessionFolders(bepinexRoot);

        if (File.Exists(marker))
        {
            // Security review: once the marker exists the new folder is authoritative; any trouble with old leftovers
            // must never send the player back to the (now empty) old places.
            try
            {
                // Old files after the move are either leftovers of an interrupted clean-up (identical to the new copies)
                // or something an older version wrote after a rollback. The second kind is packed into the archive first
                // (sessions within the same bounded budget as the first move), so removing the old places loses nothing
                // the README does not mention.
                List<DirectoryInfo> keptSessions = SelectOldSessionsToKeep(oldSessions, localNow.ToUniversalTime(), out int droppedLeftovers);
                List<string> unpacked = copies.Where(copy => !SameBytes(copy.Key, copy.Value)).Select(copy => copy.Key)
                    .Concat(extras)
                    .Concat(keptSessions.SelectMany(session => session.GetFiles("*", SearchOption.AllDirectories)).Select(file => file.FullName))
                    .ToList();
                if (unpacked.Count > 0)
                {
                    string zipPath = Path.Combine(layout.Archive, ModSuiteLayoutPolicy.ArchiveFileName(localNow, ModSuiteLayoutPolicy.PreLayoutArchiveKind));
                    List<KeyValuePair<string, string>> entries = unpacked.Select(file => Entry(bepinexRoot, configRoot, file)).ToList();
                    if (!ModSuiteZip.TryCreate(zipPath, entries, out string zipError))
                    {
                        log?.Invoke("Old BlueSage files found after the move could not be packed (" + zipError + "); they stay where they are until next start.");
                        EnsureReadme(layout);
                        return new ModSuiteMigrationResult(true, false, "layout ready; old leftovers kept");
                    }
                }

                int leftovers = RemoveOldLayout(bepinexRoot, configRoot, copies, extras, oldSessions, log);
                EnsureReadme(layout);
                return new ModSuiteMigrationResult(true, false, leftovers == 0 ? "layout ready" : "layout ready; removed " + leftovers + " old leftovers" + (droppedLeftovers > 0 ? " (" + droppedLeftovers + " old sessions past the limits not kept)" : string.Empty));
            }
            catch (Exception ex)
            {
                log?.Invoke("Old BlueSage leftovers could not be tidied this time (" + ex.GetType().Name + "); the new folder stays in use and they are tried again next start.");
                return new ModSuiteMigrationResult(true, false, "layout ready; old leftovers kept");
            }
        }

        try
        {
            Directory.CreateDirectory(layout.Trackers);
            Directory.CreateDirectory(layout.Logs);
            Directory.CreateDirectory(layout.Archive);
            bool hasOldFiles = copies.Count > 0 || extras.Count > 0 || oldSessions.Count > 0;
            int droppedSessions = 0;
            if (hasOldFiles)
            {
                // 1. Copy every live tracker and prove each copy by hash.
                foreach (KeyValuePair<string, string> copy in copies)
                {
                    if (!File.Exists(copy.Value) || !SameBytes(copy.Key, copy.Value))
                    {
                        File.Copy(copy.Key, copy.Value, overwrite: true);
                    }

                    if (!SameBytes(copy.Key, copy.Value))
                    {
                        throw new IOException("copy check failed for " + Path.GetFileName(copy.Key));
                    }
                }

                // 2. Zip the old layout: every tracker, the old journal page, and the newest old audit sessions that fit
                //    today's limits (older ones were already past what this build keeps).
                List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();
                foreach (KeyValuePair<string, string> copy in copies)
                {
                    entries.Add(Entry(bepinexRoot, configRoot, copy.Key));
                }

                foreach (string extra in extras)
                {
                    entries.Add(Entry(bepinexRoot, configRoot, extra));
                }

                foreach (DirectoryInfo session in SelectOldSessionsToKeep(oldSessions, localNow.ToUniversalTime(), out droppedSessions))
                {
                    foreach (FileInfo file in session.GetFiles("*", SearchOption.AllDirectories))
                    {
                        entries.Add(Entry(bepinexRoot, configRoot, file.FullName));
                    }
                }

                // Security review + advisor final sweep: a verified pre-layout zip left by an attempt that did not reach
                // the marker is reused, never rebuilt, so a move that keeps failing costs no repeated pause at start.
                // The live trackers above are still copied and hash-checked fresh on every attempt.
                bool alreadyPacked = new DirectoryInfo(layout.Archive).GetFiles("*-" + ModSuiteLayoutPolicy.PreLayoutArchiveKind + ".zip").Length > 0;
                string zipPath = Path.Combine(layout.Archive, ModSuiteLayoutPolicy.ArchiveFileName(localNow, ModSuiteLayoutPolicy.PreLayoutArchiveKind));
                if (!alreadyPacked && !ModSuiteZip.TryCreate(zipPath, entries, out string zipError))
                {
                    throw new IOException("the copy of the old folders could not be packed: " + zipError);
                }
            }

            // 3. From here the new folder is the one that counts, even if removing the old copies is interrupted.
            EnsureReadme(layout);
            File.WriteAllText(marker, "BlueSage QoL folder layout 1, written " + localNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "\r\n", new UTF8Encoding(false));
            if (!hasOldFiles)
            {
                RemoveOldLayout(bepinexRoot, configRoot, copies, extras, oldSessions, log);
                return new ModSuiteMigrationResult(true, false, "new folder created");
            }

            // 4. Best effort; anything left behind is removed at the next start.
            RemoveOldLayout(bepinexRoot, configRoot, copies, extras, oldSessions, log);
            return new ModSuiteMigrationResult(true, true, "moved " + copies.Count + " files" + (droppedSessions > 0 ? "; " + droppedSessions + " old audit sessions past the limits were not kept" : string.Empty));
        }
        catch (Exception ex)
        {
            // Nothing old was removed: the old places stay in use for this session and the move is retried next start.
            log?.Invoke("BlueSage folder move did not finish (" + ex.GetType().Name + ": " + ex.Message + "); old files are untouched and stay in use this session.");
            return new ModSuiteMigrationResult(false, false, "move retried next start");
        }
    }

    private static List<KeyValuePair<string, string>> BuildCopyList(string bepinexRoot, string configRoot, ModSuiteLayout layout)
    {
        List<KeyValuePair<string, string>> copies = new List<KeyValuePair<string, string>>();
        string workRelax = Path.Combine(configRoot, ModSuiteLayoutPolicy.LegacyWorkRelaxFolderName);
        if (Directory.Exists(workRelax))
        {
            foreach (FileInfo file in new DirectoryInfo(workRelax).GetFiles("p_*"))
            {
                if (ModSuiteLayoutPolicy.IsLegacyWorkRelaxFileName(file.Name))
                {
                    copies.Add(new KeyValuePair<string, string>(file.FullName, Path.Combine(layout.Trackers, ModSuiteLayoutPolicy.WorkRelaxFilePrefix + file.Name)));
                }
            }
        }

        string audit = Path.Combine(bepinexRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName);
        AddIfExists(copies, Path.Combine(audit, ModSuiteLayoutPolicy.LegacyHostJournalFolderName, ModSuiteLayoutPolicy.LegacyHostJournalFileName), layout.HostJournalPath);
        AddIfExists(copies, Path.Combine(audit, ModSuiteLayoutPolicy.LegacyRunMarkerFileName), layout.RunMarkerPath);
        string attestation = Path.Combine(configRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName, ModSuiteLayoutPolicy.LegacyAttestationFileName);
        AddIfExists(copies, attestation, layout.AttestationPath);
        AddIfExists(copies, attestation + ".bak", layout.AttestationPath + ".bak");
        return copies;
    }

    private static List<string> BuildExtraZipSources(string bepinexRoot)
    {
        List<string> extras = new List<string>();
        string previous = Path.Combine(bepinexRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName, ModSuiteLayoutPolicy.LegacyHostJournalFolderName, ModSuiteLayoutPolicy.LegacyHostJournalFileName + ".prev");
        if (File.Exists(previous))
        {
            extras.Add(previous);
        }

        return extras;
    }

    private static List<DirectoryInfo> OldSessionFolders(string bepinexRoot)
    {
        string audit = Path.Combine(bepinexRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName);
        return Directory.Exists(audit) ? new DirectoryInfo(audit).GetDirectories("session-*").ToList() : new List<DirectoryInfo>();
    }

    private static void AddIfExists(List<KeyValuePair<string, string>> copies, string source, string destination)
    {
        if (File.Exists(source))
        {
            copies.Add(new KeyValuePair<string, string>(source, destination));
        }
    }

    // Zip entry names say where the file used to live, relative to BepInEx (config files under "config/").
    private static KeyValuePair<string, string> Entry(string bepinexRoot, string configRoot, string source)
    {
        string full = Path.GetFullPath(source);
        string config = Path.GetFullPath(configRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string bepinex = Path.GetFullPath(bepinexRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string relative = full.StartsWith(config, StringComparison.OrdinalIgnoreCase) ? "config/" + full.Substring(config.Length)
            : full.StartsWith(bepinex, StringComparison.OrdinalIgnoreCase) ? full.Substring(bepinex.Length)
            : Path.GetFileName(full);
        return new KeyValuePair<string, string>(relative.Replace('\\', '/'), full);
    }

    // Exact files and folders the old QoL created, each checked against the owned-path allow-list first.
    private static int RemoveOldLayout(string bepinexRoot, string configRoot, List<KeyValuePair<string, string>> copies, List<string> extras, List<DirectoryInfo> oldSessions, Action<string>? log)
    {
        int removed = 0;
        foreach (string file in copies.Select(copy => copy.Key).Concat(extras))
        {
            if (TryDeleteOwnedFile(file, bepinexRoot, configRoot))
            {
                removed++;
            }
        }

        foreach (DirectoryInfo session in oldSessions)
        {
            if (ModSuiteLayoutPolicy.IsDeletableOwnedPath(session.FullName, bepinexRoot, configRoot))
            {
                try
                {
                    session.Delete(true);
                    removed++;
                }
                catch (Exception ex)
                {
                    log?.Invoke("An old BlueSage audit folder could not be removed yet (" + ex.GetType().Name + "); it will be tried again next start.");
                }
            }
        }

        string audit = Path.Combine(bepinexRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName);
        TryDeleteOwnedFile(Path.Combine(audit, ModSuiteLayoutPolicy.LegacyRunMarkerFileName) + ".tmp", bepinexRoot, configRoot);
        TryDeleteOwnedFile(Path.Combine(configRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName, ModSuiteLayoutPolicy.LegacyAttestationFileName) + ".tmp", bepinexRoot, configRoot);
        string workRelax = Path.Combine(configRoot, ModSuiteLayoutPolicy.LegacyWorkRelaxFolderName);
        if (Directory.Exists(workRelax))
        {
            foreach (FileInfo temp in new DirectoryInfo(workRelax).GetFiles("p_*.json.tmp"))
            {
                TryDeleteOwnedFile(temp.FullName, bepinexRoot, configRoot);
            }
        }

        TryDeleteEmptyOwnedDirectory(Path.Combine(audit, ModSuiteLayoutPolicy.LegacyHostJournalFolderName), bepinexRoot, configRoot);
        TryDeleteEmptyOwnedDirectory(audit, bepinexRoot, configRoot);
        TryDeleteEmptyOwnedDirectory(Path.Combine(configRoot, ModSuiteLayoutPolicy.LegacyAuditFolderName), bepinexRoot, configRoot);
        TryDeleteEmptyOwnedDirectory(workRelax, bepinexRoot, configRoot);
        return removed;
    }

    private static bool TryDeleteOwnedFile(string path, string bepinexRoot, string configRoot)
    {
        if (!File.Exists(path) || !ModSuiteLayoutPolicy.IsDeletableOwnedPath(path, bepinexRoot, configRoot))
        {
            return false;
        }

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDeleteEmptyOwnedDirectory(string path, string bepinexRoot, string configRoot)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any() && ModSuiteLayoutPolicy.IsDeletableOwnedPath(path, bepinexRoot, configRoot))
            {
                Directory.Delete(path);
            }
        }
        catch (Exception)
        {
            // Left for the next start.
        }
    }

    private static void EnsureReadme(ModSuiteLayout layout)
    {
        string readme = Path.Combine(layout.Root, ModSuiteLayoutPolicy.ReadmeFileName);
        if (!File.Exists(readme) || File.ReadAllText(readme) != ReadmeText)
        {
            Directory.CreateDirectory(layout.Root);
            File.WriteAllText(readme, ReadmeText, new UTF8Encoding(false));
        }
    }

    internal static bool SameBytes(string first, string second)
    {
        if (!File.Exists(first) || !File.Exists(second) || new FileInfo(first).Length != new FileInfo(second).Length)
        {
            return false;
        }

        using (SHA256 sha = SHA256.Create())
        {
            byte[] a;
            byte[] b;
            using (FileStream stream = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                a = sha.ComputeHash(stream);
            }

            using (FileStream stream = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                b = sha.ComputeHash(stream);
            }

            return a.SequenceEqual(b);
        }
    }

    internal static long FolderBytes(DirectoryInfo directory)
    {
        try
        {
            return directory.Exists ? directory.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length) : 0L;
        }
        catch (Exception)
        {
            return 0L;
        }
    }
}

internal static class ModSuiteZip
{
    // Writes <zip>.tmp, reopens it to check every entry's size, then moves it into place. Never overwrites a zip.
    internal static bool TryCreate(string zipPath, IList<KeyValuePair<string, string>> entries, out string error)
    {
        error = string.Empty;
        string temp = zipPath + ".tmp";
        try
        {
            if (File.Exists(zipPath))
            {
                throw new IOException("an archive with this name already exists");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(zipPath) ?? ".");
            Dictionary<string, long> expected = new Dictionary<string, long>(StringComparer.Ordinal);
            using (FileStream output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (ZipArchive zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                foreach (KeyValuePair<string, string> entry in entries)
                {
                    string name = entry.Key;
                    for (int suffix = 2; expected.ContainsKey(name); suffix++)
                    {
                        name = entry.Key + "." + suffix;
                    }

                    ZipArchiveEntry zipEntry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    long length;
                    using (FileStream input = new FileStream(entry.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (Stream target = zipEntry.Open())
                    {
                        input.CopyTo(target);
                        length = input.Length;
                    }

                    expected[name] = length;
                }
            }

            using (FileStream check = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (ZipArchive zip = new ZipArchive(check, ZipArchiveMode.Read))
            {
                if (zip.Entries.Count != expected.Count || zip.Entries.Any(entry => !expected.TryGetValue(entry.FullName, out long length) || entry.Length != length))
                {
                    throw new IOException("the packed copy did not match the originals");
                }
            }

            File.Move(temp, zipPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception)
            {
            }

            return false;
        }
    }
}

internal static class ModSuiteRetention
{
    // Raw session folders past the limits are packed into archive/<session>-session-logs.zip, then removed; zips
    // past their own limits are removed. Runs off the main thread at start; never touches the live session.
    internal static string Run(ModSuiteLayout layout, string? currentSessionDirectory, long nowUtcTicks, long ceilingBytes = ModSuiteLayoutPolicy.FolderCeilingBytes)
    {
        if (layout.LegacyMode || !Directory.Exists(layout.Root))
        {
            return "skipped";
        }

        int archived = 0;
        int deleted = 0;
        string current = string.IsNullOrWhiteSpace(currentSessionDirectory) ? string.Empty : Path.GetFullPath(currentSessionDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (Directory.Exists(layout.Logs))
        {
            Dictionary<string, DirectoryInfo> sessions = new Dictionary<string, DirectoryInfo>(StringComparer.Ordinal);
            List<ModSuiteItem> items = new List<ModSuiteItem>();
            foreach (DirectoryInfo session in new DirectoryInfo(layout.Logs).GetDirectories())
            {
                if (!ModSuiteLayoutPolicy.IsSessionFolderName(session.Name) || !ModSuiteLayoutPolicy.TryParseStamp(session.Name.Substring(0, 17), out DateTime started))
                {
                    continue;
                }

                sessions[session.Name] = session;
                bool isCurrent = string.Equals(Path.GetFullPath(session.FullName).TrimEnd(Path.DirectorySeparatorChar), current, StringComparison.OrdinalIgnoreCase);
                items.Add(new ModSuiteItem(session.Name, started.ToUniversalTime().Ticks, ModSuiteMigration.FolderBytes(session), isCurrent));
            }

            foreach (string name in ModSuiteLayoutPolicy.SessionsToArchive(items, nowUtcTicks))
            {
                DirectoryInfo session = sessions[name];
                List<KeyValuePair<string, string>> entries = session.GetFiles("*", SearchOption.AllDirectories)
                    .Select(file => new KeyValuePair<string, string>(name + "/" + file.FullName.Substring(session.FullName.Length + 1).Replace('\\', '/'), file.FullName))
                    .ToList();
                string zip = Path.Combine(layout.Archive, name.Substring(0, 17) + "-" + ModSuiteLayoutPolicy.SessionLogsArchiveKind + ".zip");
                bool packed = entries.Count == 0 || File.Exists(zip) || ModSuiteZip.TryCreate(zip, entries, out _);
                if (packed && ModSuiteLayoutPolicy.IsDeletableOwnedPath(session.FullName, layout.BepInExRoot, layout.ConfigRoot))
                {
                    try
                    {
                        session.Delete(true);
                        archived++;
                    }
                    catch (Exception)
                    {
                        // Retried next start.
                    }
                }
            }
        }

        if (Directory.Exists(layout.Archive))
        {
            Dictionary<string, FileInfo> zips = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
            List<ModSuiteItem> items = new List<ModSuiteItem>();
            foreach (FileInfo file in new DirectoryInfo(layout.Archive).GetFiles())
            {
                // Security review: only stale leftovers; a journal page being packed right now writes its own .tmp.
                if (file.Name.EndsWith(".zip.tmp", StringComparison.Ordinal) && file.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-10) &&
                    ModSuiteLayoutPolicy.IsDeletableOwnedPath(file.FullName, layout.BepInExRoot, layout.ConfigRoot))
                {
                    try { file.Delete(); } catch (Exception) { }
                    continue;
                }

                if (!file.Name.EndsWith(".zip", StringComparison.Ordinal) || file.Name.Length < 17 || !ModSuiteLayoutPolicy.TryParseStamp(file.Name.Substring(0, 17), out DateTime created))
                {
                    continue;
                }

                zips[file.Name] = file;
                items.Add(new ModSuiteItem(file.Name, created.ToUniversalTime().Ticks, file.Length));
            }

            foreach (string name in ModSuiteLayoutPolicy.ArchivesToDelete(items, nowUtcTicks))
            {
                FileInfo file = zips[name];
                if (ModSuiteLayoutPolicy.IsDeletableOwnedPath(file.FullName, layout.BepInExRoot, layout.ConfigRoot))
                {
                    try
                    {
                        file.Delete();
                        deleted++;
                    }
                    catch (Exception)
                    {
                        // Retried next start.
                    }
                }
            }
        }

        int ceilingRemoved = EnforceFolderCeiling(layout, current, ceilingBytes);
        return "archived " + archived + " sessions, removed " + deleted + " old zips" +
               (ceilingRemoved > 0 ? ", removed " + ceilingRemoved + " oldest items over the size ceiling" : string.Empty);
    }

    // Final safeguard (Blue 2026-10-23): the whole folder never stays over the ceiling. Evictable = archive zips and
    // finished session folders, oldest first; everything else (trackers, README, marker) counts but is never removed.
    private static int EnforceFolderCeiling(ModSuiteLayout layout, string current, long ceilingBytes)
    {
        long folder = ModSuiteMigration.FolderBytes(new DirectoryInfo(layout.Root));
        if (folder <= ceilingBytes)
        {
            return 0;
        }

        Dictionary<string, FileSystemInfo> paths = new Dictionary<string, FileSystemInfo>(StringComparer.Ordinal);
        List<ModSuiteItem> items = new List<ModSuiteItem>();
        if (Directory.Exists(layout.Archive))
        {
            foreach (FileInfo zip in new DirectoryInfo(layout.Archive).GetFiles("*.zip"))
            {
                if (zip.Name.Length >= 17 && ModSuiteLayoutPolicy.TryParseStamp(zip.Name.Substring(0, 17), out DateTime created))
                {
                    string name = "archive/" + zip.Name;
                    paths[name] = zip;
                    items.Add(new ModSuiteItem(name, created.ToUniversalTime().Ticks, zip.Length));
                }
            }
        }

        if (Directory.Exists(layout.Logs))
        {
            foreach (DirectoryInfo session in new DirectoryInfo(layout.Logs).GetDirectories())
            {
                if (ModSuiteLayoutPolicy.IsSessionFolderName(session.Name) && ModSuiteLayoutPolicy.TryParseStamp(session.Name.Substring(0, 17), out DateTime started))
                {
                    string name = "logs/" + session.Name;
                    paths[name] = session;
                    bool isCurrent = string.Equals(Path.GetFullPath(session.FullName).TrimEnd(Path.DirectorySeparatorChar), current, StringComparison.OrdinalIgnoreCase);
                    items.Add(new ModSuiteItem(name, started.ToUniversalTime().Ticks, ModSuiteMigration.FolderBytes(session), isCurrent));
                }
            }
        }

        long fixedBytes = folder - items.Sum(item => item.Bytes);
        int removed = 0;
        foreach (string name in ModSuiteLayoutPolicy.FolderCeilingEvictions(items, fixedBytes, ceilingBytes))
        {
            FileSystemInfo target = paths[name];
            if (!ModSuiteLayoutPolicy.IsDeletableOwnedPath(target.FullName, layout.BepInExRoot, layout.ConfigRoot))
            {
                continue;
            }

            try
            {
                if (target is DirectoryInfo directory)
                {
                    directory.Delete(true);
                }
                else
                {
                    target.Delete();
                }

                removed++;
            }
            catch (Exception)
            {
                // Retried at the next tidy-up.
            }
        }

        return removed;
    }
}
