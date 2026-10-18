using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SpoonsBackdropContractTests
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
    public void Spoons_are_default_off_compact_and_menu_visible()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string styleHelper = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));
        string typeahead = File.ReadAllText(Path.Combine(ProjectRoot, "src", "CommandTypeaheadController.cs"));

        Assert.That(plugin, Does.Contain("EnableSpoons = Config.Bind(\"Spoons\", \"EnableSpoons\", false"));
        Assert.That(plugin, Does.Contain("SpoonCount = Config.Bind(\"Spoons\", \"SpoonCount\", 5"));
        Assert.That(plugin, Does.Not.Contain("MoodPreset"));
        Assert.That(plugin, Does.Not.Contain("CustomMood"));
        Assert.That(plugin, Does.Not.Contain("HandleMoodCommand"));
        Assert.That(plugin, Does.Not.Contain("\"mood\""));
        Assert.That(plugin, Does.Contain("SpoonsUpdatedUtc = Config.Bind(\"Spoons\", \"SpoonsUpdatedUtc\""));
        Assert.That(plugin, Does.Contain("private const string DefaultSpoonLabel = \"sp\""));
        Assert.That(plugin, Does.Contain("$\"[{spoons}/5{GetSpoonLabel(draft.SpoonLabel)}]\""));
        Assert.That(plugin, Does.Contain("\"spoons\""));
        Assert.That(plugin, Does.Contain("isHidden: true"));
        Assert.That(plugin, Does.Contain("HandleSpoonsCommand"));
        Assert.That(plugin, Does.Contain("BuildSpoonSuffix"));
        Assert.That(plugin, Does.Contain("ApplySpoonVisibilityFromMenu"));
        Assert.That(plugin, Does.Contain("Spoons={FormatToggleState(ShouldApplySpoons)}"));
        Assert.That(menu, Does.Contain("DrawSpoonsToggle();"));
        Assert.That(menu, Does.Contain("TrySetSpoonVisibilityFromMenu(requested, out applied)"));
        Assert.That(menu, Does.Not.Contain("DrawTextSetting(\"Spoons 0-5\""));
        Assert.That(styleHelper, Does.Contain("DrawSpoonsControls"));
        Assert.That(styleHelper, Does.Contain("SaveSpoonsFromStyleUi"));
        Assert.That(styleHelper, Does.Contain("Show Tag"));
        Assert.That(styleHelper, Does.Contain("Hide Tag"));
        Assert.That(styleHelper, Does.Contain("Undo"));
        Assert.That(styleHelper, Does.Contain("restoreSpoons: true"));
        Assert.That(styleHelper, Does.Contain("RestoreSpoons"));
        Assert.That(styleHelper, Does.Contain("SpoonsEnabled"));
        Assert.That(styleHelper, Does.Contain("SpoonsUpdatedUtc"));
        Assert.That(styleHelper, Does.Contain("Custom label"));
        Assert.That(styleHelper, Does.Not.Contain("Rewind"));
        Assert.That(menu, Does.Not.Contain("Revert"));
        Assert.That(typeahead, Does.Contain("[\"spoons\"]"));
        Assert.That(typeahead, Does.Contain("Choice(\"spoons\", \"off\""));
        Assert.That(typeahead, Does.Contain("Choice(\"spoons\", \"reset\""));
        Assert.That(typeahead, Does.Contain("[\"setmood\"]"));
        Assert.That(typeahead, Does.Contain("Choice(\"setmood\""));
        Assert.That(typeahead, Does.Contain("Choice(\"setmood\", \"reset\""));
        Assert.That(typeahead, Does.Contain("MaxBrowserSuggestions"));
        Assert.That(typeahead, Does.Contain("GUILayout.BeginScrollView"));
    }

    [Test]
    public void Persistent_chat_backdrop_is_separate_from_text_outline()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string typeahead = File.ReadAllText(Path.Combine(ProjectRoot, "src", "CommandTypeaheadController.cs"));
        string patch = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatReadabilityPatch.cs"));

        Assert.That(plugin, Does.Contain("EnablePersistentChatBackdrop = Config.Bind(\"Chat Readability\", \"EnablePersistentChatBackdrop\", true"));
        Assert.That(plugin, Does.Contain("ShouldApplyPersistentChatBackdrop"));
        Assert.That(plugin, Does.Contain("\"chatbackdrop\""));
        Assert.That(plugin, Does.Contain("new[] { \"chatbg\" }"));
        Assert.That(plugin, Does.Contain("ChatBackdrop={FormatToggleState(ShouldApplyPersistentChatBackdrop)}"));
        Assert.That(menu, Does.Contain("DrawToggle(\"Persistent Chat Backdrop\""));
        Assert.That(typeahead, Does.Contain("[\"chatbackdrop\"]"));
        Assert.That(typeahead, Does.Contain("[\"chatbg\"]"));
        Assert.That(patch, Does.Contain("Plugin.ShouldApplyPersistentChatBackdrop"));
        Assert.That(patch, Does.Contain("ApplyPersistentBackdrop"));
        Assert.That(patch, Does.Contain("RestorePersistentBackdrop"));
        Assert.That(patch, Does.Contain("RefreshVisibleChat"));
        Assert.That(patch, Does.Contain("RestoreReadability"));
        Assert.That(patch, Does.Contain("OriginalBackdropColors"));
        Assert.That(patch, Does.Contain("OriginalTextStates"));
        Assert.That(patch, Does.Contain("DarkOutlineColor = new Color(0f, 0f, 0f, 0.95f)"));
        Assert.That(patch, Does.Contain("LightOutlineColor = new Color(1f, 1f, 1f, 0.92f)"));
        Assert.That(patch, Does.Contain("gameObject.AddComponent<UiOutline>()"));
        Assert.That(patch, Does.Contain("gameObject.AddComponent<UiShadow>()"));
        Assert.That(patch, Does.Contain("ShaderUtilities.ID_OutlineWidth"));
        Assert.That(patch, Does.Contain("MaxTmpOutlineWidth"));
        Assert.That(patch, Does.Contain("DestroyOwnedMaterial"));
        Assert.That(patch, Does.Not.Contain("SendMessageAsync"));
    }
}
