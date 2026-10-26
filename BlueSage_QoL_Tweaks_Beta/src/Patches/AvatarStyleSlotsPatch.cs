using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BlueSage.QoLTweaks.Core;
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

    private const int ExtendedSlotCount = TotalSlotCount - NativeSlotCount;
    private static bool _storeLoaded;
    private static bool _storeWritable;
    private static string _storePath;
    private static string _legacyConfigHash;
    private static bool _readOnlyNoticeShown;
    private static int _selectedSlot = -1;
    private static readonly string[] SlotNames = new string[ExtendedSlotCount];
    private static readonly string[] SlotPayloads = new string[ExtendedSlotCount];
    private static readonly Dictionary<int, TextMeshProUGUI> RowTexts = new();
    private static readonly Dictionary<int, GameObject> RowSelectedBgs = new();
    private static int _activeExtendedSlot = -1;
    private static bool _closingExtendedSlot;
    private static CustomizationUIController _lastController;
    private static CustomizationDataIDs3 _nativeSlotSnapshot;
    private static int _nativeSlotSnapshotIndex = -1;
    private static bool _ownNativeWrite;
    private static bool _loggedRedirect;
    private static bool _loggedBlockedWrite;
    private static int _persistBatchDepth;
    private static bool _persistPending;
    private static CustomizationDataIDs3 _wornSlotIds;
    private static int _wornReadScope;
    private static int _nativeWriterScope;

    // Round 4 WS1: the setter redirect is active only inside the two vanilla writers of the current outfit
    // (SetLemonadeHat, CloseAvatarPanel). Other mods' temporary outfit writes pass straight through untouched.
    internal static void EnterNativeWriterScope() => _nativeWriterScope++;

    internal static void ExitNativeWriterScope()
    {
        if (_nativeWriterScope > 0)
        {
            _nativeWriterScope--;
        }
    }

    // Round 4 WS1: the getter redirect is active only inside vanilla PetManager.CheckClothingRequirements.
    // The game saves with Newtonsoft JsonConvert, which serializes this public property; a global redirect
    // would write the worn outfit into the save and the loader would push it into vanilla slot 1-3.
    internal static void EnterWornReadScope() => _wornReadScope++;

    internal static void ExitWornReadScope()
    {
        if (_wornReadScope > 0)
        {
            _wornReadScope--;
        }
    }

    // Returns the cached worn slot 4-9 outfit by reference (read every frame): no allocation, no parsing.
    internal static bool TryReadWornSlot(out CustomizationDataIDs3 ids)
    {
        ids = _wornSlotIds;
        return _wornReadScope > 0 && ids != null && AvatarSlotWriteGuardPolicy.ShouldRedirectNativeWrite(_activeExtendedSlot, NativeSlotCount, TotalSlotCount, _ownNativeWrite);
    }

    internal static bool IsExtendedActive => _activeExtendedSlot >= NativeSlotCount && _activeExtendedSlot < TotalSlotCount;

    // Round 4 WS1: called from the PlayerDataZip setter guard. Returns true when the write was saved
    // into the worn slot 4-9 instead, so the vanilla slot keeps its own outfit.
    internal static bool TryRedirectNativeWrite(CustomizationDataIDs3 value)
    {
        if (value == null)
        {
            return false;
        }

        switch (AvatarSlotWriteGuardPolicy.Decide(_activeExtendedSlot, NativeSlotCount, TotalSlotCount, _ownNativeWrite, _nativeWriterScope > 0))
        {
            case NativeWriteAction.Allow:
                return false;
            case NativeWriteAction.Block:
                // Review R12: not one of the game's outfit writers. Saving it would put a temporary look into
                // vanilla slot 1-3 through the autosave; ignoring it changes no visuals (rendering never reads this).
                if (!_loggedBlockedWrite)
                {
                    _loggedBlockedWrite = true;
                    Plugin.Log?.LogWarning($"Avatar style slot {_activeExtendedSlot + 1} is worn, so an outfit write from outside the game's own outfit writers was ignored to keep vanilla slots 1-3 unchanged (logged once per session).");
                }

                return true;
        }

        try
        {
            EnsureSlotStore();
            SaveExtendedData(CloneIds(value), _activeExtendedSlot);
            if (!_loggedRedirect)
            {
                _loggedRedirect = true;
                Plugin.Log?.LogInfo($"Avatar style slot {_activeExtendedSlot + 1} kept a game outfit change; vanilla slots 1-3 stay untouched.");
            }

            return true;
        }
        catch (Exception ex)
        {
            // Fail closed: the vanilla write is still blocked, so slots 1-3 stay safe. This one change is not kept anywhere.
            Plugin.Log?.LogWarning($"Avatar style slot {_activeExtendedSlot + 1} could not keep a game outfit change, so the change was not saved anywhere (vanilla slots 1-3 untouched): {ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }

    // Round 4 WS1: slots 4-9 live in a crash-safe store (temp file + atomic replace + three backups) instead of a
    // BepInEx ConfigFile, whose Save() truncates and rewrites in place. The old .cfg is read once to migrate and
    // is then left exactly as it is.
    private static void EnsureSlotStore()
    {
        if (_storeLoaded)
        {
            return;
        }

        _storeLoaded = true;
        _storeWritable = false;
        _storePath = System.IO.Path.Combine(Paths.ConfigPath, "com.bluesage.ontogether.qol-avatar-styles.store");
        try
        {
            if (AvatarSlotStore.TryLoad(_storePath, ExtendedSlotCount, out int selected, out string recordedHash, out string[] names, out string[] payloads, out string usedFile))
            {
                _selectedSlot = selected;
                Array.Copy(names, SlotNames, ExtendedSlotCount);
                Array.Copy(payloads, SlotPayloads, ExtendedSlotCount);
                _legacyConfigHash = recordedHash;
                _storeWritable = true;
                bool restoredFromBackup = !string.Equals(usedFile, _storePath, StringComparison.OrdinalIgnoreCase);
                if (restoredFromBackup)
                {
                    Plugin.Log?.LogWarning("Avatar styles 4-9 were restored from the backup " + System.IO.Path.GetFileName(usedFile) + " because the main file was missing or incomplete.");
                }

                bool reconciled = ReconcileLegacyConfig(usedFile);
                if (restoredFromBackup || reconciled)
                {
                    PersistStore();
                }

                return;
            }

            // Store files exist but none could be read (locked or damaged): never migrate or start empty over
            // them. This session runs read-only so the files stay exactly as they are.
            if (AvatarSlotStore.AnyFileExists(_storePath))
            {
                Plugin.Log?.LogWarning("Avatar styles 4-9 storage exists but could not be read. Slot changes this session are not saved, so the files on disk stay untouched.");
                return;
            }

            MigrateFromLegacyConfig();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Avatar styles 4-9 storage could not be read: {ex.GetType().Name}. Slot changes this session are not saved, so the files on disk stay untouched.");
        }
    }

    private static void MigrateFromLegacyConfig()
    {
        for (int index = 0; index < ExtendedSlotCount; index++)
        {
            SlotNames[index] = string.Empty;
            SlotPayloads[index] = string.Empty;
        }

        if (!TryReadLegacyConfig(out int legacySelected, out string[] legacyNames, out string[] legacyPayloads))
        {
            _legacyConfigHash = string.Empty;
            _storeWritable = true;
            return;
        }

        _selectedSlot = legacySelected;
        Array.Copy(legacyNames, SlotNames, ExtendedSlotCount);
        Array.Copy(legacyPayloads, SlotPayloads, ExtendedSlotCount);
        _legacyConfigHash = LegacyConfigReimportPolicy.ContentHash(legacyNames, legacyPayloads);

        // Writable only once the old file was read in full; a read that throws leaves the session read-only.
        _storeWritable = true;
        if (PersistStore())
        {
            Plugin.Log?.LogInfo("Avatar styles 4-9 moved to crash-safe storage; the old settings file was left untouched.");
        }
    }

    // The old .cfg is read with SaveOnConfigSet off, so reading it never rewrites it. False when the file is absent.
    private static bool TryReadLegacyConfig(out int selectedSlot, out string[] names, out string[] payloads)
    {
        selectedSlot = -1;
        names = new string[ExtendedSlotCount];
        payloads = new string[ExtendedSlotCount];
        string legacyPath = System.IO.Path.Combine(Paths.ConfigPath, "com.bluesage.ontogether.qol-avatar-styles.cfg");
        if (!System.IO.File.Exists(legacyPath))
        {
            return false;
        }

        ConfigFile legacy = new ConfigFile(legacyPath, saveOnInit: false) { SaveOnConfigSet = false };
        selectedSlot = legacy.Bind("State", "SelectedExtendedSlot", -1, "Last selected BlueSage avatar style slot. -1 means a vanilla slot is active.").Value;
        for (int slot = NativeSlotCount; slot < TotalSlotCount; slot++)
        {
            int index = slot - NativeSlotCount;
            names[index] = legacy.Bind("Style " + (slot + 1), "Name", string.Empty, "Display name for BlueSage avatar style slot " + (slot + 1) + ".").Value ?? string.Empty;
            payloads[index] = legacy.Bind("Style " + (slot + 1), "Payload", string.Empty, "Versioned avatar customization payload. Managed by BlueSage QoL.").Value ?? string.Empty;
        }

        return true;
    }

    // Review R2: the frozen .cfg is the 0.2.5 rollback copy. When its content differs from what the store last
    // imported, the player edited slots under a rollback, and those edits win over the store (after a copy aside).
    // Returns true when the in-memory state changed and needs a save.
    private static bool ReconcileLegacyConfig(string loadedFile)
    {
        string currentHash = null;
        int legacySelected = -1;
        string[] legacyNames = null;
        string[] legacyPayloads = null;
        try
        {
            if (TryReadLegacyConfig(out legacySelected, out legacyNames, out legacyPayloads))
            {
                currentHash = LegacyConfigReimportPolicy.ContentHash(legacyNames, legacyPayloads);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Avatar styles 4-9: the old settings file could not be read, so it was not compared with the saved slots this session: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        bool legacyHasAnyPayload = legacyPayloads != null && LegacyConfigReimportPolicy.HasAnyPayload(legacyPayloads);
        switch (LegacyConfigReimportPolicy.Decide(_legacyConfigHash, currentHash, legacyHasAnyPayload))
        {
            case LegacyConfigAction.Reimport:
                string copy;
                try
                {
                    copy = AvatarSlotStore.BackupBeforeReimport(_storePath, loadedFile, DateTime.Now);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"Avatar styles 4-9: the old settings file changed, but the current slots could not be copied aside ({ex.GetType().Name}: {ex.Message}), so it was not re-imported this session. The saved slots are unchanged.");
                    return false;
                }

                _selectedSlot = legacySelected;
                Array.Copy(legacyNames, SlotNames, ExtendedSlotCount);
                Array.Copy(legacyPayloads, SlotPayloads, ExtendedSlotCount);
                _legacyConfigHash = currentHash;
                Plugin.Log?.LogWarning("Avatar styles 4-9 were re-imported from the old settings file because it changed since the last import (edits made under a rollback win). The previous slots were copied to " + System.IO.Path.GetFileName(copy) + ".");
                return true;
            case LegacyConfigAction.RecordHash:
                _legacyConfigHash = currentHash ?? string.Empty;
                return true;
            default:
                return false;
        }
    }

    // True when the store on disk now holds the in-memory slots. The new file replaces the old one only as the
    // final step (File.Replace), so a failure anywhere before it leaves the saved file exactly as it was.
    private static bool PersistStore()
    {
        if (_storePath == null || !_storeWritable)
        {
            return false;
        }

        // Review R10: inside a batch (one user action such as a slot switch) the save is deferred to the end,
        // so one action is one file write and one backup rotation.
        if (_persistBatchDepth > 0)
        {
            _persistPending = true;
            return true;
        }

        try
        {
            AvatarSlotStore.Save(_storePath, _selectedSlot, _legacyConfigHash, SlotNames, SlotPayloads);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Avatar styles 4-9 could not be saved: {ex.GetType().Name}: {ex.Message}. The saved file was not replaced; this change stays in memory and is written with the next successful save.");
            return false;
        }
    }

    private static void SetSelectedSlot(int slot)
    {
        if (_selectedSlot == slot)
        {
            return;
        }

        _selectedSlot = slot;
        PersistStore();
    }

    private static void BeginPersistBatch() => _persistBatchDepth++;

    private static void EndPersistBatch()
    {
        if (_persistBatchDepth > 0)
        {
            _persistBatchDepth--;
        }

        if (_persistBatchDepth == 0 && _persistPending)
        {
            _persistPending = false;
            PersistStore();
        }
    }

    internal static void ExtendDropdown(CustomizationUIController controller)
    {
        if (controller == null || Plugin.EnableExtendedAvatarStyles == null || !Plugin.EnableExtendedAvatarStyles.Value)
        {
            return;
        }

        _lastController = controller;
        EnsureSlotStore();
        if (AvatarSlotStoreNoticePolicy.ShouldShowReadOnlyNotice(_storeLoaded, _storeWritable, _readOnlyNoticeShown) &&
            Plugin.AddLocalNotificationWithToast(Plugin.FormatBlueSageWarning("Avatar styles 4-9 are read-only this session: their saved file could not be read, so nothing on disk was changed and slot edits made now will not be kept. Details are in BepInEx/LogOutput.log.")))
        {
            _readOnlyNoticeShown = true;
        }

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
            SetSelectedSlot(-1);
            Plugin.Log?.LogWarning($"Avatar Styles 4-9 deferred an invalid saved-slot restore without interrupting CustomizationUI startup: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Round 5h: called when the player opens the avatar panel (not at scene load, where the rows are built and a toast
    // would fade unseen). The first time it finds the extra rows in place, one note says they are there.
    internal static void NotifyAvatarScreenOpened()
    {
        Plugin.ShowOutfitSlotsHintOnce(Plugin.EnableExtendedAvatarStyles != null && Plugin.EnableExtendedAvatarStyles.Value ? RowTexts.Count : 0);
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
        _wornSlotIds = null;
        Plugin.Log?.LogInfo("Avatar preset slots 4-9 disabled; vanilla slots 1-3 restored and QoL preset data retained.");
    }

    internal static void SelectExtended(CustomizationUIController controller, int slot)
    {
        if (controller == null || slot < NativeSlotCount || slot >= TotalSlotCount)
        {
            return;
        }

        EnsureSlotStore();
        BeginPersistBatch();
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
            SetSelectedSlot(slot);

            CustomizationDataIDs3 ids = LoadPayload(slot) ?? new CustomizationDataIDs3();
            _wornSlotIds = ids;
            ApplyToEditor(controller, ids);
            SetAllSelectionBgs(controller, slot);
            UpdateHeader(controller, slot, GetName(slot));
            Plugin.Log?.LogInfo($"Avatar style slot {slot + 1} selected through the BlueSage compatibility bridge.");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Avatar style slot {slot + 1} selection failed safely: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            EndPersistBatch();
        }
    }

    internal static bool SelectNativeFromExtended(CustomizationUIController controller, int slot)
    {
        if (!IsExtendedActive || controller == null || slot < 0 || slot >= NativeSlotCount)
        {
            return false;
        }

        BeginPersistBatch();
        try
        {
            SaveExtended(controller, _activeExtendedSlot);
            RestoreNativeSlotSnapshot();
            SetAllSelectionBgs(controller, -1);
            _activeExtendedSlot = -1;
            _wornSlotIds = null;
            SetSelectedSlot(-1);

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
        finally
        {
            EndPersistBatch();
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
            SaveExtendedData(new CustomizationDataIDs3(editorData), _activeExtendedSlot);
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

        EnsureSlotStore();
        string normalized = styleName ?? string.Empty;
        SlotNames[_activeExtendedSlot - NativeSlotCount] = normalized;
        PersistStore();
        if (RowTexts.TryGetValue(_activeExtendedSlot, out TextMeshProUGUI text) && text != null)
        {
            text.text = "- " + normalized;
        }

        return true;
    }

    private static void RestoreExtendedSelectionIfNeeded(CustomizationUIController controller, List<GameObject> nativeBgs)
    {
        int slot = _selectedSlot;
        if (slot < NativeSlotCount || slot >= TotalSlotCount || string.IsNullOrWhiteSpace(SlotPayloads[slot - NativeSlotCount]))
        {
            _activeExtendedSlot = -1;
            return;
        }

        CustomizationDataIDs3 ids = LoadPayload(slot);
        if (ids == null)
        {
            // Unreadable payload: keep the vanilla slot worn rather than routing game changes into it.
            _activeExtendedSlot = -1;
            SetSelectedSlot(-1);
            Plugin.Log?.LogWarning($"Avatar style slot {slot + 1} could not be read at startup; your vanilla slot stays on. Slot {slot + 1}'s saved data was left as it is.");
            return;
        }

        // Re-wearing a slot 4-9 after a relog: remember the vanilla slot first, exactly as a live
        // selection does, so returning to slots 1-3 restores it.
        CaptureNativeSlotSnapshot();
        _activeExtendedSlot = slot;
        foreach (GameObject bg in nativeBgs)
        {
            bg?.SetActive(false);
        }

        _wornSlotIds = ids;
        ApplyToEditor(controller, ids);
        SetAllSelectionBgs(controller, slot);
        UpdateHeader(controller, slot, GetName(slot));
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

        _ownNativeWrite = true;
        try
        {
            _nativeSlotSnapshotIndex = Mathf.Clamp(zip.SelectedStyleIndex, 0, NativeSlotCount - 1);
            _nativeSlotSnapshot = CloneIds(zip.CurrentCustomizationDataIDs);
        }
        finally
        {
            _ownNativeWrite = false;
        }
    }

    private static void RestoreNativeSlotSnapshot()
    {
        PlayerDataZip zip = MonoSingleton<DataManager>.I?.PlayerDataZip;
        if (zip == null || _nativeSlotSnapshot == null || _nativeSlotSnapshotIndex < 0)
        {
            return;
        }

        _ownNativeWrite = true;
        try
        {
            zip.SelectedStyleIndex = _nativeSlotSnapshotIndex;
            zip.CurrentCustomizationDataIDs = CloneIds(_nativeSlotSnapshot);
        }
        finally
        {
            _ownNativeWrite = false;
        }
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

        SaveExtendedData(new CustomizationDataIDs3(data), slot);
    }

    // Review R10: callers hand over one ids object they own; it is serialized once and, when it is the worn
    // slot, kept as the getter cache without another conversion.
    private static void SaveExtendedData(CustomizationDataIDs3 ids, int slot)
    {
        if (ids == null || slot < NativeSlotCount || slot >= TotalSlotCount)
        {
            return;
        }

        SlotPayloads[slot - NativeSlotCount] = Serialize(ids);
        PersistStore();
        if (slot == _activeExtendedSlot)
        {
            _wornSlotIds = ids;
        }
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

    private static string GetName(int slot) => SlotNames[slot - NativeSlotCount] ?? string.Empty;

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
            return Deserialize(SlotPayloads[slot - NativeSlotCount]);
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

[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.ButtonOpenCustomizationUI))]
internal static class AvatarStyleSlotsOpenHintPatch
{
    [HarmonyPostfix]
    private static void Postfix(CustomizationUIController __instance)
    {
        try
        {
            if (__instance != null && __instance.CustomizationPageType == CustomizationPageType.Avatar)
            {
                AvatarStyleSlotsBridge.NotifyAvatarScreenOpened();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Extra outfit slots note skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
    }
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
        AvatarStyleSlotsBridge.EnterNativeWriterScope();
        return !AvatarStyleSlotsBridge.SaveExtendedClose(__instance, notFromStyleChanged);
    }

    [HarmonyFinalizer]
    private static void Finalizer() => AvatarStyleSlotsBridge.ExitNativeWriterScope();
}

[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.SetLemonadeHat))]
internal static class AvatarStyleSlotsLemonadeScopePatch
{
    [HarmonyPrefix]
    private static void Prefix() => AvatarStyleSlotsBridge.EnterNativeWriterScope();

    [HarmonyFinalizer]
    private static void Finalizer() => AvatarStyleSlotsBridge.ExitNativeWriterScope();
}

// Round 4 WS1: the two vanilla writes of the current outfit (CloseAvatarPanel, SetLemonadeHat) pass through
// this setter. While a slot 4-9 is worn and one of them is running, the write lands in that slot instead.
[HarmonyPatch(typeof(PlayerDataZip), nameof(PlayerDataZip.CurrentCustomizationDataIDs), MethodType.Setter)]
internal static class AvatarStyleSlotsNativeWriteGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CustomizationDataIDs3 value)
    {
        return !AvatarStyleSlotsBridge.TryRedirectNativeWrite(value);
    }
}

// Round 4 WS1: the per-frame pet clothing check judges the outfit the player is actually wearing.
[HarmonyPatch(typeof(PetManager), "CheckClothingRequirements")]
internal static class AvatarStyleSlotsPetClothingScopePatch
{
    [HarmonyPrefix]
    private static void Prefix() => AvatarStyleSlotsBridge.EnterWornReadScope();

    [HarmonyFinalizer]
    private static void Finalizer() => AvatarStyleSlotsBridge.ExitWornReadScope();
}

// Only answers inside that scope; every other read (including the game's save serializer) gets vanilla data.
[HarmonyPatch(typeof(PlayerDataZip), nameof(PlayerDataZip.CurrentCustomizationDataIDs), MethodType.Getter)]
internal static class AvatarStyleSlotsNativeReadGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref CustomizationDataIDs3 __result)
    {
        if (AvatarStyleSlotsBridge.TryReadWornSlot(out CustomizationDataIDs3 worn))
        {
            __result = worn;
            return false;
        }

        return true;
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
