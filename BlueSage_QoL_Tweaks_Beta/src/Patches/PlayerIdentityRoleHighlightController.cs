using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

internal sealed class BlueSageIdentityRoleHighlightMarker : MonoBehaviour
{
    internal UnityEngine.UI.Outline Effect;
}

internal static class PlayerIdentityRoleHighlightController
{
    private static readonly Color VictimColor = new Color(0.20f, 0.82f, 0.92f, 1f);
    private static readonly Color SuspectColor = new Color(1f, 0.43f, 0.18f, 1f);
    private static readonly Color BothColor = new Color(0.72f, 0.46f, 0.94f, 1f);
    private static readonly FieldInfo PlayerSteamIdField = AccessTools.Field(typeof(PlayerItemController), "_playerSteamId");
    private static readonly FieldInfo SteamButtonField = AccessTools.Field(typeof(PlayerItemController), "_steamButton");

    internal static void RefreshAll()
    {
        try
        {
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel?.PlayerItemControllers == null) return;
            bool applyHighlights = Plugin.ShouldApplyCloneShield && Plugin.ShouldApplyEnhancedPlayerPanel && Plugin.CanUseLobbySafety(out _);
            HashSet<string> verifiedSteamIds = applyHighlights
                ? PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIdSetCached()
                : null;
            foreach (PlayerItemController row in panel.PlayerItemControllers)
            {
                RefreshRow(row, applyHighlights, verifiedSteamIds);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Clone Shield role highlights refreshed safely with partial results: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    internal static void RefreshRows(IReadOnlyCollection<string> targetedSteamIds)
    {
        if (targetedSteamIds == null || targetedSteamIds.Count == 0) return;
        try
        {
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel?.PlayerItemControllers == null) return;

            var targets = new HashSet<string>(targetedSteamIds, StringComparer.Ordinal);
            bool applyHighlights = Plugin.ShouldApplyCloneShield && Plugin.ShouldApplyEnhancedPlayerPanel && Plugin.CanUseLobbySafety(out _);
            HashSet<string> verifiedSteamIds = applyHighlights
                ? PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIdSetCached()
                : null;
            foreach (PlayerItemController row in panel.PlayerItemControllers)
            {
                string steamId = PlayerSteamIdField?.GetValue(row) as string ?? string.Empty;
                if (targets.Contains(steamId)) RefreshRow(row, applyHighlights, verifiedSteamIds);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Clone Shield targeted identity-row highlights refreshed safely with partial results: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    internal static void RefreshRowsFromAlreadyAvailableSnapshot(
        IReadOnlyCollection<string> targetedSteamIds)
    {
        if (targetedSteamIds == null || targetedSteamIds.Count == 0) return;
        try
        {
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel?.PlayerItemControllers == null) return;

            var targets = new HashSet<string>(
                targetedSteamIds,
                StringComparer.Ordinal);
            bool applyHighlights =
                Plugin.ShouldApplyCloneShield &&
                Plugin.ShouldApplyEnhancedPlayerPanel &&
                Plugin.CanUseLobbySafety(out _);
            HashSet<string> verifiedSteamIds = applyHighlights
                ? PlayerIdentityEvidenceController
                    .GetVerifiedCurrentSteamIdSetAlreadyCached()
                : null;
            foreach (PlayerItemController row in panel.PlayerItemControllers)
            {
                string steamId =
                    PlayerSteamIdField?.GetValue(row) as string ??
                    string.Empty;
                if (targets.Contains(steamId))
                {
                    RefreshRow(
                        row,
                        applyHighlights,
                        verifiedSteamIds);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug(
                "Clone Shield cached identity-row refresh failed safely: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    internal static void ClearAll()
    {
        try
        {
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel?.PlayerItemControllers == null) return;
            foreach (PlayerItemController row in panel.PlayerItemControllers) RefreshRow(row, false, null);
        }
        catch
        {
            // Unload cleanup is best effort and never owns the native button.
        }
    }

    internal static void RefreshInitializedRow(PlayerItemController row)
    {
        if (row == null) return;
        bool applyHighlights = Plugin.ShouldApplyCloneShield && Plugin.ShouldApplyEnhancedPlayerPanel && Plugin.CanUseLobbySafety(out _);
        RefreshRow(row, applyHighlights, null);
    }

    internal static Color GetRoleTextColor(string steamId, Color fallback)
    {
        if (!Plugin.ShouldApplyCloneShield || !Plugin.CanUseLobbySafety(out _)) return fallback;
        CloneIncidentLedger.GetRoleState(steamId, out bool victim, out bool suspect);
        if (victim && suspect) return BothColor;
        if (victim) return VictimColor;
        if (suspect) return SuspectColor;
        return fallback;
    }

    internal static string GetRoleHeader(string steamId)
    {
        if (!Plugin.ShouldApplyCloneShield || !Plugin.CanUseLobbySafety(out _)) return "Steam Profile • VERIFIED";
        CloneIncidentLedger.GetRoleState(steamId, out bool victim, out bool suspect);
        if (victim && suspect) return "Steam Profile • INCIDENT ROLES";
        if (victim) return "Steam Profile • PROTECTED VICTIM";
        if (suspect) return "Steam Profile • SUSPECT — VERIFY";
        return "Steam Profile • VERIFIED";
    }

    private static void RefreshRow(PlayerItemController row, bool applyHighlights, HashSet<string> verifiedSteamIds)
    {
        if (row == null) return;
        string steamId = PlayerSteamIdField?.GetValue(row) as string ?? string.Empty;
        CloneIncidentLedger.GetRoleState(steamId, out bool victim, out bool suspect);
        if (!applyHighlights || (!victim && !suspect))
        {
            RemoveMarker(row.transform);
            return;
        }

        bool verified = verifiedSteamIds != null
            ? verifiedSteamIds.Contains(steamId)
            : PlayerIdentityEvidenceController.TryResolveExactCached(steamId, out _, out _);
        if (!verified)
        {
            victim = false;
            suspect = false;
        }
        Transform friendButton = FindSteamProfileButton(row.transform);
        if (friendButton == null) return;

        BlueSageIdentityRoleHighlightMarker marker = friendButton.GetComponent<BlueSageIdentityRoleHighlightMarker>();
        if (!victim && !suspect)
        {
            if (marker?.Effect != null) UnityEngine.Object.Destroy(marker.Effect);
            if (marker != null) UnityEngine.Object.Destroy(marker);
            return;
        }

        if (marker == null) marker = friendButton.gameObject.AddComponent<BlueSageIdentityRoleHighlightMarker>();
        if (marker.Effect == null) marker.Effect = friendButton.gameObject.AddComponent<UnityEngine.UI.Outline>();
        marker.Effect.effectColor = victim && suspect ? BothColor : victim ? VictimColor : SuspectColor;
        marker.Effect.effectDistance = new Vector2(3f, -3f);
        marker.Effect.useGraphicAlpha = false;
    }

    private static void RemoveMarker(Transform row)
    {
        Transform friendButton = FindSteamProfileButton(row);
        if (friendButton == null) return;
        BlueSageIdentityRoleHighlightMarker marker = friendButton.GetComponent<BlueSageIdentityRoleHighlightMarker>();
        if (marker?.Effect != null) UnityEngine.Object.Destroy(marker.Effect);
        if (marker != null) UnityEngine.Object.Destroy(marker);
    }

    private static Transform FindSteamProfileButton(Transform row)
    {
        if (row == null) return null;
        PlayerItemController controller = row.GetComponent<PlayerItemController>();
        Button nativeSteamButton = SteamButtonField?.GetValue(controller) as Button;
        if (nativeSteamButton != null) return nativeSteamButton.transform;

        // Voice-era prefabs still expose _steamButton. The name scan is a
        // fail-soft fallback for a future harmless field rename.
        return row.GetComponentsInChildren<Button>(true)
            .Select(button => button.transform)
            .FirstOrDefault(transform =>
            {
                string name = transform.name ?? string.Empty;
                return name.IndexOf("friend", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("steam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       name.IndexOf("profile", StringComparison.OrdinalIgnoreCase) >= 0;
            });
    }
}
