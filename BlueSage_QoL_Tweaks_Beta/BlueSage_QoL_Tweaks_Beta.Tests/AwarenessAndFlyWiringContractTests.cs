using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Runtime wiring for UAT round-1 additions (Blue, 2026-10-17): awareness toasts, Host/Helper
// announcements, the /bsqol guide pop-up, the vanilla sweep toast, and fly camera comfort.
public sealed class AwarenessAndFlyWiringContractTests
{
    [Test]
    public void Reconnect_safety_helper_and_rescue_notices_also_toast()
    {
        string plugin = ReadSource("Plugin.cs");
        string reconnect = Slice(plugin, "AddLocalNotificationWithToast(\"BlueSage reconnect is cleaning up", "BlueSage could not uniquely validate a replacement lobby");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal static bool AddLocalNotificationWithToast(string text)"));
            Assert.That(reconnect, Does.Not.Contain(" AddLocalNotification(\"BlueSage reconnect"));
            Assert.That(ReadSource("CloneShieldController.cs"), Does.Contain("Plugin.AddLocalNotificationWithToast("));
            Assert.That(ReadSource("CloneIncidentActionController.cs"), Does.Contain("Plugin.AddLocalNotificationWithToast("));
            Assert.That(ReadSource("IdentityResyncController.cs"), Does.Contain("Plugin.AddLocalNotificationWithToast("));
            Assert.That(ReadSource("SteamIdModerationController.cs"), Does.Contain("Plugin.AddLocalNotificationWithToast((success ?"));
            Assert.That(ReadSource("Patches", "OptInRescuePatch.cs"), Does.Contain("Plugin.AddLocalNotificationWithToast("));
        });
    }

    [Test]
    public void Toasts_fit_small_screens_and_identical_ones_do_not_stack()
    {
        string plugin = ReadSource("Plugin.cs");

        Assert.Multiple(() =>
        {
            // Round 3: width also follows the toast size (Auto/75-200%), still capped by the screen gutter.
            Assert.That(plugin, Does.Contain("private static float ToastWidth => ToastPolicy.ComputeWidth(Screen.width, ToastScale);"));
            Assert.That(plugin, Does.Contain("ToastPolicy.IsDuplicateOfVisible(text, _toasts, Time.unscaledTime)"));
        });
    }

    [Test]
    public void Every_menu_on_off_toggle_confirms_with_a_toast()
    {
        string menu = ReadSource("QolMenuWindow.cs");
        string toggle = Slice(menu, "private void DrawToggle(string label, ConfigEntry<bool> entry, string tooltip, Action afterChange = null)", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(toggle, Does.Contain("Plugin.Instance?.ShowToast(label + \": \" + (entry.Value ? \"On\" : \"Off\"));"));
            // DrawExperimentalToggle went with the retired Player Render Saver (ultra review 2026-10-17).
            Assert.That(menu, Does.Not.Contain("private void DrawExperimentalToggle("));
            Assert.That(menu, Does.Contain("DrawToggle(\"Host Announcements\", Plugin.EnableHostAnnouncements"));
        });
    }

    [Test]
    public void Announcements_trust_only_the_owner_or_a_published_Helper_via_their_own_member_data()
    {
        string controller = ReadSource("LobbyAnnouncementController.cs");
        string poll = Slice(controller, "internal static void Poll(", "\n    }\n}");
        string send = Slice(controller, "internal static string Send(", "internal static void Poll(");

        Assert.Multiple(() =>
        {
            Assert.That(send, Does.Contain("LobbySafetyAccessState.Host"));
            Assert.That(send, Does.Contain("LobbySafetyAccessState.HelperReady"));
            Assert.That(send, Does.Contain("SteamMatchmaking.SetLobbyMemberData("));
            Assert.That(send, Does.Contain("LobbyAnnouncementPolicy.CanSend("));
            Assert.That(poll, Does.Contain("SteamIdModerationController.GetPublishedHelpers(lobby, owner)"));
            Assert.That(poll, Does.Contain("if (!senderIsHost && !helpers.Contains(senderSteamId))"));
            Assert.That(poll, Does.Contain("SteamMatchmaking.GetLobbyMemberData(lobby, member, LobbyAnnouncementPolicy.MemberDataKey)"));
            Assert.That(poll, Does.Contain("SeenAnnouncements.Add(senderSteamId + \"|\" + announcement.Nonce)"));
            Assert.That(ReadSource("Plugin.cs"), Does.Contain("LobbyAnnouncementController.Poll(Time.unscaledTime);"));
            Assert.That(ReadSource("Plugin.cs"), Does.Contain("\"announce\","));
        });
    }

    [Test]
    public void Steam_ID_mismatch_alert_is_raised_in_the_capture_and_shown_from_Update()
    {
        string evidence = ReadSource("PlayerIdentityEvidenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(evidence, Does.Contain("SteamIdTruthAlertPolicy.ShouldAlert(_lastSteamIdMismatchCount, snapshot.SteamIdMismatchCount)"));
            Assert.That(evidence, Does.Not.Contain("AddLocalNotificationWithToast(SteamIdMismatchAlertText)"));
            Assert.That(ReadSource("Plugin.cs"), Does.Contain("if (PlayerIdentityEvidenceController.ConsumeSteamIdMismatchAlert())"));
        });
    }

    [Test]
    public void Bsqol_opens_the_guide_pop_up_and_the_welcome_card_links_to_it()
    {
        string plugin = ReadSource("Plugin.cs");
        string bsqol = Slice(plugin, "private BlueSageCommandResult HandleBlueSageQoLCommand(", "var messages = new List<string>");
        string welcome = Slice(plugin, "private void DrawWelcomeOverlayWindowContents(int windowId)", "internal void DismissWelcomeOverlay()");

        Assert.Multiple(() =>
        {
            Assert.That(bsqol, Does.Contain("firstToken == \"help\""));
            Assert.That(bsqol, Does.Contain("ShowGuideOverlayNow()"));
            Assert.That(plugin, Does.Contain("new[] { \"qol\", \"guide\", \"faq\" },"));
            Assert.That(plugin, Does.Contain("private const int GuideOverlayWindowId = 47064;"));
            Assert.That(plugin, Does.Contain("GuideOverlayPolicy.BuildSections("));
            Assert.That(welcome, Does.Contain("\"Open the full guide\""));
            Assert.That(welcome, Does.Contain("ShowGuideOverlayNow();"));
        });
    }

    [Test]
    public void Vanilla_30_minute_sweep_raises_a_toast()
    {
        string patch = ReadSource("Patches", "VanillaSweepToastPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(MainSceneManager), \"SweepRoutine\")]"));
            Assert.That(patch, Does.Contain("[HarmonyPostfix]"));
            Assert.That(patch, Does.Contain("Plugin.Instance?.ShowToast(VanillaSweepToastText)"));
        });
    }

    [Test]
    public void Fly_mode_uses_vanilla_rotation_a_wider_zoom_and_restores_vanilla_limits()
    {
        string movement = ReadSource("Patches", "SelfMovementPatch.cs");
        string plugin = ReadSource("Plugin.cs");

        Assert.Multiple(() =>
        {
            Assert.That(movement, Does.Contain("FlyCameraPolicy.PrecisionMultiplier("));
            Assert.That(movement, Does.Not.Contain("controlMultiplier = 0.35f;"));
            Assert.That(movement, Does.Contain("instance.SetCameraRotation(new Vector2(input.MouseX, input.MouseY));"));
            Assert.That(movement, Does.Contain("[HarmonyPatch(\"Zoom\")]"));
            Assert.That(movement, Does.Contain("FlyCameraPolicy.ComputeFlyZoom("));
            Assert.That(movement, Does.Contain("ResolutionMode.Desk"));
            Assert.That(movement, Does.Contain("FlyCameraPolicy.ClampToVanilla("));
            Assert.That(plugin, Does.Contain("Patches.SelfMovementPatch.RestoreVanillaCamera();"));
        });
    }

    // Review of 1836d62 (2026-10-17) fixes.
    [Test]
    public void QoL_sweeps_never_trigger_the_vanilla_sweep_notice_and_it_is_toast_only()
    {
        string plugin = ReadSource("Plugin.cs");
        string create = Slice(plugin, "private static bool TryCreateNativeSweepRoutine(", "\n    }\n");
        string patch = ReadSource("Patches", "VanillaSweepToastPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(create, Does.Contain("IsQolSweepCreatingNativeRoutine = true;"));
            Assert.That(create, Does.Contain("finally"));
            Assert.That(create, Does.Contain("IsQolSweepCreatingNativeRoutine = false;"));
            Assert.That(patch, Does.Contain("if (Plugin.IsQolSweepCreatingNativeRoutine)"));
            Assert.That(patch, Does.Not.Contain("AddLocalNotification"));
        });
    }

    [Test]
    public void Fly_zoom_field_access_is_isolated_so_a_game_update_cannot_break_movement()
    {
        string movement = ReadSource("Patches", "SelfMovementPatch.cs");
        string access = ReadSource("Patches", "FlyZoomAccess.cs");

        Assert.Multiple(() =>
        {
            Assert.That(movement, Does.Not.Contain("AccessTools.FieldRefAccess"));
            Assert.That(access, Does.Contain("AccessTools.FieldRefAccess<PlayerMovementController, float>(\"_currentZoom\")"));
            Assert.That(access, Does.Contain("catch (Exception"));
            Assert.That(access, Does.Contain("_disabled = true;"));
        });
    }

    [Test]
    public void Scrolling_any_BlueSage_window_never_zooms_the_camera()
    {
        string plugin = ReadSource("Plugin.cs");
        string zoom = Slice(ReadSource("Patches", "SelfMovementPatch.cs"), "private static bool ZoomPrefix(", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("BlueSageWindowHoverScope.RegisterWindow(GuideOverlayWindowId, rect, 30);"));
            Assert.That(plugin, Does.Contain("BlueSageWindowHoverScope.RegisterWindow(WelcomeOverlayWindowId, rect, 30);"));
            Assert.That(zoom, Does.Contain("BlueSageWindowHoverScope.IsScreenMouseOverAnyWindow()"));
            Assert.That(
                zoom.IndexOf("IsScreenMouseOverAnyWindow", System.StringComparison.Ordinal),
                Is.LessThan(zoom.IndexOf("Plugin.EnableExtendedZoom", System.StringComparison.Ordinal)),
                "The hover rule applies even with Extended Camera Zoom off.");
            Assert.That(zoom, Does.Not.Contain("ShouldApplyNoclipFly"), "Extended zoom applies walking or flying (Blue, 2026-10-17).");
        });
    }

    [Test]
    public void Announcements_throttle_each_sender_and_use_Steam_server_time()
    {
        string controller = ReadSource("LobbyAnnouncementController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("LobbyAnnouncementPolicy.ShouldAcceptFromSender("));
            Assert.That(controller, Does.Contain("SteamUtils.GetServerRealTime()"));
            Assert.That(controller, Does.Contain("long now = SteamNowUnixSeconds();"));
            Assert.That(controller, Does.Contain("long nowUnix = SteamNowUnixSeconds();"));
            Assert.That(ReadSource("Plugin.cs"), Does.Contain("new ToastEntry(text, Time.unscaledTime, pinned)"));
            Assert.That(ReadSource("Plugin.cs"), Does.Contain("ToastPolicy.IndexToEvict(_toasts)"));
        });
    }

    [Test]
    public void Mismatch_alert_resets_per_lobby_and_fly_rotation_defers_to_right_mouse()
    {
        string movement = ReadSource("Patches", "SelfMovementPatch.cs");
        string rotation = Slice(movement, "private static void ApplyFlyRotation(", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(ReadSource("PlayerIdentityEvidenceController.cs"), Does.Contain("_lastSteamIdMismatchLobbyId"));
            Assert.That(rotation, Does.Contain("!input.IsMouseButton1"));
            Assert.That(rotation, Does.Not.Contain("Enum.TryParse"));
            Assert.That(ReadSource("Plugin.cs"), Does.Contain("_guideSectionsKey"));
        });
    }

    private static string ReadSource(params string[] parts)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string path = Path.Combine(root, "src");
        foreach (string part in parts) path = Path.Combine(path, part);
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
