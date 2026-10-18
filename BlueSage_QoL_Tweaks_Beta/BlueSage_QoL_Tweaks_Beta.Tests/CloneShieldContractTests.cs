using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CloneShieldContractTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Test]
    public void Clone_shield_publishes_bounded_victim_side_alerts_without_host_dependency()
    {
        string controller = File.ReadAllText(Path.Combine(ProjectRoot, "src", "CloneShieldController.cs"));
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string readme = File.ReadAllText(Path.Combine(ProjectRoot, "README.md"));

        Assert.That(controller, Does.Contain("PlayerTransforms"));
        Assert.That(controller, Does.Contain("PlayerCustomizationController"));
        Assert.That(controller, Does.Contain("CustomizationDataIDs3"));
        Assert.That(controller, Does.Contain("AppearanceToShapeAndColors"));
        Assert.That(controller, Does.Contain("OutfitToShapeAndColors"));
        Assert.That(controller, Does.Contain("_customizationData"));
        Assert.That(controller, Does.Contain("BuildCustomizationFingerprint"));
        Assert.That(controller, Does.Contain("avatarMatches"));
        Assert.That(controller, Does.Contain("exactStyledNameMatches"));
        Assert.That(controller, Does.Contain("StringComparison.Ordinal"));
        Assert.That(controller, Does.Contain("CloneTransitionPolicy.Evaluate"));
        Assert.That(controller, Does.Contain("coverageComplete"));
        Assert.That(controller, Does.Contain("GetVerifiedCurrentSteamIds(out string verificationError, out bool coverageComplete)"));
        Assert.That(controller, Does.Contain("TryGetActiveLobbyCode"));
        Assert.That(controller, Does.Not.Contain("NetworkManager.main.isHost"));
        Assert.That(controller, Does.Not.Contain("SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID()"));
        Assert.That(controller, Does.Contain("string.Equals(victim.SteamId, localSteamId, StringComparison.Ordinal)"));
        Assert.That(controller, Does.Contain("Plugin.TrySendLobbyChatMessage(message)"));
        Assert.That(controller, Does.Contain("was potentially cloned by"));
        Assert.That(controller, Does.Contain("Be aware of the player list, Steam ID, and profile"));
        Assert.That(controller, Does.Contain("in case this alert is incorrect"));
        Assert.That(controller, Does.Contain("ID ..."));
        Assert.That(controller, Does.Contain("WarningCooldownSeconds = 300f"));
        Assert.That(controller, Does.Contain("_previousRoster.Count > 0"));
        Assert.That(plugin, Does.Contain("public alerts when someone newly copies your avatar or styled name"));
        Assert.That(menu, Does.Contain("exact styled name"));
        Assert.That(readme, Does.Contain("look-alike names for a host to review"));
        Assert.That(readme, Does.Contain("warnings, not proof, and can be cleared"));
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
