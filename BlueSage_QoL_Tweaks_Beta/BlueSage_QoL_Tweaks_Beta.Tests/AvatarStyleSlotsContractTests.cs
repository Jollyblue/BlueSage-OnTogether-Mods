using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
public sealed class AvatarStyleSlotsContractTests
{
    private static string Source => File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..", "src", "Patches", "AvatarStyleSlotsPatch.cs"));

    [Test]
    public void BridgeExposesNineSlotsWhileKeepingThreeNativeSlots()
    {
        Assert.That(Source, Does.Contain("internal const int NativeSlotCount = 3;"));
        Assert.That(Source, Does.Contain("internal const int TotalSlotCount = 9;"));
        Assert.That(Source, Does.Contain("for (int slot = NativeSlotCount; slot < TotalSlotCount; slot++)"));
    }

    [Test]
    public void ExtendedRowsDoNotExpandVanillaIndexedLists()
    {
        Assert.That(Source, Does.Not.Contain("nativeTexts.Add("));
        Assert.That(Source, Does.Not.Contain("nativeBgs.Add("));
        Assert.That(Source, Does.Not.Contain("PlayerDataZip.SelectedStyleIndex = slot;"));
        Assert.That(Source, Does.Contain("button.onClick = new Button.ButtonClickedEvent();"));
        Assert.That(Source, Does.Contain("button.onClick.AddListener(() => SelectExtended(controller, slot));"));
    }

    [Test]
    public void VanillaSelectionRemainsBoundedAndExtendedStorageIsSeparate()
    {
        Assert.That(Source, Does.Contain("slot >= NativeSlotCount"));
        Assert.That(Source, Does.Contain("Mathf.Clamp(zip.SelectedStyleIndex, 0, NativeSlotCount - 1)"));
        // Round 4 review R4: the old ".cfg" pin passed vacuously once slots moved to the crash-safe store.
        Assert.That(Source, Does.Contain("com.bluesage.ontogether.qol-avatar-styles.store"));
        Assert.That(Source, Does.Contain("AvatarSlotStore.Save(_storePath, _selectedSlot, _legacyConfigHash, SlotNames, SlotPayloads)"));
        Assert.That(Source, Does.Contain("SaveOnConfigSet = false"), "The legacy .cfg is read, never written.");
        Assert.That(Source, Does.Contain("PayloadVersion = \"BSAS1\""));
        Assert.That(Source, Does.Not.Contain("CustomizationDataIDsDict ="));
        Assert.That(Source, Does.Not.Contain("CustomizationDataIDs1Dict ="));
        Assert.That(Source, Does.Not.Contain("CustomizationDataIDs2Dict ="));
        Assert.That(Source, Does.Not.Contain("StyleNames.Add("));
    }

    [Test]
    public void NetworkApplyAssignsCanonicalCustomizationFirst()
    {
        int assign = Source.IndexOf("MonoSingleton<DataManager>.I.CustomizationData = new CustomizationData(data);");
        int apply = Source.IndexOf("MainCustomizationController.ApplyCustomization(data);");
        Assert.That(assign, Is.GreaterThanOrEqualTo(0));
        Assert.That(apply, Is.GreaterThan(assign));
    }

    [Test]
    public void ExtendedCloseAndRenameBypassVanillaPersistence()
    {
        Assert.That(Source, Does.Contain("AvatarStyleSlotsBridge.SaveExtendedClose"));
        Assert.That(Source, Does.Contain("AvatarStyleSlotsBridge.RenameExtended"));
        Assert.That(Source, Does.Contain("return !AvatarStyleSlotsBridge.SaveExtendedClose"));
        Assert.That(Source, Does.Contain("return !AvatarStyleSlotsBridge.RenameExtended"));
    }

    [Test]
    public void FeatureHasAReversibleDefaultOnMenuToggle()
    {
        string plugin = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "src", "QolMenuWindow.cs"));
        Assert.That(plugin, Does.Contain("EnableExtendedAvatarStyles"));
        Assert.That(plugin, Does.Contain("Config.Bind(\"Avatar Styles\", \"EnableExtendedAvatarStyles\", true"));
        Assert.That(menu, Does.Contain("DrawToggle(\"Extra Outfit Slots 4-9\""), "Round 5h renamed the switch in plain words (config key EnableExtendedAvatarStyles is unchanged).");
        Assert.That(menu, Does.Not.Contain("EXPERIMENTAL - Avatar Presets 4-9"));
        Assert.That(menu, Does.Contain("AvatarStyleSlotsBridge.RefreshEnabledState"));
        Assert.That(Source, Does.Contain("DisableAndRestoreVanilla"));
        Assert.That(Source, Does.Contain("QoL preset data retained"));
    }

    [Test]
    public void ReturningToAnotherNativeSlotHandsControlBackToVanilla()
    {
        Assert.That(Source, Does.Contain("if (slot != previousNativeSlot)"));
        Assert.That(Source, Does.Contain("Report \"not handled\" to the Harmony prefix"));
        Assert.That(Source, Does.Contain("return !AvatarStyleSlotsBridge.SelectNativeFromExtended"));
        Assert.That(Source, Does.Contain("ResetUnsavedCustomizationMethod?.Invoke"));
        Assert.That(Source, Does.Contain("PointerExitStyleNamingMethod?.Invoke"));
    }

    [Test]
    public void ExpandedDropdownFitsAllNineRowsInsideTheCurrentScreen()
    {
        Assert.That(Source, Does.Contain("FitPanelToScreen(panelRect);"));
        Assert.That(Source, Does.Contain("panelRect.GetWorldCorners(corners);"));
        Assert.That(Source, Does.Contain("RectTransformUtility.WorldToScreenPoint"));
        Assert.That(Source, Does.Contain("Screen.height"));
        Assert.That(Source, Does.Contain("rootCanvas.scaleFactor"));
    }

    [Test]
    public void ExtendedEditingSnapshotsAndRestoresTheSelectedVanillaSlot()
    {
        Assert.That(Source, Does.Contain("CaptureNativeSlotSnapshot();"));
        Assert.That(Source, Does.Contain("RestoreNativeSlotSnapshot();"));
        Assert.That(Source, Does.Contain("zip.CurrentCustomizationDataIDs = CloneIds(_nativeSlotSnapshot);"));
        Assert.That(Source, Does.Contain("new CustomizationDataIDs3(new CustomizationData(source))"));

        int saveExtended = Source.IndexOf("SaveExtended(controller, _activeExtendedSlot);");
        int restoreNative = Source.IndexOf("RestoreNativeSlotSnapshot();", saveExtended);
        Assert.That(saveExtended, Is.GreaterThanOrEqualTo(0));
        Assert.That(restoreNative, Is.GreaterThan(saveExtended));
    }

    [Test]
    public void ClosingExtendedSlotCopiesEditorBeforeVanillaResetAndAppliesThatCopy()
    {
        int snapshot = Source.IndexOf("CustomizationData editorData = new CustomizationData(liveEditorData);");
        int reset = Source.IndexOf("ResetUnsavedCustomizationMethod?.Invoke", snapshot);
        int save = Source.IndexOf("SaveExtendedData(new CustomizationDataIDs3(editorData), _activeExtendedSlot);", snapshot);
        int apply = Source.IndexOf("MainCustomizationController.ApplyCustomization(editorData);", snapshot);

        Assert.That(snapshot, Is.GreaterThanOrEqualTo(0));
        Assert.That(reset, Is.GreaterThan(snapshot));
        Assert.That(save, Is.GreaterThan(reset));
        Assert.That(apply, Is.GreaterThan(save));
    }
}
