using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StyleHelperInputCapacityContractTests
{
    [Test]
    public void Name_editor_uses_configured_profile_limit_and_counts_invalid_draft_not_error_text()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "StyleHelperWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("GUILayout.TextArea(_text ?? string.Empty, _textFieldStyle"));
            Assert.That(source, Does.Contain("GUILayout.TextArea(_status ?? string.Empty, _textFieldStyle"));
            Assert.That(source, Does.Not.Contain("GUILayout.TextArea(_text ?? string.Empty, Plugin.LockedMaxIdCardCharacters"));
            Assert.That(source, Does.Not.Contain("GUILayout.TextArea(_status ?? string.Empty, Plugin.LockedMaxIdCardCharacters"));
            Assert.That(source, Does.Not.Contain("GUILayout.TextField(_text ?? string.Empty, 220"));
            Assert.That(source, Does.Not.Contain("GUILayout.TextField(_status ?? string.Empty, 80"));
            Assert.That(source, Does.Contain("_previewIsValid"));
            Assert.That(source, Does.Contain("(_text ?? string.Empty).Length"));
            Assert.That(source, Does.Contain("Rich-text length:").And.Contain("characters (tags count too)"));
        });
    }

    [Test]
    public void Status_apply_requires_a_valid_rich_text_preview()
    {
        string source = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "StyleHelperWindow.cs"));
        Assert.That(source, Does.Contain("if (_tab == 1 && _previewIsValid)"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
