using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Wiring proofs for ultrareview findings #1 and #6 (2026-10-17). The decisions themselves are
// behavior-tested in HotkeyInputGatePolicyTests; these prove Update() and the windows actually
// feed and obey that policy. Real-game behavior is still a UAT check (net8 cannot load Unity).
public sealed class HotkeyTextFocusGuardContractTests
{
    [Test]
    public void Normal_hotkeys_are_gated_on_text_focus()
    {
        string plugin = ReadSource("Plugin.cs");
        string update = Slice(plugin, "private void Update()", "private void ToggleQolMenuFromHotkey()");
        string gateToMenuKey = Slice(update, "HotkeyInputGatePolicy.ShouldDispatchHotkeys(", "Input.GetKeyDown(QolMenuKey.Value)");

        Assert.Multiple(() =>
        {
            Assert.That(update, Does.Contain("bool textInputFocused = IsAnyTextInputFocused();"));
            Assert.That(update, Does.Contain("else if (HotkeyInputGatePolicy.ShouldDispatchHotkeys(textInputFocused))"));
            Assert.That(gateToMenuKey, Is.Not.Empty, "The text-focus gate must sit directly in front of the hotkey checks.");
        });
    }

    [Test]
    public void Rebind_capture_obeys_the_focus_gate_and_times_out()
    {
        string plugin = ReadSource("Plugin.cs");
        string captureBlock = Slice(plugin, "if (_pendingRebindEntry != null)", "else if (HotkeyInputGatePolicy.ShouldDispatchHotkeys(");

        Assert.Multiple(() =>
        {
            Assert.That(captureBlock, Does.Contain("HotkeyInputGatePolicy.AdvanceCaptureClock("));
            Assert.That(captureBlock, Does.Contain("HotkeyInputGatePolicy.IsRebindCaptureExpired("));
            Assert.That(captureBlock, Does.Contain("HotkeyInputGatePolicy.ShouldEvaluateRebindCapture(textInputFocused)"));
            Assert.That(captureBlock, Does.Contain("HotkeyInputGatePolicy.ShouldAnnounceRejection("));

            // Escape must be read only inside the focus gate, so pressing Esc to close chat
            // cannot cancel a pending rebind.
            int gate = captureBlock.IndexOf("ShouldEvaluateRebindCapture(textInputFocused)", System.StringComparison.Ordinal);
            int escape = captureBlock.IndexOf("KeyCode.Escape", System.StringComparison.Ordinal);
            Assert.That(escape, Is.GreaterThan(gate), "Escape-cancel must be inside the focus gate.");
        });
    }

    [Test]
    public void Text_focus_detection_covers_game_chat_game_menus_and_BlueSage_windows()
    {
        string plugin = ReadSource("Plugin.cs");
        string detector = Slice(plugin, "private static bool IsAnyTextInputFocused()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(detector, Does.Contain("ImGuiTextFocusTracker.IsEditing"), "BlueSage IMGUI text fields (Style Helper, QoL menu, Report).");
            Assert.That(detector, Does.Contain("MessageInput"), "The game's chat box.");
            Assert.That(detector, Does.Contain("currentSelectedGameObject"), "Any uGUI/TMP field the game's EventSystem has selected.");
            Assert.That(detector, Does.Contain("TMP_InputField"));
            Assert.That(detector, Does.Contain("InputField"));
        });
    }

    [TestCase("QolMenuWindow.cs")]
    [TestCase("StyleHelperWindow.cs")]
    [TestCase("BlueSageReportWindow.cs")]
    public void Every_BlueSage_window_with_text_fields_reports_editing_while_visible(string windowFile)
    {
        Assert.That(ReadSource(windowFile), Does.Contain("ImGuiTextFocusTracker.ReportWindowDrawn()"));
    }

    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int startIndex = source.IndexOf(start, System.StringComparison.Ordinal);
        if (startIndex < 0) return string.Empty;
        int endIndex = source.IndexOf(end, startIndex + start.Length, System.StringComparison.Ordinal);
        return endIndex < 0 ? string.Empty : source.Substring(startIndex, endIndex - startIndex);
    }
}
