using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatusTemplateContractTests
{
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

    [Test]
    public void Style_templates_do_not_save_or_load_status_text()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.That(source, Does.Contain("Templates save name styling only"));
        Assert.That(source, Does.Contain("Load applies the saved name style right away"));
        Assert.That(source, Does.Contain("CaptureUndo(\"Load Template \" + index, restoreName: true)"));
        Assert.That(source, Does.Contain("PlayerPrefs.HasKey(prefix + \"Text\")"));
        Assert.That(source, Does.Contain("Template \" + index + \" is empty"));
        Assert.That(source, Does.Contain("live player data was left unchanged"));
        Assert.That(source, Does.Contain("Plugin.Instance.TryApplyStyledNameFromStyleUi(_preview, out applied)"));
        Assert.That(source, Does.Contain("Loaded and applied template"));
        Assert.That(source, Does.Not.Contain("PlayerPrefs.SetString(prefix + \"Status\""));
        Assert.That(source, Does.Not.Contain("PlayerPrefs.GetString(prefix + \"Status\""));
    }

    [Test]
    public void Brb_and_afk_status_shortcuts_are_registered()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string typeahead = File.ReadAllText(Path.Combine(ProjectRoot, "src", "CommandTypeaheadController.cs"));

        Assert.That(plugin, Does.Contain("\"brb\""));
        Assert.That(plugin, Does.Contain("\"afk\""));
        Assert.That(plugin, Does.Contain("\"back\""));
        Assert.That(plugin, Does.Contain("SetPresetStatus(\"BRB\")"));
        Assert.That(plugin, Does.Contain("SetPresetStatus(\"AFK\")"));
        Assert.That(plugin, Does.Contain("HandleBackStatusCommand"));
        Assert.That(plugin, Does.Contain("_lastStatusBeforePreset"));
        Assert.That(typeahead, Does.Contain("[\"brb\"]"));
        Assert.That(typeahead, Does.Contain("[\"afk\"]"));
        Assert.That(typeahead, Does.Contain("[\"back\"]"));
    }
}
