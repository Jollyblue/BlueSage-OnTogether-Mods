using NUnit.Framework;
using BlueSage.QoLTweaks.Core;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Tests;

public sealed class BadgeTooltipLayoutPolicyTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Resolve_preserves_a_normal_three_line_measured_height()
    {
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            screenWidth: 1920f,
            screenHeight: 1080f,
            pointerX: 1600f,
            pointerY: 500f,
            requestedWidth: 340f,
            measuredHeight: 84f,
            scale: 1f);

        Assert.That(layout.Height, Is.EqualTo(84f));
    }

    [Test]
    public void Resolve_caps_the_screenshot_shaped_one_line_measurement()
    {
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            screenWidth: 1920f,
            screenHeight: 1080f,
            pointerX: 1600f,
            pointerY: 500f,
            requestedWidth: 340f,
            measuredHeight: 600f,
            scale: 1f);

        Assert.That(layout.Height, Is.EqualTo(240f).And.LessThan(300f));
    }

    [Test]
    public void ResolveWidth_bounds_a_narrow_screen_before_preserving_its_wrapped_measurement()
    {
        float boundedWidth = BadgeTooltipLayoutPolicy.ResolveWidth(
            screenWidth: 280f,
            requestedWidth: 340f);
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            screenWidth: 280f,
            screenHeight: 1080f,
            pointerX: 150f,
            pointerY: 500f,
            requestedWidth: boundedWidth,
            measuredHeight: 168f,
            scale: 1f);

        Assert.Multiple(() =>
        {
            Assert.That(boundedWidth, Is.EqualTo(264f));
            Assert.That(layout.Width, Is.EqualTo(264f));
            Assert.That(layout.Height, Is.EqualTo(168f));
        });
    }

    [Test]
    public void Marker_measures_height_at_the_screen_bounded_width()
    {
        string marker = File.ReadAllText(Path.Combine(
            Root,
            "src",
            "Patches",
            "BlueSagePlayerBadgeMarker.cs"));

        Assert.That(MarkerUsesBoundedMeasurementFlow(marker), Is.True);
    }

    [Test]
    public void Resolve_places_the_tooltip_left_of_the_trailing_badge_when_space_exists()
    {
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            screenWidth: 1920f,
            screenHeight: 1080f,
            pointerX: 1600f,
            pointerY: 500f,
            requestedWidth: 340f,
            measuredHeight: 84f,
            scale: 1f);

        Assert.That(layout.X, Is.EqualTo(1246f));
        Assert.That(layout.Y, Is.EqualTo(514f));
    }

    [Test]
    public void Resolve_flips_right_when_the_trailing_badge_is_at_the_left_edge()
    {
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            screenWidth: 1920f,
            screenHeight: 1080f,
            pointerX: 10f,
            pointerY: 500f,
            requestedWidth: 340f,
            measuredHeight: 84f,
            scale: 1f);

        Assert.That(layout.X, Is.EqualTo(24f));
    }

    [Test]
    public void Resolve_flips_above_when_the_trailing_badge_is_at_the_bottom_edge()
    {
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            screenWidth: 1920f,
            screenHeight: 1080f,
            pointerX: 1600f,
            pointerY: 1060f,
            requestedWidth: 340f,
            measuredHeight: 84f,
            scale: 1f);

        Assert.That(layout.Y, Is.EqualTo(962f));
    }

    [TestCase(2f, 2f)]
    [TestCase(1918f, 1078f)]
    public void Resolve_keeps_the_complete_tooltip_inside_the_eight_pixel_screen_margin(
        float pointerX,
        float pointerY)
    {
        const float screenWidth = 1920f;
        const float screenHeight = 1080f;
        BadgeTooltipLayout layout = BadgeTooltipLayoutPolicy.Resolve(
            screenWidth,
            screenHeight,
            pointerX,
            pointerY,
            requestedWidth: 340f,
            measuredHeight: 600f,
            scale: 1f);

        Assert.Multiple(() =>
        {
            Assert.That(layout.X, Is.GreaterThanOrEqualTo(8f));
            Assert.That(layout.Y, Is.GreaterThanOrEqualTo(8f));
            Assert.That(layout.X + layout.Width, Is.LessThanOrEqualTo(screenWidth - 8f));
            Assert.That(layout.Y + layout.Height, Is.LessThanOrEqualTo(screenHeight - 8f));
        });
    }

    private static bool MarkerUsesBoundedMeasurementFlow(string marker)
    {
        const string pattern = @"float\s+width\s*=\s*BadgeTooltipLayoutPolicy\.ResolveWidth\s*\(\s*Screen\.width\s*,\s*requestedWidth\s*\)\s*;\s*
            float\s+measuredHeight\s*=\s*style\.CalcHeight\s*\(\s*content\s*,\s*width\s*\)\s*;\s*
            float\s+pointerY\s*=\s*Screen\.height\s*-\s*Input\.mousePosition\.y\s*;\s*
            BadgeTooltipLayout\s+layout\s*=\s*BadgeTooltipLayoutPolicy\.Resolve\s*\(\s*
            Screen\.width\s*,\s*
            Screen\.height\s*,\s*
            Input\.mousePosition\.x\s*,\s*
            pointerY\s*,\s*
            width\s*,\s*
            measuredHeight\s*,\s*
            scale\s*\)\s*;";

        return Regex.IsMatch(marker, pattern, RegexOptions.IgnorePatternWhitespace);
    }
}
