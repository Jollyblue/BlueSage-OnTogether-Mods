using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Blue (2026-10-17): new fresh-install defaults (Auto menu scale, Performance Overlay on at X45/Y223)
// must be proven to scale, auto-scale, resize, and stay click-and-movable on common screens,
// including small high-DPI laptops.
public sealed class FreshDefaultsScalingProofTests
{
    private const float OverlayRowHeight = 28f;
    private const float DefaultOverlayWidth = 320f;

    // Physical resolution + the DPI Windows reports for that kind of screen.
    [TestCase(1920f, 1080f, 96f)]
    [TestCase(1920f, 1080f, 144f)]
    [TestCase(2560f, 1440f, 120f)]
    [TestCase(3840f, 2160f, 192f)]
    [TestCase(1366f, 768f, 96f)]
    [TestCase(1366f, 768f, 144f)]
    [TestCase(1280f, 720f, 120f)]
    [TestCase(1024f, 576f, 96f)]
    public void Auto_menu_scale_keeps_both_windows_fully_visible_and_apart(float width, float height, float dpi)
    {
        MenuDisplayScaleResult scale = MenuDisplayScalePolicy.Resolve(PublicFirstRunDefaults.Current.MenuScalePercent, dpi);
        float logicalWidth = scale.ToLogicalPixels(width);
        float logicalHeight = scale.ToLogicalPixels(height);
        SharedWindowPairLayout pair = SharedWindowLayoutPolicy.CreatePair(logicalWidth, logicalHeight);

        Assert.Multiple(() =>
        {
            Assert.That(scale.Percent, Is.InRange(100, MenuDisplayScalePolicy.MaximumScalePercent));
            Assert.That(SharedWindowLayoutPolicy.IsFullyVisible(pair.QolMenu, logicalWidth, logicalHeight), Is.True);
            Assert.That(SharedWindowLayoutPolicy.IsFullyVisible(pair.StyleHelper, logicalWidth, logicalHeight), Is.True);
            Assert.That(SharedWindowLayoutPolicy.Overlaps(pair.QolMenu, pair.StyleHelper), Is.False);
        });
    }

    [TestCase(1920f, 1080f)]
    [TestCase(1366f, 768f)]
    [TestCase(1280f, 720f)]
    [TestCase(1024f, 576f)]
    [TestCase(800f, 600f)]
    [TestCase(300f, 200f)]
    public void Default_overlay_position_is_fully_on_screen_at_any_resolution(float width, float height)
    {
        PublicFirstRunDefaults defaults = PublicFirstRunDefaults.Current;
        WorkRelaxOverlayLayout layout = WorkRelaxOverlayLayoutPolicy.ResolveForViewport(DefaultOverlayWidth, width, OverlayRowHeight);
        PerformanceOverlayPosition position = PerformanceOverlayPositionPolicy.ClampToScreen(
            defaults.PerformanceOverlayOffsetX, defaults.PerformanceOverlayOffsetY, layout.Width, layout.Height, width, height);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Width, Is.LessThanOrEqualTo(width), "Overlay shrinks to fit narrow screens.");
            Assert.That(position.OffsetX, Is.GreaterThanOrEqualTo(0d));
            Assert.That(position.OffsetY, Is.GreaterThanOrEqualTo(0d));
            Assert.That(position.OffsetX + layout.Width, Is.LessThanOrEqualTo(width + 0.01d));
            Assert.That(position.OffsetY + layout.Height, Is.LessThanOrEqualTo(height + 0.01d));
        });
    }

    [Test]
    public void Default_overlay_position_is_used_as_is_on_a_normal_screen()
    {
        WorkRelaxOverlayLayout layout = WorkRelaxOverlayLayoutPolicy.ResolveForViewport(DefaultOverlayWidth, 1920f, OverlayRowHeight);
        PerformanceOverlayPosition position = PerformanceOverlayPositionPolicy.ClampToScreen(45d, 223d, layout.Width, layout.Height, 1920d, 1080d);

        Assert.Multiple(() =>
        {
            Assert.That(position.OffsetX, Is.EqualTo(45d));
            Assert.That(position.OffsetY, Is.EqualTo(223d));
        });
    }

    [Test]
    public void A_dragged_overlay_dropped_off_screen_snaps_back_inside()
    {
        PerformanceOverlayPosition position = PerformanceOverlayPositionPolicy.ClampToScreen(5000d, -300d, 320d, 28d, 1366d, 768d);

        Assert.Multiple(() =>
        {
            Assert.That(position.OffsetX, Is.EqualTo(1366d - 320d));
            Assert.That(position.OffsetY, Is.EqualTo(0d));
        });
    }

    [Test]
    public void Overlay_runtime_fallbacks_match_the_fresh_install_defaults()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string controller = File.ReadAllText(Path.Combine(root, "src", "PerformanceOverlayController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("Plugin.PerformanceOverlayOffsetX?.Value ?? PublicFirstRunDefaults.Current.PerformanceOverlayOffsetX"));
            Assert.That(controller, Does.Contain("Plugin.PerformanceOverlayOffsetY?.Value ?? PublicFirstRunDefaults.Current.PerformanceOverlayOffsetY"));
            Assert.That(controller, Does.Contain("PerformanceOverlayPositionPolicy.ClampToScreen("), "Saved positions are re-clamped every frame.");
            Assert.That(controller, Does.Contain("EventType.MouseDrag"), "The overlay stays click-and-move.");
        });
    }
}
