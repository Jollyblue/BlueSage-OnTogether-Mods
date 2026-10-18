using BlueSage.QoLTweaks.Core;
using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ClientCapabilitySnapshotPolicyTests
{
    [Test]
    public void Rescue_only_heartbeat_does_not_claim_general_compatibility_or_a_guessed_version()
    {
        ClientCapabilitySnapshot snapshot = ClientCapabilitySnapshotPolicy.Resolve(
            steamId: 76561198000000001UL,
            lobbyKey: 9170UL,
            lobbyEpoch: 7,
            evidenceGeneration: 11,
            exactCurrentIdentity: true,
            compatibleHeartbeatFresh: false,
            rescueReceiverFresh: true);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CompatibleClientPresent, Is.False);
            Assert.That(snapshot.RescueReceiverReady, Is.True);
            Assert.That(snapshot.StatusText,
                Is.EqualTo("Rescue receiver ready; general QoL presence is not current."));
            Assert.That(snapshot.StatusText, Does.Not.Contain("0.2.3+"));
        });
    }

    [Test]
    public void Typed_snapshot_preserves_exact_identity_epoch_and_independent_capability_truth()
    {
        const long nowUnix = 1784134800L;
        var identity = new BlueSageIdentityTruthSnapshotV1(
            steamId: 76561198000000001UL,
            purrNetId: 42UL,
            lobbyEpoch: 7,
            evidenceGeneration: 11,
            capturedUtcTicks: 638922636000000000L,
            freshUntilUtcTicks: 638922636010000000L);

        bool resolved = ClientCapabilitySnapshotController.TryResolve(
            identity,
            lobbyKey: 9170UL,
            compatiblePayload: "",
            rescueReceiverPayload: "v1|0.2.3|1784134800",
            nowUnixSeconds: nowUnix,
            out BlueSageClientCapabilitySnapshotV1 snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(snapshot.SteamId, Is.EqualTo(identity.SteamId));
            Assert.That(snapshot.LobbyEpoch, Is.EqualTo(identity.LobbyEpoch));
            Assert.That(snapshot.EvidenceGeneration, Is.EqualTo(identity.EvidenceGeneration));
            Assert.That(snapshot.CompatibleClientPresent, Is.False);
            Assert.That(snapshot.RescueReceiverReady, Is.True);
            Assert.That(snapshot.StatusText,
                Is.EqualTo("Rescue receiver ready; general QoL presence is not current."));
        });
    }

    [Test]
    public void Current_general_and_rescue_heartbeats_agree_for_the_same_exact_identity_generation()
    {
        ClientCapabilitySnapshot snapshot = ClientCapabilitySnapshotPolicy.Resolve(
            steamId: 76561198000000002UL,
            lobbyKey: 9170UL,
            lobbyEpoch: 7,
            evidenceGeneration: 12,
            exactCurrentIdentity: true,
            compatibleHeartbeatFresh: true,
            rescueReceiverFresh: true);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsCurrentExactIdentity, Is.True);
            Assert.That(snapshot.CompatibleClientPresent, Is.True);
            Assert.That(snapshot.RescueReceiverReady, Is.True);
            Assert.That(snapshot.StatusText,
                Is.EqualTo("Compatible QoL and rescue receiver ready."));
        });
    }

    [Test]
    public void Missing_or_stale_identity_context_clears_both_capabilities()
    {
        ClientCapabilitySnapshot missingHeartbeat = ClientCapabilitySnapshotPolicy.Resolve(
            steamId: 76561198000000003UL,
            lobbyKey: 9170UL,
            lobbyEpoch: 7,
            evidenceGeneration: 13,
            exactCurrentIdentity: true,
            compatibleHeartbeatFresh: false,
            rescueReceiverFresh: false);
        ClientCapabilitySnapshot staleIdentity = ClientCapabilitySnapshotPolicy.Resolve(
            steamId: 76561198000000003UL,
            lobbyKey: 9170UL,
            lobbyEpoch: 0,
            evidenceGeneration: 0,
            exactCurrentIdentity: false,
            compatibleHeartbeatFresh: true,
            rescueReceiverFresh: true);

        Assert.Multiple(() =>
        {
            Assert.That(missingHeartbeat.CompatibleClientPresent, Is.False);
            Assert.That(missingHeartbeat.RescueReceiverReady, Is.False);
            Assert.That(missingHeartbeat.StatusText,
                Is.EqualTo("No fresh compatible QoL heartbeat."));
            Assert.That(staleIdentity.IsCurrentExactIdentity, Is.False);
            Assert.That(staleIdentity.CompatibleClientPresent, Is.False);
            Assert.That(staleIdentity.RescueReceiverReady, Is.False);
        });
    }
}
