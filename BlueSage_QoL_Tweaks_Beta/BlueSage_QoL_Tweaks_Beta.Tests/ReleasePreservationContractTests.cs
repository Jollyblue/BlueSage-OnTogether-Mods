using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed partial class ReleasePreservationContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Public_021_rc_preserves_critical_voice_update_and_performance_contracts()
    {
        string plugin = Read("src", "Plugin.cs");
        string chat = Read("src", "Patches", "ChatReadabilityPatch.cs");
        string panelPatch = Read("src", "Patches", "EnhancedPlayerPanelPatch.cs");
        string panel = Read("src", "Patches", "PlayerPanelSurfaceController.cs");
        string panelPolicy = Read("src", "Core", "PlayerPanelSurfacePolicy.cs");
        string minimap = Read("src", "Patches", "MiniMapPlayerLabelPatch.cs");
        string saver = Read("src", "PlayerRenderSaverController.cs");
        string readme = Read("README.md");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("public const string PluginVersion = \"0.2.7\""));
            Assert.That(plugin, Does.Contain("JoinByCode(targetLobbyCode, false)"));
            Assert.That(plugin, Does.Not.Contain("JoinByCode(_lastReconnectLobbyCode, true)"));
            Assert.That(plugin, Does.Contain("localUserIsMember"));
            Assert.That(plugin, Does.Contain("IsVoiceRuntimeReadyForReconnect"));
            Assert.That(plugin, Does.Contain("MarkAndrewReconnectIntentionalLeave"));
            Assert.That(chat, Does.Contain("RefreshNewestRows"));
            Assert.That(chat, Does.Contain("OwnedBackdropOverlays"));
            Assert.That(chat, Does.Contain("blocksRaycasts = false"));
            Assert.That(panelPatch, Does.Contain("PlayerPanelSurfaceController.RefreshRow"));
            // Round 4 R9: the unread reflection handles for the native sync/voice/steam buttons are removed; the
            // report-button handle is the one the lane layout reads, and native controls stay game-owned by policy.
            Assert.That(panel, Does.Not.Contain("\"_voiceMuteButton\""));
            Assert.That(panel, Does.Not.Contain("\"_syncButton\""));
            Assert.That(panel, Does.Contain("\"_reportButton\""));
            Assert.That(panelPolicy, Does.Contain("Pomo and voice are game-owned state"));
            Assert.That(panelPolicy, Does.Contain("PreserveNativeControls = true"));
            Assert.That(panel, Does.Not.Contain("RowHasPomoSurface"));
            Assert.That(minimap, Does.Contain("MiniMapIdCardClickHandler"));
            Assert.That(minimap, Does.Not.Contain("BlueSageMiniMapNameLabel"));
            Assert.That(saver, Does.Contain("RestoreAll()"));
            Assert.That(saver, Does.Contain("IsEssentialAudio"));
            Assert.That(saver, Does.Contain("voice"));
            Assert.That(readme, Does.Contain("up to 150 recent rows by default"));
            Assert.That(readme, Does.Contain("never lowers a larger amount already chosen by the game or another mod"));
            Assert.That(readme, Does.Not.Contain("defers reconnect behavior to AndrewLin Reconnect"));
            Assert.That(readme, Does.Not.Contain("automatically raises both Global and Local retained chat logs to `250` rows"));
        });
    }

    [Test]
    public void Cognitive_load_defaults_remain_safe_and_reversible()
    {
        string plugin = Read("src", "Plugin.cs");
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ChatHistoryLimitPolicy.DefaultRows"));
            Assert.That(plugin, Does.Contain("EnablePlayerRenderSaver = Config.Bind(\"Performance\", \"EnablePlayerRenderSaver\", publicDefaults.PlayerRenderSaver"));
            Assert.That(plugin, Does.Contain("EnableSpoons = Config.Bind(\"Spoons\", \"EnableSpoons\", false"));
            Assert.That(plugin, Does.Contain("ResetChatUiToVanilla"));
            Assert.That(plugin, Does.Contain("PlayerRenderSaverController.RestoreAll()"));
            Assert.That(menu, Does.Contain("Reset Chat to the Game's Default"));
            Assert.That(menu, Does.Contain("Retained chat rows"));
        });
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

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
