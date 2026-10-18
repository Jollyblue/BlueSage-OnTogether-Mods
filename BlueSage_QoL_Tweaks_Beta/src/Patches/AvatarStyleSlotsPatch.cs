using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

internal static class AvatarStyleSlotsBridge
{
    internal const int NativeSlotCount = 3;
    internal const int TotalSlotCount = 9;
    private const string PayloadVersion = "BSAS1";
    private const string RowMarkerPrefix = "BlueSage_StyleSlot_";

    private static readonly FieldInfo StyleDropdownPanelField = AccessTools.Field(typeof(CustomizationUIController), "_styleDropdownPanel");
    private static readonly FieldInfo StyleTextsField = AccessTools.Field(typeof(CustomizationUIController), "_styleTexts");
    private static readonly FieldInfo StyleSelectedBgsField = AccessTools.Field(typeof(CustomizationUIController), "_styleSelectedBgs");
    private static readonly FieldInfo StyleNameInputField = AccessTools.Field(typeof(CustomizationUIController), "_styleNameInputField");
    private static readonly FieldInfo StyleNumTextField = AccessTools.Field(typeof(CustomizationUIController), "_styleNumText");
    private static readonly FieldInfo CustomizationDataField = AccessTools.Field(typeof(CustomizationUIController), "_customizationData");
    private static readonly FieldInfo IsStyleChangedField = AccessTools.Field(typeof(CustomizationUIController), "_isStyleChanged");
    private static readonly FieldInfo CustomizationTypeField = AccessTools.Field(typeof(CustomizationUIController), "_customizationType");
    private static readonly FieldInfo GroupIndexField = AccessTools.Field(typeof(CustomizationUIController), "_groupIndex");
    private static readonly MethodInfo ResetUnsavedCustomizationMethod = AccessTools.Method(typeof(CustomizationUIController), "ResetUnsavedCustomization");
    private static readonly MethodInfo SetGroupUiMethod = AccessTools.Method(typeof(CustomizationUIController), "SetGroupUI");
    private static readonly MethodInfo PointerExitStyleNamingMethod = AccessTools.Method(typeof(CustomizationUIController), "PointerExitStyleNaming");
    private static readonly MethodInfo SetPartOptionUiMethod = AccessTools.Method(typeof(CustomizationUIController), "SetPartOptionUI");
    private static readonly MethodInfo SetCharacterMethod = AccessTools.Method(typeof(CustomizationUIController), "SetCharacter");
    private static readonly MethodInfo UpdateCustomizationCameraMethod = AccessTools.Method(typeof(CustomizationUIController), "UpdateCustomizationCamera");
    private static readonly MethodInfo WaitAndCaptureMethod = AccessTools.Method(typeof(CustomizationUIController), "WaitAndCapture");

    private static ConfigFile _config;
    private static ConfigEntry<int> _selectedExtendedSlot;
    private static readonly ConfigEntry<string>[] Names = new ConfigEntry<string>[TotalSlotCount - NativeSlotCount];
    private static readonly ConfigEntry<string>[] Payloads = new ConfigEntry<string>[TotalSlotCount - NativeSlotCount];
    private static readonly Dictionary<int, TextMeshProUGUI> RowTexts = new();
    private static readonly Dictionary<int, GameObject> RowSelectedBgs = new();
    private static int _activeExtendedSlot = -1;
    private static bool _closingExtendedSlot;
    private static CustomizationUIController _lastController;
    private static CustomizationDataIDs3 _nativeSlotSnapshot;
    private static int _nativeSlotSnapshotIndex = -1;

    internal static bool IsExtendedActive => _activeExtendedSlot >= NativeSlotCount && _activeExtendedSlot < TotalSlotCount;

