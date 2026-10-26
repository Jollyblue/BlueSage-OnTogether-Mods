using System;
using System.IO;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

// Round 5d: a snapshot is one file in the session folder, "<yyyy-MM-dd_HHmmss>-snapshot-game-log.log" (local time),
// holding the newest AuditSnapshotMaxBytes of the live BepInEx log from a whole line. Two are kept per session.
internal static class AuditSnapshotPolicy
{
    public const int MaxRetainedSnapshots = LocalFileBudgetPolicy.AuditSnapshotsPerSession;
    private const string SnapshotSuffix = "-snapshot-game-log.log";

    public static bool TryCreate(
        string sourceLogPath,
        string sessionDirectory,
        DateTime localNow,
        out string snapshotPath,
        out long capturedBytes,
        out string error)
    {
        snapshotPath = string.Empty;
        capturedBytes = 0;
        error = string.Empty;
        string temporaryPath = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(sourceLogPath) || !File.Exists(sourceLogPath))
                throw new FileNotFoundException("The true BepInEx LogOutput.log was not found.", sourceLogPath);
            if (string.IsNullOrWhiteSpace(sessionDirectory))
                throw new ArgumentException("The audit session directory is unavailable.", nameof(sessionDirectory));

            string sessionRoot = Path.GetFullPath(sessionDirectory);
            Directory.CreateDirectory(sessionRoot);
            string finalPath = Path.Combine(sessionRoot, ModSuiteLayoutPolicy.SnapshotFileName(localNow));
            if (File.Exists(finalPath))
                throw new IOException("A snapshot was already taken this second; try again in a moment.");
            temporaryPath = finalPath + ".tmp";
            using (var source = new FileStream(sourceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan))
            {
                long boundary = source.Length;
                long start = Math.Max(0L, boundary - LocalFileBudgetPolicy.AuditSnapshotMaxBytes);
                source.Position = start;
                if (start > 0)
                {
                    int next;
                    while ((next = source.ReadByte()) >= 0 && next != '\n')
                    {
                    }
                }

                using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                {
                    byte[] buffer = new byte[64 * 1024];
                    while (source.Position < boundary)
                    {
                        int wanted = (int)Math.Min(buffer.Length, boundary - source.Position);
                        int read = source.Read(buffer, 0, wanted);
                        if (read <= 0)
                            throw new EndOfStreamException("The live BepInEx log changed before its captured boundary could be copied.");
                        destination.Write(buffer, 0, read);
                        capturedBytes += read;
                    }

                    destination.Flush(true);
                }
            }

            File.Move(temporaryPath, finalPath);
            snapshotPath = finalPath;
            PruneSnapshots(sessionRoot, finalPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            TryDeleteFile(temporaryPath);
            snapshotPath = string.Empty;
            capturedBytes = 0;
            return false;
        }
    }

    private static void PruneSnapshots(string sessionRoot, string keepPath)
    {
        string root = sessionRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var snapshots = new DirectoryInfo(sessionRoot).GetFiles("*" + SnapshotSuffix)
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .ToList();

        foreach (FileInfo file in snapshots.Skip(MaxRetainedSnapshots))
        {
            string candidate = Path.GetFullPath(file.FullName);
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate, keepPath, StringComparison.OrdinalIgnoreCase))
                continue;
            file.Delete();
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
