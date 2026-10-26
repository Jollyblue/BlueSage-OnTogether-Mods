using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 WS3 / audit Must 1 (Blue, 2026-10-19): viewing a player's ID card / Steam profile is read-only and the
// game never refuses it. Players who rejoined through a reconnect mod are live game rows missing from Steam's
// lobby list, so our verified evidence is unavailable and we used to refuse. Now the clicked row opens exactly as
// vanilla would - the game's own list and the row itself must name that Steam ID at that index - and every
// wrong-row guard (index, row reference, transform, tuple stability) still runs. Moderation is unaffected.
[TestFixture]
public sealed class NativeIdCardReadOnlyFallbackTests
{
    private const string Sid = "76561198000000123";

    [Test]
    public void Verified_evidence_never_needs_the_fallback()
    {
        Assert.That(NativeIdCardReadOnlyFallbackPolicy.TryUseNativeRow(true, Sid, 4, Sid, Sid, out _), Is.False);
    }

    [Test]
    public void Unverified_row_opens_when_the_game_list_and_row_agree()
    {
        Assert.Multiple(() =>
        {
            Assert.That(NativeIdCardReadOnlyFallbackPolicy.TryUseNativeRow(false, Sid, 4, Sid, Sid, out int index), Is.True);
            Assert.That(index, Is.EqualTo(4));
        });
    }

    [TestCase(-1, Sid, Sid, TestName = "Row_not_in_the_list_stays_closed")]
    [TestCase(4, "76561198000000999", Sid, TestName = "List_names_someone_else_stays_closed")]
    [TestCase(4, Sid, "76561198000000999", TestName = "Row_names_someone_else_stays_closed")]
    [TestCase(4, "", "", TestName = "Empty_identity_stays_closed")]
    public void Disagreeing_or_missing_data_never_opens(int rowIndex, string listSteamId, string rowSteamId)
    {
        Assert.That(NativeIdCardReadOnlyFallbackPolicy.TryUseNativeRow(false, Sid, rowIndex, listSteamId, rowSteamId, out _), Is.False);
    }

    [Test]
    public void Controller_uses_the_fallback_and_keeps_the_tuple_guard()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string controller = File.ReadAllText(Path.Combine(root, "src", "NativeIdCardController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("NativeIdCardReadOnlyFallbackPolicy.TryUseNativeRow("));
            Assert.That(controller, Does.Contain("NativeIdCardInvocationGuard.Invoke("));
            Assert.That(controller, Does.Contain("IsCurrentTupleStable("));
        });
    }
}
