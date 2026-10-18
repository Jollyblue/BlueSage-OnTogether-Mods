using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal static class ChalkboardSnapshotTransportController
{
    private static readonly System.Reflection.FieldInfo RenderTextureField =
        AccessTools.Field(typeof(QuadPainterGPU), "_rt");
    private static readonly System.Reflection.FieldInfo PendingPixelsField =
        AccessTools.Field(typeof(QuadPainterGPU), "_pixelsToUpdate");
    private static bool _destructiveActionsBlocked;
    private static string _rollbackFailure = string.Empty;

    internal static bool DestructiveActionsBlocked => _destructiveActionsBlocked;
    internal static string RollbackState =>
        _destructiveActionsBlocked
            ? "unresolved (" + _rollbackFailure + ")"
            : "ready";

    internal static void MarkRollbackUnresolved(string reason)
    {
        _destructiveActionsBlocked = true;
        _rollbackFailure = Bound(reason);
    }

    internal static void ResetForBoardSetChange()
    {
        _destructiveActionsBlocked = false;
        _rollbackFailure = string.Empty;
    }

    internal static bool PrepareForFullSnapshot(QuadPainterGPU board)
    {
        if (!ChalkboardPersistenceController.Available || board == null) return false;
        try
        {
            RenderTexture target = RenderTextureField?.GetValue(board) as RenderTexture;
            var pendingPixels = PendingPixelsField?.GetValue(board) as Dictionary<Vector2Int, Color>;
            if (target == null || pendingPixels == null) return false;
            pendingPixels.Clear();
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
            }
            finally
            {
                RenderTexture.active = previous;
            }
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug(
                "Chalkboard full-snapshot render clear skipped safely: " +
                ex.GetType().Name +
                ": " +
                ex.Message);
            return false;
        }
    }

    private static string Bound(string value)
    {
        string safe = (value ?? "unknown").Replace("\r", " ").Replace("\n", " ").Trim();
        return safe.Length <= 96 ? safe : safe.Substring(0, 96);
    }
}
