using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityResyncTriggerPolicyTests
{
    [TestCase((int)IdentityResyncTrigger.RowInitialized)]
    [TestCase((int)IdentityResyncTrigger.RowDisabledOrDespawned)]
    [TestCase((int)IdentityResyncTrigger.LobbyOrOwnerIdentityChanged)]
    [TestCase((int)IdentityResyncTrigger.PanelReplacement)]
    [TestCase((int)IdentityResyncTrigger.AuthorityMembershipChanged)]
    public void Approved_lifecycle_triggers_advance_generation(int triggerValue)
    {
        var trigger = (IdentityResyncTrigger)triggerValue;
        Assert.That(IdentityResyncTriggerPolicy.AdvancesGeneration(trigger), Is.True);
    }

    [Test]
    public void Runtime_shape_classifies_lobby_owner_panel_and_row_lifecycle_changes()
    {
        var baseline = new IdentityResyncRuntimeShape(
            "lobby-a",
            "owner-a",
            10,
            8,
            101,
            "steam-a",
            "purr-a");

        Assert.Multiple(() =>
        {
            Assert.That(
                IdentityResyncTriggerPolicy.Classify(
                    baseline,
                    new IdentityResyncRuntimeShape("lobby-b", "owner-a", 10, 8, 101, "steam-a", "purr-a")),
                Is.EqualTo(IdentityResyncTrigger.LobbyOrOwnerIdentityChanged));
            Assert.That(
                IdentityResyncTriggerPolicy.Classify(
                    baseline,
                    new IdentityResyncRuntimeShape("lobby-a", "owner-a", 11, 8, 101, "steam-a", "purr-a")),
                Is.EqualTo(IdentityResyncTrigger.PanelReplacement));
            Assert.That(
                IdentityResyncTriggerPolicy.Classify(
                    baseline,
                    new IdentityResyncRuntimeShape("lobby-a", "owner-a", 10, 9, 102, "steam-a", "purr-a")),
                Is.EqualTo(IdentityResyncTrigger.RowInitialized));
            Assert.That(
                IdentityResyncTriggerPolicy.Classify(
                    baseline,
                    new IdentityResyncRuntimeShape("lobby-a", "owner-a", 10, 7, 99, "steam-a", "purr-a")),
                Is.EqualTo(IdentityResyncTrigger.RowDisabledOrDespawned));
        });
    }

    [Test]
    public void Steam_or_purrnet_membership_change_advances_a_new_generation()
    {
        var baseline = new IdentityResyncRuntimeShape(
            "lobby-a", "owner-a", 10, 14, 101, "steam-13", "purr-14");

        Assert.Multiple(() =>
        {
            Assert.That(
                IdentityResyncTriggerPolicy.Classify(
                    baseline,
                    new IdentityResyncRuntimeShape(
                        "lobby-a", "owner-a", 10, 14, 101, "steam-14", "purr-14")),
                Is.EqualTo(IdentityResyncTrigger.AuthorityMembershipChanged));
            Assert.That(
                IdentityResyncTriggerPolicy.Classify(
                    baseline,
                    new IdentityResyncRuntimeShape(
                        "lobby-a", "owner-a", 10, 14, 101, "steam-13", "purr-13")),
                Is.EqualTo(IdentityResyncTrigger.AuthorityMembershipChanged));
        });
    }

    [Test]
    public void Authority_fingerprints_are_order_independent_and_content_sensitive()
    {
        string first = IdentityAuthorityFingerprintPolicy.Build(
            new[] { "player-c", "player-a", "player-b" });
        string reordered = IdentityAuthorityFingerprintPolicy.Build(
            new[] { "player-b", "player-c", "player-a" });
        string changed = IdentityAuthorityFingerprintPolicy.Build(
            new[] { "player-c", "player-a" });
        string duplicated = IdentityAuthorityFingerprintPolicy.Build(
            new[] { "player-c", "player-a", "player-b", "player-b" });

        Assert.Multiple(() =>
        {
            Assert.That(reordered, Is.EqualTo(first));
            Assert.That(changed, Is.Not.EqualTo(first));
            Assert.That(duplicated, Is.Not.EqualTo(first));
            Assert.That(first, Has.Length.EqualTo(16));
        });
    }
}