    private static void EnsureConfig()
    {
        if (_config != null)
        {
            return;
        }

        string path = System.IO.Path.Combine(Paths.ConfigPath, "com.bluesage.ontogether.qol-avatar-styles.cfg");
        _config = new ConfigFile(path, saveOnInit: true);
        _selectedExtendedSlot = _config.Bind("State", "SelectedExtendedSlot", -1, "Last selected BlueSage avatar style slot. -1 means a vanilla slot is active.");
        for (int slot = NativeSlotCount; slot < TotalSlotCount; slot++)
        {
            int index = slot - NativeSlotCount;
            Names[index] = _config.Bind("Style " + (slot + 1), "Name", string.Empty, "Display name for BlueSage avatar style slot " + (slot + 1) + ".");
            Payloads[index] = _config.Bind("Style " + (slot + 1), "Payload", string.Empty, "Versioned avatar customization payload. Managed by BlueSage QoL.");
        }
    }

    internal static void ExtendDropdown(CustomizationUIController controller)
    {
        if (controller == null || Plugin.EnableExtendedAvatarStyles == null || !Plugin.EnableExtendedAvatarStyles.Value)
        {
            return;
        }

        _lastController = controller;
        EnsureConfig();
        List<TextMeshProUGUI> nativeTexts = StyleTextsField?.GetValue(controller) as List<TextMeshProUGUI>;
        List<GameObject> nativeBgs = StyleSelectedBgsField?.GetValue(controller) as List<GameObject>;
        GameObject panel = StyleDropdownPanelField?.GetValue(controller) as GameObject;
        if (nativeTexts == null || nativeTexts.Count < NativeSlotCount || nativeBgs == null || nativeBgs.Count < NativeSlotCount || panel == null)
        {
            Plugin.Log?.LogWarning("Avatar Styles 4-9 skipped: the vanilla three-slot dropdown surface was not complete.");
            return;
        }

        GameObject referenceRow = FindSharedRow(nativeTexts[NativeSlotCount - 1].transform, nativeBgs[NativeSlotCount - 1].transform);
        if (referenceRow == null || referenceRow.transform.parent == null)
        {
            Plugin.Log?.LogWarning("Avatar Styles 4-9 skipped: no safe vanilla style row root was found.");
            return;
        }

        Transform rowsParent = referenceRow.transform.parent;
        int createdRows = 0;
        for (int slot = NativeSlotCount; slot < TotalSlotCount; slot++)
        {
            string marker = RowMarkerPrefix + (slot + 1);
            Transform existing = rowsParent.Find(marker);
            GameObject row = existing != null ? existing.gameObject : UnityEngine.Object.Instantiate(referenceRow, rowsParent);
            if (existing == null)
            {
                createdRows++;
            }
            row.name = marker;
            row.transform.SetSiblingIndex(referenceRow.transform.GetSiblingIndex() + (slot - NativeSlotCount) + 1);

            TextMeshProUGUI nameText = FindMatchingText(referenceRow, nativeTexts[NativeSlotCount - 1], row);
            GameObject selectedBg = FindMatchingObject(referenceRow, nativeBgs[NativeSlotCount - 1], row);
            if (nameText == null || selectedBg == null)
            {
                UnityEngine.Object.Destroy(row);
                Plugin.Log?.LogWarning($"Avatar style slot {slot + 1} skipped because its cloned row could not be mapped safely.");
                continue;
            }

            ConfigureRowNumber(row, nameText, slot + 1);
            nameText.text = "- " + GetName(slot);
            selectedBg.SetActive(slot == _activeExtendedSlot);
            ConfigureRowButton(row, controller, slot);
            RowTexts[slot] = nameText;
            RowSelectedBgs[slot] = selectedBg;
        }

        ExpandPanelForRows(panel, referenceRow, createdRows);
        try
        {
            RestoreExtendedSelectionIfNeeded(controller, nativeBgs);
        }
        catch (Exception ex)
        {
            _activeExtendedSlot = -1;
            if (_selectedExtendedSlot != null)
            {
                _selectedExtendedSlot.Value = -1;
            }
            Plugin.Log?.LogWarning($"Avatar Styles 4-9 deferred an invalid saved-slot restore without interrupting CustomizationUI startup: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void RefreshEnabledState()
    {
        CustomizationUIController controller = _lastController;
        if (controller == null)
        {
            controller = UnityEngine.Object.FindFirstObjectByType<CustomizationUIController>();
        }

        if (controller == null)
        {
            return;
        }

        _lastController = controller;
        if (Plugin.EnableExtendedAvatarStyles != null && Plugin.EnableExtendedAvatarStyles.Value)
        {
            ExtendDropdown(controller);
            return;
        }

        DisableAndRestoreVanilla(controller);
    }

    private static void DisableAndRestoreVanilla(CustomizationUIController controller)
    {
        if (IsExtendedActive)
        {
            int nativeSlot = Mathf.Clamp(MonoSingleton<DataManager>.I.PlayerDataZip.SelectedStyleIndex, 0, NativeSlotCount - 1);
            SelectNativeFromExtended(controller, nativeSlot);
        }

        GameObject panel = StyleDropdownPanelField?.GetValue(controller) as GameObject;
        int removedRows = 0;
        foreach (KeyValuePair<int, TextMeshProUGUI> pair in RowTexts.ToArray())
        {
            if (pair.Value == null)
            {
                continue;
            }

            Transform row = pair.Value.transform;
            while (row.parent != null && !row.name.StartsWith(RowMarkerPrefix, StringComparison.Ordinal))
            {
                row = row.parent;
            }

            if (row.name.StartsWith(RowMarkerPrefix, StringComparison.Ordinal))
            {
                UnityEngine.Object.Destroy(row.gameObject);
                removedRows++;
            }
        }

        RowTexts.Clear();
        RowSelectedBgs.Clear();
        if (panel != null && removedRows > 0)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null && panel.GetComponent<ContentSizeFitter>() == null)
            {
                float rowHeight = 0f;
                if (StyleTextsField?.GetValue(controller) is List<TextMeshProUGUI> nativeTexts && nativeTexts.Count >= NativeSlotCount)
                {
                    GameObject nativeRow = FindSharedRow(nativeTexts[NativeSlotCount - 1].transform,
                        ((List<GameObject>)StyleSelectedBgsField.GetValue(controller))[NativeSlotCount - 1].transform);
                    rowHeight = Mathf.Abs(nativeRow?.GetComponent<RectTransform>()?.rect.height ?? 0f);
                }

                if (rowHeight > 1f)
                {
                    panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(panelRect.rect.height - (rowHeight * removedRows), rowHeight));
                }
            }
        }

        _activeExtendedSlot = -1;
        Plugin.Log?.LogInfo("Avatar preset slots 4-9 disabled; vanilla slots 1-3 restored and QoL preset data retained.");
    }

