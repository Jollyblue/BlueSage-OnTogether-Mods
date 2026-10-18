using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// UAT F3FAB760BEB5 (2026-10-17): vanilla's PlayerSteamIDs entry is self-reported by each joining
// client, so a modded client can claim any SID. On the host, PurrNet's SteamTransport knows the
// Steam-authenticated SID of every connection. That transport SID outranks Steam-lobby membership.
public sealed class SteamIdTruthPolicyTests
{
    private const string Claimed = "76561198000000001";
    private const ulong ClaimedValue = 76561198000000001UL;
    private const ulong Other = 76561198099999999UL;

    [Test]
    public void Host_transport_match_verifies_even_when_the_Steam_lobby_list_lacks_the_player()
    {
        SteamIdTruthState state = SteamIdTruthPolicy.Classify(Claimed, isLocalPlayer: false,
            transportAvailable: true, transportSteamId: ClaimedValue, isUniqueCurrentLobbyMember: false);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.TransportVerified));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.True);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Host_transport_mismatch_is_a_spoof_and_lobby_membership_never_overrides_it(bool inLobby)
    {
        SteamIdTruthState state = SteamIdTruthPolicy.Classify(Claimed, isLocalPlayer: false,
            transportAvailable: true, transportSteamId: Other, isUniqueCurrentLobbyMember: inLobby);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.TransportMismatch));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.False);
        });
    }

    [Test]
    public void Without_transport_truth_a_unique_Steam_lobby_member_is_corroborated()
    {
        SteamIdTruthState state = SteamIdTruthPolicy.Classify(Claimed, isLocalPlayer: false,
            transportAvailable: false, transportSteamId: 0UL, isUniqueCurrentLobbyMember: true);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.LobbyCorroborated));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.True);
        });
    }

    [Test]
    public void Without_transport_truth_or_lobby_membership_the_claim_stays_unconfirmed()
    {
        SteamIdTruthState state = SteamIdTruthPolicy.Classify(Claimed, isLocalPlayer: false,
            transportAvailable: false, transportSteamId: 0UL, isUniqueCurrentLobbyMember: false);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.Unconfirmed));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.False);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Host_transport_that_cannot_vouch_for_a_connection_never_falls_back_to_lobby_evidence(bool inLobby)
    {
        // Review 2026-10-17: a connection with no Steam identity yet is exactly the case transport
        // truth must refuse, or a spoofer claiming a not-yet-loaded member's SID would pass. A
        // systemic transport failure (every remote read 0) is handled by the caller turning
        // transportAvailable off, not by trusting each row's lobby membership.
        Assert.That(
            SteamIdTruthPolicy.Classify(Claimed, false, true, 0UL, inLobby),
            Is.EqualTo(SteamIdTruthState.Unconfirmed));
    }

    [Test]
    public void Local_player_row_is_trusted_as_self()
    {
        SteamIdTruthState state = SteamIdTruthPolicy.Classify(Claimed, isLocalPlayer: true,
            transportAvailable: true, transportSteamId: 0UL, isUniqueCurrentLobbyMember: false);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.LocalSelf));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.True);
        });
    }

    [TestCase("")]
    [TestCase("not-a-steam-id")]
    [TestCase("123")]
    public void Malformed_claim_is_never_trusted_even_as_local(string claim)
    {
        SteamIdTruthState state = SteamIdTruthPolicy.Classify(claim, isLocalPlayer: true,
            transportAvailable: true, transportSteamId: ClaimedValue, isUniqueCurrentLobbyMember: true);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.InvalidClaim));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.False);
        });
    }
}
