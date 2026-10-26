using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 review R2 (2026-10-19): the old qol-avatar-styles.cfg is frozen after the one-time migration, so slot
// edits made under a 0.2.5 rollback were silently dropped on re-upgrade. The store now records a hash of what it
// imported; when the .cfg content changes, the .cfg wins (Fable amendment, option a) after the current store is
// copied aside. A v1 store has no recorded hash and must never trigger a re-import on its own.
[TestFixture]
public sealed class LegacyConfigReimportPolicyTests
{
    private static readonly string[] Names = { "Cozy", "", "", "", "", "" };
    private static readonly string[] Payloads = { "BSAS1;0;1:2:3;4:5:6", "", "", "", "", "" };
    private static readonly string[] Empty = { "", "", "", "", "", "" };

    [Test]
    public void A_v1_store_records_the_current_hash_and_never_reimports()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LegacyConfigReimportPolicy.Decide(null, "abc", legacyHasAnyPayload: true), Is.EqualTo(LegacyConfigAction.RecordHash));
            Assert.That(LegacyConfigReimportPolicy.Decide(null, null, legacyHasAnyPayload: false), Is.EqualTo(LegacyConfigAction.RecordHash));
        });
    }

    [Test]
    public void An_unchanged_or_absent_cfg_is_left_alone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LegacyConfigReimportPolicy.Decide("abc", "abc", legacyHasAnyPayload: true), Is.EqualTo(LegacyConfigAction.Keep));
            Assert.That(LegacyConfigReimportPolicy.Decide("", null, legacyHasAnyPayload: false), Is.EqualTo(LegacyConfigAction.Keep));
            Assert.That(LegacyConfigReimportPolicy.Decide("abc", null, legacyHasAnyPayload: false), Is.EqualTo(LegacyConfigAction.RecordHash), "A deleted .cfg is recorded as absent.");
        });
    }

    [Test]
    public void A_changed_cfg_with_slot_data_wins_but_an_emptied_cfg_only_records()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LegacyConfigReimportPolicy.Decide("abc", "def", legacyHasAnyPayload: true), Is.EqualTo(LegacyConfigAction.Reimport));
            Assert.That(LegacyConfigReimportPolicy.Decide("", "def", legacyHasAnyPayload: true), Is.EqualTo(LegacyConfigAction.Reimport), "A .cfg that appeared after the store was created carries rollback edits.");
            Assert.That(LegacyConfigReimportPolicy.Decide("abc", "def", legacyHasAnyPayload: false), Is.EqualTo(LegacyConfigAction.RecordHash), "An emptied .cfg holds no creative work to win with.");
        });
    }

    // Advisor at the Stage A gate (2026-10-19): the selected slot is NOT part of the change hash. Clicking a different
    // slot under a 0.2.5 rollback rewrites only SelectedExtendedSlot; treating that as "changed" would replace a
    // player's round-4 slot edits with the pre-round-4 slots. Names and payloads are the creative work.
    [Test]
    public void Content_hash_follows_names_and_payloads_not_selection_or_file_bytes()
    {
        string a = LegacyConfigReimportPolicy.ContentHash(Names, Payloads);
        string again = LegacyConfigReimportPolicy.ContentHash(Names, Payloads);
        string otherName = LegacyConfigReimportPolicy.ContentHash(new[] { "Cozy!", "", "", "", "", "" }, Payloads);
        string otherPayload = LegacyConfigReimportPolicy.ContentHash(Names, new[] { "BSAS1;0;1:2:3;4:5:7", "", "", "", "", "" });
        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(again));
            Assert.That(a, Has.Length.EqualTo(64), "SHA-256 hex.");
            Assert.That(typeof(LegacyConfigReimportPolicy).GetMethod("ContentHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetParameters(), Has.Length.EqualTo(2), "Selection is not an input.");
            Assert.That(a, Is.Not.EqualTo(otherName));
            Assert.That(a, Is.Not.EqualTo(otherPayload));
            Assert.That(LegacyConfigReimportPolicy.HasAnyPayload(Payloads), Is.True);
            Assert.That(LegacyConfigReimportPolicy.HasAnyPayload(Empty), Is.False);
            Assert.That(LegacyConfigReimportPolicy.HasAnyPayload(new[] { "   ", "", "", "", "", "" }), Is.False);
        });
    }
}