    internal static void SelectExtended(CustomizationUIController controller, int slot)
    {
        if (controller == null || slot < NativeSlotCount || slot >= TotalSlotCount)
        {
            return;
        }

        EnsureConfig();
        try
        {
            bool enteringFromNative = !IsExtendedActive;
            SaveDepartingSlot(controller);
            if (enteringFromNative)
            {
                CaptureNativeSlotSnapshot();
            }
            else
            {
                RestoreNativeSlotSnapshot();
            }
            SetAllSelectionBgs(controller, -1);
            _activeExtendedSlot = slot;
            _selectedExtendedSlot.Value = slot;

            CustomizationDataIDs3 ids = LoadPayload(slot) ?? new CustomizationDataIDs3();
            ApplyToEditor(controller, ids);
            SetAllSelectionBgs(controller, slot);
            UpdateHeader(controller, slot, GetName(slot));
            Plugin.Log?.LogInfo($"Avatar style slot {slot + 1} selected through the BlueSage compatibility bridge.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Avatar style slot {slot + 1} selection failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static bool SelectNativeFromExtended(CustomizationUIController controller, int slot)
    {
        if (!IsExtendedActive || controller == null || slot < 0 || slot >= NativeSlotCount)
        {
            return false;
        }

        try
        {
            SaveExtended(controller, _activeExtendedSlot);
            RestoreNativeSlotSnapshot();
            SetAllSelectionBgs(controller, -1);
            _activeExtendedSlot = -1;
            _selectedExtendedSlot.Value = -1;

            PlayerDataZip zip = MonoSingleton<DataManager>.I.PlayerDataZip;
            int previousNativeSlot = _nativeSlotSnapshotIndex >= 0
                ? _nativeSlotSnapshotIndex
                : Mathf.Clamp(zip.SelectedStyleIndex, 0, NativeSlotCount - 1);
            CustomizationData previousNativeData = new CustomizationData(zip.CurrentCustomizationDataIDs);
            MonoSingleton<DataManager>.I.CustomizationData = new CustomizationData(previousNativeData);
            CustomizationDataField?.SetValue(controller, new CustomizationData(previousNativeData));
            IsStyleChangedField?.SetValue(controller, false);

            if (slot != previousNativeSlot)
            {
                // Report "not handled" to the Harmony prefix so the original game method owns
                // the actual 1-3 transition, UI refresh, camera state, and native save sequence.
                return false;
            }

            ApplyToEditor(controller, zip.CurrentCustomizationDataIDs);
            SetAllSelectionBgs(controller, previousNativeSlot);
            UpdateHeader(controller, previousNativeSlot, zip.StyleNames[previousNativeSlot]);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Returning to vanilla avatar style {slot + 1} failed safely: {ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }

    internal static bool SaveExtendedClose(CustomizationUIController controller, bool notFromStyleChanged)
    {
        if (!IsExtendedActive || _closingExtendedSlot || controller == null)
        {
            return false;
        }

        _closingExtendedSlot = true;
        try
        {
            CustomizationData liveEditorData = CustomizationDataField?.GetValue(controller) as CustomizationData;
            if (liveEditorData == null)
            {
                return true;
            }

            // Vanilla CloseAvatarPanel resets the active group before committing. Keep an
            // immutable copy first so that reset cannot mutate the QoL slot we save/apply.
            CustomizationData editorData = new CustomizationData(liveEditorData);

            if (notFromStyleChanged)
            {
                PointerExitStyleNamingMethod?.Invoke(controller, null);
            }

            object customizationType = CustomizationTypeField?.GetValue(controller);
            object groupIndex = GroupIndexField?.GetValue(controller);
            if (customizationType != null && groupIndex != null)
            {
                ResetUnsavedCustomizationMethod?.Invoke(controller, new[] { customizationType, groupIndex });
            }
            SetGroupUiMethod?.Invoke(controller, null);

            MonoSingleton<DataManager>.I.CustomizationData = new CustomizationData(editorData);
            SaveExtendedData(editorData, _activeExtendedSlot);
            RestoreNativeSlotSnapshot();
            if (WaitAndCaptureMethod?.Invoke(controller, null) is System.Collections.IEnumerator capture)
            {
                controller.StartCoroutine(capture);
            }

            if (NetworkSingleton<TextChannelManager>.I != null)
            {
                NetworkSingleton<TextChannelManager>.I.MainCustomizationController.ApplyCustomization(editorData);
            }

            controller.IsAnyCustomize = true;
            IsStyleChangedField?.SetValue(controller, false);
            return true;
        }
        finally
        {
            _closingExtendedSlot = false;
        }
    }

    internal static bool RenameExtended(CustomizationUIController controller, string styleName)
    {
        if (!IsExtendedActive)
        {
            return false;
        }

        EnsureConfig();
        string normalized = styleName ?? string.Empty;
        Names[_activeExtendedSlot - NativeSlotCount].Value = normalized;
        if (RowTexts.TryGetValue(_activeExtendedSlot, out TextMeshProUGUI text) && text != null)
        {
            text.text = "- " + normalized;
        }

        return true;
    }

    private static void RestoreExtendedSelectionIfNeeded(CustomizationUIController controller, List<GameObject> nativeBgs)
    {
        int slot = _selectedExtendedSlot?.Value ?? -1;
        if (slot < NativeSlotCount || slot >= TotalSlotCount || string.IsNullOrWhiteSpace(Payloads[slot - NativeSlotCount].Value))
        {
            _activeExtendedSlot = -1;
            return;
        }

        _activeExtendedSlot = slot;
        foreach (GameObject bg in nativeBgs)
        {
            bg?.SetActive(false);
        }

        CustomizationDataIDs3 ids = LoadPayload(slot);
        if (ids != null)
        {
            ApplyToEditor(controller, ids);
            SetAllSelectionBgs(controller, slot);
            UpdateHeader(controller, slot, GetName(slot));
        }
    }

    private static void SaveDepartingSlot(CustomizationUIController controller)
    {
        if (IsExtendedActive)
        {
            SaveExtended(controller, _activeExtendedSlot);
            return;
        }

        controller.CloseAvatarPanel(notFromStyleChanged: false);
        MonoSingleton<DataManager>.I.SavePlayerZipData();
    }

    private static void CaptureNativeSlotSnapshot()
    {
        PlayerDataZip zip = MonoSingleton<DataManager>.I?.PlayerDataZip;
        if (zip == null)
        {
            _nativeSlotSnapshot = null;
            _nativeSlotSnapshotIndex = -1;
            return;
        }

        _nativeSlotSnapshotIndex = Mathf.Clamp(zip.SelectedStyleIndex, 0, NativeSlotCount - 1);
        _nativeSlotSnapshot = CloneIds(zip.CurrentCustomizationDataIDs);
    }

    private static void RestoreNativeSlotSnapshot()
    {
        PlayerDataZip zip = MonoSingleton<DataManager>.I?.PlayerDataZip;
        if (zip == null || _nativeSlotSnapshot == null || _nativeSlotSnapshotIndex < 0)
        {
            return;
        }

        zip.SelectedStyleIndex = _nativeSlotSnapshotIndex;
        zip.CurrentCustomizationDataIDs = CloneIds(_nativeSlotSnapshot);
    }

    private static CustomizationDataIDs3 CloneIds(CustomizationDataIDs3 source)
    {
        return source == null ? null : new CustomizationDataIDs3(new CustomizationData(source));
    }

    private static void SaveExtended(CustomizationUIController controller, int slot)
    {
        CustomizationData data = CustomizationDataField?.GetValue(controller) as CustomizationData;
        if (data == null)
        {
            return;
        }

        SaveExtendedData(data, slot);
    }

    private static void SaveExtendedData(CustomizationData data, int slot)
    {
        if (data == null || slot < NativeSlotCount || slot >= TotalSlotCount)
        {
            return;
        }

        Payloads[slot - NativeSlotCount].Value = Serialize(new CustomizationDataIDs3(data));
    }

    private static void ApplyToEditor(CustomizationUIController controller, CustomizationDataIDs3 ids)
    {
        CustomizationData data = new CustomizationData(ids);
        MonoSingleton<DataManager>.I.CustomizationData = new CustomizationData(data);
        CustomizationDataField?.SetValue(controller, new CustomizationData(data));
        IsStyleChangedField?.SetValue(controller, true);

        SetPartOptionUiMethod?.Invoke(controller, null);
        SetCharacterMethod?.Invoke(controller, null);
        UpdateCustomizationCameraMethod?.Invoke(controller, null);

        if (NetworkSingleton<TextChannelManager>.I != null)
        {
            NetworkSingleton<TextChannelManager>.I.MainCustomizationController.ApplyCustomization(data);
        }

        controller.IsAnyCustomize = true;
    }

    private static void SetAllSelectionBgs(CustomizationUIController controller, int selectedSlot)
    {
        if (StyleSelectedBgsField?.GetValue(controller) is List<GameObject> nativeBgs)
        {
            for (int i = 0; i < nativeBgs.Count; i++)
            {
                nativeBgs[i]?.SetActive(i == selectedSlot);
            }
        }

        foreach (KeyValuePair<int, GameObject> pair in RowSelectedBgs)
        {
            pair.Value?.SetActive(pair.Key == selectedSlot);
        }
    }

    private static void UpdateHeader(CustomizationUIController controller, int slot, string name)
    {
        if (StyleNumTextField?.GetValue(controller) is TextMeshProUGUI number)
        {
            number.text = (slot + 1).ToString(CultureInfo.InvariantCulture);
        }

        if (StyleNameInputField?.GetValue(controller) is TMP_InputField input)
        {
            input.SetTextWithoutNotify(name ?? string.Empty);
        }
    }

    private static string GetName(int slot) => Names[slot - NativeSlotCount]?.Value ?? string.Empty;

    private static void ConfigureRowButton(GameObject row, CustomizationUIController controller, int slot)
    {
        Button button = row.GetComponent<Button>() ?? row.GetComponentInChildren<Button>(includeInactive: true);
        if (button == null)
        {
            return;
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => SelectExtended(controller, slot));
    }

    private static void ConfigureRowNumber(GameObject row, TextMeshProUGUI nameText, int displayNumber)
    {
        foreach (TextMeshProUGUI text in row.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true))
        {
            if (text == nameText)
            {
                continue;
            }

            string trimmed = (text.text ?? string.Empty).Trim();
            if (int.TryParse(trimmed.TrimEnd('-', ' '), out _))
            {
                text.text = displayNumber.ToString(CultureInfo.InvariantCulture);
                return;
            }
        }
    }

    private static GameObject FindSharedRow(Transform text, Transform selectedBg)
    {
        HashSet<Transform> textAncestors = new();
        for (Transform current = text; current != null; current = current.parent)
        {
            textAncestors.Add(current);
        }

        for (Transform current = selectedBg; current != null; current = current.parent)
        {
            if (textAncestors.Contains(current) && current.GetComponentInChildren<Button>(includeInactive: true) != null)
            {
                return current.gameObject;
            }
        }

        return text.parent?.gameObject;
    }

    private static TextMeshProUGUI FindMatchingText(GameObject sourceRoot, TextMeshProUGUI source, GameObject cloneRoot)
    {
        Transform mapped = cloneRoot.transform.Find(GetRelativePath(sourceRoot.transform, source.transform));
        return mapped?.GetComponent<TextMeshProUGUI>() ?? cloneRoot.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(text => text.name == source.name);
    }

    private static GameObject FindMatchingObject(GameObject sourceRoot, GameObject source, GameObject cloneRoot)
    {
        Transform mapped = cloneRoot.transform.Find(GetRelativePath(sourceRoot.transform, source.transform));
        return mapped?.gameObject ?? cloneRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == source.name)?.gameObject;
    }

    private static string GetRelativePath(Transform root, Transform child)
    {
        List<string> parts = new();
        Transform current = child;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void ExpandPanelForRows(GameObject panel, GameObject referenceRow, int addedRows)
    {
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        RectTransform rowRect = referenceRow.GetComponent<RectTransform>();
        if (panelRect == null || rowRect == null || panel.GetComponent<ContentSizeFitter>() != null)
        {
            return;
        }

        float rowHeight = Mathf.Abs(rowRect.rect.height);
        if (rowHeight > 1f)
        {
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelRect.rect.height + (rowHeight * addedRows));
            FitPanelToScreen(panelRect);
        }
    }

    private static void FitPanelToScreen(RectTransform panelRect)
    {
        Canvas canvas = panelRect.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Canvas rootCanvas = canvas.rootCanvas;
        Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        panelRect.GetWorldCorners(corners);
        float bottom = float.MaxValue;
        float top = float.MinValue;
        for (int index = 0; index < corners.Length; index++)
        {
            float screenY = RectTransformUtility.WorldToScreenPoint(camera, corners[index]).y;
            bottom = Mathf.Min(bottom, screenY);
            top = Mathf.Max(top, screenY);
        }

        const float screenMargin = 8f;
        float screenDelta = 0f;
        if (bottom < screenMargin)
        {
            screenDelta = screenMargin - bottom;
        }
        else if (top > Screen.height - screenMargin)
        {
            screenDelta = (Screen.height - screenMargin) - top;
        }

        if (Mathf.Abs(screenDelta) > 0.5f)
        {
            float scale = Mathf.Max(rootCanvas.scaleFactor, 0.01f);
            panelRect.anchoredPosition += Vector2.up * (screenDelta / scale);
        }
    }

    internal static string Serialize(CustomizationDataIDs3 data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        string appearance = SerializeMap(data.AppearanceToShapeAndColors.Select(pair => new KeyValuePair<int, ShapeAndColorsData>((int)pair.Key, pair.Value)));
        string outfit = SerializeMap(data.OutfitToShapeAndColors.Select(pair => new KeyValuePair<int, ShapeAndColorsData>((int)pair.Key, pair.Value)));
        return string.Join(";", PayloadVersion, data.IsFullBody ? "1" : "0", appearance, outfit);
    }

    internal static CustomizationDataIDs3 Deserialize(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        string[] sections = payload.Split(';');
        if (sections.Length != 4 || sections[0] != PayloadVersion)
        {
            return null;
        }

        CustomizationDataIDs3 data = new CustomizationDataIDs3
        {
            IsFullBody = sections[1] == "1",
            AppearanceToShapeAndColors = ParseMap<AppearanceType>(sections[2]),
            OutfitToShapeAndColors = ParseMap<OutfitType>(sections[3])
        };
        return data.AppearanceToShapeAndColors.Count == Enum.GetValues(typeof(AppearanceType)).Length &&
               data.OutfitToShapeAndColors.Count == Enum.GetValues(typeof(OutfitType)).Length
            ? data
            : null;
    }

    private static CustomizationDataIDs3 LoadPayload(int slot)
    {
        try
        {
            return Deserialize(Payloads[slot - NativeSlotCount].Value);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Avatar style slot {slot + 1} payload was invalid and will use a fresh default: {ex.GetType().Name}.");
            return null;
        }
    }

    private static string SerializeMap(IEnumerable<KeyValuePair<int, ShapeAndColorsData>> entries)
    {
        return string.Join("|", entries.OrderBy(pair => pair.Key).Select(pair =>
            pair.Key.ToString(CultureInfo.InvariantCulture) + ":" +
            pair.Value.ShapeIndex.ToString(CultureInfo.InvariantCulture) + ":" +
            string.Join(",", pair.Value.ColorIndexes ?? new List<int>())));
    }

    private static Dictionary<TEnum, ShapeAndColorsData> ParseMap<TEnum>(string section) where TEnum : struct
    {
        Dictionary<TEnum, ShapeAndColorsData> result = new();
        foreach (string entry in section.Split('|'))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int shape))
            {
                continue;
            }

            List<int> colors = string.IsNullOrEmpty(parts[2])
                ? new List<int>()
                : parts[2].Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToList();
            result[(TEnum)Enum.ToObject(typeof(TEnum), key)] = new ShapeAndColorsData(shape, colors);
        }

        return result;
    }
}

[HarmonyPatch(typeof(CustomizationUIController), "InitStyleDropdown")]
internal static class AvatarStyleSlotsInitPatch
{
    [HarmonyPostfix]
    private static void Postfix(CustomizationUIController __instance) => AvatarStyleSlotsBridge.ExtendDropdown(__instance);
}

[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.ButtonSelectStyle))]
internal static class AvatarStyleSlotsNativeSelectionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CustomizationUIController __instance, int styleIndex)
    {
        return !AvatarStyleSlotsBridge.SelectNativeFromExtended(__instance, styleIndex);
    }
}

[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.CloseAvatarPanel))]
internal static class AvatarStyleSlotsClosePatch
{
    [HarmonyPrefix]
    private static bool Prefix(CustomizationUIController __instance, bool notFromStyleChanged)
    {
        return !AvatarStyleSlotsBridge.SaveExtendedClose(__instance, notFromStyleChanged);
    }
}

[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.OnDeselectStyleInputField))]
internal static class AvatarStyleSlotsRenamePatch
{
    [HarmonyPrefix]
    private static bool Prefix(CustomizationUIController __instance, string styleName)
    {
        return !AvatarStyleSlotsBridge.RenameExtended(__instance, styleName);
    }
}
