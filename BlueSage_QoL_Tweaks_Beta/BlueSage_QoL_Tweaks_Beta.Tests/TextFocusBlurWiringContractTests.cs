using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Wiring proofs for ultrareview #2-1 (2026-10-17). Real-game focus behavior is a UAT check:
// type in a field, press Save, then Home must still close the menu.
public sealed class TextFocusBlurWiringContractTests
{
    [TestCase("QolMenuWindow.cs")]
    [TestCase("StyleHelperWindow.cs")]
    [TestCase("BlueSageReportWindow.cs")]
    public void Every_window_blurs_text_focus_on_a_click_before_drawing_its_controls(string windowFile)
    {
        string source = ReadSource(windowFile);
        string draw = Slice(source, "private void DrawWindow(int id)", "\n    }\n");
        int blur = draw.IndexOf("ImGuiTextFocusTracker.BlurOnClickInsideWindow();", System.StringComparison.Ordinal);
        int firstControl = IndexOfFirst(draw, "GUILayout.", "GUI.DrawTexture(", "DrawHeader(");

        Assert.Multiple(() =>
        {
            Assert.That(blur, Is.GreaterThan(0), "The window must blur on click.");
            Assert.That(firstControl, Is.GreaterThan(blur), "Blur must run before any control sees the click, so a clicked text field can re-take focus.");
        });
    }

    // Advisor-caught (2026-10-17): window rects are registered in PHYSICAL pixels, before
    // MenuDisplayScaleRuntime.Apply; Event.mousePosition inside the scaled GUI matrix is in LOGICAL
    // pixels. The outside-click decision must therefore run before Apply, or at any menu scale
    // other than 100% it compares two different coordinate spaces.
    [TestCase("QolMenuWindow.cs")]
    [TestCase("StyleHelperWindow.cs")]
    [TestCase("BlueSageReportWindow.cs")]
    public void Outside_click_blur_runs_before_the_menu_scale_matrix_is_applied(string windowFile)
    {
        string source = ReadSource(windowFile);
        int blurOutside = source.IndexOf("ImGuiTextFocusTracker.BlurOnClickOutsideWindows();", System.StringComparison.Ordinal);
        int apply = source.IndexOf("MenuDisplayScaleRuntime.Apply(out Matrix4x4 previousMatrix);", System.StringComparison.Ordinal);
        int register = source.IndexOf("BlueSageWindowHoverScope.RegisterWindow(", System.StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(blurOutside, Is.GreaterThan(register), "Blur after this window registered its physical rect.");
            Assert.That(apply, Is.GreaterThan(blurOutside), "Blur before the logical-pixel GUI matrix is applied.");
        });
    }

    [Test]
    public void Tracker_blurs_clicks_outside_every_BlueSage_window_and_uses_the_policy()
    {
        string tracker = ReadSource("ImGuiTextFocusTracker.cs");
        string hover = ReadSource("BlueSageWindowHoverScope.cs");

        Assert.Multiple(() =>
        {
            Assert.That(tracker, Does.Contain("TextFocusBlurPolicy.ShouldBlurInsideWindow("));
            Assert.That(tracker, Does.Contain("TextFocusBlurPolicy.ShouldBlurOutsideWindows("));
            Assert.That(tracker, Does.Contain("BlueSageWindowHoverScope.IsPointOverAnyWindow("));
            Assert.That(hover, Does.Contain("internal static bool IsPointOverAnyWindow(Vector2 point)"));
        });
    }

    [Test]
    public void Rebind_capture_expiry_passes_the_request_time_for_the_hard_cap()
    {
        string plugin = ReadSource("Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("HotkeyInputGatePolicy.IsRebindCaptureExpired(_pendingRebindActiveSince, _pendingRebindRequestedAt, now)"));
            Assert.That(plugin, Does.Contain("_pendingRebindRequestedAt = Time.unscaledTime;"));
        });
    }

    private static int IndexOfFirst(string text, params string[] needles)
    {
        int best = int.MaxValue;
        foreach (string needle in needles)
        {
            int i = text.IndexOf(needle, System.StringComparison.Ordinal);
            if (i >= 0 && i < best) best = i;
        }

        return best == int.MaxValue ? -1 : best;
    }

    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
