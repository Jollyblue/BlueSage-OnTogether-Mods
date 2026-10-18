using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxOverlayLayoutTests
{
    [Test]
    public void Resolve_DefaultWidthPrioritizesReadableMetricsAndWrapsSelectorBelow()
    {
        WorkRelaxOverlayLayout layout = WorkRelaxOverlayLayoutPolicy.Resolve(320f, 28f);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Wrapped, Is.True);
            AssertRect(layout.Metrics, 0f, 28f, 296f, 28f);
            AssertRect(layout.Work, 0f, 56f, 148f, 28f);
            AssertRect(layout.Relax, 148f, 56f, 148f, 28f);
            AssertRect(layout.Close, 296f, 28f, 24f, 28f);
            Assert.That(layout.Height, Is.EqualTo(84f));
            Assert.That(Intersects(layout.Work, layout.Close), Is.False);
            Assert.That(Intersects(layout.Relax, layout.Close), Is.False);
        });
    }

    [Test]
    public void Resolve_NarrowStripWrapsSelectorBelowMetricsWithoutOverlapOrNegativeWidths()
    {
        WorkRelaxOverlayLayout layout = WorkRelaxOverlayLayoutPolicy.Resolve(180f, 28f);

        Assert.Multiple(() =>
        {
            Assert.That(layout.Wrapped, Is.True);
            AssertRect(layout.Metrics, 0f, 28f, 156f, 28f);
            AssertRect(layout.Close, 156f, 28f, 24f, 28f);
            AssertRect(layout.Work, 0f, 56f, 78f, 28f);
            AssertRect(layout.Relax, 78f, 56f, 78f, 28f);
            Assert.That(layout.Height, Is.EqualTo(84f));
            Assert.That(layout.Metrics.Width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.Work.Width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.Relax.Width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(Intersects(layout.Work, layout.Close), Is.False);
            Assert.That(Intersects(layout.Relax, layout.Close), Is.False);
        });
    }

    [Test]
    public void ResolveForViewport_NormalizedDefaultWidthShrinksAndWrapsOnNarrowScreen()
    {
        PerformanceOverlaySettings settings = PerformanceOverlaySettingsPolicy.Normalize(
            0.25d, 13, 12d, 174d, 100d);

        WorkRelaxOverlayLayout layout = WorkRelaxOverlayLayoutPolicy.ResolveForViewport(
            (float)settings.Width,
            240f,
            28f);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Width, Is.EqualTo(320d), "exercise the real normalized minimum width");
            Assert.That(layout.Width, Is.EqualTo(240f));
            Assert.That(layout.Wrapped, Is.True);
            AssertRect(layout.Metrics, 0f, 28f, 216f, 28f);
            AssertRect(layout.Work, 0f, 56f, 108f, 28f);
            AssertRect(layout.Relax, 108f, 56f, 108f, 28f);
            AssertRect(layout.Close, 216f, 28f, 24f, 28f);
            Assert.That(layout.Close.Right, Is.LessThanOrEqualTo(240f));
            Assert.That(Intersects(layout.Work, layout.Close), Is.False);
            Assert.That(Intersects(layout.Relax, layout.Close), Is.False);
        });
    }

    // Round 3b (Blue, 2026-10-18): QoL-Menu and Styler-Menu buttons sit in their own row ABOVE the
    // FPS / PING / RAM bar, apart from WORK / RELAX, for players who prefer a click over a hotkey.
    [TestCase(320f)]
    [TestCase(180f)]
    [TestCase(600f)]
    public void Menu_buttons_get_their_own_row_above_the_stats_at_every_width(float width)
    {
        WorkRelaxOverlayLayout layout = WorkRelaxOverlayLayoutPolicy.Resolve(width, 28f);
        WorkRelaxOverlayRect[] others = { layout.Metrics, layout.Work, layout.Relax, layout.Close };

        Assert.Multiple(() =>
        {
            // Blue, UAT 2026-10-18: the menu buttons share WORK / RELAX's columns exactly and never
            // reach into the close button's column.
            if (layout.Wrapped)
            {
                Assert.That(layout.QolMenu.X, Is.EqualTo(layout.Work.X));
                Assert.That(layout.QolMenu.Width, Is.EqualTo(layout.Work.Width));
                Assert.That(layout.StyleMenu.X, Is.EqualTo(layout.Relax.X));
                Assert.That(layout.StyleMenu.Width, Is.EqualTo(layout.Relax.Width));
            }
            else
            {
                // Inline WORK / RELAX are only 54 px each; the menu buttons split the content width instead.
                Assert.That(layout.QolMenu.X, Is.EqualTo(0f));
                Assert.That(layout.StyleMenu.Right, Is.EqualTo(layout.Close.X));
            }
            Assert.That(layout.StyleMenu.Right, Is.LessThanOrEqualTo(layout.Close.X));
            Assert.That(layout.QolMenu.Y, Is.EqualTo(0f));
            Assert.That(layout.QolMenu.Height, Is.EqualTo(28f));
            Assert.That(layout.QolMenu.Bottom, Is.LessThanOrEqualTo(layout.Metrics.Y), "Above the stat bar.");
            Assert.That(layout.StyleMenu.Right, Is.LessThanOrEqualTo(layout.Width));
            Assert.That(layout.Height, Is.EqualTo(layout.Wrapped ? 84f : 56f));
            foreach (WorkRelaxOverlayRect other in others)
            {
                Assert.That(Intersects(layout.QolMenu, other), Is.False);
                Assert.That(Intersects(layout.StyleMenu, other), Is.False);
            }
            Assert.That(WorkRelaxOverlayLayoutPolicy.QolMenuLabel, Is.EqualTo("QoL-Menu"));
            Assert.That(WorkRelaxOverlayLayoutPolicy.StyleMenuLabel, Is.EqualTo("Styler-Menu"));
        });
    }

    private static void AssertRect(WorkRelaxOverlayRect actual, float x, float y, float width, float height)
    {
        Assert.That(actual.X, Is.EqualTo(x));
        Assert.That(actual.Y, Is.EqualTo(y));
        Assert.That(actual.Width, Is.EqualTo(width));
        Assert.That(actual.Height, Is.EqualTo(height));
    }

    private static bool Intersects(WorkRelaxOverlayRect left, WorkRelaxOverlayRect right) =>
        left.X < right.Right && left.Right > right.X &&
        left.Y < right.Bottom && left.Bottom > right.Y;
}
