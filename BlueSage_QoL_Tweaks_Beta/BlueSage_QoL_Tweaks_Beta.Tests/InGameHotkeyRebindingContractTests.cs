using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class InGameHotkeyRebindingContractTests
{
    [Test]
    public void Rebind_slash_command_is_registered()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("\"rebind\""));
            Assert.That(plugin, Does.Contain("HandleRebindCommand"));
        });
    }

    [Test]
    public void Rebind_command_parses_a_real_KeyCode_and_persists_it_without_requiring_a_config_file_edit()
    {
        string plugin = ReadPluginSource();
        string method = Slice(plugin, "private BlueSageCommandResult HandleRebindCommand", "\n    private ");

        Assert.Multiple(() =>
        {
            Assert.That(method, Does.Contain("Enum.TryParse"));
            Assert.That(method, Does.Contain("Config.Save()"));
        });
    }

    [Test]
    public void BeginRebindHotkey_sets_a_pending_capture_state_the_update_loop_can_see()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal string BeginRebindHotkey(HotkeyBindTarget target)"));
            Assert.That(plugin, Does.Contain("internal bool IsAwaitingHotkeyRebind"));
        });
    }

    [Test]
    public void Update_loop_captures_the_next_keypress_without_ever_skipping_host_polling_work()
    {
        // Advisor-caught regression: an earlier version of this capture block `return`-ed out of
        // Update() entirely while a rebind was pending, which meant a host stopped answering
        // Helper/moderation/chalk requests for as long as the rebind menu stayed open. The normal
        // hotkey checks must be skipped while capturing, but PublishHostCapability, the
        // PollHostMemberDataRequests calls, and IdentityResyncController.Tick() must not be.
        string plugin = ReadPluginSource();
        string update = Slice(plugin, "private void Update()", "private void ToggleQolMenuFromHotkey()");

        Assert.Multiple(() =>
        {
            int captureIndex = update.IndexOf("_pendingRebindEntry", StringComparison.Ordinal);
            int qolMenuCheckIndex = update.IndexOf("Input.GetKeyDown(QolMenuKey.Value)", StringComparison.Ordinal);
            int hostPollIndex = update.IndexOf("PublishHostCapability();", StringComparison.Ordinal);
            Assert.That(captureIndex, Is.GreaterThanOrEqualTo(0), "Update() must reference the pending-rebind capture state.");
            Assert.That(qolMenuCheckIndex, Is.GreaterThan(0), "Update() must still check QolMenuKey.Value.");
            Assert.That(hostPollIndex, Is.GreaterThan(qolMenuCheckIndex), "Host polling must still run after the hotkey checks.");
            Assert.That(captureIndex, Is.LessThan(qolMenuCheckIndex), "Rebind capture must run before the normal hotkey checks.");

            // No bare `return;` may appear anywhere between the start of capture handling and the
            // host-polling call: capture must use if/else, not an early exit from Update() itself.
            string captureToHostPoll = Slice(update, "_pendingRebindEntry", "PublishHostCapability();");
            Assert.That(captureToHostPoll, Does.Not.Contain("return;"),
                "Capture handling must not early-return out of Update() -- that skips host polling.");

            string captureBlock = Slice(update, "_pendingRebindEntry", "Input.GetKeyDown(QolMenuKey.Value)");
            Assert.That(captureBlock, Does.Contain("else"), "The normal hotkey checks must be the else-branch of the pending-rebind check, not reached via early return.");
            Assert.That(captureBlock, Does.Contain("KeyCode.Escape"), "A pending rebind must be cancellable.");
        });
    }

    [Test]
    public void Clicking_rebind_cannot_instantly_self_bind_to_the_mouse_click_that_opened_it()
    {
        // The Rebind button's own click is a Mouse0 down/up. If the same Update() frame that opens
        // capture mode also scans for a keydown, the mouse click that opened it would immediately
        // "complete" the bind to Mouse0/whatever triggered it, before the player ever gets to press
        // their intended key. Two independent defenses are required: mouse/joystick buttons are
        // excluded from the capturable set, AND capture must not accept a keydown on the same frame
        // the rebind started.
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            string capturableDefinition = Slice(plugin, "CapturableKeyCodes =", ";");
            Assert.That(capturableDefinition, Does.Contain("Mouse").IgnoreCase, "Mouse buttons must be explicitly excluded from the capturable set.");
            Assert.That(capturableDefinition, Does.Contain("Joystick").IgnoreCase, "Joystick buttons must be explicitly excluded from the capturable set.");

            Assert.That(plugin, Does.Contain("_pendingRebindStartFrame"), "A same-frame guard is required so the opening click can never complete the bind.");
            string update = Slice(plugin, "private void Update()", "private void ToggleQolMenuFromHotkey()");
            string captureBlock = Slice(update, "_pendingRebindEntry", "Input.GetKeyDown(QolMenuKey.Value)");
            Assert.That(captureBlock, Does.Contain("Time.frameCount"));
        });
    }

    [Test]
    public void Capture_and_direct_set_both_run_candidates_through_the_real_validation_policy()
    {
        string plugin = ReadPluginSource();
        string captureBlock = Slice(plugin, "_pendingRebindEntry", "Input.GetKeyDown(QolMenuKey.Value)");

        Assert.Multiple(() =>
        {
            Assert.That(captureBlock, Does.Contain("HotkeyRebindPolicy.TryValidateCandidate("), "In-game capture must reject reserved/colliding keys through the real policy, not accept anything.");
            Assert.That(captureBlock, Does.Contain("AddLocalNotification"), "A rejection or a successful bind must be visible to the player in chat/menu, not only in the log.");

            string commandMethod = Slice(plugin, "private BlueSageCommandResult HandleRebindCommand", "\n    private static KeyCode ResolveHotkeyDefault");
            Assert.That(commandMethod, Does.Contain("HotkeyRebindPolicy.TryValidateCandidate("), "/rebind <target> <key> must apply the same validation as in-game capture.");
            Assert.That(commandMethod, Does.Contain("CapturableKeyCodes.Contains(parsedKey)"), "/rebind <target> <key> must reject Mouse/Joystick names Enum.TryParse would otherwise accept.");
        });
    }

    [Test]
    public void Every_hotkey_has_an_in_game_reset_to_default_path()
    {
        string plugin = ReadPluginSource();
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal string ResetAllHotkeysToDefaults()"));
            Assert.That(plugin, Does.Contain("\"reset\""), "The /rebind command must accept a reset token.");
            Assert.That(menu, Does.Contain("Plugin.Instance.ResetAllHotkeysToDefaults()"), "The menu must expose a reset path too, not just the slash command.");
        });
    }

    [Test]
    public void Every_hotkey_triggered_toggle_confirms_itself_in_chat_not_only_the_log()
    {
        // Blue's ask: a fat-fingered hotkey (especially Fly, which has no visible on/off state
        // otherwise) must never leave the player guessing what just happened with only a log line
        // they'll never open.
        string plugin = ReadPluginSource();
        string styleHotkey = Slice(plugin, "private void ToggleStyleUiFromHotkey()", "\n    }");
        string flyHotkey = Slice(plugin, "private void ToggleNoclipFlyFromHotkey()", "\n    }");

        // Blue, final review 2026-10-18: the menus confirm with their own window plus a toast, so their
        // hotkeys post no chat line (no chat-log spam). Fly has no visible state, so it keeps its chat line.
        Assert.Multiple(() =>
        {
            Assert.That(styleHotkey, Does.Contain("ShowToast(result);"));
            Assert.That(styleHotkey, Does.Not.Contain("AddLocalNotification"));
            Assert.That(flyHotkey, Does.Contain("AddLocalNotification"));
        });
    }

    [Test]
    public void All_four_rebindable_targets_resolve_to_their_real_config_entries()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("HotkeyBindTarget.QolMenu"));
            Assert.That(plugin, Does.Contain("HotkeyBindTarget.StyleMenu"));
            Assert.That(plugin, Does.Contain("HotkeyBindTarget.NoclipFly"));
            Assert.That(plugin, Does.Contain("HotkeyBindTarget.ManualSweep"));
            string resolver = Slice(plugin, "private static ConfigEntry<KeyCode> ResolveHotkeyEntry(HotkeyBindTarget target)", "\n    }");
            Assert.That(resolver, Does.Contain("return QolMenuKey;"));
            Assert.That(resolver, Does.Contain("return StyleMenuKey;"));
            Assert.That(resolver, Does.Contain("return NoclipFlyKey;"));
            Assert.That(resolver, Does.Contain("return ManualSweepKey;"));
        });
    }

    [Test]
    public void QoL_menu_offers_a_press_to_rebind_button_for_every_hotkey_with_no_config_file_required()
    {
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("DrawHotkeyRow(\"QoL Menu\", Plugin.QolMenuKey, Plugin.HotkeyBindTarget.QolMenu"));
            Assert.That(menu, Does.Contain("DrawHotkeyRow(\"Style Helper\", Plugin.StyleMenuKey, Plugin.HotkeyBindTarget.StyleMenu"));
            Assert.That(menu, Does.Contain("DrawHotkeyRow(\"Noclip / Fly\", Plugin.NoclipFlyKey, Plugin.HotkeyBindTarget.NoclipFly"));
            Assert.That(menu, Does.Contain("DrawHotkeyRow(\"Manual Sweep\", Plugin.ManualSweepKey, Plugin.HotkeyBindTarget.ManualSweep"));
            Assert.That(menu, Does.Contain("Plugin.Instance.BeginRebindHotkey(target)"));
            Assert.That(menu, Does.Contain("IsAwaitingHotkeyRebind"));
        });
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
