using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RebindableHotkeysContractTests
{
    [Test]
    public void QolMenuKey_is_a_rebindable_config_entry_defaulting_to_Home()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal static ConfigEntry<KeyCode> QolMenuKey"));
            Assert.That(plugin, Does.Contain("QolMenuKey = Config.Bind(\"Hotkeys\", \"QolMenuKey\", KeyCode.Home,"));
        });
    }

    [Test]
    public void StyleMenuKey_is_a_rebindable_config_entry_defaulting_to_Insert()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal static ConfigEntry<KeyCode> StyleMenuKey"));
            Assert.That(plugin, Does.Contain("StyleMenuKey = Config.Bind(\"Hotkeys\", \"StyleMenuKey\", KeyCode.Insert,"));
        });
    }

    [Test]
    public void NoclipFlyKey_is_a_rebindable_config_entry_with_a_dedicated_default()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal static ConfigEntry<KeyCode> NoclipFlyKey"));
            Assert.That(plugin, Does.Contain("NoclipFlyKey = Config.Bind(\"Hotkeys\", \"NoclipFlyKey\", KeyCode.End,"));
        });
    }

    [Test]
    public void Update_loop_reads_the_configured_keys_and_no_longer_hardcodes_them()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            // The rebind must actually take effect at the Input.GetKeyDown call site, not just
            // exist as an unused config entry alongside a still-hardcoded literal check.
            Assert.That(plugin, Does.Contain("Input.GetKeyDown(QolMenuKey.Value)"));
            Assert.That(plugin, Does.Contain("Input.GetKeyDown(StyleMenuKey.Value)"));
            Assert.That(plugin, Does.Contain("Input.GetKeyDown(NoclipFlyKey.Value)"));
            Assert.That(plugin, Does.Not.Contain("Input.GetKeyDown(KeyCode.Home)"));
            Assert.That(plugin, Does.Not.Contain("Input.GetKeyDown(KeyCode.Insert)"));
        });
    }

    [Test]
    public void Noclip_fly_hotkey_toggles_the_same_state_as_the_QoL_menu_button()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ToggleNoclipFlyFromHotkey();"));
            string method = Slice(plugin, "private void ToggleNoclipFlyFromHotkey()", "\n    }");
            Assert.That(method, Does.Contain("ToggleNoclipFlyFromQolMenu()"));
        });
    }

    [Test]
    public void Hotkey_toggle_log_lines_report_the_actual_configured_key_not_a_fixed_literal()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Not.Contain("QoL menu toggled via Home key."));
            Assert.That(plugin, Does.Not.Contain("Style Helper toggled via Insert key."));
            Assert.That(plugin, Does.Contain("{QolMenuKey.Value}"));
            Assert.That(plugin, Does.Contain("{StyleMenuKey.Value}"));
        });
    }

    [Test]
    public void Live_status_summary_call_site_passes_the_real_configured_menu_key()
    {
        string plugin = ReadPluginSource();

        Assert.That(
            plugin,
            Does.Contain(
                "QolStatusTextPolicy.BuildPlayerSummary(\n" +
                "            NativeAssetSweepOwnership,\n" +
                "            EnableAutoSweep.Value && AutoSweepAllowed,\n" +
                "            ManualSweepAllowed,\n" +
                "            QolMenuKey.Value.ToString())"));
    }

    [Test]
    public void Welcome_message_tooltip_no_longer_hardcodes_Home_Insert_as_fixed_text()
    {
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));

        Assert.That(menu, Does.Not.Contain("Home/Insert navigation"));
    }

    [Test]
    public void Fly_toggle_button_tells_the_player_its_hotkey_without_them_needing_the_menu_open()
    {
        // Blue's ask: a casual player should be able to learn the fly hotkey from the menu once,
        // then never need the menu up again just to toggle it mid-adjustment.
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));

        Assert.That(menu, Does.Contain("{Plugin.NoclipFlyKey.Value}"));
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
