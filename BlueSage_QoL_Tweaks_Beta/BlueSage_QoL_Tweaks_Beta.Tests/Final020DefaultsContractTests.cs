using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class Final020DefaultsContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void RequestedReleaseTogglesHaveTheFinalDefaultStates()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string[] defaultOn =
        {
            "EnableChatTimestamps", "EnableNotificationTimestamps",
            "EnableChatReadability", "EnablePersistentChatBackdrop", "EnableBetterMove",
            "EnableStyleUi", "EnableExtendedAvatarStyles", "EnableChalkboardPersistence",
            "EnableLeaveNotifications", "EnableWelcomeMessage", "EnableReconnectGuard",
            "EnableReconnectAnnouncement", "EnableHostHealthMonitor", "EnableFocusAnywhere",
            "EnableEnhancedPlayerPanel", "EnableCloneShield", "EnablePingMentions",
            "EnablePingSound", "EnableChatCopy", "EnableChatUrlLinks",
            "EnableMiniMapPlayerLabels", "EnableMiniMapFriendColors", "EnableLocalAuditExports"
        };

        foreach (string key in defaultOn)
        {
            Assert.That(plugin, Does.Match($@"{key}\s*=\s*Config\.Bind\([^;]+,\s*true,"), key + " must default on");
        }

        Assert.That(plugin, Does.Match(@"EnableSpoons\s*=\s*Config\.Bind\([^;]+,\s*false,"));
        Assert.That(plugin, Does.Contain("EnableAutoSweep = Config.Bind(\"Asset Sweep\", \"EnableAutoSweep\", publicDefaults.AutomaticCleanup"));
        Assert.That(plugin, Does.Contain("EnablePlayerRenderSaver = Config.Bind(\"Performance\", \"EnablePlayerRenderSaver\", publicDefaults.PlayerRenderSaver"));
        Assert.That(plugin, Does.Contain("EnablePerformanceOverlay = Config.Bind(\"Performance Overlay\", \"EnablePerformanceOverlay\", publicDefaults.PerformanceOverlay"));
        Assert.That(plugin, Does.Contain("Use24HourTime = Config.Bind(\"Chat\", \"Use24HourTime\", false"));
    }

    [Test]
    public void RequestedCustomizeDefaultsAndReconnectTextAreCanonical()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string history = File.ReadAllText(Path.Combine(Root, "src", "Core", "ChatHistoryLimitPolicy.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("SweepIntervalMinutes\", SweepSettingsPolicy.DefaultIntervalMinutes"));
            Assert.That(plugin, Does.Contain("PingHighlightColor\", \"9B59B6\""));
            Assert.That(plugin, Does.Contain("PingMentionColor\", \"FFD700\""));
            Assert.That(plugin, Does.Contain("PingSoundMode\", \"ticket\""));
            Assert.That(plugin, Does.Contain("BlackChatOutlineColor\", \"C0C0C0\""));
            Assert.That(plugin, Does.Contain("BlackNamesOutline\", true"));
            Assert.That(history, Does.Contain("DefaultRows = 150"));
            Assert.That(plugin, Does.Contain("Auto Reconnected by Blues - BlueSage QoL Tweaks Mod"));
            Assert.That(plugin, Does.Not.Contain("ConfigMigrationTarget = PluginVersion +"));
            Assert.That(plugin, Does.Contain("ConfigMigrationPolicy.RecordMigration"));
        });
    }

    [Test]
    public void PerformanceTuningLivesInCreditsOtherAndTextLimitsEndCustomize()
    {
        string menu = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));
        int customize = menu.IndexOf("private void DrawCustomizeTab", StringComparison.Ordinal);
        int credits = menu.IndexOf("private void DrawCreditsOtherTab", customize, StringComparison.Ordinal);
        string customizeBody = menu.Substring(customize, credits - customize);
        string creditsBody = menu.Substring(credits);

        Assert.That(customizeBody, Does.Not.Contain("Player Render Saver"));
        Assert.That(customizeBody.LastIndexOf("DrawSettingsSection(\"Text Limits\"", StringComparison.Ordinal), Is.GreaterThan(customizeBody.LastIndexOf("DrawSettingsSection(\"Chat & Readability\"", StringComparison.Ordinal)));
        // Player Render Saver retired 2026-10-17; its tuning UI was removed after the ultra review.
        Assert.That(menu, Does.Not.Contain("SavePlayerRenderSaverRadius"));
        Assert.That(menu, Does.Not.Contain("SavePlayerRenderSaverMaxVisible"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
