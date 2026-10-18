#nullable enable

using System;
using System.IO;
using System.Runtime.Serialization;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

internal sealed class WorkRelaxLedgerStore
{
    private const string LedgerDirectoryName = "BlueSageWorkRelax";
    private readonly object _writerGate = new();
    private readonly string _path;
    private readonly string _profileKey;

    private WorkRelaxLedgerStore(string ledgerDirectory, string profileKey)
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
        _path = Path.Combine(ledgerDirectory, profileKey + ".json");
    }

    public static WorkRelaxLedgerStore FromConfigPath(string configPath, string profileKey)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("A configuration path is required.", nameof(configPath));
        }

        return new WorkRelaxLedgerStore(Path.Combine(configPath, LedgerDirectoryName), profileKey);
    }

    internal static WorkRelaxLedgerStore ForLedgerDirectory(string ledgerDirectory, string profileKey)
    {
        return new WorkRelaxLedgerStore(ledgerDirectory, profileKey);
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

    public void Save(WorkRelaxLedger ledger)
    {
        if (ledger == null)
        {
            throw new ArgumentNullException(nameof(ledger));
        }

        lock (_writerGate)
        {
            string expectedProfileKey = Path.GetFileNameWithoutExtension(_path);
            if (!string.Equals(ledger.ProfileKey, expectedProfileKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The ledger belongs to a different local profile.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            ledger.Recovery.LastSuccessfulSaveUtcTicks = DateTime.UtcNow.Ticks;
            string temporaryPath = _path + ".tmp";
            WriteFlushed(temporaryPath, WorkRelaxLedgerCodec.Serialize(ledger));

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
        WriteFlushed(temporaryPath, WorkRelaxLedgerCodec.Serialize(backup));
        if (File.Exists(_path))
        {
            File.Replace(temporaryPath, _path, null, true);
        }
        else
        {
            File.Move(temporaryPath, _path);
        }
    }

    private static void WriteFlushed(string path, string json)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
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
