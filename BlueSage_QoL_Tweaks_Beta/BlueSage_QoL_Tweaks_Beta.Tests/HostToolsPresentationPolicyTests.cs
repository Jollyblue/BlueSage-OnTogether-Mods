using System;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HostToolsPresentationPolicyTests
{
    [Test]
    public void Healthy_host_gets_a_calm_ready_summary()
    {
        HostToolsPresentation result = Resolve();

        Assert.Multiple(() =>
        {
            Assert.That(result.Tone, Is.EqualTo(HostToolsTone.Ready));
            Assert.That(result.Heading, Is.EqualTo("Host tools are ready"));
            Assert.That(result.Meaning, Does.Contain("healthy for this lobby"));
            Assert.That(result.Advice, Does.Contain("No action needed").And.Contain("have fun"));
            Assert.That(result.PlayerSummary, Is.EqualTo("Players matched safely: 5"));
            Assert.That(result.LargeLobbySummary, Is.EqualTo("Large-lobby support: Ready - up to 128 players"));
            Assert.That(result.LargeLobbyChatSummary, Is.EqualTo("In-game large-lobby chat: Ready"));
        });
    }

    [Test]
    public void Incomplete_player_matching_is_calm_and_actionable()
    {
        HostToolsPresentation result = Resolve(rosterReady: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Tone, Is.EqualTo(HostToolsTone.Checking));
            Assert.That(result.Heading, Is.EqualTo("Checking this lobby..."));
            Assert.That(result.Meaning, Does.Contain("matching visible player rows"));
            Assert.That(result.Advice, Does.Contain("Wait a few seconds")
                .And.Contain("close and reopen the player list")
                .And.Contain("Players already matched can still be managed")
                .And.Contain("stays protected until they are"), "Round 5e: per-row, not lobby-wide.");
            Assert.That(result.PlayerSummary, Is.EqualTo("Players matched safely: 5 - checking for updates"));
        });
    }

    [Test]
    public void Automatic_cleanup_issue_explains_the_safe_manual_path()
    {
        HostToolsPresentation result = Resolve(cleanupHealthy: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Tone, Is.EqualTo(HostToolsTone.Attention));
            Assert.That(result.Heading, Is.EqualTo("Some host tools need attention"));
            Assert.That(result.Meaning, Does.Contain("Automatic cleanup is unavailable")
                .And.Contain("does not close the lobby"));
            Assert.That(result.Advice, Does.Contain("Run Cleanup Now")
                .And.Contain("manual cleanup"));
            Assert.That(result.CleanupSummary, Is.EqualTo("Automatic cleanup: Needs attention"));
        });
    }

    [Test]
    public void Reconnect_issue_explains_how_to_recover_without_recreating_the_lobby()
    {
        HostToolsPresentation result = Resolve(reconnectHealthy: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Tone, Is.EqualTo(HostToolsTone.Attention));
            Assert.That(result.Meaning, Does.Contain("Reconnect protection is unavailable")
                .And.Contain("Normal hosting can continue"));
            Assert.That(result.Advice, Does.Contain("main menu")
                .And.Contain("restart the game"));
            Assert.That(result.ReconnectSummary, Is.EqualTo("Reconnect protection: Needs attention"));
        });
    }

    [Test]
    public void Invalid_loaded_playerlimit_gives_duplicate_mod_and_restart_guidance()
    {
        HostToolsPresentation result = Resolve(
            playerLimit: PlayerLimitCompanionStatus.FromLoadedValues(null, null, null, null));

        Assert.Multiple(() =>
        {
            Assert.That(result.Tone, Is.EqualTo(HostToolsTone.Attention));
            Assert.That(result.Meaning, Does.Contain("Large-lobby capacity could not be confirmed")
                .And.Contain("Ordinary hosting remains available"));
            Assert.That(result.Advice, Does.Contain("only one big-lobby mod")
                .And.Contain("restart the game"));
            Assert.That(result.LargeLobbySummary, Is.EqualTo("Large-lobby support: Needs attention"));
        });
    }

    [Test]
    public void Disabled_optional_features_are_choices_not_failures()
    {
        HostToolsPresentation result = Resolve(
            autoCleanupEnabled: false,
            reconnectEnabled: false,
            playerLimit: PlayerLimitCompanionStatus.NotLoaded());

        Assert.Multiple(() =>
        {
            Assert.That(result.Tone, Is.EqualTo(HostToolsTone.Ready));
            Assert.That(result.Heading, Is.EqualTo("Host tools are ready"));
            Assert.That(result.CleanupSummary, Is.EqualTo("Automatic cleanup: Off by choice - Run Cleanup Now remains available"));
            Assert.That(result.ReconnectSummary, Is.EqualTo("Reconnect protection: Off by choice"));
            Assert.That(result.LargeLobbySummary, Is.EqualTo("Large-lobby support: Not installed - standard lobby hosting is available"));
            Assert.That(result.LargeLobbyChatSummary, Is.EqualTo("In-game large-lobby chat: Standard game behavior"));
        });
    }

    [Test]
    public void Non_host_gets_plain_navigation_guidance()
    {
        HostToolsPresentation result = Resolve(isHost: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Tone, Is.EqualTo(HostToolsTone.Unavailable));
            Assert.That(result.Heading, Is.EqualTo("Host tools are available to the lobby host"));
            Assert.That(result.Advice, Does.Contain("create a lobby as host"));
        });
    }

    [Test]
    public void Primary_public_copy_never_exposes_private_or_engineering_vocabulary()
    {
        HostToolsPresentation result = Resolve(rosterReady: false);
        string publicCopy = string.Join(" ", new[]
        {
            result.Heading,
            result.Meaning,
            result.Advice,
            result.PlayerSummary,
            result.CleanupSummary,
            result.ReconnectSummary,
            result.LargeLobbySummary,
            result.LargeLobbyChatSummary
        });

        Assert.Multiple(() =>
        {
            Assert.That(publicCopy, Does.Not.Contain("24/7").IgnoreCase);
            Assert.That(publicCopy, Does.Not.Contain("degraded").IgnoreCase);
            Assert.That(publicCopy, Does.Not.Contain("converged").IgnoreCase);
            Assert.That(publicCopy, Does.Not.Contain("provider").IgnoreCase);
            Assert.That(publicCopy, Does.Not.Contain("relay").IgnoreCase);
            Assert.That(publicCopy, Does.Not.Contain("Discord").IgnoreCase);
            Assert.That(publicCopy, Does.Not.Contain("native UI").IgnoreCase);
            Assert.That(publicCopy, Does.Not.Contain("PlayerLimit=").IgnoreCase);
        });
    }

    private static HostToolsPresentation Resolve(
        bool isHost = true,
        bool rosterReady = true,
        bool cleanupHealthy = true,
        bool reconnectHealthy = true,
        bool autoCleanupEnabled = true,
        bool reconnectEnabled = true,
        PlayerLimitCompanionStatus? playerLimit = null)
    {
        PlayerLimitCompanionStatus currentPlayerLimit = playerLimit ??
            PlayerLimitCompanionStatus.FromLoadedValues(128, 64, 16, true);
        bool playerLimitHealthy = !currentPlayerLimit.IsLoaded ||
            (currentPlayerLimit.EffectiveMaxLobbySize.GetValueOrDefault() > 0 &&
             currentPlayerLimit.EffectiveDefaultLobbySize.GetValueOrDefault() > 0 &&
             currentPlayerLimit.EffectiveShiftSkipRate.GetValueOrDefault() > 0 &&
             currentPlayerLimit.ChatRelayPatchEnabled.HasValue);
        HostConsoleSnapshot snapshot = HostConsoleReadinessPolicy.Resolve(
            isHost,
            rosterReady,
            cleanupHealthy,
            reconnectHealthy,
            playerLimitHealthy,
            providerRequired: false,
            providerHealthy: false,
            relayRequired: false,
            relayHealthy: false);

        return HostToolsPresentationPolicy.Resolve(
            snapshot,
            verifiedRosterCount: 5,
            currentPlayerLimit,
            autoCleanupEnabled,
            manualCleanupAvailable: true,
            reconnectEnabled);
    }
}
