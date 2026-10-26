using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal static class ChalkboardPersistenceController
{
    private const string ChalkyGuid = "com.andrewlin.ontogether.chalky";
    private const string Magic = "BSCH1";
    private const int MaxDimension = 512;
    private const int MaxColorIndex = 255;
    private const long MemberDataPollIntervalMilliseconds = 500;
    private const int ClearCooldownSeconds = 5;
    private static readonly Regex SafeName = new Regex("^[A-Za-z0-9][A-Za-z0-9_-]{0,39}$", RegexOptions.Compiled);
    private static readonly System.Reflection.FieldInfo BoardIndexField = AccessTools.Field(typeof(DrawingManager), "_boardIndex");
    private static readonly HashSet<string> SeenClearNonces = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Queue<string> ClearNonceOrder = new Queue<string>();
    private static readonly Dictionary<string, long> LastClearRequestByIssuer =
        new Dictionary<string, long>(StringComparer.Ordinal);
    private static readonly Dictionary<string, DateTime> LastClearByBoard =
        new Dictionary<string, DateTime>(StringComparer.Ordinal);
    private static DateTime _lastSharedRestoreUtc = DateTime.MinValue;
    private static long _nextMemberDataPollUnixMilliseconds;
    private static string _memberDataLobbyKey = string.Empty;
    private static string _boardSetSignature = string.Empty;
    private static string _lastListedBoardSetSignature = string.Empty;
    private static long _lastListedBoardSetUnixSeconds;
    private static bool _rollbackUnresolved;

    internal static bool ChalkyInstalled => Chainloader.PluginInfos.ContainsKey(ChalkyGuid);
    internal static bool Available => Plugin.EnableChalkboardPersistence?.Value == true && !ChalkyInstalled;
    internal static string SaveDirectory => Path.Combine(Paths.ConfigPath, "BlueSageChalkboards");

    internal static string Status()
    {
        if (ChalkyInstalled)
        {
            return "Chalkboards: Chalky is installed, so BlueSage's chalkboard tools stay off to avoid clashing with it.";
        }

        bool undoBlocked = _rollbackUnresolved || ChalkboardSnapshotTransportController.DestructiveActionsBlocked;
        if (undoBlocked)
        {
            Plugin.Log?.LogInfo(
                "[BlueSageChalk] status: rollback=" + ChalkboardSnapshotTransportController.RollbackState +
                "; there is no authoritative drawing-owner identity.");
        }
        return "Chalkboards: " + (Available ? "on." : "off.") +
               " Boards 0 (Stage), 1 and 2 (Classroom) can be saved and viewed by anyone with QoL; the host or a Helper can load or clear them. " +
               (undoBlocked
                   ? "Undo: stuck, so load and clear stay paused until the boards reload or the game restarts."
                   : "Undo: ready.");
    }

    // Player text stays short; the full technical receipt goes to the log only.
    private static string WithLoggedDetail(string playerMessage, string detail)
    {
        Plugin.Log?.LogInfo("[BlueSageChalk] " + detail);
        return playerMessage;
    }

    internal static string Save(string name, string boardToken)
    {
        if (!Available)
        {
            return Status();
        }

        if (!TryResolve(name, boardToken, out string normalized, out QuadPainterGPU board, out int index, out string error))
        {
            return error;
        }

        TrySaveResolved(normalized, board, index, out string message);
        return message;
    }

    private static bool TrySaveResolved(
        string normalized,
        QuadPainterGPU board,
        int index,
        out string message)
    {
        try
        {
            ValidateGrid(board.PaintColors);
            Directory.CreateDirectory(SaveDirectory);
            string target = Path.Combine(SaveDirectory, normalized + ".bschalk");
            string temp = target + ".tmp";
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(board.PaintColors.Length);
                writer.Write(board.PaintColors[0].Ints.Count);
                for (int x = 0; x < board.PaintColors.Length; x++)
                {
                    for (int y = 0; y < board.PaintColors[x].Ints.Count; y++)
                    {
                        writer.Write(board.PaintColors[x].Ints[y]);
                    }
                }
                stream.Flush(true);
            }

            if (File.Exists(target))
            {
                string backup = target + ".bak";
                File.Replace(temp, target, backup, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, target);
            }

            message = WithLoggedDetail(
                $"Saved board {index} as '{normalized}' on this computer.",
                ChalkboardModerationController.BuildTruth(
                    "save",
                    index,
                    DescribeBoardScene(board, index),
                    normalized,
                    "not-needed"));
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Chalkboard save failed safely: {ex.GetType().Name}: {ex.Message}");
            message = "The board couldn't be saved. Your older save is still there.";
            return false;
        }
    }

    internal static string Load(string name, string boardToken)
    {
        if (!Available) return Status();
        if (!TryNormalizeName(name, out string normalized, out string nameError)) return nameError;
        if (!TryResolveBoard(
                boardToken,
                out QuadPainterGPU board,
                out int index,
                out string fingerprint,
                out string error))
        {
            return error;
        }
        DrawingManager manager = MonoSingleton<DrawingManager>.I;
        if (!ChalkboardCommandPolicy.TryAuthorizeDestructiveAction(
                index,
                manager?.QuadPainterGPUS?.Count ?? 0,
                _lastListedBoardSetSignature,
                _lastListedBoardSetUnixSeconds,
                BuildBoardSetSignature(manager),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                _rollbackUnresolved || ChalkboardSnapshotTransportController.DestructiveActionsBlocked,
                out string listError))
        {
            return listError;
        }
        if (!SteamIdModerationController.CanLocalIssue(out bool isHost, out string accessReason))
        {
            return accessReason;
        }

        if (isHost)
        {
            string issuerSteamId = SteamManager.Initialized
                ? ((ulong)SteamUser.GetSteamID()).ToString()
                : string.Empty;
            ExecuteLoadAsHost(normalized, index, fingerprint, issuerSteamId, out string hostMessage);
            return hostMessage;
        }

        if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil)
        {
            return "Your request couldn't reach the host. No board changed.";
        }

        try
        {
            string nonce = Guid.NewGuid().ToString("N").Substring(0, 16);
            string payload = ChalkboardModerationPolicy.BuildRestoreMemberData(
                index,
                fingerprint,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                nonce,
                normalized);
            SteamMatchmaking.SetLobbyMemberData(lobby, ChalkboardModerationPolicy.MemberDataKey, payload);
            return WithLoggedDetail(
                $"Request sent quietly to the host to load '{normalized}' onto board {index}; they'll double-check before changing the board.",
                ChalkboardModerationController.BuildTruth(
                    "load-request",
                    index,
                    DescribeBoardScene(board, index),
                    normalized,
                    "pending-host"));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "Helper chalkboard restore request failed safely: " +
                ex.GetType().Name +
                ": " +
                ex.Message);
            return "Your request couldn't be sent. No board changed.";
        }
    }

    private static ChalkboardClearResult ExecuteLoadAsHost(
        string normalized,
        int index,
        string expectedFingerprint,
        string issuerSteamId,
        out string message)
    {
        message = "No chalkboard changed.";
        if (!SafeName.IsMatch(normalized ?? string.Empty) ||
            !SteamIdModerationController.TryGetOwnedHostLobby(out _, out _) ||
            !SteamIdModerationController.IsAuthorizedCurrentIssuer(issuerSteamId))
        {
            message = "Only the host or an assigned Helper can load a board for everyone.";
            return ChalkboardClearResult.Rejected;
        }
        if (!TryResolveBoard(
                index.ToString(CultureInfo.InvariantCulture),
                out QuadPainterGPU board,
                out int resolvedIndex,
                out string currentFingerprint,
                out string error))
        {
            message = error;
            return ChalkboardClearResult.Rejected;
        }
        if (resolvedIndex != index ||
            !string.Equals(currentFingerprint, expectedFingerprint, StringComparison.Ordinal))
        {
            message = "That board changed since it was picked. No board changed; run /chalk boards again.";
            return ChalkboardClearResult.Rejected;
        }
        if (!board.isServer)
        {
            message = "Only the lobby host's game can change shared boards. No board changed.";
            return ChalkboardClearResult.Rejected;
        }
        if (_rollbackUnresolved || ChalkboardSnapshotTransportController.DestructiveActionsBlocked)
        {
            message = "An earlier board undo didn't finish, so load and clear stay paused until the boards reload or the game restarts.";
            return ChalkboardClearResult.Rejected;
        }
        if ((DateTime.UtcNow - _lastSharedRestoreUtc).TotalSeconds < 8d)
        {
            message = "A board was just loaded. Wait a few seconds before loading another.";
            return ChalkboardClearResult.Rejected;
        }

        string path = Path.Combine(SaveDirectory, normalized + ".bschalk");
        if (!File.Exists(path))
        {
            message = $"The host has no saved board called '{normalized}'. Use /chalk list on the host's game.";
            return ChalkboardClearResult.Rejected;
        }

        int[][] rollbackGrid = null;
        string recoveryName = "preload-b" + index + "-" +
                              DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        try
        {
            int[][] grid = ReadGrid(path, board);
            if (!TrySaveResolved(recoveryName, board, index, out string recoveryMessage))
            {
                message = "The backup before loading couldn't be saved, so the board was left as it was.";
                return ChalkboardClearResult.Rejected;
            }

            rollbackGrid = CopyGrid(board.PaintColors);
            ChalkboardDeliveryResult delivery = ApplyGridAndSync(board, grid);
            _lastSharedRestoreUtc = DateTime.UtcNow;
            string truth = ChalkboardModerationController.BuildTruth(
                "load",
                index,
                DescribeBoardScene(board, index),
                recoveryName,
                "ready");
            if (!delivery.IsComplete)
            {
                message = WithLoggedDetail(
                    $"Loaded '{normalized}' onto board {index}, but some players may not see it yet. Wait a few seconds, then load it again to be sure.",
                    $"load partial: attempted {delivery.Attempted}, sent {delivery.Sent}, skipped {delivery.Skipped}; repeating after the cooldown is retry-safe. " +
                    truth);
                return ChalkboardClearResult.Partial;
            }
            message = WithLoggedDetail(
                $"Loaded '{normalized}' onto board {index}. A backup of the old board was saved first.",
                truth);
            return ChalkboardClearResult.Complete;
        }
        catch (Exception ex)
        {
            bool rollbackSucceeded = rollbackGrid == null;
            if (rollbackGrid != null)
            {
                try
                {
                    ApplyGridAndSync(board, rollbackGrid);
                    rollbackSucceeded = true;
                }
                catch (Exception rollbackEx)
                {
                    RestoreGridValues(board, rollbackGrid);
                    _rollbackUnresolved = true;
                    ChalkboardSnapshotTransportController.MarkRollbackUnresolved(
                        "restore " + rollbackEx.GetType().Name);
                    Plugin.Log?.LogError(
                        "Chalkboard restore rollback repaint failed after restoring grid values: " +
                        rollbackEx.GetType().Name + ": " + rollbackEx.Message);
                }
            }
            Plugin.Log?.LogWarning($"Chalkboard restore rejected safely: {ex.GetType().Name}: {ex.Message}");
            message = WithLoggedDetail(
                rollbackSucceeded
                    ? $"Loading the board didn't work, so it was put back. A backup is saved as '{recoveryName}'."
                    : $"Loading the board didn't work and it couldn't be put back fully. Load and clear are paused; a backup is saved as '{recoveryName}'.",
                "restore failed; rollback=" +
                (rollbackSucceeded ? "restored" : "unresolved-blocking") +
                ", recovery snapshot='" + recoveryName +
                "', vanillaObserver=not-observed.");
            return ChalkboardClearResult.Rejected;
        }
    }

    internal static string List()
    {
        if (ChalkyInstalled)
        {
            return Status();
        }
        if (!Directory.Exists(SaveDirectory))
        {
            return "Chalkboard saves: none yet.";
        }
        string[] names = Directory.GetFiles(SaveDirectory, "*.bschalk")
            .Select(Path.GetFileNameWithoutExtension).OrderBy(value => value).Take(30).ToArray();
        string list = names.Length == 0
            ? "Chalkboard saves: none yet."
            : "Chalkboard saves: " + string.Join(", ", names) + ".";
        return WithLoggedDetail(
            list,
            "list: authority=local-only, snapshot=local-library, rollback=" +
            (_rollbackUnresolved ? "unresolved" : "ready") +
            ", vanillaObserver=not-applicable.");
    }

    internal static string Delete(string name)
    {
        if (ChalkyInstalled) return Status();
        if (!TryNormalizeName(name, out string normalized, out string error)) return error;
        string target = Path.Combine(SaveDirectory, normalized + ".bschalk");
        if (!File.Exists(target))
        {
            return $"There's no saved board called '{normalized}'. Use /chalk list to see your saves.";
        }

        try
        {
            string backup = target + ".bak";
            File.Copy(target, backup, overwrite: true);
            File.Delete(target);
            return WithLoggedDetail(
                $"Deleted your save '{normalized}'. A backup copy was kept as '{Path.GetFileName(backup)}'.",
                ChalkboardCommandPolicy.BuildActionTruth(
                    "delete",
                    -1,
                    "local-save-library",
                    "local-only",
                    0,
                    Path.GetFileName(backup),
                    "reversible-from-backup",
                    "not-applicable"));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "Chalkboard local delete failed safely: " + ex.GetType().Name + ": " + ex.Message);
            return "That save couldn't be deleted. It should still be there.";
        }
    }

    internal static string RefreshMap()
    {
        return "Boards found again. " + Boards();
    }

    internal static string View(string boardToken)
    {
        if (!Available) return Status();
        if (!TryResolveBoard(
                boardToken,
                out QuadPainterGPU board,
                out int index,
                out string fingerprint,
                out string error))
        {
            return error;
        }
        return WithLoggedDetail(
            "Board " + index + " found and ready. Nothing was changed.",
            "view fingerprint=" + fingerprint + ". " +
            ChalkboardModerationController.BuildTruth(
                "view",
                index,
                DescribeBoardScene(board, index),
                "none",
                _rollbackUnresolved ? "unresolved" : "ready"));
    }

    internal static string Boards()
    {
        if (!Available)
        {
            return Status();
        }
        DrawingManager manager = MonoSingleton<DrawingManager>.I;
        if (manager == null || manager.QuadPainterGPUS == null || manager.QuadPainterGPUS.Count == 0)
        {
            return "Chalkboards are not ready in this scene.";
        }
        RefreshBoardSetState(manager);
        _lastListedBoardSetSignature = _boardSetSignature;
        _lastListedBoardSetUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        int selectedIndex = (int)(BoardIndexField?.GetValue(manager) ?? -1);
        PlayerController localPlayer = NetworkSingleton<TextChannelManager>.I?.MainPlayerController;
        Vector3 localPosition = localPlayer != null ? localPlayer.transform.position : Vector3.zero;
        bool hasLocalPosition = localPlayer != null;
        int nearestIndex = -1;
        float nearestDistance = float.MaxValue;
        if (hasLocalPosition)
        {
            int candidateCount = Math.Min(
                manager.QuadPainterGPUS.Count,
                ChalkboardCatalogPolicy.MaxCatalogIndex + 1);
            for (int index = 0; index < candidateCount; index++)
            {
                QuadPainterGPU candidate = manager.QuadPainterGPUS[index];
                if (candidate == null) continue;
                float distance = Vector3.Distance(localPosition, candidate.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            }
        }

        var descriptions = new List<string>();
        foreach (ChalkboardCatalogEntry entry in ChalkboardCatalogPolicy.Build24259292)
        {
            int index = entry.Index;
            if (index >= manager.QuadPainterGPUS.Count)
            {
                descriptions.Add(index + " " + entry.Label + " unavailable in current scene");
                continue;
            }
            QuadPainterGPU board = manager.QuadPainterGPUS[index];
            if (board == null)
            {
                descriptions.Add(index + " " + entry.Label + " unavailable");
                continue;
            }

            string hierarchyPath = BuildHierarchyPath(board.transform);
            string sceneName = board.gameObject.scene.name ?? string.Empty;
            Vector3 position = board.transform.position;
            string observedLocation = ChalkboardModerationPolicy.ClassifyBuild24259292Location(
                index,
                position.x,
                position.y,
                position.z);
            string mappingState = string.Equals(
                    observedLocation,
                    entry.Label,
                    StringComparison.Ordinal)
                ? "mapped"
                : "AMBIGUOUS-MAPPING";
            string markers = (index == selectedIndex ? " selected" : string.Empty) +
                             (index == nearestIndex ? " nearest" : string.Empty);
            string distanceText = hasLocalPosition
                ? ", " + Vector3.Distance(localPosition, position).ToString("0.0", CultureInfo.InvariantCulture) + "m away"
                : string.Empty;
            string displayPath = hierarchyPath.Length > 96 ? "…" + hierarchyPath.Substring(hierarchyPath.Length - 95) : hierarchyPath;
            Plugin.Log?.LogInfo(
                $"[BlueSageChalk] board {index} {entry.Label} scene={SafeDisplayName(sceneName)} state={mappingState} at ({position.x:0.0},{position.y:0.0},{position.z:0.0}) — " +
                displayPath);
            descriptions.Add(
                $"{index} {entry.Label}{distanceText}" +
                (string.Equals(mappingState, "mapped", StringComparison.Ordinal) ? string.Empty : " (not where expected; locked)") +
                (markers.Length > 0 ? " [" + markers.Trim() + "]" : string.Empty));
        }

        string ignoredExtra = manager.QuadPainterGPUS.Count > 3
            ? " Other boards in this scene are left alone."
            : string.Empty;
        Plugin.Log?.LogInfo(
            "[BlueSageChalk] boards: compatibleRecipients=" + ChalkboardModerationController.CountCompatibleRecipients() +
            ", snapshot=required-before-destructive-action, rollback=" +
            (_rollbackUnresolved ? "unresolved" : "ready") +
            ", vanillaObserver=not-observed.");
        return "Chalkboards: " +
               string.Join("; ", descriptions) +
               "." + ignoredExtra +
               " Anyone can view, save, or delete their own saves. The host or a Helper can load or clear within the next " +
               ChalkboardCommandPolicy.FreshListSeconds + " seconds.";
    }

    internal static string TryClear(string boardToken)
    {
        if (!Available)
        {
            return Status();
        }
        if (!TryResolveBoard(boardToken, out QuadPainterGPU board, out int index, out string fingerprint, out string error))
        {
            return error;
        }
        DrawingManager manager = MonoSingleton<DrawingManager>.I;
        string currentSignature = BuildBoardSetSignature(manager);
        if (!ChalkboardCommandPolicy.TryAuthorizeDestructiveAction(
                index,
                manager?.QuadPainterGPUS?.Count ?? 0,
                _lastListedBoardSetSignature,
                _lastListedBoardSetUnixSeconds,
                currentSignature,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                _rollbackUnresolved || ChalkboardSnapshotTransportController.DestructiveActionsBlocked,
                out string listError))
        {
            return listError;
        }
        if (!SteamIdModerationController.CanLocalIssue(out bool isHost, out string accessReason))
        {
            return accessReason;
        }

        if (isHost)
        {
            string issuerSteamId = SteamManager.Initialized
                ? ((ulong)SteamUser.GetSteamID()).ToString()
                : string.Empty;
            ExecuteClearAsHost(index, fingerprint, issuerSteamId, out string hostMessage);
            return hostMessage;
        }

        if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil)
        {
            return "Your request couldn't reach the host. No board changed.";
        }

        try
        {
            string nonce = Guid.NewGuid().ToString("N").Substring(0, 16);
            string payload = ChalkboardModerationPolicy.BuildMemberData(
                index,
                fingerprint,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                nonce);
            SteamMatchmaking.SetLobbyMemberData(lobby, ChalkboardModerationPolicy.MemberDataKey, payload);
            return WithLoggedDetail(
                "Request sent quietly to the host; they'll double-check before changing the board.",
                ChalkboardModerationController.BuildTruth(
                    "clear-request",
                    index,
                    DescribeBoardScene(board, index),
                    "host-pre-action-required",
                    "pending-host"));
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Helper chalkboard clear request failed safely: " + ex.GetType().Name + ": " + ex.Message);
            return "Your request couldn't be sent. No board changed.";
        }
    }

    internal static void PollHostMemberDataRequests()
    {
        long nowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMilliseconds < _nextMemberDataPollUnixMilliseconds) return;
        _nextMemberDataPollUnixMilliseconds = nowMilliseconds + MemberDataPollIntervalMilliseconds;
        if (!Available || !SteamIdModerationController.TryGetOwnedHostLobby(out CSteamID lobby, out string ownerSteamId))
        {
            return;
        }

        string lobbyKey = ((ulong)lobby).ToString();
        if (!string.Equals(_memberDataLobbyKey, lobbyKey, StringComparison.Ordinal))
        {
            _memberDataLobbyKey = lobbyKey;
            ResetRelayAndCooldownState();
        }
        RefreshBoardSetState(MonoSingleton<DrawingManager>.I);

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int memberIndex = 0; memberIndex < memberCount; memberIndex++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, memberIndex);
            string senderSteamId = ((ulong)member).ToString();
            if (string.Equals(senderSteamId, ownerSteamId, StringComparison.Ordinal)) continue;
            string payload = SteamMatchmaking.GetLobbyMemberData(
                lobby,
                member,
                ChalkboardModerationPolicy.MemberDataKey);
            if (!ChalkboardModerationPolicy.TryParseMemberData(payload, out ChalkboardClearRequest request))
            {
                continue;
            }
            ProcessHelperClearRequest(lobby, senderSteamId, request);
        }
    }

    internal static void NotifyModerationBoardReview(string targetSteamId)
    {
        if (!Available) return;
        Plugin.Log?.LogInfo(
            "[BlueSageChalk] board review after ban of Steam …" + Suffix(targetSteamId) +
            ": the game exposes no authoritative drawing-owner identity, so no board was guessed or auto-cleared.");
        Plugin.AddLocalNotification(
            "Chalkboards after that ban: the game doesn't say who drew what, so no board was cleared automatically. " +
            "If unkind drawings remain, run /chalk boards, then /chalk clear <number>. A backup is saved before every clear.");
    }

    internal static bool PrepareForFullSnapshot(QuadPainterGPU board)
    {
        return ChalkboardSnapshotTransportController.PrepareForFullSnapshot(board);
    }

    private static void ProcessHelperClearRequest(
        CSteamID lobby,
        string senderSteamId,
        ChalkboardClearRequest request)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!ChalkboardModerationPolicy.IsFresh(request.IssuedUnixSeconds, now) ||
            !PlayerIdentityEvidenceController.TryResolveExact(senderSteamId, out _, out _) ||
            CountLobbyMembers(lobby, senderSteamId) != 1 ||
            !SteamIdModerationController.IsAuthorizedCurrentIssuer(senderSteamId) ||
            !RememberClearNonce(request.Nonce))
        {
            return;
        }
        if (LastClearRequestByIssuer.TryGetValue(senderSteamId, out long lastRequest) && now - lastRequest < 2)
        {
            return;
        }
        LastClearRequestByIssuer[senderSteamId] = now;

        ChalkboardClearResult result;
        string message;
        if (string.Equals(request.Action, "load", StringComparison.Ordinal))
        {
            result = ExecuteLoadAsHost(
                request.SnapshotName,
                request.BoardIndex,
                request.BoardFingerprint,
                senderSteamId,
                out message);
        }
        else if (string.Equals(request.Action, "clear", StringComparison.Ordinal))
        {
            result = ExecuteClearAsHost(
                request.BoardIndex,
                request.BoardFingerprint,
                senderSteamId,
                out message);
        }
        else
        {
            return;
        }
        Plugin.Log?.LogInfo(
            "Host non-chat chalkboard " + request.Action +
            " request from Steam …" + Suffix(senderSteamId) + ": " + message);
        string notificationPrefix = result == ChalkboardClearResult.Complete
            ? "Helper board request done: "
            : result == ChalkboardClearResult.Partial
                ? "Helper board request partly done: "
                : "Helper board request not carried out: ";
        Plugin.AddLocalNotification(notificationPrefix + message);
    }

    private static ChalkboardClearResult ExecuteClearAsHost(
        int index,
        string expectedFingerprint,
        string issuerSteamId,
        out string message)
    {
        message = "No chalkboard changed.";
        if (!SteamIdModerationController.TryGetOwnedHostLobby(out CSteamID lobby, out _) ||
            !SteamIdModerationController.IsAuthorizedCurrentIssuer(issuerSteamId))
        {
            message = "Only the host or an assigned Helper can clear a board for everyone.";
            return ChalkboardClearResult.Rejected;
        }
        if (!TryResolveBoard(index.ToString(CultureInfo.InvariantCulture), out QuadPainterGPU board, out int resolvedIndex, out string currentFingerprint, out string error))
        {
            message = error;
            return ChalkboardClearResult.Rejected;
        }
        if (resolvedIndex != index || !string.Equals(currentFingerprint, expectedFingerprint, StringComparison.Ordinal))
        {
            message = "That board changed since it was picked. No board changed; run /chalk boards again.";
            return ChalkboardClearResult.Rejected;
        }
        if (!board.isServer)
        {
            message = "Only the lobby host's game can change shared boards. No board changed.";
            return ChalkboardClearResult.Rejected;
        }
        if (_rollbackUnresolved || ChalkboardSnapshotTransportController.DestructiveActionsBlocked)
        {
            message = "An earlier board undo didn't finish, so load and clear stay paused until the boards reload or the game restarts.";
            return ChalkboardClearResult.Rejected;
        }

        string cooldownKey = ((ulong)lobby) + "|" + currentFingerprint;
        if (LastClearByBoard.TryGetValue(cooldownKey, out DateTime lastClear) &&
            (DateTime.UtcNow - lastClear).TotalSeconds < ClearCooldownSeconds)
        {
            message = "That board was just cleared. Wait a few seconds, then run /chalk boards again.";
            return ChalkboardClearResult.Rejected;
        }

        string backupName = "modclear-b" + index + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        if (!TrySaveResolved(backupName, board, index, out string backupMessage))
        {
            message = "The backup before clearing couldn't be saved, so the board was left as it was.";
            return ChalkboardClearResult.Rejected;
        }

        int[][] rollbackGrid = CopyGrid(board.PaintColors);
        try
        {
            int[][] blankGrid = CreateBlankGrid(board.PaintColors);
            ChalkboardDeliveryResult delivery = ApplyGridAndSync(board, blankGrid);
            LastClearByBoard[cooldownKey] = DateTime.UtcNow;
            string truth = ChalkboardModerationController.BuildTruth(
                "clear",
                index,
                DescribeBoardScene(board, index),
                backupName,
                "ready");
            if (!delivery.IsComplete)
            {
                message = WithLoggedDetail(
                    $"Cleared board {index} (backup saved as '{backupName}'), but some players may still see the old drawing. " +
                    $"After {ClearCooldownSeconds} seconds, /chalk clear {index} is safe to run again.",
                    $"clear partial: attempted {delivery.Attempted}, sent {delivery.Sent}, skipped {delivery.Skipped}; retry-safe because the authoritative board is already blank. " +
                    truth);
                return ChalkboardClearResult.Partial;
            }
            message = WithLoggedDetail(
                $"Cleared board {index}. A backup was saved first as '{backupName}'.",
                $"clear complete: attempted {delivery.Attempted}, sent {delivery.Sent}, skipped {delivery.Skipped}. " +
                truth);
            return ChalkboardClearResult.Complete;
        }
        catch (Exception ex)
        {
            bool rollbackSucceeded = false;
            try
            {
                ApplyGridAndSync(board, rollbackGrid);
                rollbackSucceeded = true;
            }
            catch (Exception rollbackEx)
            {
                RestoreGridValues(board, rollbackGrid);
                _rollbackUnresolved = true;
                ChalkboardSnapshotTransportController.MarkRollbackUnresolved(
                    "clear " + rollbackEx.GetType().Name);
                Plugin.Log?.LogError(
                    "Chalkboard clear rollback repaint failed: " + rollbackEx.GetType().Name + ": " + rollbackEx.Message);
            }
            Plugin.Log?.LogWarning("Chalkboard clear failed safely: " + ex.GetType().Name + ": " + ex.Message);
            message = WithLoggedDetail(
                rollbackSucceeded
                    ? $"Clearing the board didn't work, so it was put back. A backup is saved as '{backupName}'."
                    : $"Clearing the board didn't work and it couldn't be put back fully. Load and clear are paused; a backup is saved as '{backupName}'.",
                "clear failed; rollback=" +
                (rollbackSucceeded ? "restored" : "unresolved-blocking") +
                ", recovery snapshot='" + backupName +
                "', vanillaObserver=not-observed.");
            return ChalkboardClearResult.Rejected;
        }
    }

    private static ChalkboardDeliveryResult ApplyGridAndSync(QuadPainterGPU board, int[][] grid)
    {
        if (board == null || board.PaintColors == null || grid == null ||
            grid.Length != board.PaintColors.Length)
        {
            throw new InvalidDataException("board grid dimensions changed");
        }
        for (int x = 0; x < grid.Length; x++)
        {
            if (board.PaintColors[x].Ints == null || grid[x] == null ||
                grid[x].Length != board.PaintColors[x].Ints.Count)
            {
                throw new InvalidDataException("board grid dimensions changed");
            }
            for (int y = 0; y < grid[x].Length; y++)
            {
                board.PaintColors[x].Ints[y] = grid[x][y];
            }
        }

        if (!ChalkboardSnapshotTransportController.PrepareForFullSnapshot(board))
        {
            throw new InvalidOperationException("full-board render replacement could not clear the pending-pixel queue and render texture");
        }
        board.GetQuadImage_Original_1(default(PlayerID), board.PaintColors);
        IReadOnlyList<PlayerIdentityEvidence> verifiedRecipients =
            PlayerIdentityEvidenceController.GetVerifiedRoster() ?? Array.Empty<PlayerIdentityEvidence>();
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        string localNetworkPlayerId = board.localPlayerForced.ToString();
        int attemptedRecipients = 0;
        int sentRecipients = 0;
        int skippedRecipients = 0;
        foreach (PlayerIdentityEvidence evidence in verifiedRecipients)
        {
            if (evidence == null ||
                evidence.IsLocal ||
                string.Equals(evidence.NetworkPlayerId, localNetworkPlayerId, StringComparison.Ordinal))
            {
                continue;
            }
            bool compatibleRecipient =
                ChalkboardModerationController.IsCompatibleRecipient(evidence);
            if (compatibleRecipient)
            {
                attemptedRecipients++;
            }
            int rosterIndex = evidence.RosterIndex;
            if (panel?.PlayerIDs == null ||
                rosterIndex < 0 ||
                rosterIndex >= panel.PlayerIDs.Count)
            {
                if (compatibleRecipient)
                {
                    skippedRecipients++;
                }
                continue;
            }
            PlayerID player = panel.PlayerIDs[rosterIndex];
            if (!string.Equals(player.ToString(), evidence.NetworkPlayerId, StringComparison.Ordinal) ||
                player == board.localPlayerForced)
            {
                if (compatibleRecipient)
                {
                    skippedRecipients++;
                }
                continue;
            }
            try
            {
                board.GetQuadImage(player, board.PaintColors);
                if (compatibleRecipient)
                {
                    sentRecipients++;
                }
            }
            catch (Exception ex)
            {
                if (compatibleRecipient)
                {
                    skippedRecipients++;
                }
                Plugin.Log?.LogWarning(
                    "Chalkboard verified-recipient snapshot delivery skipped safely for roster " +
                    rosterIndex + ": " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        return new ChalkboardDeliveryResult(attemptedRecipients, sentRecipients, skippedRecipients);
    }

    private static int[][] CopyGrid(IntList[] grid)
    {
        ValidateGrid(grid);
        var copy = new int[grid.Length][];
        for (int x = 0; x < grid.Length; x++)
        {
            copy[x] = grid[x].Ints.ToArray();
        }
        return copy;
    }

    private static int[][] CreateBlankGrid(IntList[] grid)
    {
        ValidateGrid(grid);
        var blank = new int[grid.Length][];
        for (int x = 0; x < grid.Length; x++)
        {
            blank[x] = new int[grid[x].Ints.Count];
        }
        return blank;
    }

    private static void RestoreGridValues(QuadPainterGPU board, int[][] grid)
    {
        if (board?.PaintColors == null || grid == null || grid.Length != board.PaintColors.Length)
        {
            return;
        }
        for (int x = 0; x < grid.Length; x++)
        {
            if (grid[x] == null || board.PaintColors[x].Ints == null ||
                grid[x].Length != board.PaintColors[x].Ints.Count)
            {
                continue;
            }
            for (int y = 0; y < grid[x].Length; y++)
            {
                board.PaintColors[x].Ints[y] = grid[x][y];
            }
        }
    }

    private static bool TryResolveBoard(
        string boardToken,
        out QuadPainterGPU board,
        out int index,
        out string fingerprint,
        out string error)
    {
        board = null;
        index = -1;
        fingerprint = string.Empty;
        DrawingManager manager = MonoSingleton<DrawingManager>.I;
        RefreshBoardSetState(manager);
        int boardCount = manager?.QuadPainterGPUS?.Count ?? 0;
        if (!ChalkboardModerationPolicy.TryParseExplicitBoardIndex(boardToken, boardCount, out index, out error))
        {
            return false;
        }
        board = manager.QuadPainterGPUS[index];
        if (board == null)
        {
            error = "Board " + index + " is unavailable. Run /chalk boards again.";
            return false;
        }
        if (!ChalkboardCatalogPolicy.TryGet(index, out ChalkboardCatalogEntry catalogEntry))
        {
            error = "Board " + index + " isn't one BlueSage can save or clear. Use boards 0, 1, or 2.";
            return false;
        }

        string sceneName = board.gameObject.scene.name ?? string.Empty;
        string hierarchyPath = BuildHierarchyPath(board.transform);
        Vector3 position = board.transform.position;
        string mappedLocation = ChalkboardModerationPolicy.ClassifyBuild24259292Location(
            index,
            position.x,
            position.y,
            position.z);
        if (!string.Equals(mappedLocation, catalogEntry.Label, StringComparison.Ordinal))
        {
            error = "Board " + index + " isn't where BlueSage expects it, so nothing was done. Run /chalk boards again.";
            return false;
        }
        fingerprint = ChalkboardModerationPolicy.BuildBoardFingerprint(
            sceneName,
            hierarchyPath,
            position.x,
            position.y,
            position.z);
        return true;
    }

    private static void RefreshBoardSetState(DrawingManager manager)
    {
        string currentSignature = BuildBoardSetSignature(manager);
        if (string.Equals(_boardSetSignature, currentSignature, StringComparison.Ordinal))
        {
            return;
        }

        _boardSetSignature = currentSignature;
        ResetRelayAndCooldownState();
    }

    private static string BuildBoardSetSignature(DrawingManager manager)
    {
        if (manager?.QuadPainterGPUS == null || manager.QuadPainterGPUS.Count == 0)
        {
            return "<no-boards>";
        }

        var fingerprints = new List<string>(manager.QuadPainterGPUS.Count);
        for (int index = 0; index < manager.QuadPainterGPUS.Count; index++)
        {
            QuadPainterGPU board = manager.QuadPainterGPUS[index];
            if (board == null)
            {
                fingerprints.Add(index.ToString(CultureInfo.InvariantCulture) + ":unavailable");
                continue;
            }

            Vector3 position = board.transform.position;
            fingerprints.Add(
                index.ToString(CultureInfo.InvariantCulture) + ":" +
                ChalkboardModerationPolicy.BuildBoardFingerprint(
                    board.gameObject.scene.name ?? string.Empty,
                    BuildHierarchyPath(board.transform),
                    position.x,
                    position.y,
                    position.z));
        }
        return string.Join("|", fingerprints);
    }

    private static void ResetRelayAndCooldownState()
    {
        SeenClearNonces.Clear();
        ClearNonceOrder.Clear();
        LastClearRequestByIssuer.Clear();
        LastClearByBoard.Clear();
        _lastSharedRestoreUtc = DateTime.MinValue;
        _lastListedBoardSetSignature = string.Empty;
        _lastListedBoardSetUnixSeconds = 0;
        _rollbackUnresolved = false;
        ChalkboardSnapshotTransportController.ResetForBoardSetChange();
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        var segments = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            segments.Push(SafeDisplayName(current.name) + "[" + current.GetSiblingIndex() + "]");
            current = current.parent;
        }
        return string.Join("/", segments.ToArray());
    }

    private static string SafeDisplayName(string value)
    {
        string normalized = (value ?? "Board")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("|", "/")
            .Trim();
        return normalized.Length == 0 ? "Board" : normalized;
    }

    private static int CountLobbyMembers(CSteamID lobby, string steamId)
    {
        int matches = 0;
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int index = 0; index < memberCount; index++)
        {
            if (string.Equals(
                    ((ulong)SteamMatchmaking.GetLobbyMemberByIndex(lobby, index)).ToString(),
                    steamId,
                    StringComparison.Ordinal))
            {
                matches++;
            }
        }
        return matches;
    }

    private static bool RememberClearNonce(string nonce)
    {
        if (!SeenClearNonces.Add(nonce)) return false;
        ClearNonceOrder.Enqueue(nonce);
        while (ClearNonceOrder.Count > 128)
        {
            SeenClearNonces.Remove(ClearNonceOrder.Dequeue());
        }
        return true;
    }

    private static string Suffix(string steamId)
    {
        return steamId != null && steamId.Length > 6
            ? steamId.Substring(steamId.Length - 6)
            : steamId ?? "unknown";
    }

    private static int[][] ReadGrid(string path, QuadPainterGPU board)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            if (reader.ReadString() != Magic)
            {
                throw new InvalidDataException("unsupported save version");
            }
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            if (width != board.PaintColors.Length || width < 1 || width > MaxDimension ||
                height != board.PaintColors[0].Ints.Count || height < 1 || height > MaxDimension)
            {
                throw new InvalidDataException("board dimensions do not match this board");
            }
            int[][] grid = new int[width][];
            for (int x = 0; x < width; x++)
            {
                grid[x] = new int[height];
                for (int y = 0; y < height; y++)
                {
                    int value = reader.ReadInt32();
                    if (value < 0 || value > MaxColorIndex)
                    {
                        throw new InvalidDataException("invalid chalk color index");
                    }
                    grid[x][y] = value;
                }
            }
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException("unexpected trailing board data");
            }
            return grid;
        }
    }

    private static bool TryNormalizeName(
        string name,
        out string normalized,
        out string error)
    {
        normalized = (name ?? string.Empty).Trim();
        error = string.Empty;
        if (SafeName.IsMatch(normalized)) return true;
        error = "Chalkboard name must be 1-40 letters, numbers, underscores, or hyphens.";
        return false;
    }

    private static string DescribeBoardScene(QuadPainterGPU board, int index)
    {
        string label = ChalkboardCatalogPolicy.TryGet(index, out ChalkboardCatalogEntry entry)
            ? entry.Label
            : "unmapped";
        string scene = board?.gameObject.scene.name ?? "unavailable";
        return label + "@" + SafeDisplayName(scene);
    }

    private static bool TryResolve(string name, string boardToken, out string normalized, out QuadPainterGPU board, out int index, out string error)
    {
        normalized = string.Empty;
        board = null;
        index = -1;
        error = string.Empty;
        if (!TryNormalizeName(name, out normalized, out error))
        {
            return false;
        }
        DrawingManager manager = MonoSingleton<DrawingManager>.I;
        if (manager == null || manager.QuadPainterGPUS == null)
        {
            error = "Chalkboards are not ready in this scene.";
            return false;
        }
        RefreshBoardSetState(manager);
        index = !string.IsNullOrWhiteSpace(boardToken) && int.TryParse(boardToken, out int parsed)
            ? parsed
            : (int)(BoardIndexField?.GetValue(manager) ?? -1);
        if (index < 0 || index >= manager.QuadPainterGPUS.Count)
        {
            error = $"Choose a board first or provide an index from 0 to {Math.Max(0, manager.QuadPainterGPUS.Count - 1)}.";
            return false;
        }
        board = manager.QuadPainterGPUS[index];
        if (board == null ||
            !ChalkboardCatalogPolicy.TryGet(index, out ChalkboardCatalogEntry entry))
        {
            error = "That board isn't one BlueSage can save. Use boards 0, 1, or 2.";
            return false;
        }
        Vector3 position = board.transform.position;
        string mapped = ChalkboardModerationPolicy.ClassifyBuild24259292Location(
            index,
            position.x,
            position.y,
            position.z);
        if (!string.Equals(mapped, entry.Label, StringComparison.Ordinal))
        {
            error = "That board isn't where BlueSage expects it. Run /chalk boards and pick 0, 1, or 2.";
            return false;
        }
        return true;
    }

    private static void ValidateGrid(IntList[] grid)
    {
        if (grid == null || grid.Length < 1 || grid.Length > MaxDimension || grid[0].Ints == null ||
            grid[0].Ints.Count < 1 || grid[0].Ints.Count > MaxDimension)
        {
            throw new InvalidDataException("unsupported board dimensions");
        }
        int height = grid[0].Ints.Count;
        foreach (IntList column in grid)
        {
            if (column.Ints == null || column.Ints.Count != height || column.Ints.Any(value => value < 0 || value > MaxColorIndex))
            {
                throw new InvalidDataException("invalid board grid");
            }
        }
    }
}
