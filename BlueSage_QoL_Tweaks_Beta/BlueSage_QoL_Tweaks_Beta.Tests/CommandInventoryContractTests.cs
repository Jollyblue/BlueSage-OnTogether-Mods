using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed partial class CommandInventoryContractTests
{
    [Test]
    public void StatusFamilyUsesProspectiveDraftBeforeMutationAndNativePersistence()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        Assert.That(source, Does.Contain("CaptureDisplayNameDraft(captureLiveBase: true)"));
        Assert.That(source, Does.Contain("DisplayNameMutationPolicy.TryCompose"));
        Assert.That(source.IndexOf("DisplayNameMutationPolicy.TryCompose", System.StringComparison.Ordinal), Is.LessThan(source.IndexOf("dataManager.PlayerData.Name = displayName", System.StringComparison.Ordinal)));
        Assert.That(source.IndexOf("dataManager.PlayerData.Name = displayName", System.StringComparison.Ordinal), Is.LessThan(source.IndexOf("CommitDisplayNameDraft(draft);", System.StringComparison.Ordinal)));
        Assert.That(source, Does.Contain("dataManager.SavePlayerData();"));
        Assert.That(source, Does.Not.Contain("preferLiveBaseName"));
    }

    [Test]
    public void BothPublicWindowHeadersDisplayTheRuntimePluginVersion()
    {
        string qol = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string style = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));
        Assert.That(qol, Does.Contain("BlueSage QoL Menu  v{Plugin.PluginDisplayVersion}"));
        Assert.That(style, Does.Contain("BlueSage Style Helper  v{Plugin.PluginDisplayVersion}"));
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
