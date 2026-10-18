using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Fly-mode comfort (Blue, 2026-10-17): finer ALT precision, rotate with a rebindable mouse button
// (middle by default), and a wider zoom range. Vanilla limits come from GameSettings
// (ZoomMin 0.5, ZoomMax 1.5, ZoomSpeed 0.2) and are restored when fly ends.
public sealed class FlyCameraPolicyTests
{
    [TestCase(15, 0.15f)]
    [TestCase(1, 0.05f)]
    [TestCase(90, 0.5f)]
    public void Alt_precision_is_finer_by_default_and_clamped_to_a_safe_range(int percent, float expected)
    {
        Assert.That(FlyCameraPolicy.PrecisionMultiplier(percent), Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void Default_alt_precision_is_finer_than_the_old_fixed_slow_down()
    {
        Assert.That(FlyCameraPolicy.PrecisionMultiplier(FlyCameraPolicy.DefaultPrecisionPercent), Is.LessThan(0.35f));
    }

    [Test]
    public void Fly_zoom_reaches_well_past_both_vanilla_limits()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlyCameraPolicy.MinFlyZoom(0.5f), Is.LessThan(0.5f));
            Assert.That(FlyCameraPolicy.MaxFlyZoom(1.5f), Is.GreaterThanOrEqualTo(6f));
        });
    }

    [Test]
    public void Fly_zoom_scrolls_out_and_in_within_the_wider_range()
    {
        float outward = FlyCameraPolicy.ComputeFlyZoom(1.5f, scrollY: -1f, zoomSpeed: 0.2f, vanillaMin: 0.5f, vanillaMax: 1.5f, shiftHeld: false);
        float farthest = FlyCameraPolicy.ComputeFlyZoom(99f, -1f, 0.2f, 0.5f, 1.5f, false);
        float nearest = FlyCameraPolicy.ComputeFlyZoom(0f, 1f, 0.2f, 0.5f, 1.5f, false);

        Assert.Multiple(() =>
        {
            Assert.That(outward, Is.GreaterThan(1.5f), "Scrolling out keeps going past the vanilla limit.");
            Assert.That(farthest, Is.EqualTo(FlyCameraPolicy.MaxFlyZoom(1.5f)).Within(0.0001f));
            Assert.That(nearest, Is.EqualTo(FlyCameraPolicy.MinFlyZoom(0.5f)).Within(0.0001f));
        });
    }

    [Test]
    public void Far_zoom_steps_scale_up_so_the_wide_range_does_not_take_dozens_of_notches()
    {
        float nearStep = FlyCameraPolicy.ComputeFlyZoom(1.5f, -1f, 0.2f, 0.5f, 1.5f, false) - 1.5f;
        float farStep = FlyCameraPolicy.ComputeFlyZoom(4.5f, -1f, 0.2f, 0.5f, 1.5f, false) - 4.5f;

        Assert.That(farStep, Is.GreaterThan(nearStep));
    }

    [Test]
    public void Shift_scroll_does_not_change_fly_zoom_like_vanilla_ignores_shift_scroll()
    {
        Assert.That(FlyCameraPolicy.ComputeFlyZoom(2f, -1f, 0.2f, 0.5f, 1.5f, shiftHeld: true), Is.EqualTo(2f));
    }

    [TestCase(4f, 1.5f)]
    [TestCase(0.1f, 0.5f)]
    [TestCase(1f, 1f)]
    public void Leaving_fly_snaps_zoom_back_into_vanilla_limits(float current, float expected)
    {
        Assert.That(FlyCameraPolicy.ClampToVanilla(current, 0.5f, 1.5f), Is.EqualTo(expected));
    }

    [Test]
    public void Rotate_button_cycles_through_mouse_buttons_with_friendly_names()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FlyCameraPolicy.DefaultRotateButton, Is.EqualTo("Mouse2"));
            Assert.That(FlyCameraPolicy.FriendlyRotateButtonName("Mouse2"), Is.EqualTo("Middle Mouse"));
            Assert.That(FlyCameraPolicy.NextRotateButton("Mouse2"), Is.EqualTo("Mouse3"));
            Assert.That(FlyCameraPolicy.NextRotateButton("Mouse1"), Is.EqualTo("Mouse2"));
            Assert.That(FlyCameraPolicy.NextRotateButton("Banana"), Is.EqualTo("Mouse2"));
        });
    }

    [TestCase("Mouse3", "Mouse3")]
    [TestCase("Mouse1", "Mouse1")]
    [TestCase("Space", "Mouse2")]
    [TestCase(null, "Mouse2")]
    public void Only_offered_mouse_buttons_are_accepted_as_the_rotate_button(string? button, string expected)
    {
        Assert.That(FlyCameraPolicy.NormalizeRotateButton(button), Is.EqualTo(expected));
    }

    [TestCase(true, true, "Mouse2", true)]
    [TestCase(false, true, "Mouse2", false)]
    [TestCase(true, false, "Mouse2", false)]
    [TestCase(true, true, "Mouse1", false)]
    public void Fly_rotation_only_drives_the_camera_for_a_held_non_right_button(bool flying, bool held, string button, bool expected)
    {
        // Right mouse already rotates the camera in vanilla; doubling it would rotate twice as fast.
        Assert.That(FlyCameraPolicy.ShouldDriveRotation(flying, held, button), Is.EqualTo(expected));
    }
}
