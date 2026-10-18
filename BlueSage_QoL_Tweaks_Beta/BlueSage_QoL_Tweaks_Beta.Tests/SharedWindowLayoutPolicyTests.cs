using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SharedWindowLayoutPolicyTests
{
    [TestCase(920f, 640f)]
    [TestCase(1024f, 576f)]
    [TestCase(1453f, 759f)]
    [TestCase(1920f, 1080f)]
    public void Public_window_pair_never_overlaps_or_leaves_the_visible_screen(float screenWidth, float screenHeight)
    {
        SharedWindowPairLayout pair = SharedWindowLayoutPolicy.CreatePair(screenWidth, screenHeight);

        Assert.Multiple(() =>
        {
            Assert.That(SharedWindowLayoutPolicy.Overlaps(pair.QolMenu, pair.StyleHelper), Is.False);
            Assert.That(SharedWindowLayoutPolicy.IsFullyVisible(pair.QolMenu, screenWidth, screenHeight), Is.True);
            Assert.That(SharedWindowLayoutPolicy.IsFullyVisible(pair.StyleHelper, screenWidth, screenHeight), Is.True);
        });
    }

    [TestCase(920f, 640f)]
    [TestCase(1024f, 576f)]
    [TestCase(1453f, 759f)]
    [TestCase(1920f, 1080f)]
    public void Shared_help_frames_keep_content_state_and_help_inside_each_window(float screenWidth, float screenHeight)
    {
        SharedWindowPairLayout pair = SharedWindowLayoutPolicy.CreatePair(screenWidth, screenHeight);
        AssertFrameFits(BlueSageHelpFrameKind.QolMenu, pair.QolMenu);
        AssertFrameFits(BlueSageHelpFrameKind.StyleHelper, pair.StyleHelper);
    }

    // UAT F3FAB760BEB5 (2026-10-17): long tooltips were cut off in the footer because the help line
    // was capped at 22px (one line) while the footer had ~60px of unused space below it. The help
    // line must use all the footer space left after padding, the state line, and action rows.
    [TestCase(false, 1453f, 759f)]
    [TestCase(false, 1920f, 1080f)]
    [TestCase(true, 1453f, 759f)]
    [TestCase(true, 1920f, 1080f)]
    public void Help_line_fills_the_remaining_footer_space_so_long_tooltips_are_not_cut_off(
        bool styleHelper, float windowWidth, float windowHeight)
    {
        BlueSageHelpFrameKind kind = styleHelper ? BlueSageHelpFrameKind.StyleHelper : BlueSageHelpFrameKind.QolMenu;
        HelpFrameLayout frame = HelpFramePolicy.Resolve(kind, windowWidth, windowHeight);
        float remaining = frame.FooterHeight - frame.FooterVerticalPadding - frame.StateLineHeight - (frame.ActionRows * frame.ActionRowHeight);

        Assert.Multiple(() =>
        {
            Assert.That(frame.HelpLineHeight, Is.EqualTo(remaining).Within(0.1f));
            Assert.That(frame.HelpLineHeight, Is.GreaterThanOrEqualTo(22f));
        });
    }

    [Test]
    public void QoL_menu_help_line_has_room_for_several_wrapped_lines()
    {
        HelpFrameLayout frame = HelpFramePolicy.Resolve(BlueSageHelpFrameKind.QolMenu, 1453f, 759f);
        Assert.That(frame.HelpLineHeight, Is.GreaterThanOrEqualTo(60f), "At least three 14px wrapped lines.");
    }

    private static void AssertFrameFits(BlueSageHelpFrameKind kind, WindowFitResult window)
    {
        HelpFrameLayout frame = HelpFramePolicy.Resolve(kind, window.Width, window.Height);

        Assert.Multiple(() =>
        {
            Assert.That(frame.ContentHeight, Is.GreaterThan(0f));
            Assert.That(frame.FooterHeight, Is.GreaterThan(0f));
            Assert.That(frame.StateLineHeight, Is.GreaterThan(0f));
            Assert.That(frame.HelpLineHeight, Is.GreaterThan(0f));
            Assert.That(frame.ContentHeight + frame.FooterHeight + frame.FixedChromeHeight,
                Is.LessThanOrEqualTo(window.Height + 0.1f));
            Assert.That(
                frame.StateLineHeight + frame.HelpLineHeight + frame.FooterVerticalPadding +
                (frame.ActionRows * frame.ActionRowHeight),
                Is.LessThanOrEqualTo(frame.FooterHeight + 0.1f));
        });
    }
}
