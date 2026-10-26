using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// UAT on candidate 861AEF4EFB58 (Blue, 2026-10-17): QoL menu hotkey toasts like Style Helper; the
// chat welcome shows on every lobby join (reconnects and lobby-hopping included); the welcome and
// guide link the Thunderstore README.
public sealed class UatRound2PolicyAndWiringTests
{
    [Test]
    public void Rejoining_the_same_lobby_is_a_new_welcome_because_the_game_scene_is_new()
    {
        string first = WelcomeLobbyKeyPolicy.Build(109775241234567890UL, sceneInstanceId: 1001, lobbyCode: "ABCD", hasVisibleLobby: true);
        string rejoin = WelcomeLobbyKeyPolicy.Build(109775241234567890UL, sceneInstanceId: 2002, lobbyCode: "ABCD", hasVisibleLobby: true);

        Assert.That(rejoin, Is.Not.EqualTo(first));
    }

    [Test]
    public void Hopping_between_lobbies_without_codes_is_a_new_welcome()
    {
        string a = WelcomeLobbyKeyPolicy.Build(109775240000000001UL, 1001, lobbyCode: "", hasVisibleLobby: true);
        string b = WelcomeLobbyKeyPolicy.Build(109775240000000002UL, 1001, lobbyCode: "", hasVisibleLobby: true);

        Assert.That(b, Is.Not.EqualTo(a));
    }

    [Test]
    public void The_same_lobby_and_scene_keeps_one_welcome()
    {
        Assert.That(
            WelcomeLobbyKeyPolicy.Build(109775241234567890UL, 1001, "ABCD", true),
            Is.EqualTo(WelcomeLobbyKeyPolicy.Build(109775241234567890UL, 1001, "ABCD", true)));
    }

    [Test]
    public void Without_a_Steam_lobby_id_the_code_or_visible_lobby_still_counts_per_scene()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WelcomeLobbyKeyPolicy.Build(0UL, 1001, "ABCD", false), Is.EqualTo("code:ABCD|scene:1001"));
            Assert.That(WelcomeLobbyKeyPolicy.Build(0UL, 1001, "", true), Is.EqualTo("visible|scene:1001"));
            Assert.That(WelcomeLobbyKeyPolicy.Build(0UL, 0, "", false), Is.Empty, "Not in a lobby.");
        });
    }

    [Test]
    public void Qol_menu_hotkey_opens_the_menu_without_a_toast()
    {
        string plugin = ReadSource("Plugin.cs");
        string toggle = Slice(plugin, "_qolMenuWindow.ToggleVisible();", "private void ToggleStyleUiFromHotkey()");

        Assert.Multiple(() =>
        {
            // Voice pass (2026-10-23) NOISE 4: the visible menu is the answer; the opened/closed toast was noise.
            Assert.That(toggle, Is.Not.Empty);
            Assert.That(toggle, Does.Not.Contain("ShowToast("));
            Assert.That(plugin, Does.Not.Contain("\"QoL menu opened.\""));
            Assert.That(plugin, Does.Not.Contain("\"QoL menu closed.\""));
            Assert.That(ReadSource("QolMenuWindow.cs"), Does.Contain("internal bool IsVisible => _visible;"));
        });
    }

    [Test]
    public void Welcome_detection_uses_the_Steam_lobby_and_game_scene()
    {
        string plugin = ReadSource("Plugin.cs");
        string detect = Slice(plugin, "private static bool TryGetCurrentLobbyKeyForWelcomeMessage(out string lobbyKey)", "private bool ShowWelcomeMessageForLobby(bool quietReconnectVisit)");

        Assert.Multiple(() =>
        {
            Assert.That(detect, Does.Contain("WelcomeLobbyKeyPolicy.Build("));
            Assert.That(detect, Does.Contain("TryGetRescueLobby("));
            Assert.That(detect, Does.Contain("MonoSingleton<MainSceneManager>.I"));
        });
    }

    [Test]
    public void Welcome_card_and_guide_link_the_Thunderstore_README()
    {
        string plugin = ReadSource("Plugin.cs");
        string welcome = Slice(plugin, "private void DrawWelcomeOverlayWindowContents(int windowId)", "internal void DismissWelcomeOverlay()");
        string guide = Slice(plugin, "private void DrawGuideOverlayWindowContents(int windowId)", "private BlueSageCommandResult HandleWelcomeOverlayCommand(");

        Assert.Multiple(() =>
        {
            Assert.That(HeaderActionLayoutPolicy.ThunderstoreUrl, Is.EqualTo("https://thunderstore.io/c/on-together/p/Blues/BlueSage_QoL_Tweaks_Beta/"));
            Assert.That(welcome, Does.Contain("Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl)"));
            Assert.That(guide, Does.Contain("Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl)"));
            Assert.That(ReadSource("QolMenuWindow.cs"), Does.Contain("Application.OpenURL(HeaderActionLayoutPolicy.ThunderstoreUrl)"));
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
