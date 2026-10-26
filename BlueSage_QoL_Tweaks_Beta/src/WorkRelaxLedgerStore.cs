#nullable enable

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

internal sealed class WorkRelaxLedgerStore
{
    // Round 5 Stage S: the session summary reports ledger size and write count; the store counts its own writes.
    internal static long LastWrittenBytes { get; private set; }
    internal static int WriteCount { get; private set; }

    private const string LedgerDirectoryName = "BlueSageWorkRelax";
    private readonly object _writerGate = new();
    private readonly ManualResetEvent _backgroundIdle = new(true);
    private readonly string _path;
    private readonly string _profileKey;
    private int _writeInFlight;

    private WorkRelaxLedgerStore(string ledgerDirectory, string profileKey, string fileNamePrefix = "")
    {
        if (string.IsNullOrWhiteSpace(ledgerDirectory))
        {
            throw new ArgumentException("A ledger directory is required.", nameof(ledgerDirectory));
        }

        if (!IsValidProfileKey(profileKey))
        {
            throw new ArgumentException("The profile key is invalid.", nameof(profileKey));
        }

        _profileKey = profileKey;
        // Round 5d: in BlueSageQoLModSuite/trackers the file is "work-relax-p_<id>.json"; the profile check still
        // compares the ledger's own key, so the prefix only names the file for players.
        _path = Path.Combine(ledgerDirectory, (fileNamePrefix ?? string.Empty) + profileKey + ".json");
    }

    public static WorkRelaxLedgerStore FromConfigPath(string configPath, string profileKey)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("A configuration path is required.", nameof(configPath));
        }

        return new WorkRelaxLedgerStore(Path.Combine(configPath, LedgerDirectoryName), profileKey);
    }

    internal static WorkRelaxLedgerStore ForLedgerDirectory(string ledgerDirectory, string profileKey, string fileNamePrefix = "")
    {
        return new WorkRelaxLedgerStore(ledgerDirectory, profileKey, fileNamePrefix);
    }

    public WorkRelaxLedgerLoadResult Load()
    {
        lock (_writerGate)
        {
            string primaryError = string.Empty;
            if (TryLoad(_path, out WorkRelaxLedger? primary, out primaryError))
            {
                return new WorkRelaxLedgerLoadResult(primary, false, string.Empty);
            }

            string backupPath = _path + ".bak";
            if (!TryLoad(backupPath, out WorkRelaxLedger? backup, out _))
            {
                return new WorkRelaxLedgerLoadResult(null, false, primaryError);
            }

            backup!.Recovery.RecoveryCount++;
            RepairPrimaryFromBackup(backup);
            return new WorkRelaxLedgerLoadResult(backup, true, primaryError);
        }
    }

    // Synchronous save with a disk sync. Round 4 Stage G: used only at shutdown (and by tests); routine saves go
    // through SaveInBackground so no file work runs on the game's autosave frame.
    public void Save(WorkRelaxLedger ledger)
    {
        Save(ledger, flushToDisk: true);
    }

    internal bool IsWriteInFlight => Volatile.Read(ref _writeInFlight) == 1;

    // Round 4 Stage G (Marbles, 2026-10-20): serialize and write a deep-cloned snapshot on a worker thread, without a
    // disk sync. Single-flight: returns false while an earlier write is still running, and the caller keeps its
    // dirty flag. The completion callback runs on the worker thread with the error, or null on success.
    internal bool SaveInBackground(WorkRelaxLedger snapshot, Action<Exception?>? completed = null)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (Interlocked.CompareExchange(ref _writeInFlight, 1, 0) != 0)
        {
            return false;
        }

        _backgroundIdle.Reset();
        bool queued;
        try
        {
            queued = ThreadPool.QueueUserWorkItem(_ =>
            {
                Exception? error = null;
                try
                {
                    Save(snapshot, flushToDisk: false);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
                finally
                {
                    Interlocked.Exchange(ref _writeInFlight, 0);
                    _backgroundIdle.Set();
                }

                try
                {
                    completed?.Invoke(error);
                }
                catch
                {
                    // A failing callback must never take the worker down.
                }
            });
        }
        catch
        {
            queued = false;
        }

        if (!queued)
        {
            Interlocked.Exchange(ref _writeInFlight, 0);
            _backgroundIdle.Set();
        }

        return queued;
    }

    internal bool WaitForBackgroundWrite(int timeoutMilliseconds)
    {
        return _backgroundIdle.WaitOne(Math.Max(0, timeoutMilliseconds));
    }

    private void Save(WorkRelaxLedger ledger, bool flushToDisk)
    {
        if (ledger == null)
        {
            throw new ArgumentNullException(nameof(ledger));
        }

        lock (_writerGate)
        {
            // Round 5d review: compare the store's own key, not the file name (which carries "work-relax-" in trackers/).
            if (!string.Equals(ledger.ProfileKey, _profileKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The ledger belongs to a different local profile.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            ledger.Recovery.LastSuccessfulSaveUtcTicks = DateTime.UtcNow.Ticks;
            string temporaryPath = _path + ".tmp";
            string serialized = WorkRelaxLedgerCodec.Serialize(ledger);
            WriteFlushed(temporaryPath, serialized, flushToDisk);
            LastWrittenBytes = System.Text.Encoding.UTF8.GetByteCount(serialized);
            WriteCount++;

            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, _path + ".bak", true);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
    }

    private bool TryLoad(string path, out WorkRelaxLedger? ledger, out string error)
    {
        ledger = null;
        error = string.Empty;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            ledger = WorkRelaxLedgerCodec.Deserialize(File.ReadAllText(path, System.Text.Encoding.UTF8));
            if (!string.Equals(ledger.ProfileKey, _profileKey, StringComparison.Ordinal))
            {
                ledger = null;
                error = "The ledger belongs to a different local profile.";
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is SerializationException || exception is ArgumentException)
        {
            error = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private void RepairPrimaryFromBackup(WorkRelaxLedger backup)
    {
        string temporaryPath = _path + ".tmp";
        WriteFlushed(temporaryPath, WorkRelaxLedgerCodec.Serialize(backup), flushToDisk: true);
        if (File.Exists(_path))
        {
            File.Replace(temporaryPath, _path, null, true);
        }
        else
        {
            File.Move(temporaryPath, _path);
        }
    }

    private static void WriteFlushed(string path, string json, bool flushToDisk)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        if (flushToDisk)
        {
            stream.Flush(true);
        }
    }

    private static bool IsValidProfileKey(string profileKey)
    {
        if (profileKey == null || profileKey.Length != 66 || !profileKey.StartsWith("p_", StringComparison.Ordinal))
        {
            return false;
        }

        for (int index = 2; index < profileKey.Length; index++)
        {
            char character = profileKey[index];
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class WorkRelaxLedgerLoadResult
{
    public WorkRelaxLedgerLoadResult(
        WorkRelaxLedger? ledger,
        bool recoveredFromBackup,
        string primaryError)
    {
        Ledger = ledger;
        RecoveredFromBackup = recoveredFromBackup;
        PrimaryError = primaryError ?? string.Empty;
    }

    public WorkRelaxLedger? Ledger { get; }

    public bool RecoveredFromBackup { get; }

    public string PrimaryError { get; }
}
