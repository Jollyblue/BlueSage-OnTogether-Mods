using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatusManagerContractTests
{
    [Test]
    public void PluginRegistersBlueSageStatusCommands()
    {
        string pluginSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));

        Assert.That(pluginSource, Does.Contain("StatusBaseName = Config.Bind(\"Status Helper\", \"StatusBaseName\""));
        Assert.That(pluginSource, Does.Contain("StatusMessage = Config.Bind(\"Status Helper\", \"StatusMessage\""));
        Assert.That(pluginSource, Does.Contain("StatusColor = Config.Bind(\"Status Helper\", \"StatusColor\""));
        Assert.That(pluginSource, Does.Contain("\"setname\""));
        Assert.That(pluginSource, Does.Contain("\"status\""));
        Assert.That(pluginSource, Does.Contain("\"clearstatus\""));
        Assert.That(pluginSource, Does.Contain("\"statuscolor\""));
        Assert.That(pluginSource, Does.Contain("HandleSetNameCommand"));
        Assert.That(pluginSource, Does.Contain("HandleStatusCommand"));
        Assert.That(pluginSource, Does.Contain("ApplyStatusNameToPlayerData"));
        Assert.That(pluginSource, Does.Contain("MainCustomizationController.UpdatePlayerInfo"));
        Assert.That(pluginSource, Does.Contain("GetSteamPersonaName()"));
        Assert.That(pluginSource, Does.Contain("name reset to your current Steam persona name"));
        Assert.That(pluginSource, Does.Contain("Status Helper color: reset to #FFD45D"));
    }

    [Test]
    public void StatusCommandsAvoidDirectSaveFileWrites()
    {
        string pluginSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));

        Assert.That(pluginSource, Does.Not.Contain("PlayerData.txt"));
        Assert.That(pluginSource, Does.Not.Contain("File.Write"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find BlueSage_QoL_Tweaks_Beta project root.");
    }
}
