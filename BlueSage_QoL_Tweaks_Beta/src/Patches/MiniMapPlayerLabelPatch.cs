using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using Steamworks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(MapManager), "UpdatePosition")]
internal static class MiniMapPlayerLabelPatch
{
    private const float RefreshIntervalSeconds = 2f;
    private const float FailureLogRepeatSeconds = 30f;
    private const string MiniMapFriendsGuid =
        "com.xaviergmail.minimapfriends";
    private static readonly Color FriendColor =
        new Color(0.24f, 0.86f, 0.35f, 1f);
    private static readonly Dictionary<string, bool> FriendCache =
        new Dictionary<string, bool>(StringComparer.Ordinal);
    private static readonly Dictionary<int, MiniMapIdCardClickHandler>
        OwnedHandlersByIconInstanceId =
            new Dictionary<int, MiniMapIdCardClickHandler>();
    private static readonly Dictionary<int, bool>
        AppliedFriendColorStateByIconInstanceId =
            new Dictionary<int, bool>();
    private static readonly FieldInfo PlayerIconsField =
        AccessTools.Field(typeof(MapManager), "_playerIcons");
    private static float _nextRefreshAt;
    private static float _nextFailureLogAt;

    [HarmonyPostfix]
    private static void Postfix(MapManager __instance)
    {
        if (__instance == null)
        {
            return;
        }
        if (Time.unscaledTime < _nextRefreshAt)
        {
            return;
        }
        _nextRefreshAt =
            Time.unscaledTime + RefreshIntervalSeconds;

        try
        {
            List<RectTransform> icons =
                PlayerIconsField?.GetValue(__instance)
                    as List<RectTransform>;
            if (icons == null || icons.Count == 0)
            {
                return;
            }

            PlayerPanelController panel =
                NetworkSingleton<PlayerPanelController>.I;
            bool friendColors =
                Plugin.ShouldApplyMiniMapFriendColors &&
                !Chainloader.PluginInfos.ContainsKey(
                    MiniMapFriendsGuid);
            if ((!Plugin.ShouldApplyMiniMapPlayerLabels &&
                 !friendColors) ||
                panel == null ||
                panel.IDInfos == null ||
                panel.PlayerSteamIDs == null ||
                panel.PlayerIDs == null ||
                panel.PlayerTransforms == null ||
                panel.PlayerItemControllers == null)
            {
                SetAllIconEnhancementsEnabled(icons, false);
                return;
            }

            int safeCount = Math.Min(
                icons.Count,
                Math.Min(
                    panel.IDInfos.Count,
                    Math.Min(
                        panel.PlayerSteamIDs.Count,
                        Math.Min(
                            panel.PlayerIDs.Count,
                            Math.Min(
                                panel.PlayerTransforms.Count,
                                panel.PlayerItemControllers.Count)))));
            for (int index = 0; index < icons.Count; index++)
            {
                RectTransform icon = icons[index];
                if (icon == null)
                {
                    continue;
                }

                if (index >= safeCount)
                {
                    ConfigureIconClick(
                        icon,
                        -1,
                        string.Empty,
                        string.Empty,
                        null,
                        null,
                        enabled: false);
                    continue;
                }

                string steamId =
                    panel.PlayerSteamIDs[index] ?? string.Empty;
                string playerId =
                    PurrNetPlayerIdPolicy.Format(
                        (ulong)panel.PlayerIDs[index].id);
                NetworkTransform expectedTransform =
                    panel.PlayerTransforms[index];
                PlayerItemController expectedRow =
                    panel.PlayerItemControllers[index];
                bool exactTuplePresent =
                    ModerationProtocolPolicy.IsSteamId64(steamId) &&
                    !string.IsNullOrWhiteSpace(playerId) &&
                    expectedTransform != null &&
                    expectedRow != null;
                ConfigureIconClick(
                    icon,
                    index,
                    steamId,
                    playerId,
                    expectedRow,
                    expectedTransform,
                    Plugin.ShouldApplyMiniMapPlayerLabels &&
                    exactTuplePresent);

                Graphic graphic = icon.GetComponent<Graphic>();
                if (graphic != null)
                {
                    ApplyFriendColorIfChanged(
                        icon,
                        graphic,
                        friendColors &&
                        IsSteamFriendCached(steamId));
                }
            }
        }
        catch (Exception ex)
        {
            if (Time.unscaledTime >= _nextFailureLogAt)
            {
                _nextFailureLogAt =
                    Time.unscaledTime + FailureLogRepeatSeconds;
                Plugin.Log?.LogWarning(
                    "MiniMap ID Click skipped safely: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    private static bool IsSteamFriendCached(string steamId)
    {
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return false;
        }
        if (FriendCache.TryGetValue(
                steamId,
                out bool cached))
        {
            return cached;
        }

        bool isFriend =
            ulong.TryParse(steamId, out ulong numeric) &&
            SteamFriends.GetFriendRelationship(
                new CSteamID(numeric)) ==
            EFriendRelationship.k_EFriendRelationshipFriend;
        if (FriendCache.Count > 256)
        {
            FriendCache.Clear();
        }
        FriendCache[steamId] = isFriend;
        return isFriend;
    }

    private static void ApplyFriendColorIfChanged(
        RectTransform icon,
        Graphic graphic,
        bool isFriend)
    {
        if (icon == null || graphic == null)
        {
            return;
        }

        int iconInstanceId = icon.gameObject.GetInstanceID();
        Color desiredColor = isFriend
            ? FriendColor
            : Color.white;
        bool hadAppliedState =
            AppliedFriendColorStateByIconInstanceId.TryGetValue(
                iconInstanceId,
                out bool previousFriendState);
        if (!hadAppliedState ||
            previousFriendState != isFriend)
        {
            graphic.color = desiredColor;
            if (AppliedFriendColorStateByIconInstanceId.Count > 256)
            {
                AppliedFriendColorStateByIconInstanceId.Clear();
            }
            AppliedFriendColorStateByIconInstanceId[
                iconInstanceId] = isFriend;
        }
        else if (graphic.color != desiredColor)
        {
            graphic.color = desiredColor;
        }

        Transform parent = icon.parent;
        if (MiniMapFriendVisualPolicy.ShouldMoveToLastSibling(
                isFriend,
                icon.GetSiblingIndex(),
                parent == null ? 0 : parent.childCount))
        {
            icon.SetAsLastSibling();
        }
    }

    private static void ConfigureIconClick(
        RectTransform icon,
        int playerIndex,
        string steamId,
        string playerId,
        PlayerItemController expectedRow,
        NetworkTransform expectedTransform,
        bool enabled)
    {
        if (icon == null)
        {
            return;
        }

        int iconInstanceId = icon.gameObject.GetInstanceID();
        OwnedHandlersByIconInstanceId.TryGetValue(
            iconInstanceId,
            out MiniMapIdCardClickHandler handler);
        if (handler == null)
        {
            handler = icon.GetComponent<MiniMapIdCardClickHandler>();
        }
        if (!enabled)
        {
            if (handler != null)
            {
                handler.DisableAndDestroy();
            }
            return;
        }
        if (handler == null)
        {
            handler =
                icon.gameObject
                    .AddComponent<MiniMapIdCardClickHandler>();
        }
        OwnedHandlersByIconInstanceId[iconInstanceId] = handler;
        handler.SetOwnerIconInstanceId(iconInstanceId);
        handler.Configure(
            playerIndex,
            steamId,
            playerId,
            expectedRow,
            expectedTransform,
            enabled);
    }

    internal static void OpenIdCardFromMiniMap(
        int playerIndex,
        string steamId,
        string playerId,
        PlayerItemController expectedRow,
        NetworkTransform expectedTransform)
    {
        NativeIdCardController.TryOpen(
            "minimap-id",
            steamId,
            playerIndex,
            playerId,
            expectedRow,
            expectedTransform,
            requireObservedIndex: true);
    }

    private static void SetAllIconEnhancementsEnabled(
        IEnumerable<RectTransform> icons,
        bool enabled)
    {
        foreach (RectTransform icon in icons)
        {
            if (icon == null)
            {
                continue;
            }

            MiniMapIdCardClickHandler handler =
                icon.GetComponent<MiniMapIdCardClickHandler>();
            if (handler != null)
            {
                handler.DisableAndDestroy();
            }
        }
    }

    internal static void ClearAll()
    {
        var snapshot =
            new List<MiniMapIdCardClickHandler>(
                OwnedHandlersByIconInstanceId.Values);
        OwnedHandlersByIconInstanceId.Clear();
        foreach (MiniMapIdCardClickHandler handler in snapshot)
        {
            if (handler != null)
            {
                handler.DisableAndDestroy();
            }
        }

        FriendCache.Clear();
        AppliedFriendColorStateByIconInstanceId.Clear();
        _nextRefreshAt = 0f;
        _nextFailureLogAt = 0f;
    }

    internal static void UnregisterOwnedHandler(
        int iconInstanceId,
        MiniMapIdCardClickHandler handler)
    {
        if (OwnedHandlersByIconInstanceId.TryGetValue(
                iconInstanceId,
                out MiniMapIdCardClickHandler registered) &&
            registered == handler)
        {
            OwnedHandlersByIconInstanceId.Remove(iconInstanceId);
        }
    }
}

internal sealed class MiniMapIdCardClickHandler :
    MonoBehaviour,
    IPointerClickHandler
{
    private int _playerIndex = -1;
    private string _steamId = string.Empty;
    private string _playerId = string.Empty;
    private PlayerItemController _expectedRow;
    private NetworkTransform _expectedTransform;
    private bool _enabled;
    private bool _raycastCaptured;
    private bool _originalRaycastTarget;
    private int _ownerIconInstanceId;
    private bool _destroyRequested;

    internal void SetOwnerIconInstanceId(int iconInstanceId)
    {
        _ownerIconInstanceId = iconInstanceId;
    }

    public void Configure(
        int playerIndex,
        string steamId,
        string playerId,
        PlayerItemController expectedRow,
        NetworkTransform expectedTransform,
        bool enabled)
    {
        _playerIndex = playerIndex;
        _steamId = steamId ?? string.Empty;
        _playerId = playerId ?? string.Empty;
        _expectedRow = expectedRow;
        _expectedTransform = expectedTransform;
        _enabled = enabled;

        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            return;
        }
        if (!_raycastCaptured)
        {
            _originalRaycastTarget = graphic.raycastTarget;
            _raycastCaptured = true;
        }
        graphic.raycastTarget =
            enabled || _originalRaycastTarget;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_enabled ||
            eventData == null ||
            eventData.button !=
            PointerEventData.InputButton.Left)
        {
            return;
        }

        MiniMapPlayerLabelPatch.OpenIdCardFromMiniMap(
            _playerIndex,
            _steamId,
            _playerId,
            _expectedRow,
            _expectedTransform);
    }

    internal void DisableAndDestroy()
    {
        if (_destroyRequested)
        {
            return;
        }

        _enabled = false;
        _playerIndex = -1;
        _steamId = string.Empty;
        _playerId = string.Empty;
        _expectedRow = null;
        _expectedTransform = null;

        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null && _raycastCaptured)
        {
            graphic.raycastTarget = _originalRaycastTarget;
        }

        _destroyRequested = true;
        enabled = false;
        Destroy(this);
    }

    private void OnDestroy()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null && _raycastCaptured)
        {
            graphic.raycastTarget = _originalRaycastTarget;
        }
        MiniMapPlayerLabelPatch.UnregisterOwnedHandler(
            _ownerIconInstanceId,
            this);
    }
}
