using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StyleMoodTimestampContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Public_ui_exposes_rich_text_history_custom_mood_and_time_format()
    {
        string style = File.ReadAllText(Path.Combine(Root, "src", "StyleHelperWindow.cs"));
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));

        Assert.That(style, Does.Contain("RichTextDraftPolicy.TryValidate"));
        Assert.That(style, Does.Contain("_undoHistory"));
        Assert.That(style, Does.Contain("_redoHistory"));
        Assert.That(style, Does.Contain("Custom label"));
        Assert.That(plugin, Does.Contain("HandleSetMoodCommand"));
        Assert.That(plugin, Does.Contain("token == \"12\""));
        Assert.That(plugin, Does.Contain("token == \"24\""));
        Assert.That(menu, Does.Contain("12-hour AM/PM"));
        Assert.That(menu, Does.Contain("24-hour"));
        Assert.That(plugin, Does.Contain("Spoons: reset to hidden default [5/5sp]"));
        Assert.That(plugin, Does.Contain("Timestamps: reset to enabled 12-hour AM/PM defaults"));
        Assert.That(plugin, Does.Contain("string.Equals(requested, \"reset\""));
    }
}
