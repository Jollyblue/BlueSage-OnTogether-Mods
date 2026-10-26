using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Voice pass (2026-10-23) NOISE 1 + 2: a Reconnect Guard return to the SAME lobby skips the welcome line, the long
// Tip line and the host's pinned message; the long Tip line shows on the first lobby visit of a game session only.
public sealed class WelcomeRepeatPolicyTests
{
    private const string Lobby = "109775241234567890";
    private const string OtherLobby = "109775249999999999";

    [TestCase("steam:" + Lobby + "|scene:42", Lobby)]
    [TestCase("code:" + Lobby + "|scene:42", Lobby)]
    [TestCase("visible|scene:42", "")]
    [TestCase("", "")]
    [TestCase(null, "")]
    [TestCase("steam:" + Lobby, "")]
    [TestCase("other:" + Lobby + "|scene:42", "")]
    public void Lobby_id_comes_from_steam_or_code_visit_keys_only(string? visitKey, string expected)
    {
        Assert.That(WelcomeRepeatPolicy.LobbyIdOf(visitKey), Is.EqualTo(expected));
    }

    [Test]
    public void Lobby_id_matches_the_welcome_key_builder()
    {
        string key = WelcomeLobbyKeyPolicy.Build(ulong.Parse(Lobby), 7, null, false);
        Assert.That(WelcomeRepeatPolicy.LobbyIdOf(key), Is.EqualTo(Lobby));
    }

    [Test]
    public void Reconnect_into_the_same_lobby_is_quiet()
    {
        Assert.That(WelcomeRepeatPolicy.IsQuietReconnectReturn("steam:" + Lobby + "|scene:9", Lobby, Lobby), Is.True);
        Assert.That(WelcomeRepeatPolicy.IsQuietReconnectReturn("code:" + Lobby + "|scene:9", " " + Lobby + " ", Lobby), Is.True, "The join code may carry spaces.");
    }

    [TestCase("steam:" + Lobby + "|scene:9", "", Lobby, TestName = "Manual_join_without_a_reconnect_shows_the_welcome")]
    [TestCase("steam:" + OtherLobby + "|scene:9", OtherLobby, Lobby, TestName = "Replacement_lobby_from_the_same_host_shows_the_welcome")]
    [TestCase("steam:" + OtherLobby + "|scene:9", Lobby, Lobby, TestName = "Landing_somewhere_else_shows_the_welcome")]
    [TestCase("steam:" + Lobby + "|scene:9", Lobby, "", TestName = "No_known_lobby_before_the_drop_shows_the_welcome")]
    [TestCase("visible|scene:9", Lobby, Lobby, TestName = "A_visit_without_a_lobby_id_is_never_quiet")]
    [TestCase("", Lobby, Lobby, TestName = "Not_in_a_lobby_is_never_quiet")]
    public void Anything_but_a_same_lobby_reconnect_return_is_not_quiet(string visitKey, string target, string beforeDrop)
    {
        Assert.That(WelcomeRepeatPolicy.IsQuietReconnectReturn(visitKey, target, beforeDrop), Is.False);
    }

    [Test]
    public void Quiet_return_skips_welcome_tip_and_host_message()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WelcomeRepeatPolicy.ShouldShowWelcome(quietReconnectReturn: true), Is.False);
            Assert.That(WelcomeRepeatPolicy.ShouldShowTip(tipShownThisSession: false, quietReconnectReturn: true), Is.False);
            Assert.That(WelcomeRepeatPolicy.ShouldShowHostMessage(quietReconnectReturn: true), Is.False);
            Assert.That(WelcomeRepeatPolicy.ShouldShowWelcome(quietReconnectReturn: false), Is.True);
            Assert.That(WelcomeRepeatPolicy.ShouldShowHostMessage(quietReconnectReturn: false), Is.True);
        });
    }

    [TestCase(false, true, TestName = "First_visit_of_the_session_shows_the_Tip")]
    [TestCase(true, false, TestName = "Later_visits_skip_the_Tip")]
    public void Tip_shows_once_per_game_session(bool tipShownThisSession, bool expected)
    {
        Assert.That(WelcomeRepeatPolicy.ShouldShowTip(tipShownThisSession, quietReconnectReturn: false), Is.EqualTo(expected));
    }
}
