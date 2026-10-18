using System;
using System.Collections.Generic;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

internal static class PlayerPanelSurfaceController
{
    private const string LaneName = "BlueSage_ActionLane";
    private const int HierarchyDiagnosticMaximumEntries = 128;
    private const float HierarchyDiagnosticRepeatSeconds = 30f;
    private const int HierarchyDiagnosticMaximumDepth = 8;
    private const int HierarchyDiagnosticMaximumPathLength = 256;

    private static readonly FieldInfo ReportButtonField =
        AccessTools.Field(typeof(PlayerItemController), "_reportButton");
    private static readonly FieldInfo SyncButtonField =
        AccessTools.Field(typeof(PlayerItemController), "_syncButton");
    private static readonly FieldInfo SyncImageField =
        AccessTools.Field(typeof(PlayerItemController), "_syncImage");
    private static readonly FieldInfo SteamButtonField =
        AccessTools.Field(typeof(PlayerItemController), "_steamButton");
    private static readonly FieldInfo VoiceButtonField =
        AccessTools.Field(typeof(PlayerItemController), "_voiceMuteButton");
    private static readonly Dictionary<string, float> LastHierarchyDiagnosticAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private static readonly Dictionary<int, BlueSagePlayerPanelSurfaceMarker>
        OwnedLanesByRowInstanceId =
            new Dictionary<int, BlueSagePlayerPanelSurfaceMarker>();

