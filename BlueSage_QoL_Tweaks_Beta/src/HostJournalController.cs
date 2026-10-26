using System;
using System.Collections.Generic;
using System.IO;
using BlueSage.QoLTweaks.Core;
using PurrNet;

namespace BlueSage.QoLTweaks;

// Round 4 Stage D (Blue, 2026-10-19): the host journal. Always on whenever the local player hosts; guests write
// nothing. Action rows come from every host action that passes TryExecuteHostAction (commands, the menu, the helper
// relay, and the routed vanilla Ban buttons), status rows from host health ticks and identity-coverage transitions,
// written on change plus a heartbeat. The ring feeds the Host Tools box; the JSONL file under the audit root is the
// record and the Discord steward's /lobby status reads it (steward re-wire: separate repo session).
internal static class HostJournalController
{
    private const string DefaultSurface = "command or menu";
    // Round 4 Stage H: two rings. Status rows arrive on every count change in a big lobby and used to evict the rare
    // action rows from one shared ring within minutes; actions are the record a host looks for.
    private static readonly HostJournalRing ActionRing = new HostJournalRing(100);
    private static readonly HostJournalRing StatusRing = new HostJournalRing(20);
    private static HostJournalRow _latestStatus;
    private static int _statusChangeCount;
    private static string _surface = DefaultSurface;
    private static string _noteName = string.Empty;
    private static string _notePurrNetId = string.Empty;
    private static string _noteLabel = string.Empty;
    private static string _lastStatusKey;
    private static long _lastStatusWriteUtcTicks;
    private static bool _lobbyValid;
    private static int _steamCount;
    private static int _steamLimit;
    private static int _liveRoster;
    private static int _transportVerified;
    private static string _lastHostHealth = "not checked yet";
    private static bool _fileFailureLogged;

    internal static string FilePath => ModSuitePaths.Current.HostJournalPath;

    internal static IReadOnlyList<HostJournalRow> SnapshotActionsNewestFirst() => ActionRing.SnapshotNewestFirst();

    internal static IReadOnlyList<HostJournalRow> SnapshotStatusNewestFirst() => StatusRing.SnapshotNewestFirst();

    internal static HostJournalRow LatestStatus => _latestStatus;

    internal static int StatusChangeCount => _statusChangeCount;

    // The surface a host action came from (ID card, player row, report, helper relay). Set around the call, always cleared.
    internal static void BeginSurface(string surface)
    {
        _surface = string.IsNullOrWhiteSpace(surface) ? DefaultSurface : surface;
    }

    internal static void EndSurface()
    {
        _surface = DefaultSurface;
    }

    // Target details become known inside TryBan/TryUnban; the row is written by TryExecuteHostAction right after.
    internal static void NoteTarget(string name, string purrNetId, string label)
    {
        _noteName = name ?? string.Empty;
        _notePurrNetId = purrNetId ?? string.Empty;
        _noteLabel = label ?? string.Empty;
    }

    // Not re-gated: TryExecuteHostAction already proved the caller owns the lobby (TryGetOwnedHostLobby) before it
    // got here, and a second, different host predicate could only lose rows for bans that did happen (advisor, D gate).
    internal static void RecordAction(string action, string issuerSteamId, string ownerSteamId, string targetSteamId, bool success, string result)
    {
        try
        {
            string role = string.Equals(issuerSteamId, ownerSteamId, StringComparison.Ordinal) ? "HOST" : "HELPER";
            HostJournalRow row = HostJournalRow.ForAction(DateTime.UtcNow.Ticks, issuerSteamId, role, _surface, action, targetSteamId, _noteName, _notePurrNetId, _noteLabel, success, result);
            ActionRing.Add(row);
            Write(row, flushToDisk: true);
            SessionSummaryController.NoteJournalRow(true);
        }
        finally
        {
            NoteTarget(string.Empty, string.Empty, string.Empty);
        }
    }

    // Identity coverage changed (PlayerIdentityEvidenceController logs it on change already). This is the ONE owner
    // of the counts and the limit on status rows: a second source for the Steam limit (the panel shows 111 where
    // Steam's raw max is 112, lesson lsn_6e59feed) would make the change key alternate and write rows for nothing.
    internal static void RecordCoverage(bool lobbyValid, int steamCount, int steamLimit, int liveRoster, int transportVerified)
    {
        _lobbyValid = lobbyValid;
        _steamCount = steamCount;
        _steamLimit = steamLimit;
        _liveRoster = liveRoster;
        _transportVerified = transportVerified;
        if (IsNetworkHostNow())
        {
            SessionSummaryController.NoteRoster(steamCount);
        }
        RecordStatus();
    }

    // Host health owns only the lobby-valid flag and the health text; the counts ride along from the coverage cache.
    internal static void RecordHostHealth(bool lobbyValid, string hostHealth)
    {
        _lobbyValid = lobbyValid;
        _lastHostHealth = hostHealth ?? string.Empty;
        RecordStatus();
    }

    private static void RecordStatus()
    {
        if (!IsNetworkHostNow())
        {
            return;
        }

        long now = DateTime.UtcNow.Ticks;
        HostJournalRow row = HostJournalRow.ForStatus(now, true, _lobbyValid, _steamCount, _steamLimit, _liveRoster, _transportVerified, _lastHostHealth);
        // Round 5d review: the Host Tools "Now:" line always shows the live counts; only the ring and file are gated.
        _latestStatus = row;
        string key = row.StatusKey();
        if (!HostJournalStatusPolicy.ShouldWrite(_lastStatusKey, key, _lastStatusWriteUtcTicks, now))
        {
            return;
        }

        bool changed = !string.Equals(_lastStatusKey, key, StringComparison.Ordinal);
        _lastStatusKey = key;
        _lastStatusWriteUtcTicks = now;
        if (changed)
        {
            _statusChangeCount++;
        }

        StatusRing.Add(row);
        Write(row, flushToDisk: false);
        SessionSummaryController.NoteJournalRow(false);
    }

    // "Whenever the local player hosts" (Blue): the network host, the same signal the ban router and
    // TryGetOwnedHostLobby use, not the stricter Steam-owner check that can fail closed on a bad Steam read.
    private static bool IsNetworkHostNow()
    {
        try
        {
            return NetworkManager.main != null && NetworkManager.main.isHost;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Action rows are the record and rare: they are forced to disk. Status rows can arrive dozens of times during a
    // join wave on the main thread, so they are appended without the per-row fsync (WS2: no hitches).
    private static void Write(HostJournalRow row, bool flushToDisk)
    {
        try
        {
            HostJournalFile.Append(FilePath, HostJournalFormat.ToJsonLine(row), HostJournalFile.DefaultMaxBytes, flushToDisk, ModSuitePaths.Current.HostJournalArchiveDirectory);
        }
        catch (Exception ex)
        {
            // The in-game box still has the row; the file failure is logged once per session, not per row.
            if (!_fileFailureLogged)
            {
                _fileFailureLogged = true;
                Plugin.Log?.LogWarning("Host journal file could not be written (" + ex.GetType().Name + ": " + ex.Message + "). The in-game journal box keeps working; later rows are not retried to disk this session.");
            }
        }
    }
}
