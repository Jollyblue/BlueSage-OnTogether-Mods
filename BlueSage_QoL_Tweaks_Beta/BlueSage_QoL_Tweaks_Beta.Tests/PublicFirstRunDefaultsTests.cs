using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
public sealed class PublicFirstRunDefaultsTests
{
    [Test]
    public void Fresh_install_matches_the_public_menu_values_qualified_by_blue_on_2026_08_06()
    {
        PublicFirstRunDefaults defaults = PublicFirstRunDefaults.Current;

        Assert.Multiple(() =>
        {
            Assert.That(defaults.AutomaticCleanup, Is.True);
            Assert.That(defaults.ManualCleanupWithOverlappingSweepMod, Is.False);
            Assert.That(defaults.CommunitySafetyList, Is.True);
            Assert.That(defaults.PlayerRenderSaver, Is.False, "Retired 2026-10-17 (visual glitching).");
            // 2026-10-17 (Blue): his live UAT settings become everyone's fresh-install defaults:
            // Performance Overlay on at his position, Auto menu scale (0), 4000 chat characters.
            Assert.That(defaults.PerformanceOverlay, Is.True);
            Assert.That(defaults.PerformanceOverlayOffsetX, Is.EqualTo(45f));
            Assert.That(defaults.PerformanceOverlayOffsetY, Is.EqualTo(223f));
            Assert.That(defaults.MenuScalePercent, Is.EqualTo(0), "0 means Auto.");
            Assert.That(defaults.Theme, Is.EqualTo("Midnight Dock"));
            Assert.That(defaults.ChatTextLimit, Is.EqualTo(4000));
            Assert.That(defaults.ProfileTextLimit, Is.EqualTo(4000));
            Assert.That(defaults.RoomNameLimit, Is.EqualTo(4000));
        });
    }
}