    internal static void RefreshRow(
        PlayerItemController row,
        string playerSteamId,
        bool isSelf,
        bool featureEnabled)
    {
        if (row == null)
        {
            return;
        }

        try
        {
            Button reportButton = ReportButtonField?.GetValue(row) as Button;
            Transform anchor = ResolveSerializedReportButtonParent(row, reportButton);
            BlueSagePlayerPanelSurfaceMarker marker =
                FindOwnedLane(row, anchor);
            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            int panelInstanceId = panel != null ? panel.GetInstanceID() : 0;
            int anchorInstanceId = anchor != null ? anchor.GetInstanceID() : 0;

            var context = new PlayerPanelSurfaceContext
            {
                FeatureEnabled = featureEnabled,
                IsSelf = isSelf,
                RowActive = row.gameObject.activeSelf,
                SerializedReportButtonPresent = reportButton != null,
                SerializedReportButtonParentPresent = anchor != null,
                OwnedLanePresent = marker != null,
                IdentityChanged =
                    marker != null &&
                    !string.Equals(
                        marker.BoundSteamId,
                        playerSteamId,
                        StringComparison.Ordinal),
                PanelChanged =
                    marker != null &&
                    marker.PanelInstanceId != panelInstanceId,
                AnchorChanged =
                    marker != null &&
                    marker.AnchorInstanceId != anchorInstanceId,
                RuntimeHierarchyObserved = false,
                PomoState = ReadPomoState(row),
            };
            PlayerPanelSurfaceDecision decision =
                PlayerPanelSurfacePolicy.Evaluate(context);

            if (decision.Action == PlayerPanelSurfaceAction.Remove)
            {
                DestroyOwnedLane(marker);
                EmitHierarchyDiagnosticIfEnabled(
                    "owned-lane-removed",
                    row,
                    reportButton,
                    anchor);
                return;
            }

            if (decision.Action == PlayerPanelSurfaceAction.None)
            {
                EmitHierarchyDiagnosticIfEnabled(
                    anchor == null
                        ? "serialized-report-anchor-unavailable"
                        : "surface-ineligible",
                    row,
                    reportButton,
                    anchor);
                return;
            }

            // Stable pooled rows are the hot path in large lobbies. Their owned
            // lane, identity, panel, and anchor already match, so do not trigger
            // layout rebuilding, listener replacement, or role-highlight work.
            if (decision.Action == PlayerPanelSurfaceAction.Keep)
            {
                return;
            }

            if (marker == null)
            {
                marker = CreateOwnedLane(anchor, row);
            }

            if (marker == null || anchor == null || reportButton == null)
            {
                DestroyOwnedLane(marker);
                EmitHierarchyDiagnosticIfEnabled(
                    "owned-lane-create-failed",
                    row,
                    reportButton,
                    anchor);
                return;
            }

            if (decision.RebindAnchor &&
                marker.transform.parent != anchor)
            {
                marker.transform.SetParent(anchor, false);
            }

            ConfigureLaneLayout(marker, anchor, reportButton);
            marker.RequestLayoutSettle(reportButton);
            PlayerPanelController currentPanel =
                NetworkSingleton<PlayerPanelController>.I;
            int observedIndex =
                currentPanel?.PlayerItemControllers?.IndexOf(row) ?? -1;
            NetworkTransform expectedTransform =
                currentPanel != null &&
                currentPanel.PlayerTransforms != null &&
                observedIndex >= 0 &&
                observedIndex < currentPanel.PlayerTransforms.Count
                    ? currentPanel.PlayerTransforms[observedIndex]
                    : null;
            string expectedPlayerId =
                currentPanel != null &&
                currentPanel.PlayerIDs != null &&
                observedIndex >= 0 &&
                observedIndex < currentPanel.PlayerIDs.Count
                    ? PurrNetPlayerIdPolicy.Format(
                        (ulong)currentPanel.PlayerIDs[observedIndex].id)
                    : string.Empty;

            Button mentionButton = EnsureOwnedButton(
                marker.transform,
                "Button_BlueSageMention",
                "@",
                reportButton,
                slot: 0);
            Button nativeIdButton = EnsureOwnedButton(
                marker.transform,
                "Button_BlueSageNativeId",
                "ID",
                reportButton,
                slot: 1);

            if (mentionButton == null || nativeIdButton == null)
            {
                DestroyOwnedLane(marker);
                EmitHierarchyDiagnosticIfEnabled(
                    "owned-button-create-failed",
                    row,
                    reportButton,
                    anchor);
                return;
            }

            mentionButton.interactable = decision.ShowMentionAction;
            nativeIdButton.interactable = decision.ShowNativeIdAction;
            if (decision.ShowMentionAction)
            {
                BindOwnedListener(
                    mentionButton,
                    () => MentionCurrentPlayer(
                        row,
                        playerSteamId,
                        observedIndex,
                        expectedPlayerId,
                        expectedTransform));
            }
            else
            {
                mentionButton.onClick.RemoveAllListeners();
            }
            if (decision.ShowNativeIdAction)
            {
                BindOwnedListener(
                    nativeIdButton,
                    () => NativeIdCardController.TryOpen(
                        "player-row-id",
                        playerSteamId,
                        observedIndex,
                        expectedPlayerId,
                        row,
                        expectedTransform,
                        requireObservedIndex: false));
            }
            else
            {
                nativeIdButton.onClick.RemoveAllListeners();
            }
            if (decision.RebindIdentityAndListeners)
            {
                marker.Generation++;
            }

            marker.BoundSteamId = playerSteamId ?? string.Empty;
            marker.AnchorInstanceId = anchorInstanceId;
            marker.PanelInstanceId = panelInstanceId;
            marker.ObservedIndex = observedIndex;
            marker.ExpectedPlayerId = expectedPlayerId;
            marker.ExpectedNetworkTransform = expectedTransform;
            PlayerIdentityRoleHighlightController.RefreshInitializedRow(row);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "Enhanced Player Panel skipped a row without interrupting native UI: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    internal static void PruneInactivePooledRows(
        PlayerPanelController panel)
    {
        var snapshot =
            new List<KeyValuePair<int, BlueSagePlayerPanelSurfaceMarker>>(
                OwnedLanesByRowInstanceId);
        foreach (KeyValuePair<int, BlueSagePlayerPanelSurfaceMarker> pair in snapshot)
        {
            BlueSagePlayerPanelSurfaceMarker marker = pair.Value;
            if (marker == null ||
                marker.OwnerRow == null ||
                marker.OwnerRow.gameObject == null ||
                !marker.OwnerRow.gameObject.activeSelf ||
                marker.IsRetired)
            {
                if (marker == null)
                {
                    OwnedLanesByRowInstanceId.Remove(pair.Key);
                }
                else
                {
                    DestroyOwnedLane(marker);
                }
            }
        }
    }

    internal static void PruneRowsNotIn(
        ISet<PlayerItemController> currentRows)
    {
        if (currentRows == null || currentRows.Count == 0)
        {
            return;
        }

        var currentRowInstanceIds = new List<int>();
        foreach (PlayerItemController row in currentRows)
        {
            if (row == null || row.gameObject == null)
            {
                return;
            }
            currentRowInstanceIds.Add(row.GetInstanceID());
        }

        AuthoritativePlayerRowReconciliationPolicy.Reconcile(
            authoritativeCoverageComplete: true,
            currentRowInstanceIds,
            new List<int>(OwnedLanesByRowInstanceId.Keys),
            rowInstanceId =>
            {
                if (!OwnedLanesByRowInstanceId.TryGetValue(
                        rowInstanceId,
                        out BlueSagePlayerPanelSurfaceMarker marker) ||
                    marker == null)
                {
                    OwnedLanesByRowInstanceId.Remove(rowInstanceId);
                    return;
                }
                DestroyOwnedLane(marker);
            });
    }

    internal static void ClearAll()
    {
        var snapshot =
            new List<BlueSagePlayerPanelSurfaceMarker>(
                OwnedLanesByRowInstanceId.Values);
        OwnedLanesByRowInstanceId.Clear();
        foreach (BlueSagePlayerPanelSurfaceMarker marker in snapshot)
        {
            DestroyOwnedLane(marker);
        }
        LastHierarchyDiagnosticAt.Clear();
    }

    internal static void UnregisterOwnedLane(
        BlueSagePlayerPanelSurfaceMarker marker)
    {
        if (marker == null || marker.OwnerRowInstanceId == 0)
        {
            return;
        }

        if (OwnedLanesByRowInstanceId.TryGetValue(
                marker.OwnerRowInstanceId,
                out BlueSagePlayerPanelSurfaceMarker registered) &&
            ReferenceEquals(registered, marker))
        {
            OwnedLanesByRowInstanceId.Remove(
                marker.OwnerRowInstanceId);
        }
    }

    internal static void RefreshOwnedLaneGeometry(
        BlueSagePlayerPanelSurfaceMarker marker)
    {
        if (marker == null ||
            marker.IsRetired ||
            marker.OwnerRow == null ||
            !marker.OwnerRow.gameObject.activeInHierarchy ||
            marker.LayoutReferenceButton == null)
        {
            return;
        }

        Transform anchor = ResolveSerializedReportButtonParent(
            marker.OwnerRow,
            marker.LayoutReferenceButton);
        if (anchor == null || marker.transform.parent != anchor)
        {
            return;
        }

        ConfigureLaneLayout(
            marker,
            anchor,
            marker.LayoutReferenceButton);
    }

    private static BlueSagePlayerPanelSurfaceMarker FindOwnedLane(
        PlayerItemController row,
        Transform anchor)
    {
        int rowInstanceId = row.GetInstanceID();
        if (OwnedLanesByRowInstanceId.TryGetValue(
                rowInstanceId,
                out BlueSagePlayerPanelSurfaceMarker registered))
        {
            if (registered != null &&
                !registered.IsRetired &&
                registered.OwnerRow != null &&
                ReferenceEquals(registered.OwnerRow, row))
            {
                return registered;
            }
            OwnedLanesByRowInstanceId.Remove(rowInstanceId);
        }

        if (anchor == null)
        {
            return null;
        }

        for (int index = 0; index < anchor.childCount; index++)
        {
            BlueSagePlayerPanelSurfaceMarker candidate =
                anchor.GetChild(index)
                    .GetComponent<BlueSagePlayerPanelSurfaceMarker>();
            if (candidate != null &&
                !candidate.IsRetired &&
                candidate.OwnerRow != null &&
                ReferenceEquals(candidate.OwnerRow, row))
            {
                candidate.OwnerRowInstanceId = rowInstanceId;
                OwnedLanesByRowInstanceId[rowInstanceId] = candidate;
                return candidate;
            }
        }

        return null;
    }

    private static Transform ResolveSerializedReportButtonParent(
        PlayerItemController row,
        Button reportButton)
    {
        if (row == null ||
            reportButton == null ||
            reportButton.transform == null ||
            !reportButton.transform.IsChildOf(row.transform))
        {
            return null;
        }

        Transform anchor = reportButton.transform.parent;
        if (anchor == null ||
            (anchor != row.transform &&
             !anchor.IsChildOf(row.transform)))
        {
            return null;
        }

        return anchor;
    }

    private static BlueSagePlayerPanelSurfaceMarker CreateOwnedLane(
        Transform anchor,
        PlayerItemController row)
    {
        if (anchor == null || row == null)
        {
            return null;
        }

        var laneObject = new GameObject(
            LaneName,
            typeof(RectTransform),
            typeof(LayoutElement),
            typeof(BlueSagePlayerPanelSurfaceMarker));
        laneObject.transform.SetParent(anchor, false);
        LayoutElement layoutElement = laneObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;
        BlueSagePlayerPanelSurfaceMarker marker =
            laneObject.GetComponent<BlueSagePlayerPanelSurfaceMarker>();
        marker.OwnerRow = row;
        marker.OwnerRowInstanceId = row.GetInstanceID();
        OwnedLanesByRowInstanceId[marker.OwnerRowInstanceId] = marker;
        return marker;
    }

    private static void ConfigureLaneLayout(
        BlueSagePlayerPanelSurfaceMarker marker,
        Transform anchor,
        Button reportButton)
    {
        RectTransform lane = marker.transform as RectTransform;
        RectTransform reportRect = reportButton.transform as RectTransform;
        if (lane == null || reportRect == null)
        {
            return;
        }

        float referenceWidth = Dimension(reportRect.rect.width, reportRect.sizeDelta.x, 36f);
        float referenceHeight = Dimension(reportRect.rect.height, reportRect.sizeDelta.y, 36f);
        float buttonWidth = Mathf.Clamp(referenceWidth, 30f, 38f);
        float buttonHeight = Mathf.Clamp(referenceHeight, 30f, 38f);
        float spacing = 4f;
        // Leave a little more air than the vanilla spacing because the
        // game-owned Pomo button can animate into this boundary on synced
        // participant rows. The lane remains independent and clickable.
        float gap = 9f;
        float nativeLeft = MeasureNativeLeftEdge(
            anchor,
            marker.transform,
            reportRect);

        lane.anchorMin = reportRect.anchorMin;
        lane.anchorMax = reportRect.anchorMax;
        lane.pivot = new Vector2(1f, 0.5f);
        lane.sizeDelta = new Vector2(
            (buttonWidth * 2f) + spacing,
            buttonHeight);
        lane.anchoredPosition = new Vector2(
            nativeLeft - gap,
            reportRect.anchoredPosition.y);
    }

    private static float MeasureNativeLeftEdge(
        Transform anchor,
        Transform ownedLane,
        RectTransform reportRect)
    {
        float left = reportRect.anchoredPosition.x -
                     (Dimension(
                          reportRect.rect.width,
                          reportRect.sizeDelta.x,
                          36f) *
                      reportRect.pivot.x);
        for (int index = 0; index < anchor.childCount; index++)
        {
            Transform child = anchor.GetChild(index);
            if (child == null ||
                child == ownedLane ||
                child.GetComponent<BlueSagePlayerPanelSurfaceMarker>() != null ||
                child.GetComponent<Button>() == null)
            {
                continue;
            }

            RectTransform rect = child as RectTransform;
            if (rect == null)
            {
                continue;
            }

            float width = Dimension(
                rect.rect.width,
                rect.sizeDelta.x,
                36f);
            left = Mathf.Min(
                left,
                rect.anchoredPosition.x - (width * rect.pivot.x));
        }

        return left;
    }

    private static Button EnsureOwnedButton(
        Transform lane,
        string objectName,
        string label,
        Button referenceButton,
        int slot)
    {
        BlueSageEnhancedPanelButtonMarker owned = null;
        for (int index = 0; index < lane.childCount; index++)
        {
            BlueSageEnhancedPanelButtonMarker candidate =
                lane.GetChild(index)
                    .GetComponent<BlueSageEnhancedPanelButtonMarker>();
            if (candidate != null &&
                string.Equals(
                    candidate.ActionName,
                    objectName,
                    StringComparison.Ordinal))
            {
                owned = candidate;
                break;
            }
        }

        GameObject buttonObject;
        if (owned == null)
        {
            buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(BlueSageEnhancedPanelButtonMarker));
            buttonObject.transform.SetParent(lane, false);
            owned =
                buttonObject.GetComponent<BlueSageEnhancedPanelButtonMarker>();
            owned.ActionName = objectName;
        }
        else
        {
            buttonObject = owned.gameObject;
        }

        RectTransform laneRect = lane as RectTransform;
        RectTransform rect = buttonObject.transform as RectTransform;
        if (laneRect == null || rect == null)
        {
            return null;
        }

        float spacing = 4f;
        float buttonWidth =
            Mathf.Max(1f, (laneRect.sizeDelta.x - spacing) / 2f);
        float buttonHeight = Mathf.Max(1f, laneRect.sizeDelta.y);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        rect.anchoredPosition = new Vector2(
            -(slot * (buttonWidth + spacing)),
            0f);

        CopyReferenceVisual(referenceButton, buttonObject);
        AddOrRefreshLabel(buttonObject.transform, label);
        return buttonObject.GetComponent<Button>();
    }

