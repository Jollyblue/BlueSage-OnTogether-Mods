using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ToastNotificationWiringContractTests
{
    [Test]
    public void EnableToastNotifications_is_a_config_entry_defaulting_on()
    {
        string plugin = ReadPluginSource();
        Assert.That(plugin, Does.Contain("EnableToastNotifications = Config.Bind(\"Notifications\", \"EnableToastNotifications\", true,"));
    }

    [Test]
    public void ShowToast_is_gated_by_the_toggle_and_feeds_the_real_fade_policy()
    {
        string plugin = ReadPluginSource();
        string showToast = Slice(plugin, "internal void ShowToast(string text)", "\n    }");
        string onGui = Slice(plugin, "private void OnGUI()", "\n    }\n\n    internal bool IsAwaitingHotkeyRebind");

        Assert.Multiple(() =>
        {
            Assert.That(showToast, Does.Contain("EnableToastNotifications"));
            Assert.That(showToast, Does.Contain("ShowToastUnchecked(text, pinned: false)"));
            Assert.That(Slice(plugin, "private void ShowToastUnchecked(string text, bool pinned)", "\n    }"), Does.Contain("ToastPolicy.MaxVisible"));
            Assert.That(onGui, Does.Contain("ToastPolicy.IsExpired"));
            Assert.That(onGui, Does.Contain("ToastPolicy.ComputeAlpha"));
        });
    }

    [Test]
    public void Every_hotkey_confirmation_this_sprint_fires_a_toast_alongside_the_chat_notification()
    {
        // Blue's ask: toast is additive, alongside AddLocalNotification, never a replacement for it.
        string plugin = ReadPluginSource();

        string[] markers =
        {
            "Rebind cancelled for {cancelledLabel}.",
            "Rebind: {rejectionReason}",
            "{_pendingRebindLabel} is now bound to {candidate}.",
            "BlueSage QoL swept unused assets.",
        };

        Assert.Multiple(() =>
        {
            foreach (string marker in markers)
            {
                int addIndex = plugin.IndexOf("AddLocalNotification($\"" + marker + "\")", StringComparison.Ordinal);
                if (addIndex < 0)
                {
                    addIndex = plugin.IndexOf("AddLocalNotification(\"" + marker + "\")", StringComparison.Ordinal);
                }
                Assert.That(addIndex, Is.GreaterThanOrEqualTo(0), "Missing AddLocalNotification for: " + marker);

                string window = plugin.Substring(addIndex, Math.Min(400, plugin.Length - addIndex));
                Assert.That(window, Does.Contain("ShowToast("), "No ShowToast alongside AddLocalNotification for: " + marker);
            }

            string styleHotkey = Slice(plugin, "private void ToggleStyleUiFromHotkey()", "\n    }");
            string flyHotkey = Slice(plugin, "private void ToggleNoclipFlyFromHotkey()", "\n    }");
            Assert.That(styleHotkey, Does.Contain("ShowToast(result)"));
            Assert.That(flyHotkey, Does.Contain("ShowToast(result)"));
        });
    }

    [Test]
    public void Toast_toggle_is_reachable_from_the_menu()
    {
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));
        Assert.That(menu, Does.Contain("DrawToggle(\"Toast Pop-ups\", Plugin.EnableToastNotifications,"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage QoL project root.");
    }

    private static string ReadPluginSource()
    {
        return File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Start marker missing: " + startMarker);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "End marker missing: " + endMarker);
        return source.Substring(start, end - start);
    }
}
