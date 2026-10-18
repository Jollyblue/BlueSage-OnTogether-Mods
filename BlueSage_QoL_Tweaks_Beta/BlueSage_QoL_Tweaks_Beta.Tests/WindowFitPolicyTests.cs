using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WindowFitPolicyTests
{
    [Test]
    public void Report_resize_merges_callback_dimensions_after_gui_window_returns()
    {
        WindowFitResult result = ReportWindowResizePolicy.MergeGuiWindowResult(
            new WindowFitResult(110f, 90f, 1180f, 900f),
            hasRequestedSize: true,
            requestedWidth: 1255f,
            requestedHeight: 860f);

        Assert.That((result.X, result.Y, result.Width, result.Height),
            Is.EqualTo((110f, 90f, 1255f, 860f)));
    }

    [TestCase(920f, true)]
    [TestCase(1180f, false)]
    public void Report_allocation_editor_stacks_when_inline_controls_do_not_fit(
        float windowWidth,
        bool expected)
    {
        float contentWidth = Math.Max(1f, windowWidth - ReportWindowLayoutPolicy.HorizontalChromeWidth);
        Assert.That(ReportWindowLayoutPolicy.UseStackedAllocationLayout(contentWidth), Is.EqualTo(expected));
    }

    [Test]
    public void FitInitial_uses_Blues_reference_size_for_qol_on_large_displays()
    {
        WindowFitResult result = WindowFitPolicy.FitInitial(
            BlueSageMenuSurface.QolMenu,
            2560f,
            1440f);

        Assert.That(result.Width, Is.EqualTo(1390f));
        Assert.That(result.Height, Is.EqualTo(1210f));
        Assert.That(result.X, Is.EqualTo(585f));
        Assert.That(result.Y, Is.EqualTo(115f));
    }

    [Test]
    public void FitInitial_uses_Blues_taller_reference_size_for_style_helper()
    {
        WindowFitResult result = WindowFitPolicy.FitInitial(
            BlueSageMenuSurface.StyleHelper,
            2560f,
            1440f);

        Assert.That(result.Width, Is.EqualTo(1220f));
        Assert.That(result.Height, Is.EqualTo(1310f));
        Assert.That(result.X, Is.EqualTo(670f));
        Assert.That(result.Y, Is.EqualTo(65f));
    }

    [Test]
    public void FitInitial_uses_all_available_space_and_scrolls_on_small_displays()
    {
        WindowFitResult result = WindowFitPolicy.FitInitial(
            BlueSageMenuSurface.QolMenu,
            1024f,
            576f);

        Assert.That(result.Width, Is.EqualTo(1004f));
        Assert.That(result.Height, Is.EqualTo(556f));
        Assert.That(result.X, Is.EqualTo(10f));
        Assert.That(result.Y, Is.EqualTo(10f));
    }

    [Test]
    public void FitForOpen_uses_reviewed_default_only_on_first_open()
    {
        WindowFitResult firstOpen = WindowFitPolicy.FitForOpen(
            BlueSageMenuSurface.QolMenu,
            hasOpened: false,
            x: 40f,
            y: 50f,
            width: 960f,
            height: 640f,
            preferredMinWidth: 920f,
            preferredMinHeight: 640f,
            screenWidth: 1920f,
            screenHeight: 1080f);

        Assert.That(firstOpen.Width, Is.EqualTo(1390f));
        Assert.That(firstOpen.Height, Is.EqualTo(1060f));
    }

    [Test]
    public void FitForOpen_preserves_intentional_resize_and_clamps_out_of_bounds_position()
    {
        WindowFitResult reopened = WindowFitPolicy.FitForOpen(
            BlueSageMenuSurface.QolMenu,
            hasOpened: true,
            x: 1900f,
            y: 1000f,
            width: 980f,
            height: 660f,
            preferredMinWidth: 920f,
            preferredMinHeight: 640f,
            screenWidth: 1920f,
            screenHeight: 1080f);

        Assert.That(reopened.Width, Is.EqualTo(980f));
        Assert.That(reopened.Height, Is.EqualTo(660f));
        Assert.That(reopened.X + reopened.Width, Is.LessThanOrEqualTo(1910.1f));
        Assert.That(reopened.Y + reopened.Height, Is.LessThanOrEqualTo(1070.1f));
    }

    [Test]
    public void Two_hundred_percent_laptop_viewport_fits_to_logical_safe_area()
    {
        MenuDisplayScaleResult scale = MenuDisplayScalePolicy.Resolve(0, 192f);

        WindowFitResult result = WindowFitPolicy.FitInitial(
            BlueSageMenuSurface.QolMenu,
            scale.ToLogicalPixels(2560f),
            scale.ToLogicalPixels(1600f));

        Assert.That(result.X, Is.EqualTo(10f));
        Assert.That(result.Y, Is.EqualTo(10f));
        Assert.That(result.Width, Is.EqualTo(1260f));
        Assert.That(result.Height, Is.EqualTo(780f));
    }

    [Test]
    public void FitToScreen_clamps_dragged_windows_back_inside_screen_edges()
    {
        WindowFitResult result = WindowFitPolicy.FitToScreen(
            x: 3000f,
            y: 1800f,
            width: 960f,
            height: 720f,
            preferredMinWidth: 920f,
            preferredMinHeight: 640f,
            screenWidth: 1600f,
            screenHeight: 900f);

        Assert.That(result.X + result.Width, Is.LessThanOrEqualTo(1600f - WindowFitPolicy.ScreenMargin + 0.1f));
        Assert.That(result.Y + result.Height, Is.LessThanOrEqualTo(900f - WindowFitPolicy.ScreenMargin + 0.1f));
    }

    [Test]
    public void FitToScreen_preserves_a_manual_size_when_it_fits()
    {
        WindowFitResult result = WindowFitPolicy.FitToScreen(100f, 80f, 960f, 640f, 920f, 640f, 1600f, 900f);
        Assert.That(result.Width, Is.EqualTo(960f));
        Assert.That(result.Height, Is.EqualTo(640f));
    }
}