    private static void CopyReferenceVisual(
        Button referenceButton,
        GameObject ownedButton)
    {
        if (ownedButton == null)
        {
            return;
        }

        Button button = ownedButton.GetComponent<Button>();
        Image image = ownedButton.GetComponent<Image>();
        if (referenceButton == null)
        {
            image.color = new Color(0.38f, 0.73f, 0.95f, 1f);
            return;
        }

        button.transition = referenceButton.transition;
        button.colors = referenceButton.colors;
        button.spriteState = referenceButton.spriteState;
        button.navigation = referenceButton.navigation;
        Image referenceImage = referenceButton.GetComponent<Image>();
        if (referenceImage != null)
        {
            image.sprite = referenceImage.sprite;
            image.type = referenceImage.type;
            image.preserveAspect = referenceImage.preserveAspect;
            image.color = referenceImage.color;
        }
    }

    private static void AddOrRefreshLabel(
        Transform parent,
        string label)
    {
        TextMeshProUGUI text = null;
        for (int index = 0; index < parent.childCount; index++)
        {
            text = parent.GetChild(index).GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                break;
            }
        }

        if (text == null)
        {
            var labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            text = labelObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        text.text = label;
        text.fontSize = label.Length > 1 ? 15f : 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static void BindOwnedListener(
        Button button,
        Action action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            action?.Invoke();
            SFXManager sfxManager = MonoSingleton<SFXManager>.I;
            if (sfxManager != null)
            {
                sfxManager.PlayUIClick();
            }
        });
    }

    private static void MentionCurrentPlayer(
        PlayerItemController row,
        string steamId,
        int observedIndex,
        string expectedPlayerId,
        NetworkTransform expectedTransform)
    {
        if (!NativeIdCardController.TryResolveCurrentRow(
                "player-row-mention",
                steamId,
                observedIndex,
                expectedPlayerId,
                row,
                expectedTransform,
                requireObservedIndex: false,
                NativeIdCardCallerKind.OrdinaryUser,
                out NativeIdCardTarget target,
                out _))
        {
            return;
        }

        UIManager uiManager = MonoSingleton<UIManager>.I;
        if (uiManager == null || uiManager.MessageInput == null)
        {
            return;
        }

        string cleanName =
            MentionNamePolicy.ToMentionInsertionToken(
                target.DisplayNameRaw);
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            return;
        }

        PlayerPanelController panel =
            NetworkSingleton<PlayerPanelController>.I;
        if (panel != null)
        {
            panel.ButtonClosePanel();
        }

        if (uiManager.IsMessagePanelHidden)
        {
            uiManager.ButtonHide();
        }
        if (!uiManager.IsMessagePanelActivated)
        {
            uiManager.OsSelectMessageInput();
        }

        string mention = "@" + cleanName + " ";
        TMP_InputField input = uiManager.MessageInput;
        if (string.IsNullOrWhiteSpace(input.text))
        {
            input.text = mention;
        }
        else
        {
            string separator =
                input.text.EndsWith(" ", StringComparison.Ordinal)
                    ? string.Empty
                    : " ";
            input.text += separator + mention;
        }

        input.Select();
        input.ActivateInputField();
        input.caretPosition = input.text.Length;
    }

    private static PlayerPanelPomoState ReadPomoState(
        PlayerItemController row)
    {
        string state = row.CurrentPomoState.ToString();
        if (string.Equals(
                state,
                "Syncable",
                StringComparison.Ordinal))
        {
            return PlayerPanelPomoState.Syncable;
        }
        if (string.Equals(
                state,
                "Synced",
                StringComparison.Ordinal))
        {
            return PlayerPanelPomoState.Synced;
        }
        if (string.Equals(
                state,
                "SyncedWithMe",
                StringComparison.Ordinal))
        {
            return PlayerPanelPomoState.SyncedWithMe;
        }
        return PlayerPanelPomoState.Inactive;
    }

    private static void DestroyOwnedLane(
        BlueSagePlayerPanelSurfaceMarker marker)
    {
        if (marker != null)
        {
            marker.IsRetired = true;
            UnregisterOwnedLane(marker);
            RemoveOwnedListeners(marker);
            marker.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(marker.gameObject);
        }
    }

    private static void RemoveOwnedListeners(
        BlueSagePlayerPanelSurfaceMarker marker)
    {
        if (marker == null)
        {
            return;
        }

        Transform lane = marker.transform;
        for (int index = 0; index < lane.childCount; index++)
        {
            Button button = lane.GetChild(index).GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
        }
    }

    private static void EmitHierarchyDiagnosticIfEnabled(
        string reason,
        PlayerItemController row,
        Button reportButton,
        Transform anchor)
    {
        if (!Plugin.ShouldEmitHiddenDiagnostics || row == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        string key = row.GetInstanceID() + "|" + (reason ?? string.Empty);
        if (LastHierarchyDiagnosticAt.TryGetValue(
                key,
                out float lastAt) &&
            now - lastAt < HierarchyDiagnosticRepeatSeconds)
        {
            return;
        }
        if (LastHierarchyDiagnosticAt.Count >=
            HierarchyDiagnosticMaximumEntries)
        {
            LastHierarchyDiagnosticAt.Clear();
        }
        LastHierarchyDiagnosticAt[key] = now;

        Button syncButton = SyncButtonField?.GetValue(row) as Button;
        Image syncImage = SyncImageField?.GetValue(row) as Image;
        Button steamButton = SteamButtonField?.GetValue(row) as Button;
        Button voiceButton = VoiceButtonField?.GetValue(row) as Button;
        Plugin.Log?.LogInfo(
            "[BlueSagePanelHierarchy] authority=diagnostic-only;" +
            " fallbackEnabled=false;" +
            " reason=" + SafeDiagnosticValue(reason) + ";" +
            " rowPath=" + BuildBoundedPath(row.transform, row.transform) + ";" +
            " reportPath=" +
            BuildBoundedPath(row.transform, reportButton?.transform) + ";" +
            " anchorPath=" +
            BuildBoundedPath(row.transform, anchor) + ";" +
            " syncSharesAnchor=" +
            SharesParent(syncButton?.transform, anchor) + ";" +
            " syncImageSharesAnchor=" +
            SharesParent(syncImage?.transform, anchor) + ";" +
            " steamSharesAnchor=" +
            SharesParent(steamButton?.transform, anchor) + ";" +
            " voiceSharesAnchor=" +
            SharesParent(voiceButton?.transform, anchor) + ";" +
            " pomoState=" + row.CurrentPomoState + ".");
    }

    private static bool SharesParent(
        Transform control,
        Transform expectedParent)
    {
        return control != null &&
               expectedParent != null &&
               ReferenceEquals(control.parent, expectedParent);
    }

    private static string BuildBoundedPath(
        Transform root,
        Transform node)
    {
        if (root == null || node == null)
        {
            return "unavailable";
        }

        var segments = new List<string>();
        Transform current = node;
        int depth = 0;
        while (current != null &&
               depth < HierarchyDiagnosticMaximumDepth)
        {
            segments.Add(SafeDiagnosticValue(current.name));
            if (current == root)
            {
                break;
            }
            current = current.parent;
            depth++;
        }
        segments.Reverse();
        string path = string.Join("/", segments.ToArray());
        return path.Length <= HierarchyDiagnosticMaximumPathLength
            ? path
            : path.Substring(
                  0,
                  HierarchyDiagnosticMaximumPathLength - 1) + "…";
    }

    private static string SafeDiagnosticValue(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Replace("\r", " ").Replace("\n", " ").Replace(";", "_");
        return safe.Length <= 64
            ? safe
            : safe.Substring(0, 63) + "…";
    }

    private static float Dimension(
        float rectValue,
        float sizeDeltaValue,
        float fallback)
    {
        if (rectValue > 0f)
        {
            return rectValue;
        }
        if (Mathf.Abs(sizeDeltaValue) > 0f)
        {
            return Mathf.Abs(sizeDeltaValue);
        }
        return fallback;
    }
}

