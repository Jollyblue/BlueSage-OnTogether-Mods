using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class MenuDisplayScalePolicyTests
{
    [TestCase(96f, 100)]
    [TestCase(120f, 125)]
    [TestCase(144f, 150)]
    [TestCase(192f, 200)]
    public void Auto_scale_maps_reported_dpi_to_physical_readability(float reportedDpi, int expectedPercent)
    {
        MenuDisplayScaleResult result = MenuDisplayScalePolicy.Resolve(
            configuredPercent: MenuDisplayScalePolicy.AutoScalePercent,
            reportedDpi: reportedDpi);

        Assert.That(result.Percent, Is.EqualTo(expectedPercent));
        Assert.That(result.Source, Is.EqualTo(MenuDisplayScaleSource.ReportedDpi));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    public void Auto_scale_falls_back_to_readable_100_percent_when_dpi_is_unavailable(float reportedDpi)
    {
        MenuDisplayScaleResult result = MenuDisplayScalePolicy.Resolve(
            configuredPercent: MenuDisplayScalePolicy.AutoScalePercent,
            reportedDpi: reportedDpi);

        Assert.That(result.Percent, Is.EqualTo(100));
        Assert.That(result.Source, Is.EqualTo(MenuDisplayScaleSource.Fallback));
    }

    [TestCase(50, 75)]
    [TestCase(125, 125)]
    [TestCase(250, 200)]
    public void Manual_scale_is_bounded_and_wins_over_reported_dpi(int configuredPercent, int expectedPercent)
    {
        MenuDisplayScaleResult result = MenuDisplayScalePolicy.Resolve(configuredPercent, reportedDpi: 192f);

        Assert.That(result.Percent, Is.EqualTo(expectedPercent));
        Assert.That(result.Source, Is.EqualTo(MenuDisplayScaleSource.Manual));
    }

    [Test]
    public void High_dpi_scale_exposes_logical_viewport_for_uniform_imgui_scaling()
    {
        MenuDisplayScaleResult result = MenuDisplayScalePolicy.Resolve(
            configuredPercent: MenuDisplayScalePolicy.AutoScalePercent,
            reportedDpi: 192f);

        Assert.That(result.Scale, Is.EqualTo(2f));
        Assert.That(result.ToLogicalPixels(2560f), Is.EqualTo(1280f));
        Assert.That(result.ToLogicalPixels(1600f), Is.EqualTo(800f));
    }

    [Test]
    public void Comfort_defaults_match_Blues_reference_and_keep_style_helper_taller()
    {
        MenuComfortSize menu = MenuDisplayScalePolicy.GetComfortSize(BlueSageMenuSurface.QolMenu);
        MenuComfortSize helper = MenuDisplayScalePolicy.GetComfortSize(BlueSageMenuSurface.StyleHelper);

        Assert.Multiple(() =>
        {
            Assert.That(menu.Width, Is.EqualTo(1390f));
            Assert.That(menu.Height, Is.EqualTo(1210f));
            Assert.That(helper.Width, Is.EqualTo(1220f));
            Assert.That(helper.Height, Is.EqualTo(1310f));
        });
    }
}
