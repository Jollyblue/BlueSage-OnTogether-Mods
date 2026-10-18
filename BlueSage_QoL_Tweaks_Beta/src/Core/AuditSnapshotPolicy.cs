using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal static class AuditSnapshotPolicy
{
    public const int MaxRetainedSnapshots = 8;

    public static bool TryCreate(
        string sourceLogPath,
        string sessionDirectory,
        DateTime utcNow,
        string nonce,
        out string snapshotDirectory,
        out long capturedBytes,
        out string error)
    {
        snapshotDirectory = string.Empty;
        capturedBytes = 0;
        error = string.Empty;
        string temporaryPath = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(sourceLogPath) || !File.Exists(sourceLogPath))
                throw new FileNotFoundException("The true BepInEx LogOutput.log was not found.", sourceLogPath);
            if (string.IsNullOrWhiteSpace(sessionDirectory))
                throw new ArgumentException("The audit session directory is unavailable.", nameof(sessionDirectory));

            string safeNonce = NormalizeNonce(nonce);
            string sessionRoot = Path.GetFullPath(sessionDirectory);
            string snapshotsRoot = Path.Combine(sessionRoot, "snapshots");
            Directory.CreateDirectory(snapshotsRoot);
            string token = "snapshot-" + utcNow.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + "-" + safeNonce;
            snapshotDirectory = Path.Combine(snapshotsRoot, token);
            Directory.CreateDirectory(snapshotDirectory);

            string finalPath = Path.Combine(snapshotDirectory, "LogOutput.log");
            temporaryPath = Path.Combine(snapshotDirectory, "LogOutput.log.tmp");
            using (var source = new FileStream(sourceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan))
            {
                long boundary = source.Length;
                using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                {
                    byte[] buffer = new byte[64 * 1024];
                    while (capturedBytes < boundary)
                    {
                        int wanted = (int)Math.Min(buffer.Length, boundary - capturedBytes);
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
            PruneSnapshots(snapshotsRoot, snapshotDirectory);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            TryDeleteFile(temporaryPath);
            TryDeleteEmptyDirectory(snapshotDirectory);
            snapshotDirectory = string.Empty;
            capturedBytes = 0;
            return false;
        }
    }

    private static string NormalizeNonce(string nonce)
    {
        string value = new string((nonce ?? string.Empty).Where(char.IsLetterOrDigit).Take(16).ToArray()).ToLowerInvariant();
        if (value.Length < 4) throw new ArgumentException("Snapshot nonce must contain at least four letters or digits.", nameof(nonce));
        return value;
    }

    private static void PruneSnapshots(string snapshotsRoot, string keepDirectory)
    {
        string root = Path.GetFullPath(snapshotsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string keep = Path.GetFullPath(keepDirectory);
        var directories = new DirectoryInfo(snapshotsRoot).GetDirectories("snapshot-*")
            .OrderByDescending(directory => directory.Name, StringComparer.Ordinal)
            .ToList();

        foreach (DirectoryInfo directory in directories.Skip(MaxRetainedSnapshots))
        {
            string candidate = Path.GetFullPath(directory.FullName);
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate, keep, StringComparison.OrdinalIgnoreCase))
                continue;
            directory.Delete(true);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try { if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path); }
        catch { }
    }
}