[HarmonyPatch(typeof(PlayerPanelController), "UpdateServerPanel")]
internal static class PlayerPanelSurfaceRosterLifecyclePatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerPanelController __instance)
    {
        PlayerPanelSurfaceController.PruneInactivePooledRows(
            __instance);
    }
}

internal sealed class BlueSagePlayerPanelSurfaceMarker : MonoBehaviour
{
    private const int LayoutSettleFrameBudget = 30;
    private static int _created;
    private static int _destroyed;
    private static int _live;

    internal string BoundSteamId = string.Empty;
    internal bool IsRetired;
    internal PlayerItemController OwnerRow;
    internal int OwnerRowInstanceId;
    internal int AnchorInstanceId;
    internal int PanelInstanceId;
    internal int Generation;
    internal int ObservedIndex = -1;
    internal string ExpectedPlayerId = string.Empty;
    internal NetworkTransform ExpectedNetworkTransform;
    internal Button LayoutReferenceButton;
    private int _layoutSettleFramesRemaining;

    private void Awake()
    {
        System.Threading.Interlocked.Increment(ref _created);
        System.Threading.Interlocked.Increment(ref _live);
    }

    internal void RequestLayoutSettle(Button reportButton)
    {
        LayoutReferenceButton = reportButton;
        _layoutSettleFramesRemaining = LayoutSettleFrameBudget;
    }

