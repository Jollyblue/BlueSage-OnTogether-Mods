using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class MidnightDockThemeContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void TextInputsOwnEveryInteractionStateInsteadOfStretchingTheGameSkin()
    {
        string theme = Read("src", "BlueSageUiTheme.cs");
        string menu = Read("src", "QolMenuWindow.cs");
        string helper = Read("src", "StyleHelperWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(theme, Does.Contain("CreateTextInputStyle"));
            Assert.That(theme, Does.Contain("ApplyState(style.normal"));
            Assert.That(theme, Does.Contain("ApplyState(style.hover"));
            Assert.That(theme, Does.Contain("ApplyState(style.active"));
            Assert.That(theme, Does.Contain("ApplyState(style.focused"));
            Assert.That(theme, Does.Contain("ApplyState(style.onFocused"));
            Assert.That(menu, Does.Contain("BlueSageUiTheme.CreateTextInputStyle"));
            Assert.That(helper, Does.Contain("BlueSageUiTheme.CreateTextInputStyle"));
            Assert.That(menu, Does.Not.Contain("GUI.skin.textField.normal.background"));
        });
    }

    [Test]
    public void ToggleStatesAndFeatureGroupsUseDistinctSemanticStyles()
    {
        string theme = Read("src", "BlueSageUiTheme.cs");
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(theme, Does.Contain("EnabledButtonText"));
            Assert.That(theme, Does.Contain("DisabledButtonText"));
            Assert.That(menu, Does.Contain("entry.Value ? _enabledButtonStyle : _disabledButtonStyle"));
            Assert.That(menu, Does.Contain("_experimentalButtonStyle, GUILayout.Height(32f)"), "Risky moderation previews keep the experimental tone.");
            Assert.That(menu, Does.Contain("FeatureSectionTone.Comfort"));
            Assert.That(menu, Does.Contain("FeatureSectionTone.Chat"));
            Assert.That(menu, Does.Contain("FeatureSectionTone.Identity"));
            Assert.That(menu, Does.Contain("FeatureSectionTone.Personalization"));
            Assert.That(menu, Does.Contain("FeatureSectionTone.Experimental"));
        });
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
