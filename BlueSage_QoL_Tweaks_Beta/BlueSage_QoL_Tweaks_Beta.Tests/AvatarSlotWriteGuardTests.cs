using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 WS1 (Blue, 2026-10-19): players must never find a vanilla outfit slot changed next session.
// Vanilla SetLemonadeHat (from UseLemonade) writes PlayerDataZip.CurrentCustomizationDataIDs and saves,
// which copied a worn slot 4-9 outfit into vanilla slot 1-3. The guard sits on that setter, scoped to the two
// vanilla writers, so their writes land in the slot the player is actually wearing.
[TestFixture]
public sealed class AvatarSlotWriteGuardTests
{
    private static string Source => File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..", "src", "Patches", "AvatarStyleSlotsPatch.cs")).Replace("\r\n", "\n");

    [TestCase(-1, false, false, TestName = "Vanilla_slot_worn_writes_go_to_vanilla")]
    [TestCase(0, false, false, TestName = "Native_index_is_never_redirected")]
    [TestCase(2, false, false, TestName = "Last_native_index_is_never_redirected")]
    [TestCase(3, false, true, TestName = "Slot_4_worn_redirects_vanilla_writes")]
    [TestCase(8, false, true, TestName = "Slot_9_worn_redirects_vanilla_writes")]
    [TestCase(9, false, false, TestName = "Out_of_range_slot_is_ignored")]
    [TestCase(5, true, false, TestName = "Our_own_snapshot_restore_bypasses_the_guard")]
    public void Vanilla_writes_follow_the_worn_slot(int activeSlot, bool bypass, bool expectedRedirect)
    {
        Assert.That(AvatarSlotWriteGuardPolicy.ShouldRedirectNativeWrite(activeSlot, nativeSlotCount: 3, totalSlotCount: 9, ownWriteInProgress: bypass), Is.EqualTo(expectedRedirect));
    }

    // Review R12 (2026-10-19, taken by Blue): while a slot 4-9 is worn, a write to the vanilla property from
    // outside the game's two outfit writers (another mod's temporary look, for example) is BLOCKED, not passed:
    // the autosave would otherwise keep that look in vanilla slot 1-3. Visuals never depended on the property
    // (verified: BlueExclusive renders via ApplyCustomization). This reverses the 22:08 pass-through.
    [TestCase(-1, false, false, NativeWriteAction.Allow, TestName = "Vanilla_slot_worn_allows_every_write")]
    [TestCase(-1, false, true, NativeWriteAction.Allow, TestName = "Vanilla_slot_worn_allows_vanilla_writers")]
    [TestCase(5, true, false, NativeWriteAction.Allow, TestName = "Our_own_restore_is_allowed_while_worn")]
    [TestCase(3, false, true, NativeWriteAction.Redirect, TestName = "Vanilla_writer_while_slot_4_worn_is_redirected")]
    [TestCase(8, false, true, NativeWriteAction.Redirect, TestName = "Vanilla_writer_while_slot_9_worn_is_redirected")]
    [TestCase(3, false, false, NativeWriteAction.Block, TestName = "Outside_writer_while_slot_4_worn_is_blocked")]
    [TestCase(8, false, false, NativeWriteAction.Block, TestName = "Outside_writer_while_slot_9_worn_is_blocked")]
    [TestCase(9, false, false, NativeWriteAction.Allow, TestName = "Out_of_range_slot_allows_every_write")]
    public void Writes_while_a_slot_4_to_9_is_worn_are_redirected_or_blocked(int activeSlot, bool ownWrite, bool insideVanillaWriter, object expected)
    {
        // The enum is internal, so the public test method takes it as object.
        Assert.That(AvatarSlotWriteGuardPolicy.Decide(activeSlot, nativeSlotCount: 3, totalSlotCount: 9, ownWriteInProgress: ownWrite, insideNativeWriterScope: insideVanillaWriter), Is.EqualTo((NativeWriteAction)expected));
    }

