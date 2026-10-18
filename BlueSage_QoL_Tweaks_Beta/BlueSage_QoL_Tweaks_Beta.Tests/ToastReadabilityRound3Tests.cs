using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 3 (Blue, live UAT 2026-10-17): slower readers set how long a toast stays readable, and toasts
// scale with the screen (Auto) or a chosen size, like the menus do on laptops and big monitors.
public sealed class ToastReadabilityRound3Tests
{
    [Test]
    public void Reading_time_is_configurable_and_clamped_with_todays_default()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToastPolicy.DefaultHoldSeconds, Is.EqualTo(3.5f));
            Assert.That(ToastPolicy.ClampHoldSeconds(0f), Is.EqualTo(ToastPolicy.MinHoldSeconds));
            Assert.That(ToastPolicy.ClampHoldSeconds(99f), Is.EqualTo(ToastPolicy.MaxHoldSeconds));
            Assert.That(ToastPolicy.MinHoldSeconds, Is.EqualTo(2f));
            Assert.That(ToastPolicy.MaxHoldSeconds, Is.EqualTo(15f));
        });
    }

    [Test]
    public void A_longer_reading_time_keeps_the_toast_fully_visible_longer()
    {
        float holdTen = 10f;
        Assert.Multiple(() =>
        {
            Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.FadeInSeconds + 8f, holdTen), Is.EqualTo(1f));
            Assert.That(ToastPolicy.IsExpired(ToastPolicy.FadeInSeconds + 8f, holdTen), Is.False);
            Assert.That(ToastPolicy.IsExpired(ToastPolicy.FadeInSeconds + holdTen + ToastPolicy.FadeOutSeconds, holdTen), Is.True);
            Assert.That(ToastPolicy.ComputeAlpha(ToastPolicy.FadeInSeconds + 8f), Is.EqualTo(0f), "The default 3.5 s is unchanged.");
        });
    }

    [TestCase(0, 768f, 1f, TestName = "Auto_laptop_never_shrinks")]
    [TestCase(0, 1080f, 1f, TestName = "Auto_1080p_is_baseline")]
    [TestCase(0, 1440f, 1.3333f, TestName = "Auto_1440p_grows")]
    [TestCase(0, 2160f, 2f, TestName = "Auto_4K_doubles")]
    [TestCase(0, 4320f, 2f, TestName = "Auto_caps_at_double")]
    [TestCase(150, 768f, 1.5f, TestName = "Manual_percent_wins")]
    [TestCase(50, 1080f, 0.75f, TestName = "Manual_floor_75")]
    [TestCase(400, 1080f, 2f, TestName = "Manual_ceiling_200")]
    public void Toast_size_follows_the_screen_or_the_chosen_percent(int configuredPercent, float screenHeight, float expected)
    {
        Assert.That(ToastPolicy.ResolveScale(configuredPercent, screenHeight), Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Scaled_toasts_still_fit_the_screen_with_a_gutter()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToastPolicy.ComputeWidth(3840f, 2f), Is.EqualTo(ToastPolicy.MaxWidth * 2f));
            Assert.That(ToastPolicy.ComputeWidth(700f, 2f), Is.EqualTo(700f - (2f * ToastPolicy.ScreenGutter)), "A doubled 720px toast is capped to a 700px screen minus gutters.");
            Assert.That(ToastPolicy.ComputeWidth(1920f, 1f), Is.EqualTo(ToastPolicy.ComputeWidth(1920f)));
        });
    }

    [Test]
    public void Plugin_and_menu_wire_the_two_settings()
    {
        string plugin = Read("Plugin.cs");
        string menu = Read("QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ToastHoldSeconds = Config.Bind(\"Notifications\", \"ToastHoldSeconds\", ToastPolicy.DefaultHoldSeconds"));
            Assert.That(plugin, Does.Contain("ToastScalePercent = Config.Bind(\"Notifications\", \"ToastScalePercent\", 0"));
            Assert.That(plugin, Does.Contain("ToastPolicy.IsExpired(now - _toasts[i].ShownAt, LockedToastHoldSeconds)"));
            Assert.That(plugin, Does.Contain("ToastPolicy.ComputeAlpha(now - _toasts[i].ShownAt, LockedToastHoldSeconds)"));
            Assert.That(plugin, Does.Contain("ToastPolicy.ResolveScale(ToastScalePercent?.Value ?? 0, Screen.height)"));
            Assert.That(menu, Does.Contain("DrawTextSetting(\"Toast reading time (seconds)\""));
            Assert.That(menu, Does.Contain("DrawTextSetting(\"Toast size\""));
        });
    }

    private static string Read(string name)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", name)).Replace("\r\n", "\n");
    }
}
