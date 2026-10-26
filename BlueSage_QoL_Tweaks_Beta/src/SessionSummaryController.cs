#nullable enable
using System;
using System.IO;
using BepInEx.Logging;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

// Round 5 Stage S (advisor, 2026-10-22): one [BlueSageSession] line at quit and every 6 hours. The counters are
// fed by the code that already does the work (sweeps, journal rows, ledger writes, the coverage roster read)
// and by our own ManualLogSource's events for warnings and errors; nothing here scans, polls or allocates per
// frame. Host-only numbers stay "n/a" until this client has hosted. No ids, no lobby name.
internal static class SessionSummaryController
{
    private static long _startUtcTicks;
    private static long _lastWriteUtcTicks;
    private static double _heapStartMiB;
    private static bool _hosted;
    private static int _peakPlayers;
    private static int _joins;
    private static int _lastSteamCount = -1;
    private static int _sweeps;
    private static int _journalActions;
    private static int _journalStatus;
    private static int _warnings;
    private static int _errors;

    internal static void Start(long nowUtcTicks)
    {
        if (_startUtcTicks != 0) return;
        _startUtcTicks = nowUtcTicks;
        _lastWriteUtcTicks = nowUtcTicks;
        _heapStartMiB = GC.GetTotalMemory(false) / 1048576d;
    }

    // Subscribed once to Plugin.Log.LogEvent, so only this plugin's own lines are counted.
    internal static void OnLogEvent(object sender, LogEventArgs args)
    {
        if (args == null) return;
        if ((args.Level & LogLevel.Warning) != 0) _warnings++;
        else if ((args.Level & (LogLevel.Error | LogLevel.Fatal)) != 0) _errors++;
    }

    internal static void NoteSweep()
    {
        _sweeps++;
    }

    internal static void NoteJournalRow(bool action)
    {
        _hosted = true;
        if (action) _journalActions++;
        else _journalStatus++;
    }

    // Fed from the coverage roster read while hosting: peak is the largest Steam member count seen, joins is the
    // sum of every increase (the same arithmetic the audit used on the journal).
    internal static void NoteRoster(int steamCount)
    {
        _hosted = true;
        if (steamCount > _peakPlayers) _peakPlayers = steamCount;
        if (_lastSteamCount >= 0 && steamCount > _lastSteamCount) _joins += steamCount - _lastSteamCount;
        _lastSteamCount = steamCount;
    }

    internal static void Tick(long nowUtcTicks)
    {
        Start(nowUtcTicks);
        if (!SessionSummaryPolicy.IsDue(_lastWriteUtcTicks, nowUtcTicks)) return;
        _lastWriteUtcTicks = nowUtcTicks;
        Write("6h", nowUtcTicks);
    }

    internal static void OnShutdown(string trigger)
    {
        if (_startUtcTicks == 0) return;
        Write(trigger, DateTime.UtcNow.Ticks);
    }

    private static void Write(string trigger, long nowUtcTicks)
    {
        try
        {
            string line = SessionSummaryPolicy.Format(
                trigger,
                (nowUtcTicks - _startUtcTicks) / (double)TimeSpan.TicksPerMinute,
                _hosted,
                _peakPlayers,
                _joins,
                _sweeps,
                WorkRelaxLedgerStore.LastWrittenBytes,
                WorkRelaxLedgerStore.WriteCount,
                _journalActions,
                _journalStatus,
                _heapStartMiB,
                GC.GetTotalMemory(false) / 1048576d,
                _warnings,
                _errors,
                journalBytes: FileBytes(HostJournalController.FilePath) + FileBytes(HostJournalController.FilePath + ".prev"),
                folderBytes: ModSuiteMigration.FolderBytes(new DirectoryInfo(ModSuitePaths.Current.Root)));
            Plugin.Log?.LogInfo(line);
        }
        catch (Exception)
        {
            // The summary is a convenience; it never takes the session down.
        }
    }

    // Round 5d: the line also says how big our own files are (a handful of files, read every 6 h and at quit).
    private static long FileBytes(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
        catch (Exception)
        {
            return 0L;
        }
    }
}
