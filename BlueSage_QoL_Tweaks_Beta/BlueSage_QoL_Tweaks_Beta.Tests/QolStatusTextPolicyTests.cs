using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class QolStatusTextPolicyTests
{
    [TestCase(true, true, "Game cleanup: native timer detected; unchanged", "BlueSage cleanup: automatic cleanup on; manual controls ready")]
    [TestCase(true, false, "Game cleanup: native timer detected; unchanged", "BlueSage cleanup: automatic cleanup off; manual controls ready")]
    [TestCase(false, true, "Game cleanup: not detected", "BlueSage cleanup: automatic cleanup on; manual controls ready")]
    [TestCase(false, false, "Game cleanup: not detected", "BlueSage cleanup: automatic cleanup off; manual controls ready")]
    public void Player_summary_separates_game_and_bluesage_cleanup_ownership(
        bool nativeSweepDetected,
        bool blueSageSweepEnabled,
        string expectedGameText,
        string expectedBlueSageText)
    {
        string result = QolStatusTextPolicy.BuildPlayerSummary(nativeSweepDetected, blueSageSweepEnabled);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain(expectedGameText));
            Assert.That(result, Does.Contain(expectedBlueSageText));
            Assert.That(result, Does.Contain("Home or /qolmenu"));
            Assert.That(result.Length, Is.LessThanOrEqualTo(QolStatusTextPolicy.MaxPlayerCharacters));
        });
    }

    [Test]
    public void Player_summary_excludes_raw_configuration_and_internal_counters()
    {
        string result = QolStatusTextPolicy.BuildPlayerSummary(true, false);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Not.Contain("ChatOutline").And.Not.Contain("ChatRows"));
            Assert.That(result, Does.Not.Contain("hidden=").And.Not.Contain("max32"));
            Assert.That(result, Does.Not.Contain("sha256").IgnoreCase);
            Assert.That(result, Does.Not.Contain("=").And.Not.Contain("%"));
        });
    }

    [Test]
    public void Player_summary_preserves_incomplete_native_inspection_as_a_distinct_state()
    {
        string result = QolStatusTextPolicy.BuildPlayerSummary(
            NativeAssetSweepOwnershipState.InspectionIncomplete,
            blueSageSweepEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Game cleanup: inspection incomplete; native behavior unchanged"));
            Assert.That(result, Does.Contain("BlueSage cleanup: automatic cleanup on; manual controls ready"));
        });
    }

    [TestCase("Home", "Home or /qolmenu")]
    [TestCase("F7", "F7 or /qolmenu")]
    // 2026-10-17 (ultrareview #9): ("None", "None or /qolmenu") removed - it pinned the bug.
    // HotkeyLabelPolicyTests.Status_summary_says_only_the_command_when_the_menu_key_is_unbound replaces it.
    public void Player_summary_reports_the_actual_configured_menu_key(
        string configuredKeyLabel,
        string expectedSuffix)
    {
        string result = QolStatusTextPolicy.BuildPlayerSummary(
            NativeAssetSweepOwnershipState.NativeGame,
            blueSageAutomaticLoopReady: true,
            blueSageManualControlsReady: true,
            configuredKeyLabel);

        Assert.That(result, Does.Contain(expectedSuffix));
        Assert.That(result.Length, Is.LessThanOrEqualTo(QolStatusTextPolicy.MaxPlayerCharacters));
    }

    [TestCase(NativeAssetSweepOwnershipState.NativeGame, false, true, "automatic cleanup off; manual controls ready")]
    [TestCase(NativeAssetSweepOwnershipState.InspectionIncomplete, false, true, "automatic cleanup off; manual controls ready")]
    [TestCase(NativeAssetSweepOwnershipState.BlueSage, true, false, "automatic cleanup on; manual controls off")]
    public void Player_summary_separates_automatic_loop_and_manual_control_readiness(
        object ownershipValue,
        bool automaticLoopReady,
        bool manualControlsReady,
        string expectedCleanupState)
    {
        System.Reflection.MethodInfo? buildSummary = typeof(QolStatusTextPolicy).GetMethod(
            "BuildPlayerSummary",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(NativeAssetSweepOwnershipState), typeof(bool), typeof(bool) },
            modifiers: null);

        Assert.That(buildSummary, Is.Not.Null, "The player status policy must receive automatic-loop and manual-control readiness separately.");

        string result = (string)buildSummary!.Invoke(null, new object[]
        {
            (NativeAssetSweepOwnershipState)ownershipValue,
            automaticLoopReady,
            manualControlsReady
        })!;

        Assert.That(result, Does.Contain("BlueSage cleanup: " + expectedCleanupState));
    }
}
