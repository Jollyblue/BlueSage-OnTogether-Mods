using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReconnectRc020ContractTests
{
    [Test]
    public void RcUsesNativeTeardownAndCleanMenuJoinOnly()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string policy = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Core", "ReconnectGuardPolicy.cs"));
        string terminalCleanupPolicy = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Core", "ReconnectTerminalCleanupPolicy.cs"));
        Assert.That(plugin, Does.Contain("native teardown must reach the clean menu before any join attempt"));
        Assert.That(plugin, Does.Contain("JoinByCode(targetLobbyCode, false)"));
        Assert.That(plugin, Does.Contain("activeSceneBuildIndex: activeScene.buildIndex"));
        Assert.That(terminalCleanupPolicy, Does.Contain("state.ActiveSceneBuildIndex == 1"));
        Assert.That(plugin, Does.Contain("nativeTeardownAlreadyStarting: true"));
        Assert.That(plugin, Does.Contain("return true;"));
        Assert.That(policy, Does.Contain("Native disconnect recovery accepted; the visible lobby count is already unavailable."));
        Assert.That(policy, Does.Not.Contain("Scheduling managed reconnect because visible lobby player count could not be confirmed."));
    }

    [Test]
    public void RcPlayableQuorumRejectsPartiallyInitializedScene()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        Assert.That(plugin, Does.Contain("activeScene.buildIndex != 2"));
        Assert.That(plugin, Does.Contain("!multiplayerManager.LobbyStatus"));
        Assert.That(plugin, Does.Contain("textChannelManager.MainPlayerController == null"));
        Assert.That(plugin, Does.Contain("localOccurrences == 1"));
        Assert.That(plugin, Does.Contain("LockedReconnectReadyStabilitySeconds = 2"));
        Assert.That(plugin, Does.Contain("Runtime probe:"));
    }

    [Test]
    public void RcContainsVerifiedInitializationFaultsWithoutHidingOtherExceptions()
    {
        string avatar = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "AvatarStyleSlotsPatch.cs"));
        string reflection = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "NetworkReflectionNullGuardPatch.cs"));
        Assert.That(avatar, Does.Contain("without interrupting CustomizationUI startup"));
        Assert.That(reflection, Does.Contain("ObserversRpc_Original_1"));
        Assert.That(reflection, Does.Contain("reflectedValues != null"));
        Assert.That(reflection, Does.Contain("return true;"));
        Assert.That(reflection, Does.Contain("return false;"));
    }

    private static string ProjectRoot
    {
        get
        {
            string current = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(current, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
            }
            return current;
        }
    }
}
