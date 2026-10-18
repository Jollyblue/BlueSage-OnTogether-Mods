using System;
using System.Collections.Generic;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

internal static class HostVisibilityProtocolPatch
{
    private static readonly Dictionary<Renderer, bool> RendererStates = new Dictionary<Renderer, bool>();
    private static readonly Dictionary<Canvas, bool> CanvasStates = new Dictionary<Canvas, bool>();
    private static readonly Dictionary<TMP_Text, bool> TextStates = new Dictionary<TMP_Text, bool>();
    private static readonly FieldInfo PlayerCanvasesField = AccessTools.Field(typeof(PlayerController), "_canvases");
    private static readonly FieldInfo PlayerCanvasField = AccessTools.Field(typeof(PlayerController), "_playerCanvas");
    private static readonly FieldInfo PlayerNameTextField = AccessTools.Field(typeof(PlayerController), "_playerNameText");
    private static readonly FieldInfo TextBubbleCanvasField = AccessTools.Field(typeof(TextBubbleController), "_textBubbleCanvas");
    private static readonly FieldInfo TextBubbleTextField = AccessTools.Field(typeof(TextBubbleController), "_textBubble");
    private static readonly FieldInfo TextBubbleNameTextField = AccessTools.Field(typeof(TextBubbleController), "_nameText");
    private static Transform _hostRoot;
    private static float _lastReceivedAt = -100f;
    private static float _nextValidationAt;
    private static string _ownerSteam = string.Empty;
    private static int _lastReportedRendererCount;
    private const string MemberDataKey = "bluesage_visibility_v2";
    private const int MemberDataExpirySeconds = 8;

