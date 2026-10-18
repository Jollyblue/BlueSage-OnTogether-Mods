using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PerformanceOverlayOwnershipPolicyTests
{
    [Test]
    public void Started_enabled_overlay_is_active_only_outside_observation_only_mode()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                PerformanceOverlayOwnershipPolicy.IsActive(
                    pluginReady: true,
                    pluginEnabled: true,
                    observationOnly: false,
                    controllerStarted: true,
                    overlayEnabled: true),
                Is.True);
            Assert.That(
                PerformanceOverlayOwnershipPolicy.IsActive(
                    pluginReady: true,
                    pluginEnabled: true,
                    observationOnly: true,
                    controllerStarted: true,
                    overlayEnabled: true),
                Is.False,
                "QoL observation-only does not create or own the public overlay.");
        });
    }

    [TestCase(false, true, false, true, true)]
    [TestCase(true, false, false, true, true)]
    [TestCase(true, true, false, false, true)]
    [TestCase(true, true, false, true, false)]
    public void Incomplete_overlay_runtime_never_claims_ownership(
        bool pluginReady,
        bool pluginEnabled,
        bool observationOnly,
        bool controllerStarted,
        bool overlayEnabled)
    {
        Assert.That(
            PerformanceOverlayOwnershipPolicy.IsActive(
                pluginReady,
                pluginEnabled,
                observationOnly,
                controllerStarted,
                overlayEnabled),
            Is.False);
    }
}
