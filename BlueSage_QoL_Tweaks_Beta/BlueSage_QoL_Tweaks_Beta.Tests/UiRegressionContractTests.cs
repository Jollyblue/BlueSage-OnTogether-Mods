using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
public sealed class UiRegressionContractTests
{
    [Test]
    public void PlayerRenderSaverIsHonestAboutItsDefaultAndLocalOnlyScope()
    {
        string menu = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "src", "QolMenuWindow.cs"));
        string readme = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "README.md"));

        // Player Render Saver retired 2026-10-17: the menu must not describe it as a live feature.
        Assert.That(menu, Does.Not.Contain("starts On for fresh installs"));
        Assert.That(menu, Does.Not.Contain("Render saver radius"));
        Assert.That(menu, Does.Not.Contain("EXPERIMENTAL - Avatar Presets 4-9"));
        Assert.That(menu, Does.Not.Contain("EXPERIMENTAL - Chalkboard Persistence"));
        Assert.That(readme, Does.Not.Contain("Player Render Saver start on"), "Retired 2026-10-17; the README must not promise it.");
    }
    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName));
    }

    [Test]
    public void Qol_status_keeps_verbose_output_out_of_the_fixed_footer()
    {
        string source = ReadSource("QolMenuWindow.cs");
        Assert.That(source, Does.Contain("_message = Plugin.Instance.RunQolStatusFromQolMenu();"));
        Assert.That(source, Does.Not.Contain("_message = Plugin.Instance != null ? Plugin.Instance.RunQolStatusFromQolMenu()"));

        string plugin = ReadSource("Plugin.cs");
        Assert.That(plugin, Does.Contain("string status = BuildQolStatusMessage();"));
        Assert.That(plugin, Does.Contain("return AddLocalNotification(status)"));
        Assert.That(plugin, Does.Contain("QoL status was sent to your local chat."));
        Assert.That(source, Does.Contain("ContentChromeHeight = FooterHeight + 152f"));
    }

    [Test]
    public void Customize_undo_restores_the_value_from_before_the_last_save()
    {
        string source = ReadSource("QolMenuWindow.cs");
        Assert.That(source, Does.Contain("_settingUndoValues[label] = savedBefore"));
        Assert.That(source, Does.Contain("_settingUndoValues.TryGetValue(label, out string valueBeforeLastSave)"));
        Assert.That(source, Does.Contain("value = valueBeforeLastSave;"));
        Assert.That(source, Does.Contain("saveAction();"));
        Assert.That(source, Does.Contain("_settingUndoValues.Remove(label)"));
    }

    [Test]
    public void Timestamp_buttons_are_compact_equal_width_neighbors()
    {
        string source = ReadSource("QolMenuWindow.cs");
        Assert.That(source, Does.Contain("\"12-hour AM/PM\"").And.Contain("GUILayout.ExpandWidth(true)"));
        Assert.That(source, Does.Contain("GUILayout.Space(6f)"));
        Assert.That(source, Does.Contain("\"24-hour\"").And.Contain("GUILayout.Height(30f)"));
        Assert.That(source, Does.Not.Contain("GUILayout.Width(142f)"));
    }

    [Test]
    public void Slash_popup_wraps_rows_inside_a_screen_fitted_viewport()
    {
        string source = ReadSource("CommandTypeaheadController.cs");
        Assert.That(source, Does.Contain("Mathf.Clamp(Screen.width - 24f, 300f, 560f)"));
        Assert.That(source, Does.Contain("GUILayout.Width(width - 24f)"));
        Assert.That(source, Does.Contain("GUILayout.Width(width - 48f)"));
        Assert.That(source, Does.Contain("GUILayout.Height(36f)"));
        Assert.That(source, Does.Contain("wordWrap = true"));
        Assert.That(source, Does.Contain("clipping = TextClipping.Overflow"));
        Assert.That(source, Does.Not.Contain("BeginScrollView(_scrollPosition, true"));
    }
}
