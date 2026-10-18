using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HarborUiContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void HarborIsTheSharedDefaultWithoutRemovingSavedThemeChoices()
    {
        string theme = Read("src", "BlueSageUiTheme.cs");
        string plugin = Read("src", "Plugin.cs");
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(theme, Does.Contain("DefaultThemeKey = \"BlueSage Harbor\""));
            Assert.That(theme, Does.Contain("\"Midnight Dock\""));
            Assert.That(theme, Does.Contain("\"Vanilla Cream OT\""));
            Assert.That(theme, Does.Contain("\"OG\""));
            Assert.That(plugin, Does.Contain("UiThemePreset = Config.Bind(\"Style Helper\", \"UiThemePreset\", publicDefaults.Theme"));
            Assert.That(menu, Does.Contain("Reset to BlueSage Harbor"));
            Assert.That(menu, Does.Contain("BlueSageUiTheme.Normalize(Plugin.UiThemePreset?.Value)"));
            Assert.That(theme, Does.Contain("HideFlags.HideAndDontSave"));
            Assert.That(theme, Does.Contain("RuntimeTextureGeneration"));
            Assert.That(theme, Does.Contain("ReleaseRuntimeTextures"));
            Assert.That(menu, Does.Contain("BlueSageUiTheme.AreRuntimeTexturesAlive()"));
        });
    }

    [Test]
    public void HarborUsesGroupedResponsiveCardsAndSemanticStates()
    {
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("Comfort & Movement"));
            Assert.That(menu, Does.Contain("Chat & Readability"));
            Assert.That(menu, Does.Contain("Identity & Safety"));
            Assert.That(menu, Does.Contain("Personalization"));
            Assert.That(menu, Does.Not.Contain("DrawFeatureSection(\"Large-Lobby Performance\""), "Render saver section retired 2026-10-17.");
            Assert.That(menu, Does.Contain("_windowRect.width >= 1320f ? 3 : (_windowRect.width >= 880f ? 2 : 1)"));
            Assert.That(menu, Does.Contain("_theme.ActiveAccent"));
            Assert.That(menu, Does.Contain("_theme.EnabledAccent"));
            Assert.That(menu, Does.Contain("_theme.DisabledAccent"));
            Assert.That(menu, Does.Contain("_theme.ExperimentalAccent"));
            Assert.That(menu, Does.Contain("_footerBoxStyle"));
            Assert.That(menu, Does.Contain("private const float FooterHeight = 120f;"));
        });
    }

    [Test]
    public void StyleHelperSharesHarborActiveTabsFieldsAndFooter()
    {
        string helper = Read("src", "StyleHelperWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(helper, Does.Contain("BlueSageUiTheme.CreateStateButtonStyle"));
            Assert.That(helper, Does.Contain("_theme.Panel"));
            Assert.That(helper, Does.Contain("_theme.FooterBackground"));
            Assert.That(helper, Does.Contain("_theme.FooterText"));
            Assert.That(helper, Does.Contain("private const float FooterHeight = 156f;"));
            Assert.That(helper, Does.Contain("private const float CompactFooterHeight = 198f;"));
            Assert.That(helper, Does.Contain("GUILayout.TextArea(_text ?? string.Empty"));
            Assert.That(helper, Does.Not.Contain("GUILayout.TextField(_text ?? string.Empty, Plugin.LockedMaxIdCardCharacters"));
        });
    }

    [Test]
    public void QolMenuReservesVerticalScrollbarWidthAndNeverDrawsAHorizontalRail()
    {
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("private const float ContentHorizontalChrome = 96f;"));
            Assert.That(menu, Does.Contain("private float GetContentLayoutWidth()"));
            Assert.That(menu, Does.Contain("GUILayout.BeginScrollView(_contentScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar"));
            Assert.That(menu, Does.Not.Contain("Mathf.Max(280f, _windowRect.width - 76f)"));
            Assert.That(Count(menu, "GetContentLayoutWidth()"), Is.GreaterThanOrEqualTo(5));
        });
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
