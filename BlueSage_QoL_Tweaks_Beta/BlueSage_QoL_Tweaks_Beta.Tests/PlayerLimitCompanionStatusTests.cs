using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerLimitCompanionStatusTests
{
    [Test]
    public void NotLoaded_HasQuietDiagnosticSummary()
    {
        PlayerLimitCompanionStatus status = PlayerLimitCompanionStatus.NotLoaded();

        Assert.That(status.IsLoaded, Is.False);
        Assert.That(status.ToDiagnosticText(), Is.EqualTo("PlayerLimit=not loaded"));
    }

    [Test]
    public void FromLoadedValues_FormatsHostHealthSummary()
    {
        PlayerLimitCompanionStatus status = PlayerLimitCompanionStatus.FromLoadedValues(
            effectiveMaxLobbySize: 128,
            effectiveDefaultLobbySize: 64,
            effectiveShiftSkipRate: 16,
            chatRelayPatchEnabled: true);

        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.ToDiagnosticText(), Is.EqualTo("PlayerLimit=loaded lobby=? pllMax=128 default=64 shift=16 chatRelay=on"));
    }

    // Blue, 2026-10-17: Host Health said "cap=128" while his lobby showed 1/111. 128 is
    // PlayerLimitLift's ceiling (MaxLobbySize); the lobby itself has 112 Steam slots and the game
    // shows MaxPlayers - 1. Report the real lobby and label the ceiling as such.
    [Test]
    public void Host_health_reports_the_real_lobby_count_the_game_shows_and_names_the_ceiling()
    {
        PlayerLimitCompanionStatus status = PlayerLimitCompanionStatus.FromLoadedValues(128, 16, 16, true)
            .WithLobby(memberCount: 1, steamMaxPlayers: 112);

        Assert.That(status.ToDiagnosticText(), Is.EqualTo("PlayerLimit=loaded lobby=1/111 pllMax=128 default=16 shift=16 chatRelay=on"));
    }

    [TestCase(null, 112, "lobby=?/111")]
    [TestCase(5, null, "lobby=5/?")]
    [TestCase(1, 1, "lobby=1/?")]
    public void Partial_lobby_readings_never_invent_numbers(int? members, int? steamMax, string expected)
    {
        string text = PlayerLimitCompanionStatus.FromLoadedValues(128, 16, 16, true).WithLobby(members, steamMax).ToDiagnosticText();

        Assert.That(text, Does.Contain(expected + " pllMax=128"));
    }

    [Test]
    public void Host_health_and_host_tools_use_the_status_with_the_live_lobby()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        // Round 4 WS2: the third caller was the removed hidden diagnostic snapshot; Host Health and Host Tools remain.
        Assert.That(plugin.Split(new[] { "CapturePlayerLimitCompanionStatusWithLobby();" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(2));
    }

    [Test]
    public void FromLoadedValues_ToleratesUnknownReflectionValues()
    {
        PlayerLimitCompanionStatus status = PlayerLimitCompanionStatus.FromLoadedValues(
            effectiveMaxLobbySize: null,
            effectiveDefaultLobbySize: null,
            effectiveShiftSkipRate: null,
            chatRelayPatchEnabled: null);

        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.ToDiagnosticText(), Is.EqualTo("PlayerLimit=loaded lobby=? pllMax=? default=? shift=? chatRelay=?"));
    }
}
