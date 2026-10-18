using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Blue (2026-10-17): the player reduction / render saver experiment caused visual glitching, so the
// feature set is scrapped for now. It is retired behind one switch: nothing is hidden, the menu no
// longer offers it, and the dormant code can return later by flipping Retired.
public sealed class PlayerRenderSaverRetirementTests
{
    [Test]
    public void Render_saver_is_retired_and_off_for_fresh_installs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlayerRenderSaverPolicy.Retired, Is.True);
            Assert.That(PublicFirstRunDefaults.Current.PlayerRenderSaver, Is.False);
        });
    }

    [Test]
    public void Runtime_never_applies_or_starts_the_render_saver_while_retired()
    {
        string plugin = ReadSource("Plugin.cs");
        string start = Slice(plugin, "private void StartPlayerRenderSaverController()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("internal static bool ShouldApplyPlayerRenderSaver => !PlayerRenderSaverPolicy.Retired &&"));
            Assert.That(start, Does.Contain("if (PlayerRenderSaverPolicy.Retired)"));
        });
    }

    [Test]
    public void Menu_no_longer_offers_the_large_lobby_render_saver_section()
    {
        string menu = ReadSource("QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Not.Contain("DrawFeatureSection(\"Large-Lobby Performance\""));
            Assert.That(menu, Does.Not.Contain("DrawExperimentalToggle(\"Player Render Saver\""));
        });
    }

    [Test]
    public void Playersaver_command_explains_the_retirement_instead_of_acting()
    {
        string handler = Slice(ReadSource("Plugin.cs"), "private BlueSageCommandResult HandlePlayerRenderSaverCommand(", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(handler, Does.Contain("if (PlayerRenderSaverPolicy.Retired)"));
            Assert.That(handler, Does.Contain("PlayerRenderSaverPolicy.RetiredMessage"));
            Assert.That(PlayerRenderSaverPolicy.RetiredMessage, Does.Contain("retired"));
        });
    }

    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
