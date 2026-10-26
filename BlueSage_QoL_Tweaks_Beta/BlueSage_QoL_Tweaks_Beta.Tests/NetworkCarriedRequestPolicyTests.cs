using System;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage I (WS4 closure, Blue 2026-10-20): a Helper who rejoined through a reconnect mod is in the game but
// not in Steam's lobby list, so a member-data request could never reach the host. Hush and TogetherPlus carry
// delegate actions over the game connection; so do we now, but validated on the host by the authoritative network
// sender, never by anything inside the packet, with the same nonce, freshness and rate limit as member data.
public sealed class NetworkCarriedRequestPolicyTests
{
    private const string Claimed = "76561198000000001";

    [Test]
    public void Sender_must_map_to_exactly_one_live_roster_row()
    {
        Assert.Multiple(() =>
        {
            Assert.That(NetworkCarriedRequestPolicy.Decide(0, Claimed, 1, 0UL, true).Accepted, Is.False);
            Assert.That(NetworkCarriedRequestPolicy.Decide(0, Claimed, 1, 0UL, true).Reason, Does.Contain("no live roster row"));
            Assert.That(NetworkCarriedRequestPolicy.Decide(2, Claimed, 1, 0UL, true).Accepted, Is.False);
            Assert.That(NetworkCarriedRequestPolicy.Decide(2, Claimed, 1, 0UL, true).Reason, Does.Contain("more than one"));
        });
    }

    // Advisor at the I gate: one row per sender is not enough. A second client that joined claiming the Helper's
    // Steam ID has its own PlayerID and would pass; the claimed ID must have exactly one live row, the same rule the
    // ban path applies to a Reconnected target.
    [Test]
    public void Claimed_steam_id_must_have_exactly_one_live_row_in_the_lobby()
    {
        Assert.Multiple(() =>
        {
            Assert.That(NetworkCarriedRequestPolicy.Decide(1, Claimed, 0, 0UL, true).Accepted, Is.False);
            Assert.That(NetworkCarriedRequestPolicy.Decide(1, Claimed, 0, 0UL, true).Reason, Does.Contain("no live row carries"));
            Assert.That(NetworkCarriedRequestPolicy.Decide(1, Claimed, 2, 0UL, true).Accepted, Is.False);
            Assert.That(NetworkCarriedRequestPolicy.Decide(1, Claimed, 2, 0UL, true).Reason, Does.Contain("more than one live row carries"));
            Assert.That(NetworkCarriedRequestPolicy.Decide(1, Claimed, 1, 0UL, true).Accepted, Is.True);
        });
    }

    [Test]
    public void Claimed_steam_id_must_be_a_steam_id_64()
    {
        Assert.That(NetworkCarriedRequestPolicy.Decide(1, "not-an-id", 1, 0UL, true).Accepted, Is.False);
    }

    [Test]
    public void Transport_steam_id_when_known_must_match_the_row()
    {
        NetworkCarriedRequestDecision mismatch = NetworkCarriedRequestPolicy.Decide(1, Claimed, 1, 76561198000000002UL, true);
        NetworkCarriedRequestDecision match = NetworkCarriedRequestPolicy.Decide(1, Claimed, 1, 76561198000000001UL, true);
        Assert.Multiple(() =>
        {
            Assert.That(mismatch.Accepted, Is.False);
            Assert.That(mismatch.Reason, Does.Contain("transport"));
            Assert.That(match.Accepted, Is.True);
            Assert.That(match.Surface, Is.EqualTo("helper relay (game connection)"));
        });
    }

    [Test]
    public void Unknown_transport_is_accepted_and_a_reconnected_issuer_is_labeled_in_the_surface()
    {
        NetworkCarriedRequestDecision inLobby = NetworkCarriedRequestPolicy.Decide(1, Claimed, 1, 0UL, senderInSteamLobby: true);
        NetworkCarriedRequestDecision reconnected = NetworkCarriedRequestPolicy.Decide(1, Claimed, 1, 0UL, senderInSteamLobby: false);
        Assert.Multiple(() =>
        {
            Assert.That(inLobby.Accepted, Is.True);
            Assert.That(inLobby.Surface, Is.EqualTo("helper relay (game connection)"));
            Assert.That(reconnected.Accepted, Is.True);
            Assert.That(reconnected.Surface, Is.EqualTo("helper relay (game connection, issuer not in Steam list)"));
        });
    }

