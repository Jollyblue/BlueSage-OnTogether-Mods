using System;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerPanelController), "OpenButtonInfoPopup")]
internal static class PlayerIdentityTooltipPatch
{
    private static bool _defaultPanelSizeCaptured;
    private static int _capturedPanelInstanceId;
    private static Vector2 _defaultPanelSize;
    private static TextWrappingModes _defaultWrappingMode;
    private static TextOverflowModes _defaultOverflowMode;
    private static bool _defaultAutoSizing;
    private static float _defaultBodyFontSize;
    private static float _defaultFontSizeMin;
    private static float _defaultFontSizeMax;
    private static Color _defaultHeaderColor;

    [HarmonyPrefix]
    private static void Prefix(PlayerPanelController __instance)
    {
        RectTransform panel = AccessTools.Field(typeof(PlayerPanelController), "_buttonInfoPanelTransfrom")?.GetValue(__instance) as RectTransform;
        TMP_Text header = AccessTools.Field(typeof(PlayerPanelController), "_buttonInfoHeaderText")?.GetValue(__instance) as TMP_Text;
        TMP_Text body = AccessTools.Field(typeof(PlayerPanelController), "_buttonInfoText")?.GetValue(__instance) as TMP_Text;
        if (panel == null || header == null || body == null) return;
        int panelInstanceId = panel.GetInstanceID();
        if (!_defaultPanelSizeCaptured || panelInstanceId != _capturedPanelInstanceId)
        {
            _capturedPanelInstanceId = panelInstanceId;
            _defaultPanelSize = panel.sizeDelta;
            _defaultWrappingMode = body.textWrappingMode;
            _defaultOverflowMode = body.overflowMode;
            _defaultAutoSizing = body.enableAutoSizing;
            _defaultBodyFontSize = body.fontSize;
            _defaultFontSizeMin = body.fontSizeMin;
            _defaultFontSizeMax = body.fontSizeMax;
            _defaultHeaderColor = header.color;
            _defaultPanelSizeCaptured = true;
        }
        else
        {
            panel.sizeDelta = _defaultPanelSize;
            body.textWrappingMode = _defaultWrappingMode;
            body.overflowMode = _defaultOverflowMode;
            body.enableAutoSizing = _defaultAutoSizing;
            body.fontSize = _defaultBodyFontSize;
            body.fontSizeMin = _defaultFontSizeMin;
            body.fontSizeMax = _defaultFontSizeMax;
            header.color = _defaultHeaderColor;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(PlayerPanelController __instance, PlayerPanelButtonType buttonType, RectTransform buttonTransform)
    {
        if (!Plugin.ShouldApplyEnhancedPlayerPanel || __instance == null || buttonTransform == null || buttonType != PlayerPanelButtonType.AddFriend) return;

        try
        {
            PlayerItemController row = buttonTransform.GetComponentInParent<PlayerItemController>();
            string capturedSteamId = AccessTools.Field(typeof(PlayerItemController), "_playerSteamId")?.GetValue(row) as string ?? string.Empty;
            TMP_Text header = AccessTools.Field(typeof(PlayerPanelController), "_buttonInfoHeaderText")?.GetValue(__instance) as TMP_Text;
            TMP_Text body = AccessTools.Field(typeof(PlayerPanelController), "_buttonInfoText")?.GetValue(__instance) as TMP_Text;
            if (header == null || body == null) return;

            ExpandProfilePopup(__instance, body);
            bool hasLobbySafetyAccess = Plugin.CanUseLobbySafety(out _);

            if (PlayerIdentityEvidenceController.TryResolveExactCached(capturedSteamId, out PlayerIdentityEvidence evidence, out string error))
            {
                if (!hasLobbySafetyAccess)
                {
                    header.text = "Steam Profile • LIVE";
                    header.color = Color.white;
                    body.text = "Current Steam-lobby member.\nClick to open profile; QoL is not required.";
                    return;
                }

                string roleHeader = PlayerIdentityRoleHighlightController.GetRoleHeader(capturedSteamId);
                header.text = string.IsNullOrWhiteSpace(roleHeader) ? "Steam Profile • VERIFIED" : roleHeader;
                header.color = PlayerIdentityRoleHighlightController.GetRoleTextColor(capturedSteamId, Color.white);
                body.text = "SteamID64: " + evidence.SteamId +
                            "\nVerified live row #" + (evidence.RosterIndex + 1) + " • PlayerID " + Compact(evidence.NetworkPlayerId, 12) +
                            "\nSteam: " + Compact(evidence.SteamPersona, 24) + " • " + BuildCompactRoleSummary(evidence) +
                            "\nClick profile • HOME > Lobby Safety to review or clear flags.";
            }
            else
            {
                bool staleHistoryRow = error.StartsWith("left/stale", StringComparison.OrdinalIgnoreCase);
                bool liveSteamCheck = error.StartsWith("live/steam-check", StringComparison.OrdinalIgnoreCase);
                header.text = staleHistoryRow
                    ? "Steam Profile • LEFT / STALE"
                    : liveSteamCheck ? "Steam Profile • LIVE / STEAM CHECK" : "Steam Profile • CHECKING";
                header.color = Color.white;
                body.text = staleHistoryRow
                    ? "Player-history row; not in the current lobby.\nProfile opens, but moderation stays locked."
                    : liveSteamCheck
                        ? "Live in game, but missing from Steam's lobby list (usually an automatic reconnect).\nProfile opens; host moderation works and is labeled RECONNECTED."
                    : "Live identity is still syncing.\nReopen the player list before using this row.";
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Steam-profile identity tooltip skipped safely: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ExpandProfilePopup(PlayerPanelController instance, TMP_Text body)
    {
        RectTransform panel = AccessTools.Field(typeof(PlayerPanelController), "_buttonInfoPanelTransfrom")?.GetValue(instance) as RectTransform;
        if (panel != null)
        {
            panel.sizeDelta = new Vector2(Mathf.Max(panel.sizeDelta.x, 390f), Mathf.Max(panel.sizeDelta.y, 156f));
        }
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;
        body.enableAutoSizing = true;
        body.fontSizeMin = 12f;
        body.fontSizeMax = Mathf.Min(body.fontSizeMax, 20f);
    }

    private static string Compact(string value, int maximum)
    {
        string clean = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= maximum ? clean : clean.Substring(0, maximum - 1) + "…";
    }

    private static string BuildCompactRoleSummary(PlayerIdentityEvidence evidence)
    {
        string[] baseRoles = (evidence.Role ?? string.Empty).Split('•')
            .Select(role => role.Trim())
            .Where(role => role.Length > 0 &&
                           !role.StartsWith("VICTIM", StringComparison.Ordinal) &&
                           !role.StartsWith("SUSPECT", StringComparison.Ordinal))
            .ToArray();
        int victimCount = CloneIncidentLedger.Snapshot.Count(item => string.Equals(item.VictimSteamId, evidence.SteamId, StringComparison.Ordinal));
        int suspectCount = CloneIncidentLedger.Snapshot.Count(item => string.Equals(item.SuspectSteamId, evidence.SteamId, StringComparison.Ordinal));
        string roles = baseRoles.Length == 0 ? "PLAYER" : string.Join("/", baseRoles);
        return victimCount + suspectCount == 0 ? roles : roles + " • flags V" + victimCount + "/S" + suspectCount;
    }
}
