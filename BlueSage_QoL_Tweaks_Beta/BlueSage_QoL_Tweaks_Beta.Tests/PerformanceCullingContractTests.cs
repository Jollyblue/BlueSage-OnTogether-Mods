using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PerformanceCullingContractTests
{
    [Test]
    public void PlayerRenderSaver_is_clean_install_on_local_only_and_menu_configurable()
    {
        string projectRoot = FindProjectRoot();
        string pluginSource = File.ReadAllText(Path.Combine(projectRoot, "src", "Plugin.cs"));
        string menuSource = File.ReadAllText(Path.Combine(projectRoot, "src", "QolMenuWindow.cs"));
        string typeaheadSource = File.ReadAllText(Path.Combine(projectRoot, "src", "CommandTypeaheadController.cs"));
        string controllerSource = File.ReadAllText(Path.Combine(projectRoot, "src", "PlayerRenderSaverController.cs"));

        Assert.That(pluginSource, Does.Contain("EnablePlayerRenderSaver = Config.Bind(\"Performance\", \"EnablePlayerRenderSaver\", publicDefaults.PlayerRenderSaver"));
        Assert.That(pluginSource, Does.Contain("PlayerRenderSaverRadiusMeters = Config.Bind(\"Performance\", \"PlayerRenderSaverRadiusMeters\", PlayerRenderSaverPolicy.DefaultRadiusMeters"));
        Assert.That(pluginSource, Does.Contain("PlayerRenderSaverMaxVisiblePlayers = Config.Bind(\"Performance\", \"PlayerRenderSaverMaxVisiblePlayers\", PlayerRenderSaverPolicy.DefaultMaxVisiblePlayers"));
        Assert.That(pluginSource, Does.Contain("StartPlayerRenderSaverController();"));
        Assert.That(pluginSource, Does.Contain("HandlePlayerRenderSaverCommand"));
        Assert.That(pluginSource, Does.Contain("\"playersaver\""));
        Assert.That(pluginSource, Does.Contain("new[] { \"rendercull\", \"playercull\" }"));
        Assert.That(pluginSource, Does.Contain("PlayerRenderSaverController.RestoreAll();"));
        Assert.That(pluginSource, Does.Contain("Player Render Saver: off. Restored all hidden player renderers."));

        // Retired 2026-10-17 (Blue: visual glitching): the menu no longer offers or tunes it; the
        // dormant controller/command code below stays so it can return behind PlayerRenderSaverPolicy.Retired.
        Assert.That(menuSource, Does.Not.Contain("DrawExperimentalToggle(\"Player Render Saver\""));
        Assert.That(menuSource, Does.Not.Contain("DrawFeatureSection(\"Large-lobby performance\""));
        Assert.That(menuSource, Does.Contain("RenderSaver=Retired"));

        Assert.That(typeaheadSource, Does.Contain("[\"playersaver\"] = PlayerRenderSaverChoices(\"playersaver\")"));
        Assert.That(typeaheadSource, Does.Contain("Choice(command, \"radius\", \"set hide radius in meters\")"));
        Assert.That(typeaheadSource, Does.Contain("Choice(command, \"max\", \"set closest visible player count\")"));

        Assert.That(controllerSource, Does.Contain("PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;"));
        Assert.That(controllerSource, Does.Contain("SteamUser.GetSteamID()"));
        Assert.That(controllerSource, Does.Contain("GetComponentsInChildren<Renderer>(true)"));
        Assert.That(controllerSource, Does.Contain("renderer.enabled = false"));
        Assert.That(controllerSource, Does.Contain("AnimatorCullingMode.CullCompletely"));
        Assert.That(controllerSource, Does.Contain("emission.enabled = false"));
        Assert.That(controllerSource, Does.Contain("IsEssentialAudio(audio)"));
        Assert.That(controllerSource, Does.Contain("HiddenOwners"));
        Assert.That(controllerSource, Does.Contain("LastCandidateRemotePlayers"));
        Assert.That(controllerSource, Does.Contain("LastSuppressedRenderers"));
        Assert.That(controllerSource, Does.Contain("LastRuntimeState"));
        Assert.That(controllerSource, Does.Contain("players selected for hiding, but no avatar renderers were found"));
        Assert.That(controllerSource, Does.Not.Contain("canvas.enabled = false"));
        Assert.That(controllerSource, Does.Contain("state.Key.enabled = state.Value"));
        Assert.That(controllerSource, Does.Not.Contain(".SetActive(false)"));
        Assert.That(controllerSource, Does.Not.Contain("SendMessageAsync"));
        Assert.That(controllerSource, Does.Not.Contain("SetLobbyData"));
    }

    [Test]
    public void Per_frame_helpers_are_local_owner_gated_and_idle_input_does_not_rebuild_suggestions()
    {
        string projectRoot = FindProjectRoot();
        string rescueSource = File.ReadAllText(Path.Combine(projectRoot, "src", "Patches", "OptInRescuePatch.cs"));
        string visibilitySource = File.ReadAllText(Path.Combine(projectRoot, "src", "Patches", "HostVisibilityProtocolPatch.cs"));
        string mentionSource = File.ReadAllText(Path.Combine(projectRoot, "src", "MentionPingController.cs"));
        string commandSource = File.ReadAllText(Path.Combine(projectRoot, "src", "CommandTypeaheadController.cs"));

        int localControllerGuard = rescueSource.IndexOf("if (__instance != NetworkSingleton<TextChannelManager>.I?.MainMovementController) return;", StringComparison.Ordinal);
        int visibilityTick = rescueSource.IndexOf("HostVisibilityProtocolPatch.Tick();", StringComparison.Ordinal);
        Assert.That(localControllerGuard, Is.GreaterThanOrEqualTo(0));
        Assert.That(visibilityTick, Is.GreaterThan(localControllerGuard), "Visibility maintenance must run once through the local controller, not once per remote avatar.");
        Assert.That(visibilitySource, Does.Contain("Time.unscaledTime < _nextValidationAt"));
        Assert.That(visibilitySource, Does.Contain("_nextValidationAt = Time.unscaledTime + 0.5f"));

        Assert.That(mentionSource, Does.Contain("_lastObservedInputText"));
        Assert.That(mentionSource, Does.Contain("currentCaret != _lastObservedCaret"));
        Assert.That(commandSource, Does.Contain("_lastObservedInputText"));
        Assert.That(commandSource, Does.Contain("currentCaret != _lastObservedCaret"));
    }

    private static string FindProjectRoot()
    {
        string? directory = TestContext.CurrentContext.TestDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        Assert.Fail("Could not locate project root from " + TestContext.CurrentContext.TestDirectory);
        return string.Empty;
    }
}