    [Test]
    public void Network_transport_is_advertised_under_its_own_key_and_the_v3_capability_string_is_untouched()
    {
        string value = ModerationCapabilityPolicy.BuildNetworkTransport("76561198000000009", 1000);
        Assert.Multiple(() =>
        {
            Assert.That(value, Is.EqualTo("76561198000000009|net-v1-chat|1000"));
            Assert.That(ModerationCapabilityPolicy.IsNetworkTransportFresh(value, "76561198000000009", 1010), Is.True);
            Assert.That(ModerationCapabilityPolicy.IsNetworkTransportFresh(value, "76561198000000009", 1030), Is.False, "same 20 s lease as the capability");
            Assert.That(ModerationCapabilityPolicy.IsNetworkTransportFresh(value, "76561198000000008", 1010), Is.False);
            Assert.That(ModerationCapabilityPolicy.IsNetworkTransportFresh("76561198000000009|v3-memberdata|1000", "76561198000000009", 1010), Is.False, "the member-data capability never implies the network path");
            Assert.That(ModerationCapabilityPolicy.Build("76561198000000009", 1000), Is.EqualTo("76561198000000009|v3-memberdata|1000"), "0.2.5 Helpers match this string exactly; changing it would lock them out");
        });
    }

    [Test]
    public void Packet_reuses_the_legacy_prefix_so_an_older_host_swallows_it_instead_of_relaying_it_to_chat()
    {
        string packet = ModerationProtocolPolicy.Build("kick", "76561198000000002", "none", 1700000000, "ab12CD34ef");
        Assert.Multiple(() =>
        {
            Assert.That(packet, Does.StartWith("!bsmod:v1:"));
            Assert.That(ModerationProtocolPolicy.Prefix, Is.EqualTo("!bsmod:v1:"), "a 0.2.5 host's relay patch recognizes exactly this prefix and never relays it");
            Assert.That(ModerationProtocolPolicy.TryParse(packet, out ModerationRequest request), Is.True);
            Assert.That(request.Action, Is.EqualTo("kick"));
        });
    }

