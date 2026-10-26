using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 3b (Blue, 2026-10-18): the Performance Overlay gets QoL-Menu and Styler-Menu buttons above the
// stat bar. They open the same windows as the (rebindable) hotkeys and give the same toast feedback.
public sealed class OverlayMenuButtonsContractTests
{
    [Test]
    public void Overlay_draws_both_menu_buttons_from_the_layout_and_routes_them_to_the_plugin()
    {
        string controller = ReadSource("PerformanceOverlayController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("Rect qolMenuRect = ToScreenRect(layout.QolMenu);"));
            Assert.That(controller, Does.Contain("Rect styleMenuRect = ToScreenRect(layout.StyleMenu);"));
            Assert.That(controller, Does.Contain("GUI.Button(qolMenuRect, new GUIContent(WorkRelaxOverlayLayoutPolicy.QolMenuLabel"));
            Assert.That(controller, Does.Contain("GUI.Button(styleMenuRect, new GUIContent(WorkRelaxOverlayLayoutPolicy.StyleMenuLabel"));
            Assert.That(controller, Does.Contain("Plugin.Instance?.ToggleQolMenuFromOverlayButton();"));
            Assert.That(controller, Does.Contain("Plugin.Instance?.ToggleStyleUiFromOverlayButton();"));
            // Dragging still starts only on the stat bar, so a button click never moves the overlay.
            Assert.That(controller, Does.Contain("HandleDrag(settings, labelRect);"));
        });
    }

    [Test]
    public void Buttons_toggle_the_same_windows_as_the_hotkeys_with_the_same_feedback()
    {
        string plugin = ReadSource("Plugin.cs");
        string qol = Slice(plugin, "internal void ToggleQolMenuFromOverlayButton()", "\n    }\n");
        string hotkey = Slice(plugin, "private void ToggleQolMenuFromHotkey()", "\n    }\n");
        string shared = Slice(plugin, "private void ToggleQolMenuWindow(string via)", "\n    }\n");
        string style = Slice(plugin, "internal void ToggleStyleUiFromOverlayButton()", "\n    }\n");
        string styleHotkey = Slice(plugin, "private void ToggleStyleUiFromHotkey()", "\n    }\n");

        // Review 2026-10-18: one shared path, so a later fix to the hotkey can't miss the button.
        Assert.Multiple(() =>
        {
            Assert.That(qol, Does.Contain("ToggleQolMenuWindow(\"the overlay's QoL Menu button\");"));
            Assert.That(hotkey, Does.Contain("ToggleQolMenuWindow($\"{QolMenuKey.Value} key\");"));
            Assert.That(shared, Does.Contain("StartQolMenuWindow();"));
            Assert.That(shared, Does.Contain("_qolMenuWindow.ToggleVisible();"));
            // Voice pass (2026-10-23) NOISE 4: the menu itself appearing is the answer; no "QoL menu opened" toast.
            Assert.That(shared, Does.Not.Contain("ShowToast("));
            Assert.That(shared, Does.Not.Contain("QoL menu opened."));
            // Blue, 2026-10-18 (final review): opening or closing either menu shows a toast only. No chat line,
            // so the chat log doesn't fill up with menu toggles.
            Assert.That(shared, Does.Not.Contain("AddLocalNotification("));
            Assert.That(style, Does.Contain("ToggleStyleUiFromQolMenu();"));
            Assert.That(style, Does.Contain("ShowToast(result);"));
            Assert.That(style, Does.Not.Contain("AddLocalNotification"));
            Assert.That(styleHotkey, Does.Contain("ShowToast(result);"));
            Assert.That(styleHotkey, Does.Not.Contain("AddLocalNotification"));
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
