using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage E, the cheap approved items (Blue, 2026-10-19 audit: #10, #11, #12).
[TestFixture]
public sealed class StageEPerfAndToastContractTests
{
    private static string Root
    {
        get
        {
            string root = TestContext.CurrentContext.TestDirectory;
            while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
            }

            return root;
        }
    }

    // #12 (Blue asked why it helps): each badge marker has its own OnGUI, and Unity runs an extra layout pass for it
    // every frame even when nothing is hovered. The marker draws only with GUI.Box (no GUILayout), so turning layout
    // off skips that pass and the [U]/[H]/[O] badges and the ♥/♪ developer tags keep their hover tooltips exactly.
    // Hover comes from UGUI pointer events (OnPointerEnter/OnPointerExit), not from IMGUI.
    [Test]
    public void Badge_markers_skip_the_imgui_layout_pass_and_keep_ugui_hover()
    {
        string marker = File.ReadAllText(Path.Combine(Root, "src", "Patches", "BlueSagePlayerBadgeMarker.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(marker, Does.Contain("useGUILayout = false;"));
            Assert.That(marker, Does.Not.Contain("GUILayout."), "With layout off, GUILayout calls would draw nothing.");
            Assert.That(marker, Does.Contain("GUI.Box("));
            Assert.That(marker, Does.Contain("public void OnPointerEnter(PointerEventData eventData)"));
            Assert.That(marker, Does.Contain("public void OnPointerExit(PointerEventData eventData)"));
        });
    }

    // #10: SteamRuntimeFacade.CaptureLive ran on every Update frame (lobby lookup + GetLobbyOwner + GetSteamID).
    // The shutdown snapshot only needs a recent tuple, so once per second is plenty; shutdown itself never probes.
    [Test]
    public void Steam_runtime_capture_is_throttled_to_once_per_second()
    {
        string facade = File.ReadAllText(Path.Combine(Root, "src", "SteamRuntimeFacade.cs")).Replace("\r\n", "\n");
        int start = facade.IndexOf("internal static void CaptureLive()", System.StringComparison.Ordinal);
        string body = facade.Substring(start, facade.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(facade, Does.Contain("internal const float CaptureIntervalSeconds = 1f;"));
            Assert.That(body, Does.Contain("NativeLookupCooldownPolicy.ShouldScan(_lastCaptureAt, now, CaptureIntervalSeconds)"), "Same clock-safe cooldown rule as the native manager lookups (a clock reset always allows a capture).");
            Assert.That(body, Does.Contain("_lastCaptureAt = now;"));
            Assert.That(body.IndexOf("PluginShutdownController.IsShuttingDown", System.StringComparison.Ordinal), Is.LessThan(body.IndexOf("ShouldScan", System.StringComparison.Ordinal)), "Shutdown is checked before the throttle, so a shutting-down plugin never probes.");
        });
    }

    // #11 (Blue: leave toast timing alone, make toasts click-through if possible). Verified: toasts are an IMGUI
    // GUI.Window with no controls and no Event.Use, and they are never registered in BlueSageWindowHoverScope, so the
    // game's UGUI buttons and world clicks under a toast already work and BlueSage's own hover gating ignores them.
    // Pinned so a later change cannot make the stack swallow clicks.
    [Test]
    public void Toasts_are_drawn_on_every_event_and_never_count_as_a_hovered_window()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        int start = plugin.IndexOf("private void DrawToastWindowContents(int windowId)", System.StringComparison.Ordinal);
        string body = plugin.Substring(start, plugin.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        int drawStart = plugin.IndexOf("private void DrawToasts()", System.StringComparison.Ordinal);
        string draw = plugin.Substring(drawStart, plugin.IndexOf("\n    }\n", drawStart, System.StringComparison.Ordinal) - drawStart);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Not.Contain("GUI.Button(").And.Not.Contain("GUILayout.Button(").And.Not.Contain("Event.current.Use()"), "No control inside the toast stack consumes a click.");
            Assert.That(draw, Does.Not.Contain("BlueSageWindowHoverScope.RegisterWindow("), "Toasts never count as a hovered BlueSage window (hotkeys and camera zoom keep working under them).");
            Assert.That(plugin, Does.Not.Contain("BlueSageWindowHoverScope.RegisterWindow(ToastWindowId"));
            Assert.That(draw, Does.Contain("GUI.Window(ToastWindowId, stackRect"));
            // Round 5g (Blue's live run of 2D38DB3637FB, 2026-10-25: "not seeing toasts at all"): the round 5d guard that
            // created the window on Repaint only drew nothing in the game. Unity's window list is built from the Layout
            // pass, so a window first created on Repaint never paints. The window is created on every event again,
            // exactly as in 0.2.6 where toasts showed. Click-through returns only with a method proven on the UAT profile.
            Assert.That(draw, Does.Not.Contain("EventType.Repaint"), "No event-type guard may sit in front of the toast window (round 5g).");
            Assert.That(draw, Does.Not.Contain("Event.current"), "The toast window is created on every event so Unity's Layout pass registers it (round 5g).");
            Assert.That(draw + body, Does.Not.Contain("GUI.FocusWindow").And.Not.Contain("GUIUtility.hotControl").And.Not.Contain("GUIUtility.keyboardControl"));
        });
    }

    [Test]
    public void Cozy_toasts_answer_a_manual_cleanup_and_a_successful_reconnect()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        int start = plugin.IndexOf("internal bool RunSweep(string reason, bool requireManualPermission)", System.StringComparison.Ordinal);
        string sweep = plugin.Substring(start, plugin.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        int announce = plugin.IndexOf("private void AnnounceManagedReconnectSuccess()", System.StringComparison.Ordinal);
        string success = plugin.Substring(announce, plugin.IndexOf("\n    }\n", announce, System.StringComparison.Ordinal) - announce);
        Assert.Multiple(() =>
        {
            // A chat line as well as the toast, so the answer arrives even with Toast Pop-ups off.
            Assert.That(sweep, Does.Contain("AnswerManualSweep(ManualSweepPausedNotice, reason)").And.Contain("AnswerManualSweep(ManualSweepBusyNotice, reason)").And.Contain("AnswerManualSweep(ManualSweepFailedNotice, reason)"));
            Assert.That(sweep.Split(new[] { "AnswerManualSweep(ManualSweepBusyNotice, reason)" }, System.StringSplitOptions.None)[0], Does.Contain("if (requireManualPermission)"), "The automatic timer stays quiet.");
            // Round 5d review: answered once. The hotkey gets a chat line + toast (works with toasts off); /sweep and the
            // menu print the sentence themselves, so they only get the toast.
            int answer = plugin.IndexOf("private void AnswerManualSweep(string notice, string reason)", System.StringComparison.Ordinal);
            string answerBody = plugin.Substring(answer, plugin.IndexOf("\n    }\n", answer, System.StringComparison.Ordinal) - answer);
            Assert.That(answerBody, Does.Contain("\"manual hotkey\"").And.Contain("AddLocalNotificationWithToast(notice)").And.Contain("ShowToast(notice)"));
            Assert.That(success, Does.Contain("ShowToast(ReconnectBackInNotice)"));
            Assert.That(success.IndexOf("ShowToast(ReconnectBackInNotice)", System.StringComparison.Ordinal), Is.LessThan(success.IndexOf("EnableReconnectAnnouncement.Value", System.StringComparison.Ordinal)), "The toast does not depend on the lobby chat announcement.");
        });
    }

    [Test]
    public void Identical_toast_is_not_stacked_again_for_the_players_own_reading_time()
    {
        var visible = new System.Collections.Generic.List<BlueSage.QoLTweaks.Core.ToastEntry> { new BlueSage.QoLTweaks.Core.ToastEntry("same", 0f, false) };
        Assert.Multiple(() =>
        {
            Assert.That(BlueSage.QoLTweaks.Core.ToastPolicy.IsDuplicateOfVisible("same", visible, 8f, 10f), Is.True, "A 10 s reading time keeps the first toast on screen at 8 s, so the twin is not stacked.");
            Assert.That(BlueSage.QoLTweaks.Core.ToastPolicy.IsDuplicateOfVisible("same", visible, 20f, 10f), Is.False);
        });
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        Assert.That(plugin, Does.Contain("ToastPolicy.IsDuplicateOfVisible(text, _toasts, Time.unscaledTime, LockedToastHoldSeconds)"));
    }
}