    [Test]
    public void Host_relay_accepts_the_packet_through_the_shared_nonce_and_rate_limit_and_never_relays_it()
    {
        string patch = ReadSource("src", "Patches", "ModerationRelayPatch.cs");
        string controller = ReadSource("src", "SteamIdModerationController.cs");
        string accept = MethodBody(controller, "internal static bool TryAcceptNetworkCarriedRequest(RPCInfo info, ModerationRequest request)");
        string send = MethodBody(controller, "internal static bool TrySendHelperRequest(string action, string targetSteamId, string incidentId, out string message)");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("ModerationProtocolPolicy.TryParse(text, out ModerationRequest request)"));
            Assert.That(patch, Does.Contain("SteamIdModerationController.TryAcceptNetworkCarriedRequest(info, request)"));
            Assert.That(patch, Does.Contain("Rejected a legacy chat-shaped BlueSage moderation request"), "an unparseable or refused packet still logs once and is never relayed");
            Assert.That(accept, Does.Contain("TryGetOwnedHostLobby("));
            Assert.That(accept, Does.Contain("SteamIdTruthReader.ReadTransportSteamId(info.sender)"));
            Assert.That(accept, Does.Contain("NetworkCarriedRequestPolicy.Decide("));
            Assert.That(accept, Does.Contain("ModerationProtocolPolicy.IsFresh(request, now)"));
            Assert.That(accept, Does.Contain("ModerationAuthorizationPolicy.IsAuthorizedIssuer("));
            Assert.That(accept, Does.Contain("RememberNonce(request.Nonce)"), "nonce memory is shared with the member-data path");
            Assert.That(accept, Does.Contain("LastIssuerRequest"), "rate limit is shared with the member-data path");
            Assert.That(accept, Does.Contain("HostJournalController.BeginSurface(decision.Surface)"));
            Assert.That(accept, Does.Contain("TryCountLiveTargetRows(panel, claimedSteamId,"), "the claimed Steam ID must have exactly one live row (advisor, I gate)");
            Assert.That(accept, Does.Contain("RecordNetworkRefusal("), "refusals are journal rows, the abuse monitor Blue asked for");
            Assert.That(controller, Does.Contain("private const int NetworkRefusalIntervalSeconds = 10;"), "refusal rows and warnings are rate-limited per sender");
            Assert.That(MethodBody(controller, "private static void RecordNetworkRefusal(string senderKey, string claimedSteamId, string targetSteamId, string reason)"), Does.Contain("HostJournalController.RecordAction("));
            Assert.That(send, Does.Contain("ModerationCapabilityPolicy.IsNetworkTransportFresh("), "the chat packet goes only to a host that advertises the network path");
            Assert.That(send, Does.Contain("Plugin.TrySendLobbyChatMessage(ModerationProtocolPolicy.Build("));
            Assert.That(send, Does.Contain("message = HelperNetworkPathMessage;"));
            Assert.That(send, Does.Contain("message = HelperNotInSteamLobbyMessage;"), "an older host still gets the honest rejoin message");
            Assert.That(controller, Does.Contain("NetworkTransportKey, ModerationCapabilityPolicy.BuildNetworkTransport(ownerSteamId, now)"));
            Assert.That(controller, Does.Contain("internal const string HelperNetworkPathMessage"));
            Assert.That(SteamIdModerationControllerMessage(controller), Does.Contain("you won't get a reply here either way"));
            Assert.That(SteamIdModerationControllerMessage(controller), Does.Not.Contain("older BlueSage"), "the packet only ever goes to a host that advertised the path, so that case cannot happen (advisor, I gate)");
        });
    }

    // Advisor at the I gate: the reconnected Helper this stage serves may have no current lobby on their client
    // (vanilla's LobbyCode getter dereferences CurrentLobby) and is no longer a Steam member, so cached lobby data
    // goes stale after the 20 s lease. The client falls back to the Reconnect Guard's saved lobby id and asks Steam
    // to refresh the lobby's data while it is not a member.
    [Test]
    public void Reconnected_helper_client_still_resolves_the_lobby_and_refreshes_its_data()
    {
        string plugin = ReadSource("src", "Plugin.cs");
        string controller = ReadSource("src", "SteamIdModerationController.cs");
        string resolve = MethodBody(plugin, "private static bool TryGetCurrentSteamLobby(out CSteamID lobbyId)");

        Assert.Multiple(() =>
        {
            Assert.That(resolve, Does.Contain("catch"), "a null CurrentLobby must not throw out of the resolver");
            Assert.That(resolve, Does.Contain("LastReconnectLobbyCode"), "falls back to the Reconnect Guard's saved lobby id");
            Assert.That(plugin, Does.Contain("SteamIdModerationController.RefreshLobbyDataIfNotMember();"));
            Assert.That(MethodBody(controller, "internal static void RefreshLobbyDataIfNotMember()"), Does.Contain("SteamMatchmaking.RequestLobbyData(lobby)"));
        });
    }

    private static string SteamIdModerationControllerMessage(string controller)
    {
        int start = controller.IndexOf("internal const string HelperNetworkPathMessage", StringComparison.Ordinal);
        int end = controller.IndexOf("\";", start, StringComparison.Ordinal);
        return controller.Substring(start, end - start);
    }

    private static string MethodBody(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] == '}' && --depth == 0)
            {
                return source.Substring(open, index - open + 1);
            }
        }

        throw new InvalidOperationException("Unbalanced method body for " + signature);
    }

    private static string ReadSource(params string[] pathParts)
    {
        string path = FindProjectRoot();
        foreach (string part in pathParts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
