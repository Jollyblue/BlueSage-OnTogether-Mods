using NUnit.Framework;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks.Tests;

public sealed class NativeAssetSweepPolicyTests
{
    [TestCase(false, false, false, false)]
    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(true, true, false, false)]
    [TestCase(true, true, true, true)]
    public void Confirmed_detection_requires_timer_duration_and_unload_call(
        bool mainSceneTimerPresent,
        bool settingsDurationPresent,
        bool unloadUnusedAssetsCallPresent,
        bool expected)
    {
        Assert.That(
            NativeAssetSweepPolicy.IsPresent(
                mainSceneTimerPresent,
                settingsDurationPresent,
                unloadUnusedAssetsCallPresent),
            Is.EqualTo(expected));
    }

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public void Partial_native_signature_stands_down_fail_closed(
        bool mainSceneTimerPresent,
        bool settingsDurationPresent,
        bool unloadUnusedAssetsCallPresent)
    {
        Assert.That(
            NativeAssetSweepPolicy.ShouldStandDown(
                mainSceneTimerPresent,
                settingsDurationPresent,
                unloadUnusedAssetsCallPresent,
                inspectionFailed: false),
            Is.True);
    }

    [Test]
    public void Uninspectable_runtime_stands_down_fail_closed()
    {
        Assert.That(
            NativeAssetSweepPolicy.ShouldStandDown(
                mainSceneTimerPresent: false,
                settingsDurationPresent: false,
                unloadUnusedAssetsCallPresent: false,
                inspectionFailed: true),
            Is.True);
    }

    [Test]
    public void Retail_without_any_native_signature_keeps_blue_sweep_owner()
    {
        Assert.That(
            NativeAssetSweepPolicy.ShouldStandDown(
                mainSceneTimerPresent: false,
                settingsDurationPresent: false,
                unloadUnusedAssetsCallPresent: false,
                inspectionFailed: false),
            Is.False);
    }

    [Test]
    public void Native_owner_does_not_block_explicitly_opted_in_blue_sage_cleanup()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                NativeAssetSweepPolicy.AllowBlueSageAutoSweep(
                    nativeSweepPresent: true,
                    compatibilityAllows: true,
                    savedEnabled: true),
                Is.True);
            Assert.That(
                NativeAssetSweepPolicy.AllowBlueSageManualSweep(
                    nativeSweepPresent: true,
                    compatibilityAllows: true),
                Is.True);
        });
    }

    [Test]
    public void Retail_without_native_owner_keeps_existing_compatibility_and_config_rules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                NativeAssetSweepPolicy.AllowBlueSageAutoSweep(
                    nativeSweepPresent: false,
                    compatibilityAllows: true,
                    savedEnabled: true),
                Is.True);
            Assert.That(
                NativeAssetSweepPolicy.AllowBlueSageAutoSweep(
                    nativeSweepPresent: false,
                    compatibilityAllows: true,
                    savedEnabled: false),
                Is.False);
            Assert.That(
                NativeAssetSweepPolicy.AllowBlueSageManualSweep(
                    nativeSweepPresent: false,
                    compatibilityAllows: false),
                Is.False);
        });
    }
}
