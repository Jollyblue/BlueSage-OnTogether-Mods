using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WelcomeOverlayWiringContractTests
{
    [Test]
    public void EnableWelcomeOverlay_and_seen_version_are_config_entries()
    {
        string plugin = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("EnableWelcomeOverlay = Config.Bind(\"Notifications\", \"EnableWelcomeOverlay\", true,"));
            // Review 2026-10-18: the seen-version key was removed once the pop-up became per-visit (DEAD_CODE_REGISTER).
            Assert.That(plugin, Does.Not.Contain("LastSeenWelcomeOverlayVersion"));
        });
    }

    [Test]
    public void The_per_lobby_welcome_loop_offers_the_overlay_through_the_real_policy()
    {
        string plugin = ReadPluginSource();
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("WelcomeOverlayPolicy.ShouldOffer("));
            // Round 3b: keyed by the lobby visit, the same key that gives every join its chat welcome.
            Assert.That(plugin, Does.Contain("_welcomeOverlayOfferedVisitKey = overlayLobbyKey;"));
            Assert.That(plugin, Does.Not.Contain("_welcomeOverlayOfferedThisSession"));
            Assert.That(plugin, Does.Contain("_welcomeOverlayVisible = true;"));
        });
    }

    [Test]
    public void Overlay_offer_check_runs_before_the_chat_welcome_toggle_can_skip_it()
    {
        // Real bug this proves stays fixed: the overlay must not live inside the
        // EnableWelcomeMessage early-continue branch, or turning the chat welcome message off
        // also silently kills the overlay (they are supposed to be independent toggles).
        string plugin = ReadPluginSource();
        string loop = Slice(plugin, "private IEnumerator WelcomeMessageLoop()", "private void ResetWelcomeMessageLobbyState()");

        int offerIndex = loop.IndexOf("WelcomeOverlayPolicy.ShouldOffer(", StringComparison.Ordinal);
        int chatGateIndex = loop.IndexOf("EnableWelcomeMessage == null", StringComparison.Ordinal);

        Assert.That(offerIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(chatGateIndex, Is.GreaterThan(0));
        Assert.That(offerIndex, Is.LessThan(chatGateIndex), "Overlay offer check must run before the chat-welcome early-continue.");
    }

    [Test]
    public void Toggle_and_reopen_path_exist_so_the_overlay_is_never_config_file_only()
    {
        string plugin = ReadPluginSource();
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("DrawToggle(\"Welcome Overlay\", Plugin.EnableWelcomeOverlay,"));
            Assert.That(menu, Does.Contain("Plugin.Instance.ShowWelcomeOverlayNow();"));
            Assert.That(plugin, Does.Contain("internal string ShowWelcomeOverlayNow()"));
            Assert.That(plugin, Does.Contain("\"welcome\","));
            Assert.That(plugin, Does.Contain("HandleWelcomeOverlayCommand"));
        });
    }

    [Test]
    public void OnGUI_respects_the_same_shutdown_and_uat_observation_guards_as_Update()
    {
        string plugin = ReadPluginSource();
        string onGui = Slice(plugin, "private void OnGUI()", "\n    private void DrawToasts()");

        Assert.Multiple(() =>
        {
            Assert.That(onGui, Does.Contain("PluginShutdownController.IsShuttingDown"));
            Assert.That(onGui, Does.Contain("EnableBetaUatObservationOnly?.Value == true"));
        });
    }

    [Test]
    public void Toasts_and_overlay_are_front_most_windows_above_the_menu_window()
    {
        // 2026-10-17 (ultrareview #3), replacing the old "GUI.depth =" pin, which encoded the bug:
        // Unity always draws GUI.Window above plain GUI.Box calls, and the QoL menu is a window
        // (47058), so boxes drew behind it no matter what GUI.depth said; restoring GUI.depth
        // before OnGUI returned also threw the ordering away. They must be windows themselves,
        // raised every frame.
        string plugin = ReadPluginSource();
        string onGui = Slice(plugin, "private void OnGUI()", "\n    private void DrawToasts()");
        string toasts = Slice(plugin, "private void DrawToasts()", "\n    private void DrawWelcomeOverlay()");
        string overlay = Slice(plugin, "private void DrawWelcomeOverlay()", "\n    internal void DismissWelcomeOverlay()");

        Assert.Multiple(() =>
        {
            Assert.That(onGui, Does.Not.Contain("GUI.depth = previousDepth"), "Restoring depth before return discards the ordering.");
            Assert.That(plugin, Does.Contain("private const int ToastWindowId = 47063;"));
            Assert.That(plugin, Does.Contain("private const int WelcomeOverlayWindowId = 47062;"));
            Assert.That(toasts, Does.Contain("GUI.Window(ToastWindowId"));
            Assert.That(toasts, Does.Contain("GUI.BringWindowToFront(ToastWindowId)"));
            Assert.That(overlay, Does.Contain("GUI.Window(WelcomeOverlayWindowId"));
            Assert.That(overlay, Does.Contain("GUI.BringWindowToFront(WelcomeOverlayWindowId)"));
        });
    }

    [Test]
    public void Enter_does_not_dismiss_the_overlay_because_it_is_the_chat_key()
    {
        // 2026-10-17 (ultrareview #7): Return dismissed the overlay for good (persisting the seen
        // version) while the same keypress also opened chat. Only the button or Escape dismiss.
        string plugin = ReadPluginSource();
        string overlay = Slice(plugin, "private void DrawWelcomeOverlay()", "\n    internal void DismissWelcomeOverlay()");

        Assert.Multiple(() =>
        {
            Assert.That(overlay, Does.Not.Contain("KeyCode.Return"));
            Assert.That(overlay, Does.Contain("KeyCode.Escape"));
        });
    }

    [Test]
    public void Toast_and_overlay_boxes_size_to_their_actual_text_instead_of_a_fixed_box()
    {
        // Real bug this proves stays fixed: a fixed-height box clips multi-line toast/overlay text.
        string plugin = ReadPluginSource();
        string toasts = Slice(plugin, "private void DrawToasts()", "\n    private void DrawWelcomeOverlay()");
        string overlay = Slice(plugin, "private void DrawWelcomeOverlay()", "\n    internal void DismissWelcomeOverlay()");

        Assert.Multiple(() =>
        {
            Assert.That(toasts, Does.Contain("CalcHeight("));
            Assert.That(overlay, Does.Contain("CalcHeight("));
        });
    }

    [Test]
    public void OnGUI_draws_the_overlay_using_the_real_content_builder()
    {
        string plugin = ReadPluginSource();
        string draw = Slice(plugin, "private void DrawWelcomeOverlay()", "\n    internal void DismissWelcomeOverlay()");

        Assert.Multiple(() =>
        {
            Assert.That(draw, Does.Contain("WelcomeOverlayPolicy.BuildBody("));
            Assert.That(draw, Does.Contain("QolMenuKey.Value.ToString()"));
            Assert.That(draw, Does.Contain("StyleMenuKey.Value.ToString()"));
            Assert.That(draw, Does.Contain("NoclipFlyKey.Value.ToString()"));
            Assert.That(draw, Does.Contain("ManualSweepKey.Value.ToString()"));
            Assert.That(draw, Does.Contain("DismissWelcomeOverlay()"));
        });
    }

    // Round 3b (Blue, 2026-10-18) replaces the old "dismissing hides it for this version for good" pin:
    // "Got it" closes it for this visit only, and "Don't show this again" is the real opt-out. It turns
    // the Welcome Overlay toggle off, so Toggles or /welcome bring it back.
    [Test]
    public void Got_it_closes_for_this_visit_and_dont_show_again_turns_the_toggle_off()
    {
        string plugin = ReadPluginSource();
        string dismiss = Slice(plugin, "internal void DismissWelcomeOverlay()", "\n    }");
        string optOut = Slice(plugin, "internal void TurnOffWelcomeOverlay()", "\n    }");
        string window = Slice(plugin, "private void DrawWelcomeOverlayWindowContents(int windowId)", "\n    internal void DismissWelcomeOverlay()");

        Assert.Multiple(() =>
        {
            Assert.That(dismiss, Does.Contain("_welcomeOverlayVisible = false;"));
            Assert.That(dismiss, Does.Not.Contain("EnableWelcomeOverlay"), "Got it must not opt the player out.");
            // Review 2026-10-18: nothing reads the seen-version any more, so "Got it" writes no config.
            Assert.That(dismiss, Does.Not.Contain("Config.Save()").And.Not.Contain("LastSeenWelcomeOverlayVersion"));
            Assert.That(plugin, Does.Contain("if (!WelcomeLobbyKeyPolicy.IsSameVisit(_welcomeLobbyKey, lobbyKey))"), "The chat welcome compares visits the same way.");
            Assert.That(optOut, Does.Contain("DismissWelcomeOverlay();"));
            Assert.That(optOut, Does.Contain("EnableWelcomeOverlay.Value = false;"));
            Assert.That(optOut, Does.Contain("Config.Save();"));
            Assert.That(optOut, Does.Contain("AddLocalNotificationWithToast("));
            Assert.That(window, Does.Contain("GUILayout.Button(\"Don't show this again\""));
            Assert.That(window, Does.Contain("TurnOffWelcomeOverlay();"));
        });
    }

    // Blue, UAT 2026-10-18: the height comes from the real style margins, and the button count it uses
    // must equal the buttons the window actually draws, so a new button can never be cut off again.
    [Test]
    public void Overlay_height_is_measured_from_the_styles_and_the_real_button_count()
    {
        string plugin = ReadPluginSource();
        string overlay = Slice(plugin, "private void DrawWelcomeOverlay()", "\n    private void DrawWelcomeOverlayWindowContents(");
        string contents = Slice(plugin, "private void DrawWelcomeOverlayWindowContents(int windowId)", "\n    internal void DismissWelcomeOverlay()");
        int drawnButtons = contents.Split("GUILayout.Button(").Length - 1;

        Assert.Multiple(() =>
        {
            Assert.That(overlay, Does.Contain("WelcomeOverlayPolicy.WindowHeight("));
            Assert.That(overlay, Does.Contain("_welcomeOverlayBoxStyle.padding.vertical"));
            Assert.That(overlay, Does.Contain("_welcomeOverlayBodyStyle.margin.vertical"));
            Assert.That(overlay, Does.Contain("GUI.skin.button.margin.vertical"));
            Assert.That(plugin, Does.Contain($"private const int WelcomeOverlayButtonCount = {drawnButtons};"));
            Assert.That(overlay, Does.Not.Contain("(4f * WelcomeOverlayButtonHeight)"));
        });
    }

    [Test]
    public void Rebind_command_has_a_quick_keys_review_alias()
    {
        string plugin = ReadPluginSource();
        Assert.That(plugin, Does.Contain("new[] { \"bind\", \"kb\", \"keys\" }"));
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