    internal static void Tick()
    {
        if (Time.unscaledTime < _nextValidationAt) return;
        _nextValidationAt = Time.unscaledTime + 0.5f;
        PollOwnerMemberData();
        if (_hostRoot == null) return;
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        bool sameOwner = Plugin.TryGetRescueLobby(out CSteamID lobby) && string.Equals(((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString(), _ownerSteam, StringComparison.Ordinal);
        if (!sameOwner || Time.unscaledTime - _lastReceivedAt > MemberDataExpirySeconds || _hostRoot.gameObject == null || !_hostRoot.gameObject.activeInHierarchy || panel?.PlayerSteamIDs == null || !panel.PlayerSteamIDs.Contains(_ownerSteam)) RestoreAll();
    }

    private static void PollOwnerMemberData()
    {
        try
        {
            if (!SteamManager.Initialized || !Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil)
            {
                RestoreAll();
                return;
            }
            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobby);
            string ownerSteam = ((ulong)ownerId).ToString();
            string localSteam = ((ulong)SteamUser.GetSteamID()).ToString();
            if (!HostVisibilityReceiverPolicy.ShouldConsumeRemoteOwnerState(ownerSteam, localSteam))
            {
                // A companion mod owns the host client's exact local character lease. Replaying a
                // public receiver snapshot on that same client can capture false renderer states
                // and re-hide Blue after the private lease restores. Drop that stale receiver
                // snapshot without writing it back; the private owner remains authoritative.
                ReleaseTrackedStateWithoutRestore("local owner heartbeat ignored");
                return;
            }

            string value = SteamMatchmaking.GetLobbyMemberData(lobby, ownerId, MemberDataKey) ?? string.Empty;
            string[] parts = value.Split('|');
            if (parts.Length != 4 || parts[0] != "v2" || (parts[1] != "hide" && parts[1] != "show") ||
                !long.TryParse(parts[2], out long issued) || parts[3].Length < 4 || parts[3].Length > 16) return;
            foreach (char character in parts[3]) if (!char.IsLetterOrDigit(character)) return;
            long age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issued;
            if (age < -2 || age > MemberDataExpirySeconds) return;
            _lastReceivedAt = Time.unscaledTime;
            if (parts[1] == "show")
            {
                RestoreAll();
                _nextValidationAt = Time.unscaledTime + 0.5f;
                return;
            }
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel?.PlayerSteamIDs == null || panel.PlayerTransforms == null) return;
            int ownerIndex = -1;
            for (int index = 0; index < panel.PlayerSteamIDs.Count && index < panel.PlayerTransforms.Count; index++)
            {
                if (!string.Equals(panel.PlayerSteamIDs[index], ownerSteam, StringComparison.Ordinal)) continue;
                Transform candidate = panel.PlayerTransforms[index]?.transform;
                if (candidate == null || candidate.gameObject == null || !candidate.gameObject.activeInHierarchy) continue;
                if (ownerIndex >= 0)
                {
                    Plugin.Log?.LogDebug("BlueSage hidden visibility poll rejected duplicate active owner rows.");
                    return;
                }

                ownerIndex = index;
            }
            if (ownerIndex < 0) return;
            Transform ownerRoot = panel.PlayerTransforms[ownerIndex]?.transform;
            if (ownerRoot == null) return;
            if (_hostRoot != ownerRoot || !string.Equals(_ownerSteam, ownerSteam, StringComparison.Ordinal))
            {
                RestoreAll();
                _hostRoot = ownerRoot;
                _ownerSteam = ownerSteam;
            }
            _ownerSteam = ownerSteam;
            _lastReceivedAt = Time.unscaledTime;
            HideCurrentVisuals();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("BlueSage hidden visibility poll failed closed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void HideCurrentVisuals()
    {
        if (_hostRoot == null) return;
        foreach (Renderer renderer in _hostRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            if (!RendererStates.ContainsKey(renderer)) RendererStates[renderer] = renderer.enabled;
            renderer.enabled = false;
        }

        PlayerController player = _hostRoot.GetComponent<PlayerController>() ?? _hostRoot.GetComponentInChildren<PlayerController>(true);
        if (player != null)
        {
            if (PlayerCanvasesField?.GetValue(player) is List<Canvas> playerCanvases)
            {
                foreach (Canvas canvas in playerCanvases) CaptureAndHideCanvas(canvas);
            }

            GameObject playerCanvas = PlayerCanvasField?.GetValue(player) as GameObject;
            if (playerCanvas != null) CaptureAndHideCanvas(playerCanvas.GetComponent<Canvas>());
            CaptureAndHideText(PlayerNameTextField?.GetValue(player) as TMP_Text);

            TextBubbleController textBubble = player.GetComponentInChildren<TextBubbleController>(true);
            if (textBubble != null)
            {
                CaptureAndHideCanvas(TextBubbleCanvasField?.GetValue(textBubble) as Canvas);
                CaptureAndHideText(TextBubbleTextField?.GetValue(textBubble) as TMP_Text);
                CaptureAndHideText(TextBubbleNameTextField?.GetValue(textBubble) as TMP_Text);
            }
        }

        int hiddenVisualCount = RendererStates.Count + CanvasStates.Count + TextStates.Count;
        if (hiddenVisualCount != _lastReportedRendererCount)
        {
            _lastReportedRendererCount = hiddenVisualCount;
            string suffix = _ownerSteam.Length > 4 ? _ownerSteam.Substring(_ownerSteam.Length - 4) : _ownerSteam;
            Plugin.Log?.LogDebug("BlueSage hidden visibility receiver: owner *" + suffix + ", avatar visuals hidden=" + hiddenVisualCount + ".");
        }
    }

    private static void CaptureAndHideCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        if (!CanvasStates.ContainsKey(canvas)) CanvasStates[canvas] = canvas.enabled;
        canvas.enabled = false;
    }

    private static void CaptureAndHideText(TMP_Text text)
    {
        if (text == null) return;
        if (!TextStates.ContainsKey(text)) TextStates[text] = text.enabled;
        text.enabled = false;
    }

    internal static void RestoreAll()
    {
        int restored = RendererStates.Count + CanvasStates.Count + TextStates.Count;
        foreach (KeyValuePair<Renderer, bool> state in RendererStates) if (state.Key != null) state.Key.enabled = state.Value;
        foreach (KeyValuePair<Canvas, bool> state in CanvasStates) if (state.Key != null) state.Key.enabled = state.Value;
        foreach (KeyValuePair<TMP_Text, bool> state in TextStates) if (state.Key != null) state.Key.enabled = state.Value;
        ClearVisualSnapshots();
        _hostRoot = null;
        _nextValidationAt = 0f;
        _ownerSteam = string.Empty;
        _lastReportedRendererCount = 0;
        if (restored > 0) Plugin.Log?.LogDebug("BlueSage hidden visibility receiver: restored=" + restored + ".");
    }

    private static void ReleaseTrackedStateWithoutRestore(string reason)
    {
        int released = RendererStates.Count + CanvasStates.Count + TextStates.Count;
        ClearVisualSnapshots();
        _hostRoot = null;
        _ownerSteam = string.Empty;
        _lastReceivedAt = -100f;
        _lastReportedRendererCount = 0;
        if (released > 0)
        {
            Plugin.Log?.LogDebug("BlueSage hidden visibility receiver: released=" + released + " without replay (" + reason + ").");
        }
    }

    private static void ClearVisualSnapshots()
    {
        RendererStates.Clear();
        CanvasStates.Clear();
        TextStates.Clear();
    }
}
