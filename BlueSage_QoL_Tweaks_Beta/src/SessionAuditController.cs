using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BlueSage.QoLTweaks.Core;
using BlueSage.QoLTweaks.Patches;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class SessionAuditController : MonoBehaviour
{
    // Round 5d: ceilings live in LocalFileBudgetPolicy (0.2.6 allowed 12 sessions of 2 x 64 MiB mirror + 4 x 16 MiB chat).
    private const long MaxChatPartBytes = LocalFileBudgetPolicy.AuditChatPartBytes;
    private const long MaxBepInExPartBytes = LocalFileBudgetPolicy.AuditMirrorPartBytes;
    private const int MaxChatParts = LocalFileBudgetPolicy.AuditChatParts;
    private const int MaxBepInExParts = LocalFileBudgetPolicy.AuditMirrorParts;
    private const int MirrorChunkBytes = 512 * 1024;

    private static SessionAuditController _instance;
    private static PendingModerationSave _pendingModerationSave;
    private readonly Dictionary<string, NativeBanRecord> _nativeBanSnapshot = new Dictionary<string, NativeBanRecord>(StringComparer.Ordinal);
    private readonly AuditMirrorTransferBuffer _bepInExMirrorBuffer = new AuditMirrorTransferBuffer();
    private string _sessionDirectory = string.Empty;
    private string _sessionStamp = string.Empty;
    private string _chatPath = string.Empty;
    private string _bepInExPath = string.Empty;
    private StreamWriter _chatWriter;
    private FileStream _bepInExWriter;
    private long _bepInExSourceOffset;
    private int _chatPart = 1;
    private int _bepInExPart = 1;
    private float _nextMaintenanceAt;
    private bool _nativeBanSnapshotReady;
    private bool _initializationFailed;
    private bool _wasDisabled;
    private bool _suppressNextBanBaselineEvents;

    // Round 5d: BlueSageQoLModSuite/logs, one <yyyy-MM-dd_HHmmss>-session folder per game session (local time).
    internal static string AuditRoot => ModSuitePaths.Current.Logs;

    // While the folder move is unfinished the optional audit stays off for the session (no files in two places).
    internal static bool Enabled => Plugin.EnableLocalAuditExports != null && Plugin.EnableLocalAuditExports.Value && !ModSuitePaths.Current.LegacyMode;
    internal static string CurrentSessionDirectory => _instance?._sessionDirectory ?? AuditRoot;
    internal static string CurrentChatPath => _instance?._chatPath ?? Path.Combine(AuditRoot, "chat-audit-not-started.jsonl");
    internal static string CurrentBepInExPath => _instance?._bepInExPath ?? Path.Combine(AuditRoot, "bepinex-not-started.log");

    private void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
        // Round 5d: older sessions are packed into archive zips and old zips removed, off the main thread, whether
        // or not the audit is on; the session this run is writing is never touched.
        ModSuitePaths.RunRetentionInBackground(_sessionDirectory);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextMaintenanceAt) return;
        _nextMaintenanceAt = Time.unscaledTime + 1f;

        if (!Enabled)
        {
            PauseCapture();
            return;
        }

        EnsureInitialized();
        MirrorBepInExLog(MirrorChunkBytes);
        PollNativeBanStore();
    }

    private void OnDestroy()
    {
        if (Enabled)
        {
            MirrorBepInExLog(8 * 1024 * 1024);
            PollNativeBanStore();
        }
        CloseWriters();
        if (ReferenceEquals(_instance, this)) _instance = null;
    }

    internal static void RecordChat(
        string displayNameRaw,
        string messageRaw,
        bool isLocal,
        int senderIndex,
        AuthoritativeChatSenderContext senderContext)
    {
        if (!Enabled || _instance == null) return;
        _instance.EnsureInitialized();
        if (_instance._chatWriter == null) return;

        bool authorized = Plugin.CanUseLobbySafety(out bool isHost);
        string identityState = authorized ? "unverified-current-row" : "redacted-normal-client";
        string observerAuthority = authorized ? (isHost ? "host" : "helper") : "normal-client";
        string senderRole = string.Empty;
        string steamId = string.Empty;
        string rosterNameRaw = string.Empty;
        string persona = string.Empty;
        int auditedSenderIndex = senderIndex;
        if (authorized && senderContext?.ExactTupleCaptured == true)
        {
            identityState = "verified-current-row";
            senderRole = senderContext.Role;
            steamId = senderContext.SteamId;
            rosterNameRaw = senderContext.RosterNameRaw;
            persona = senderContext.SteamPersona;
            auditedSenderIndex = senderContext.NativeRowIndex;
        }

        _instance.AppendChatLine(SessionAuditPolicy.BuildChatRecord(
            UtcNow(),
            isLocal ? "local" : "global",
            auditedSenderIndex,
            identityState,
            observerAuthority,
            senderRole,
            steamId,
            displayNameRaw ?? string.Empty,
            rosterNameRaw,
            persona,
            messageRaw ?? string.Empty,
            CurrentAuditLobbyId()));
    }

    internal static void RecordNotification(string text)
    {
        if (!Enabled || _instance == null) return;
        _instance.EnsureInitialized();
        _instance.AppendChatLine(SessionAuditPolicy.BuildNotificationRecord(UtcNow(), text ?? string.Empty, CurrentAuditLobbyId()));
    }

    internal static void RecordRunMarker(
        string classification,
        string ageBucket,
        string markerPath)
    {
        if (!Enabled || _instance == null) return;
        _instance.EnsureInitialized();
        _instance.AppendChatLine(SessionAuditPolicy.BuildRunMarkerRecord(
            UtcNow(),
            classification ?? string.Empty,
            ageBucket ?? string.Empty,
            Path.GetFileName(markerPath ?? string.Empty)));
    }

    internal static void RecordIdentityResync(
        string source,
        string outcome,
        int targetedRows,
        int healedRows,
        int lockedRows,
        long durationMilliseconds,
        string reason)
    {
        if (!Enabled || _instance == null) return;
        _instance.EnsureInitialized();
        string boundedReason = (reason ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("|", "/")
            .Trim();
        if (boundedReason.Length > 160)
        {
            boundedReason = boundedReason.Substring(0, 160);
        }
        string receipt =
            "identity-resync" +
            "|source=" + (source ?? string.Empty) +
            "|outcome=" + (outcome ?? string.Empty) +
            "|targetedRows=" + Math.Max(0, targetedRows) +
            "|healedRows=" + Math.Max(0, healedRows) +
            "|lockedRows=" + Math.Max(0, lockedRows) +
            "|durationMilliseconds=" +
            Math.Max(0L, durationMilliseconds) +
            "|reason=" + boundedReason;
        _instance.AppendChatLine(
            SessionAuditPolicy.BuildNotificationRecord(
                UtcNow(),
                receipt,
                string.Empty));
    }

    internal static void RecordModeration(
        string action,
        string source,
        string outcome,
        string steamId,
        string displayNameRaw,
        string persona,
        string issuerSteamId = "",
        string incidentId = "",
        string targetRole = "")
    {
        if (!Enabled || _instance == null || !Plugin.CanUseLobbySafety(out _)) return;
        _instance.EnsureInitialized();
        if (_instance._nativeBanSnapshotReady && ModerationProtocolPolicy.IsSteamId64(steamId))
        {
            if (string.Equals(action, "ban-added", StringComparison.Ordinal))
                _instance._nativeBanSnapshot[steamId] = new NativeBanRecord(steamId, displayNameRaw);
            else if (string.Equals(action, "ban-removed", StringComparison.Ordinal))
                _instance._nativeBanSnapshot.Remove(steamId);
        }
        _instance.AppendChatLine(SessionAuditPolicy.BuildModerationRecord(
            UtcNow(),
            action ?? string.Empty,
            source ?? string.Empty,
            outcome ?? string.Empty,
            steamId ?? string.Empty,
            displayNameRaw ?? string.Empty,
            persona ?? string.Empty,
            CurrentAuditLobbyId(),
            issuerSteamId ?? string.Empty,
            incidentId ?? string.Empty,
            targetRole ?? string.Empty));
    }

    internal static void ArmPersistedModerationAudit(
        string action,
        string source,
        string steamId,
        string displayNameRaw,
        string persona,
        string issuerSteamId,
        string incidentId,
        string targetRole)
    {
        if (!Enabled || _instance == null || !Plugin.CanUseLobbySafety(out bool isHost) || !isHost) return;
        _pendingModerationSave = new PendingModerationSave
        {
            Action = action ?? string.Empty,
            Source = source ?? string.Empty,
            SteamId = steamId ?? string.Empty,
            DisplayNameRaw = displayNameRaw ?? string.Empty,
            Persona = persona ?? string.Empty,
            IssuerSteamId = issuerSteamId ?? string.Empty,
            IncidentId = incidentId ?? string.Empty,
            TargetRole = targetRole ?? string.Empty,
        };
    }

    internal static void CancelPersistedModerationAudit()
    {
        _pendingModerationSave = null;
    }

    internal static void CapturePersistedNativeBanSave()
    {
        if (!Enabled || _instance == null || !Plugin.CanUseLobbySafety(out bool isHost) || !isHost)
        {
            _pendingModerationSave = null;
            return;
        }

        PendingModerationSave pending = _pendingModerationSave;
        _pendingModerationSave = null;
        if (pending != null)
        {
            RecordModeration(
                pending.Action,
                pending.Source,
                "persisted",
                pending.SteamId,
                pending.DisplayNameRaw,
                pending.Persona,
                pending.IssuerSteamId,
                pending.IncidentId,
                pending.TargetRole);
            _instance.SynchronizeNativeBanSnapshot();
            return;
        }

        _instance.PollNativeBanStore("native-ban-save-observed");
    }

    internal static string StatusText()
    {
        if (!Enabled) return OffText;
        if (_instance == null || _instance._initializationFailed || _instance._chatWriter == null || _instance._bepInExWriter == null)
            return WriteTroubleText;
        return "Local Audit is on. Your files are in " + CurrentSessionDirectory + ". A snapshot saves the newest 8 MB of your game log as one file; the 2 newest are kept for each session.";
    }

    // Voice pass (2026-10-23): one calm "Local Audit" voice. Details (writer errors, identity enrichment) stay in
    // the log and the folder README; the player hears what is on, where it is, and what to try.
    internal const string OffText = "Local Audit is off (that's the default). Type /auditlog on if you'd like to keep local log copies.";
    internal const string WriteTroubleText = "Local Audit is on but couldn't save files just now. Try /auditlog on again in a moment.";
    internal const string SnapshotFailedText = "The snapshot couldn't be saved this time. Nothing was lost; try again in a moment.";
    internal const string ResumedText = "Local Audit is back on. Anything from while it was off wasn't saved.";

    internal static string CopyAuditFolder()
    {
        GUIUtility.systemCopyBuffer = CurrentSessionDirectory;
        return "Local Audit folder copied. It stays on this computer until you choose to share it.";
    }

    internal static string SnapshotNow()
    {
        if (!Enabled || _instance == null) return OffText;
        _instance.EnsureInitialized();
        if (_instance._initializationFailed || _instance._chatWriter == null || _instance._bepInExWriter == null)
        {
            Plugin.Log?.LogWarning("Local audit snapshot failed: one or both local writers are unavailable.");
            return SnapshotFailedText;
        }
        _instance.MirrorBepInExLog(8 * 1024 * 1024);
        if (_instance._initializationFailed || _instance._chatWriter == null || _instance._bepInExWriter == null)
        {
            Plugin.Log?.LogWarning("Local audit snapshot failed while mirroring BepInEx output.");
            return SnapshotFailedText;
        }
        _instance.WriteRosterCheckpoint();
        _instance.PollNativeBanStore();
        try
        {
            _instance._chatWriter.Flush();
            _instance._bepInExWriter.Flush();
        }
        catch (Exception ex)
        {
            _instance.MarkCaptureFailed("snapshot flush", ex);
            return SnapshotFailedText;
        }
        string sourceLogPath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
        if (!AuditSnapshotPolicy.TryCreate(
                sourceLogPath,
                _instance._sessionDirectory,
                DateTime.Now,
                out string snapshotPath,
                out long capturedBytes,
                out string error))
        {
            Plugin.Log?.LogWarning("Local audit snapshot failed while copying the true BepInEx LogOutput.log: " + error);
            return SnapshotFailedText;
        }

        GUIUtility.systemCopyBuffer = snapshotPath;
        Plugin.Log?.LogInfo("Local audit snapshot saved: " + capturedBytes + " bytes from the true BepInEx LogOutput.log.");
        return "Snapshot saved: " + snapshotPath + ". Its file path is copied, ready to paste.";
    }

    internal static void ReconcileEnabledState()
    {
        if (_instance == null) return;
        if (Enabled)
        {
            bool resumed = _instance._wasDisabled;
            if (_instance._wasDisabled)
            {
                _instance.AdvanceBepInExSourceOffsetToEnd();
                _instance._wasDisabled = false;
            }
            _instance._initializationFailed = false;
            _instance.EnsureInitialized();
            if (resumed)
                _instance.AppendChatLine(SessionAuditPolicy.BuildNotificationRecord(
                    UtcNow(),
                    ResumedText,
                    CurrentAuditLobbyId()));
        }
        else _instance.PauseCapture();
    }

    private void EnsureInitialized()
    {
        if (_initializationFailed || !Enabled || (_chatWriter != null && _bepInExWriter != null)) return;
        try
        {
            Directory.CreateDirectory(AuditRoot);
            if (string.IsNullOrWhiteSpace(_sessionDirectory))
            {
                DateTime started = DateTime.Now;
                _sessionStamp = ModSuiteLayoutPolicy.Stamp(started);
                _sessionDirectory = Path.Combine(AuditRoot, ModSuiteLayoutPolicy.SessionFolderName(started));
                Directory.CreateDirectory(_sessionDirectory);
                File.WriteAllText(
                    Path.Combine(_sessionDirectory, _sessionStamp + "-README.txt"),
                    "BlueSage QoL local session audit\r\n" +
                    "Contains a chat/notification JSONL transcript and a running mirror of BepInEx/LogOutput.log.\r\n" +
                    "Chat raw fields are the rendered UI arguments seen after the game's/mods' earlier patches, not a pristine network payload.\r\n" +
                    "Exact SteamID/persona enrichment is written only while this client is the verified host or an assigned Helper.\r\n" +
                    "The BepInEx mirror faithfully preserves whatever the game and installed mods already write, which may include player identifiers.\r\n" +
                    "These files are never uploaded automatically. Review them before sharing.\r\n",
                    new UTF8Encoding(false));
            }

            OpenChatWriter();
            OpenBepInExWriter();
            MirrorBepInExLog(8 * 1024 * 1024);
        }
        catch (Exception ex)
        {
            _initializationFailed = true;
            CloseWriters();
            Plugin.Log?.LogWarning("Local audit export could not start: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void OpenChatWriter(bool truncate = false)
    {
        _chatPath = Path.Combine(_sessionDirectory, PartName(ModSuiteLayoutPolicy.ChatFileStem(_sessionStamp), _chatPart, ".jsonl"));
        _chatWriter = new StreamWriter(new FileStream(_chatPath, truncate ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
    }

    private void OpenBepInExWriter(bool truncate = false)
    {
        _bepInExPath = Path.Combine(_sessionDirectory, PartName(ModSuiteLayoutPolicy.GameLogFileStem(_sessionStamp), _bepInExPart, ".log"));
        _bepInExWriter = new FileStream(_bepInExPath, truncate ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.Read);
    }

    private void AppendChatLine(string line)
    {
        if (_chatWriter == null || string.IsNullOrWhiteSpace(line)) return;
        try
        {
            if (_chatWriter.BaseStream.Length >= MaxChatPartBytes)
            {
                _chatWriter.Dispose();
                _chatWriter = null;
                _chatPart++;
                OpenChatWriter(truncate: true);
                PruneAuditParts("chat-audit*.jsonl", _chatPath, MaxChatParts);
            }
            _chatWriter.WriteLine(line);
        }
        catch (Exception ex)
        {
            MarkCaptureFailed("chat write", ex);
        }
    }

    private void MirrorBepInExLog(int maximumBytes)
    {
        if (_bepInExWriter == null || maximumBytes <= 0) return;
        string sourcePath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
        try
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (source.Length < _bepInExSourceOffset) _bepInExSourceOffset = 0L;
                source.Position = _bepInExSourceOffset;
                int remaining = maximumBytes;
                while (remaining > 0 && source.Position < source.Length)
                {
                    if (_bepInExWriter.Length >= MaxBepInExPartBytes)
                    {
                        _bepInExWriter.Dispose();
                        _bepInExWriter = null;
                        _bepInExPart++;
                        OpenBepInExWriter(truncate: true);
                        PruneAuditParts("bepinex*.log", _bepInExPath, MaxBepInExParts);
                    }

                    byte[] buffer = _bepInExMirrorBuffer.Acquire(source.Length, source.Position, remaining);
                    if (buffer == null) break;
                    int read = source.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                    if (read <= 0) break;
                    _bepInExWriter.Write(buffer, 0, read);
                    _bepInExSourceOffset += read;
                    remaining -= read;
                }
                _bepInExWriter.Flush();
            }
        }
        catch (FileNotFoundException)
        {
            // BepInEx may not have opened its disk listener yet. The next tick retries.
        }
        catch (Exception ex)
        {
            MarkCaptureFailed("BepInEx mirror", ex);
        }
    }

    private void PollNativeBanStore(string observedSource = "native-ban-store-observed")
    {
        if (!Plugin.CanUseLobbySafety(out bool isHost) || !isHost) return;
        if (!SteamIdModerationController.TryGetNativeBanRecords(out IReadOnlyList<NativeBanRecord> current, out string readError))
        {
            Plugin.Log?.LogDebug("Native ban audit observation skipped safely: " + readError);
            return;
        }
        if (!_nativeBanSnapshotReady)
        {
            foreach (NativeBanRecord record in current)
            {
                _nativeBanSnapshot[record.SteamId] = record;
                if (!_suppressNextBanBaselineEvents) WriteNativeBanChange("ban-existing", record);
            }
            _nativeBanSnapshotReady = true;
            _suppressNextBanBaselineEvents = false;
            return;
        }

        var now = current.GroupBy(record => record.SteamId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        NativeBanRecord[] added = current.Where(record => !_nativeBanSnapshot.ContainsKey(record.SteamId)).ToArray();
        NativeBanRecord[] removed = _nativeBanSnapshot.Values.Where(prior => !now.ContainsKey(prior.SteamId)).ToArray();
        NativeBanRecord[] renamed = current.Where(record =>
            _nativeBanSnapshot.TryGetValue(record.SteamId, out NativeBanRecord prior) &&
            !string.Equals(record.DisplayNameRaw, prior.DisplayNameRaw, StringComparison.Ordinal)).ToArray();

        _nativeBanSnapshot.Clear();
        foreach (KeyValuePair<string, NativeBanRecord> pair in now) _nativeBanSnapshot[pair.Key] = pair.Value;
        foreach (NativeBanRecord record in added) WriteNativeBanChange("ban-added", record, observedSource);
        foreach (NativeBanRecord prior in removed) WriteNativeBanChange("ban-removed", prior, observedSource);
        foreach (NativeBanRecord record in renamed) WriteNativeBanChange("ban-name-updated", record, observedSource);
    }

    private void WriteNativeBanChange(string action, NativeBanRecord record, string source = "native-ban-store-observed")
    {
        RecordModeration(
            action,
            action == "ban-existing" ? "native-ban-store-baseline" : source,
            "persisted",
            record.SteamId,
            record.DisplayNameRaw,
            ReadSteamPersona(record.SteamId));
    }

    private void SynchronizeNativeBanSnapshot()
    {
        if (!SteamIdModerationController.TryGetNativeBanRecords(out IReadOnlyList<NativeBanRecord> current, out _)) return;
        _nativeBanSnapshot.Clear();
        foreach (NativeBanRecord record in current) _nativeBanSnapshot[record.SteamId] = record;
        _nativeBanSnapshotReady = true;
        _suppressNextBanBaselineEvents = false;
    }

    private void WriteRosterCheckpoint()
    {
        if (!Plugin.CanUseLobbySafety(out _)) return;
        foreach (PlayerIdentityEvidence evidence in PlayerIdentityEvidenceController.GetVerifiedRoster())
        {
            RecordModeration(
                "roster-checkpoint",
                "manual-audit-snapshot",
                evidence.Role,
                evidence.SteamId,
                evidence.DisplayNameRaw,
                evidence.SteamPersona,
                targetRole: evidence.Role + ";rosterIndex=" + evidence.RosterIndex + ";playerId=" + evidence.NetworkPlayerId);
        }
    }

    private void CloseWriters()
    {
        try { _chatWriter?.Flush(); } catch { }
        try { _chatWriter?.Dispose(); } catch { }
        try { _bepInExWriter?.Flush(); } catch { }
        try { _bepInExWriter?.Dispose(); } catch { }
        _chatWriter = null;
        _bepInExWriter = null;
    }

    private void MarkCaptureFailed(string operation, Exception ex)
    {
        _initializationFailed = true;
        CloseWriters();
        Plugin.Log?.LogWarning("Local audit paused: the " + operation + " could not be written (" + ex.GetType().Name + ": " + ex.Message + "). Type /auditlog on to try again.");
    }

    private void PauseCapture()
    {
        CloseWriters();
        AdvanceBepInExSourceOffsetToEnd();
        _nativeBanSnapshot.Clear();
        _nativeBanSnapshotReady = false;
        _suppressNextBanBaselineEvents = true;
        _wasDisabled = true;
    }

    private void AdvanceBepInExSourceOffsetToEnd()
    {
        try
        {
            string sourcePath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                _bepInExSourceOffset = source.Length;
        }
        catch (FileNotFoundException) { }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("BepInEx audit opt-out cursor update skipped safely: " + ex.GetType().Name + ": " + ex.Message);
        }
    }


    private void PruneAuditParts(string pattern, string currentPath, int maximumParts)
    {
        try
        {
            FileInfo[] parts = new DirectoryInfo(_sessionDirectory).GetFiles(pattern)
                .Where(file => !string.Equals(file.FullName, currentPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.LastWriteTimeUtc)
                .ToArray();
            int excess = Math.Max(0, parts.Length + 1 - maximumParts);
            for (int index = 0; index < excess; index++) parts[index].Delete();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Local audit old-part cleanup skipped safely: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static string PartName(string stem, int part, string extension)
    {
        return part <= 1 ? stem + extension : stem + ".part" + part.ToString("00") + extension;
    }

    private static string UtcNow() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    private static string CurrentLobbyId()
    {
        try
        {
            return Plugin.TryGetRescueLobby(out CSteamID lobby) && lobby != CSteamID.Nil
                ? ((ulong)lobby).ToString()
                : string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string CurrentAuditLobbyId()
    {
        return Plugin.CanUseLobbySafety(out _) ? CurrentLobbyId() : string.Empty;
    }

    private static string ReadSteamPersona(string steamId)
    {
        try
        {
            return ulong.TryParse(steamId, out ulong parsed)
                ? SteamFriends.GetFriendPersonaName(new CSteamID(parsed)) ?? string.Empty
                : string.Empty;
        }
        catch { return string.Empty; }
    }

    private sealed class PendingModerationSave
    {
        internal string Action = string.Empty;
        internal string Source = string.Empty;
        internal string SteamId = string.Empty;
        internal string DisplayNameRaw = string.Empty;
        internal string Persona = string.Empty;
        internal string IssuerSteamId = string.Empty;
        internal string IncidentId = string.Empty;
        internal string TargetRole = string.Empty;
    }
}
