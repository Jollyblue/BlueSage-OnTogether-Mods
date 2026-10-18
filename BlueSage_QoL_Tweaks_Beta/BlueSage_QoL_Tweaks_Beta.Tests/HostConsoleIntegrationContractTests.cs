using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HostConsoleIntegrationContractTests
{
    [Test]
    public void Host_console_uses_live_host_roster_cleanup_reconnect_and_playerlimit_truth()
    {
        string root = FindProjectRoot();
        string controller = File.ReadAllText(
            Path.Combine(root, "src", "HostConsoleController.cs"));
        string plugin = File.ReadAllText(
            Path.Combine(root, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("SteamIdModerationController.GetLocalAccessState"));
            Assert.That(controller, Does.Contain("PlayerIdentityEvidenceController.GetVerifiedRoster"));
            Assert.That(controller, Does.Contain("GetCachedIdentityResyncCoverage"));
            Assert.That(controller, Does.Contain("Plugin.GetPlayerLimitCompanionStatus"));
            Assert.That(controller, Does.Contain("HostConsoleReadinessPolicy.Resolve("));
            Assert.That(controller, Does.Contain("HostToolsPresentationPolicy.Resolve("));
            Assert.That(controller, Does.Contain("RefreshIntervalTicks"));
            Assert.That(controller, Does.Contain("_lastSnapshot"));
            Assert.That(controller, Does.Contain("AuditNativeModerationUi"));
            Assert.That(controller, Does.Contain("ButtonMutePlayer"));
            Assert.That(controller, Does.Contain("ButtonIgnorePlayer"));
            Assert.That(controller, Does.Contain("ButtonReport"));
            Assert.That(controller, Does.Contain("ButtonBan"));
            Assert.That(plugin, Does.Contain("HostCleanupLaneHealthy"));
            Assert.That(plugin, Does.Contain("HostReconnectLaneHealthy"));
        });
    }

    [Test]
    public void Shared_menu_presents_friendly_host_tools_and_helper_safety_without_private_copy()
    {
        string menu = File.ReadAllText(
            Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("isHost ? \"Host Tools\" : \"Safety\""));
            Assert.That(menu, Does.Contain("DrawHostReadinessCard("));
            Assert.That(menu, Does.Contain("Show technical details"));
            Assert.That(menu, Does.Contain("Hide technical details"));
            Assert.That(menu, Does.Contain("Check game moderation buttons"));
            Assert.That(menu, Does.Contain("Read-only compatibility check"));
            Assert.That(menu, Does.Not.Contain("24/7 Ready"));
            Assert.That(menu, Does.Not.Contain("24/7 Degraded"));
            Assert.That(menu, Does.Not.Contain("Audit Native UI"));
            Assert.That(menu, Does.Not.Contain("  •  Provider "));
            Assert.That(menu, Does.Not.Contain("  •  Relay "));
            Assert.That(menu, Does.Contain("HostModerationSurfacePolicy.Evaluate("));
            Assert.That(menu, Does.Not.Contain("BlueExclusive moderation audit"));
        });
    }

    // Live log, 2026-10-18 (Default profile, round 3e): "Getting control 13's position in a group with only
    // 13 controls when doing repaint" from DrawHostReadinessCard, then "pushing more GUIClips than you are
    // popping". A companion published its capability snapshot between Layout and Repaint, so the card drew one
    // more control in Repaint than Layout had counted. The card must read live state only during Layout.
    [Test]
    public void Host_readiness_card_reads_live_state_only_during_layout()
    {
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs"));
        int start = menu.IndexOf("private void DrawHostReadinessCard()", StringComparison.Ordinal);
        int end = menu.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        string card = menu.Substring(start, end - start).Replace("\r\n", "\n");
        int latch = card.IndexOf("Event.current.type == EventType.Layout", StringComparison.Ordinal);
        int latchEnd = card.IndexOf("_hostCardCaptured = true;", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(latch, Is.GreaterThan(0), "The card refreshes its state on the Layout event.");
            Assert.That(latchEnd, Is.GreaterThan(latch));
            string outsideLatch = card.Substring(0, latch) + card.Substring(latchEnd);
            Assert.That(outsideLatch, Does.Not.Contain("HostConsoleController.CapturePresentation()"));
            Assert.That(outsideLatch, Does.Not.Contain("HostConsoleController.HasCapabilitySnapshot"));
            Assert.That(outsideLatch, Does.Not.Contain("HostConsoleController.LastCapabilitySnapshot"));
            Assert.That(outsideLatch, Does.Not.Contain("if (_showHostToolsTechnicalDetails)"),
                "The details toggle applies from the next Layout, not halfway through a frame.");
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage QoL project root.");
    }
}
