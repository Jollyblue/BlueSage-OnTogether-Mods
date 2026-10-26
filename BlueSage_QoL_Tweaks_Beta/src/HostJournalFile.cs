using System;
using System.IO;
using System.Text;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

// Round 4 Stage D: the host journal's local JSONL file. Append-only, one rotation at the size cap by rename (the
// previous file is kept once, never rewritten), and the folder is created on demand. Action rows are flushed to disk
// per row (a power loss costs at most the row being written); status rows are appended without the fsync because
// they can arrive dozens of times during a join wave on the main thread. No Unity types, so the net8 tests exercise
// the real I/O.
internal static class HostJournalFile
{
    internal const string FileName = "host-journal-v1.jsonl";
    internal const long DefaultMaxBytes = LocalFileBudgetPolicy.HostJournalMaxBytes;

    internal static bool Append(string path, string line, long maxBytes)
    {
        return Append(path, line, maxBytes, flushToDisk: true);
    }

    internal static bool Append(string path, string line, long maxBytes, bool flushToDisk)
    {
        return Append(path, line, maxBytes, flushToDisk, archiveDirectory: null);
    }

    // Returns true when the file was rotated before this row was written. Round 5d: with an archive folder, a full
    // page is packed whole into archive/<date_time>-host-journal.zip (readable with a double-click) and the journal
    // starts a fresh page; without one (old layout), or if packing fails, the previous page is kept once as .prev.
    internal static bool Append(string path, string line, long maxBytes, bool flushToDisk, string archiveDirectory)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] bytes = new UTF8Encoding(false).GetBytes(line + "\n");
        bool rotated = false;
        if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > maxBytes && TryArchivePage(path, archiveDirectory))
        {
            rotated = true;
        }
        else if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > maxBytes)
        {
            string previous = path + ".prev";
            if (File.Exists(previous))
            {
                File.Delete(previous);
            }

            if (new FileInfo(path).Length > maxBytes)
            {
                // Round 5d: a journal written under the old 2 MiB cap keeps only its newest maxBytes as .prev.
                WriteTail(path, previous, maxBytes);
                File.Delete(path);
            }
            else
            {
                File.Move(path, previous);
            }

            rotated = true;
        }

        using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            stream.Write(bytes, 0, bytes.Length);
            if (flushToDisk)
            {
                stream.Flush(true);
            }
        }

        return rotated;
    }

    private static bool TryArchivePage(string path, string archiveDirectory)
    {
        if (string.IsNullOrEmpty(archiveDirectory))
        {
            return false;
        }

        DateTime now = DateTime.Now;
        string zip = Path.Combine(archiveDirectory, ModSuiteLayoutPolicy.ArchiveFileName(now, ModSuiteLayoutPolicy.HostJournalArchiveKind));
        var entries = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
        {
            new System.Collections.Generic.KeyValuePair<string, string>(ModSuiteLayoutPolicy.Stamp(now) + "-" + ModSuiteLayoutPolicy.HostJournalFileName, path),
        };
        if (!ModSuiteZip.TryCreate(zip, entries, out _))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    // Copies the last maxBytes of source to destination, starting at a whole line.
    private static void WriteTail(string source, string destination, long maxBytes)
    {
        using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            input.Position = Math.Max(0L, input.Length - maxBytes);
            if (input.Position > 0)
            {
                int next;
                while ((next = input.ReadByte()) >= 0 && next != '\n')
                {
                }
            }

            input.CopyTo(output);
        }
    }
}
