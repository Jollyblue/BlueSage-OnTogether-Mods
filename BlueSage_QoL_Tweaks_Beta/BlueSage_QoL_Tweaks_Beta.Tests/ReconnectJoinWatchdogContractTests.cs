using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReconnectTriggerSafetyContractTests
{
    [Test]
    public void OrdinaryJoinByCode_CannotArmOrStartDestructiveReconnectRecovery()
    {
        string root = FindProjectRoot();
        string patchPath = Path.Combine(root, "src", "Patches", "ReconnectJoinWatchdogPatch.cs");
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        Assert.That(File.Exists(patchPath), Is.False, "A normal/manual JoinByCode must never arm destructive reconnect logic.");
        Assert.That(plugin, Does.Not.Contain("ArmReconnectJoinWatchdog"));
        Assert.That(plugin, Does.Not.Contain("LobbyJoinWatchdogLoop"));
        Assert.That(plugin, Does.Not.Contain("join watchdog timed out; starting clean-menu recovery"));
        Assert.That(plugin, Does.Not.Contain("StartCleanMenuReconnect($\"join watchdog timeout:"));
    }

    [Test]
    public void PluginReconnectRuntimeHealth_UsesCleanMenuGenerationWithoutNestedFallbackLoops()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string terminalCleanupPolicy = File.ReadAllText(Path.Combine(root, "src", "Core", "ReconnectTerminalCleanupPolicy.cs"));

        Assert.That(source, Does.Contain("LockedReconnectCleanupTimeoutSeconds = 10"));
        Assert.That(source, Does.Contain("LockedReconnectJoinValidationTimeoutSeconds = 45"));
        Assert.That(source, Does.Contain("IsGameRuntimeUsableForReconnect"));
        Assert.That(source, Does.Contain("textChannelManager.MainPlayer != null"));
        Assert.That(source, Does.Contain("native teardown must reach the clean menu before any join attempt"));
        Assert.That(source, Does.Contain("JoinByCode(targetLobbyCode, false)"));
        Assert.That(source, Does.Contain("activeSceneBuildIndex: activeScene.buildIndex"));
        Assert.That(terminalCleanupPolicy, Does.Contain("state.ActiveSceneBuildIndex == 1"));
        Assert.That(source, Does.Contain("vanillaIsConnecting="));
        Assert.That(source, Does.Contain("blueSageJoinIdle"));
        Assert.That(source, Does.Not.Contain("bool joinIdle = multiplayerManager == null || !multiplayerManager.IsConnecting"));
        Assert.That(source, Does.Contain("activeScene.buildIndex != 2"));
        Assert.That(source, Does.Contain("localOccurrences == 1"));
        Assert.That(source, Does.Contain("LockedReconnectReadyStabilitySeconds = 2"));
        Assert.That(source, Does.Not.Contain("BlueSage reconnect join is still loading; retrying once."));
        Assert.That(source, Does.Not.Contain("BlueSage reconnect could not finish loading; returning to menu."));
        Assert.That(source, Does.Contain("_reconnectMenuFallbackInProgress"));
        Assert.That(source, Does.Contain("ClearReconnectMenuFallbackFlagSoon"));
        Assert.That(source, Does.Contain("ReconnectRuntimeHealthInput("));
        Assert.That(source, Does.Contain("gameRuntimeUsable: gameRuntimeUsable"));
        Assert.That(source, Does.Contain("StartCleanMenuReconnect($\"native {notificationStatus}\""));
    }

    [Test]
    public void TerminalCleanup_SkipsRedundantNativeReturn_AndDoesNotWaitOnItsOwnActiveGuard()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        Assert.That(source, Does.Contain("bool initialMenuReady = IsCleanMenuReadyForReconnect(requireBlueSageJoinIdle: false"));
        Assert.That(source, Does.Contain("if (!initialMenuReady)"));
        Assert.That(source, Does.Contain("initial menu state is already clean; skipped redundant native ReturnMenu"));
        Assert.That(source, Does.Contain("IsCleanMenuReadyForReconnect(requireBlueSageJoinIdle: false"));
        Assert.That(source, Does.Contain("terminal menu state is already clean; skipped redundant native ReturnMenu"));
        Assert.That(source, Does.Contain("if (!terminalMenuReady)"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage_QoL_Tweaks_Beta project root.");
    }
}