    [Test]
    public void Setter_guard_is_patched_and_saves_into_the_worn_slot()
    {
        string source = Source;
        int start = source.IndexOf("internal static bool TryRedirectNativeWrite(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        string blockCase = body.Substring(body.IndexOf("case NativeWriteAction.Block:", System.StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("[HarmonyPatch(typeof(PlayerDataZip), nameof(PlayerDataZip.CurrentCustomizationDataIDs), MethodType.Setter)]"));
            Assert.That(source, Does.Contain("AvatarStyleSlotsBridge.TryRedirectNativeWrite(value)"));
            Assert.That(body, Does.Contain("AvatarSlotWriteGuardPolicy.Decide(_activeExtendedSlot, NativeSlotCount, TotalSlotCount, _ownNativeWrite, _nativeWriterScope > 0)"));
            Assert.That(body, Does.Contain("SaveExtendedData(CloneIds(value), _activeExtendedSlot);"), "One copy of the incoming ids, no round trip through CustomizationData (R10).");
            Assert.That(blockCase.Substring(0, blockCase.IndexOf("case ", 5, System.StringComparison.Ordinal) < 0 ? blockCase.Length : blockCase.IndexOf("case ", 5, System.StringComparison.Ordinal)), Does.Contain("return true;"), "A blocked write skips the vanilla setter.");
            Assert.That(body, Does.Contain("_loggedBlockedWrite"), "Blocked writes are logged once per session.");
        });
    }

    // Code review 2026-10-19 (advisor): a global setter redirect also caught other mods' temporary outfit
    // writes and saved them into the worn slot for good. Only the two vanilla writers of the current outfit
    // (decompile build 25966942: SetLemonadeHat, CloseAvatarPanel) are redirected. R12: every other write while
    // a slot 4-9 is worn is blocked instead of saved anywhere.
    [Test]
    public void Setter_redirect_is_scoped_to_the_two_vanilla_outfit_writers()
    {
        string source = Source;
        int start = source.IndexOf("internal static bool TryRedirectNativeWrite(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        int closeStart = source.IndexOf("internal static class AvatarStyleSlotsClosePatch", System.StringComparison.Ordinal);
        string closePatch = source.Substring(closeStart, source.IndexOf("\n}\n", closeStart, System.StringComparison.Ordinal) - closeStart);
        int hatStart = source.IndexOf("internal static class AvatarStyleSlotsLemonadeScopePatch", System.StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("_nativeWriterScope > 0"));
            Assert.That(source, Does.Contain("[HarmonyPatch(typeof(CustomizationUIController), nameof(CustomizationUIController.SetLemonadeHat))]"));
            Assert.That(hatStart, Is.GreaterThan(0));
            Assert.That(closePatch, Does.Contain("AvatarStyleSlotsBridge.EnterNativeWriterScope();"));
            Assert.That(closePatch, Does.Contain("[HarmonyFinalizer]"));
            Assert.That(closePatch, Does.Contain("AvatarStyleSlotsBridge.ExitNativeWriterScope();"));
        });
    }

    // Code review 2026-10-19: if saving into the worn slot fails, the vanilla write must still be blocked,
    // otherwise the worn outfit lands in vanilla slot 1-3, the exact thing the guard promises to prevent.
    [Test]
    public void A_failed_redirect_still_blocks_the_vanilla_write()
    {
        string source = Source;
        int start = source.IndexOf("internal static bool TryRedirectNativeWrite(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        string catchBlock = body.Substring(body.IndexOf("catch (Exception ex)", System.StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(catchBlock, Does.Contain("return true;"));
            Assert.That(catchBlock, Does.Not.Contain("return false;"));
        });
    }

    // The game's pet-unlock check (PetManager Update -> CheckClothingRequirements) reads the current outfit
    // every frame and must see the worn slot 4-9. NOTHING else may: the game saves with Newtonsoft JsonConvert,
    // which serializes this public property, so a global getter redirect would write the worn outfit into the
    // save and the loader would push it into vanilla slot 1-3 next launch (advisor catch, 2026-10-19).
    // So the redirect is scoped to that one vanilla reader and served from a cache with no allocation.
    [Test]
    public void Getter_redirect_is_scoped_to_the_pet_clothing_check_only()
    {
        string source = Source;
        int start = source.IndexOf("internal static bool TryReadWornSlot(", System.StringComparison.Ordinal);
        string body = start < 0 ? string.Empty : source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("[HarmonyPatch(typeof(PlayerDataZip), nameof(PlayerDataZip.CurrentCustomizationDataIDs), MethodType.Getter)]"));
            Assert.That(source, Does.Contain("AvatarStyleSlotsBridge.TryReadWornSlot(out CustomizationDataIDs3 worn)"));
            Assert.That(body, Does.Contain("_wornReadScope > 0"));
            Assert.That(body, Does.Contain("_wornSlotIds"));
            Assert.That(body, Does.Not.Contain("Deserialize("));
            Assert.That(body, Does.Not.Contain("new "));
            Assert.That(source, Does.Contain("_wornSlotIds = ids;"), "The saved ids object is the cache; no second conversion (R10).");
            Assert.That(source, Does.Contain("[HarmonyPatch(typeof(PetManager), \"CheckClothingRequirements\")]"));
            Assert.That(source, Does.Contain("AvatarStyleSlotsBridge.EnterWornReadScope();"));
            Assert.That(source, Does.Contain("[HarmonyFinalizer]"));
            Assert.That(source, Does.Contain("AvatarStyleSlotsBridge.ExitWornReadScope();"));
        });
    }

    // An unreadable saved slot 4-9 must not become "worn": the setter guard would then route game outfit
    // changes into a slot the player can't see. Stay on the vanilla slot instead.
    [Test]
    public void Unreadable_saved_slot_keeps_the_vanilla_slot_worn()
    {
        string source = Source;
        int start = source.IndexOf("private static void RestoreExtendedSelectionIfNeeded(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        int load = body.IndexOf("CustomizationDataIDs3 ids = LoadPayload(slot);", System.StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(load, Is.GreaterThan(0));
            Assert.That(load, Is.LessThan(body.IndexOf("_activeExtendedSlot = slot;", System.StringComparison.Ordinal)));
            Assert.That(body, Does.Contain("if (ids == null)"));
        });
    }

    [Test]
    public void No_save_or_load_path_is_inside_the_worn_read_scope()
    {
        string source = Source;
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("\"SavePlayerZipData\")]"));
            Assert.That(source, Does.Not.Contain("\"SaveBackupZipData\")]"));
            Assert.That(source, Does.Not.Contain("\"LoadPlayerData\")]"));
        });
    }

    [Test]
    public void Our_own_reads_of_the_vanilla_slot_bypass_the_getter_guard()
    {
        string source = Source;
        int start = source.IndexOf("private static void CaptureNativeSlotSnapshot()", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.That(body, Does.Contain("_ownNativeWrite = true;"));
    }

    [Test]
    public void Our_snapshot_restore_writes_through_the_bypass()
    {
        string source = Source;
        int start = source.IndexOf("private static void RestoreNativeSlotSnapshot()", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("_ownNativeWrite = true;"));
            Assert.That(body, Does.Contain("zip.CurrentCustomizationDataIDs = CloneIds(_nativeSlotSnapshot);"));
            Assert.That(body, Does.Contain("finally"));
            Assert.That(body, Does.Contain("_ownNativeWrite = false;"));
        });
    }

    [Test]
    public void Startup_restore_of_a_worn_slot_snapshots_the_vanilla_slot_first()
    {
        string source = Source;
        int start = source.IndexOf("private static void RestoreExtendedSelectionIfNeeded(", System.StringComparison.Ordinal);
        string body = source.Substring(start, source.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.That(body.IndexOf("CaptureNativeSlotSnapshot();", System.StringComparison.Ordinal), Is.GreaterThan(0)
            .And.LessThan(body.IndexOf("_activeExtendedSlot = slot;", System.StringComparison.Ordinal)));
    }
}
