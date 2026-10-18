using System;
using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 3 (Blue, live UAT 2026-10-17): his tested sweet spot becomes the default, the water search
// covers all directions within the rod's real range, a catch is released after a short delay instead of
// waiting out the game's hold, and the fishing settings live in a purple Accessibility section.
public sealed class FishingAidRound3Tests
{
    [Test]
    public void Blues_sweet_spot_is_the_default_for_new_installs()
    {
        Assert.Multiple(() =>
        {
            // Blue, UAT 2026-10-18: 0.7 s reaction and 3.5 s release are the values for every player.
            Assert.That(FishingAidPolicy.DefaultReactionSeconds, Is.EqualTo(0.7f));
            Assert.That(FishingAidPolicy.DefaultClicksPerSecond, Is.EqualTo(7f));
            Assert.That(FishingAidPolicy.DefaultReleaseSeconds, Is.EqualTo(3.5f));
        });
    }

    [Test]
    public void Release_delay_is_clamped_to_a_sensible_range()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.ClampReleaseSeconds(0f), Is.EqualTo(FishingAidPolicy.MinReleaseSeconds));
            Assert.That(FishingAidPolicy.ClampReleaseSeconds(99f), Is.EqualTo(FishingAidPolicy.MaxReleaseSeconds));
            Assert.That(FishingAidPolicy.ClampReleaseSeconds(2.5f), Is.EqualTo(2.5f));
            Assert.That(FishingAidPolicy.MinReleaseSeconds, Is.EqualTo(0.2f));
            Assert.That(FishingAidPolicy.MaxReleaseSeconds, Is.EqualTo(5f));
        });
    }

    [Test]
    public void A_shown_catch_is_released_once_after_the_delay()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FishingAidPolicy.ShouldAutoRelease(true, fishShowing: true, alreadyReleased: false, secondsShowing: 1.1f, releaseSeconds: 1f), Is.True);
            Assert.That(FishingAidPolicy.ShouldAutoRelease(true, true, false, 0.5f, 1f), Is.False, "Lets the player see the catch first.");
            Assert.That(FishingAidPolicy.ShouldAutoRelease(true, true, true, 3f, 1f), Is.False, "Only one release per catch.");
            Assert.That(FishingAidPolicy.ShouldAutoRelease(true, false, false, 3f, 1f), Is.False, "Nothing on show.");
            Assert.That(FishingAidPolicy.ShouldAutoRelease(false, true, false, 3f, 1f), Is.False, "Aid off: the game keeps its own pace.");
        });
    }

    [Test]
    public void Water_search_covers_every_direction_and_still_starts_straight_ahead()
    {
        var offsets = FishingAidPolicy.CandidateCastOffsets(0f, 1f, 10f, 1f);
        double[] headings = offsets.Select(o => Math.Atan2(o.X, o.Z) * 180d / Math.PI).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(offsets.Count, Is.EqualTo(FishingAidPolicy.MaxWaterChecks));
            Assert.That(offsets[0].X, Is.EqualTo(0f).Within(0.01f));
            Assert.That(offsets[0].Z, Is.EqualTo(6f).Within(0.01f), "Straight ahead at medium range first.");
            Assert.That(offsets.Any(o => o.Z < -5.9f && Math.Abs(o.X) < 0.01f), Is.True, "Directly behind the player is checked too.");
            for (int heading = -180; heading < 180; heading += 5)
            {
                double nearest = headings.Min(h => Math.Abs(((h - heading) % 360d + 540d) % 360d - 180d));
                Assert.That(nearest, Is.LessThanOrEqualTo(15.001d), $"No blind spot near {heading} degrees.");
            }
            foreach (var o in offsets)
            {
                double d = Math.Sqrt(o.X * o.X + o.Z * o.Z);
                Assert.That(d, Is.InRange(1f - 0.001f, 10f + 0.001f), "Stays within the rod's real cast range.");
            }
        });
    }

    [Test]
    public void Release_wiring_uses_the_games_own_release_button_after_the_policy_says_so()
    {
        string patches = ReadSource(Path.Combine("Patches", "FishingAidPatches.cs"));
        string plugin = ReadSource("Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patches, Does.Contain("FishingAidPolicy.ShouldAutoRelease("));
            Assert.That(patches, Does.Contain("manager.ButtonReleaseFish();"));
            Assert.That(plugin, Does.Contain("FishingAidReleaseSeconds = Config.Bind(\"Accessibility\", \"FishingAidReleaseSeconds\", FishingAidPolicy.DefaultReleaseSeconds"));
        });
    }

    [Test]
    public void Fishing_settings_live_in_a_purple_Accessibility_section_not_in_General()
    {
        string menu = ReadSource("QolMenuWindow.cs");
        string theme = ReadSource("BlueSageUiTheme.cs");
        string accessibility = Slice(menu, "private void DrawAccessibilityToggles()", "\n    }\n");
        string general = Slice(menu, "private void DrawGeneralSettings()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(theme, Does.Contain("public Color AccessibilityAccent"));
            Assert.That(menu, Does.Contain("FeatureSectionTone.Accessibility => _accessibilitySectionBoxStyle"));
            Assert.That(menu, Does.Contain("DrawAccessibilityToggles, availableWidth, FeatureSectionTone.Accessibility"));
            Assert.That(menu, Does.Not.Contain("DrawAccessibilityToggles, availableWidth, FeatureSectionTone.Comfort"));
            Assert.That(accessibility, Does.Contain("\"Fishing Aid bite reaction (seconds)\""));
            Assert.That(accessibility, Does.Contain("\"Fishing Aid last-round clicks per second\""));
            Assert.That(accessibility, Does.Contain("\"Fishing Aid auto release after catch (seconds)\""));
            Assert.That(general, Does.Not.Contain("Fishing Aid"));
        });
    }

    [Test]
    public void Customize_spreads_sections_across_two_columns_and_sizes_labels_to_the_column()
    {
        string menu = ReadSource("QolMenuWindow.cs");
        string customize = Slice(menu, "private void DrawCustomizeTab()", "private void DrawGeneralSettings()");
        string textSetting = Slice(menu, "private void DrawTextSetting(string label", "string previous = value");

        Assert.Multiple(() =>
        {
            Assert.That(customize, Does.Contain("bool useTwoColumns = _windowRect.width >= CustomizeTwoColumnMinWidth;"));
            Assert.That(menu, Does.Contain("private const float CustomizeTwoColumnMinWidth = 1100f;"));
            // Right column now carries Self movement as well as Chat & Readability.
            int right = customize.IndexOf("DrawSelfMovementSection(columnWidth)", customize.IndexOf("GUILayout.EndVertical();", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.That(right, Is.GreaterThan(0), "Self movement moved to the right column.");
            Assert.That(textSetting, Does.Contain("GUILayout.Width(_textSettingLabelWidth)"));
            Assert.That(textSetting, Does.Not.Contain("GUILayout.Width(170f)"));
            Assert.That(menu, Does.Contain("_textSettingLabelWidth = Mathf.Clamp(width * 0.42f, 170f, 340f);"));
        });
    }

    private static string ReadSource(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", relative)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