    private void OnEnable()
    {
        _layoutSettleFramesRemaining = LayoutSettleFrameBudget;
    }

    private void LateUpdate()
    {
        if (_layoutSettleFramesRemaining <= 0 || IsRetired)
        {
            return;
        }

        PlayerPanelSurfaceController.RefreshOwnedLaneGeometry(this);
        _layoutSettleFramesRemaining--;
    }

    private void OnDestroy()
    {
        PlayerPanelSurfaceController.UnregisterOwnedLane(this);
        System.Threading.Interlocked.Increment(ref _destroyed);
        System.Threading.Interlocked.Decrement(ref _live);
    }

    internal static string GetDiagnosticSummary()
    {
        return "lanesLive=" +
               System.Threading.Interlocked.CompareExchange(
                   ref _live,
                   0,
                   0) +
               ",lanesCreated=" +
               System.Threading.Interlocked.CompareExchange(
                   ref _created,
                   0,
                   0) +
               ",lanesDestroyed=" +
               System.Threading.Interlocked.CompareExchange(
                   ref _destroyed,
                   0,
                   0);
    }
}

internal sealed class BlueSageEnhancedPanelButtonMarker : MonoBehaviour
{
    private static int _created;
    private static int _destroyed;
    private static int _live;

    internal string ActionName = string.Empty;

    private void Awake()
    {
        System.Threading.Interlocked.Increment(ref _created);
        System.Threading.Interlocked.Increment(ref _live);
    }

    private void OnDestroy()
    {
        System.Threading.Interlocked.Increment(ref _destroyed);
        System.Threading.Interlocked.Decrement(ref _live);
    }

    internal static string GetDiagnosticSummary()
    {
        return "buttonsLive=" +
               System.Threading.Interlocked.CompareExchange(
                   ref _live,
                   0,
                   0) +
               ",buttonsCreated=" +
               System.Threading.Interlocked.CompareExchange(
                   ref _created,
                   0,
                   0) +
               ",buttonsDestroyed=" +
               System.Threading.Interlocked.CompareExchange(
                   ref _destroyed,
                   0,
                   0) +
               "," +
               BlueSagePlayerPanelSurfaceMarker.GetDiagnosticSummary();
    }
}
