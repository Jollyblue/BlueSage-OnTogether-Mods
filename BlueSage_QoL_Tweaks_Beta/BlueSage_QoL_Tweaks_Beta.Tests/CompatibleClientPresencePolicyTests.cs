using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CompatibleClientPresencePolicyTests
{
    [Test]
    public void Current_protocol_heartbeat_round_trips_at_the_freshness_boundaries()
    {
        Assert.That(CompatibleClientPresencePolicy.TryBuild(1_000, out string payload), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(payload, Is.EqualTo("1|1000"));
            Assert.That(CompatibleClientPresencePolicy.IsFresh(payload, 1_000), Is.True);
            Assert.That(CompatibleClientPresencePolicy.IsFresh(
                payload,
                1_000 + CompatibleClientPresencePolicy.FreshnessSeconds), Is.True);
            Assert.That(CompatibleClientPresencePolicy.IsFresh(
                payload,
                1_001 + CompatibleClientPresencePolicy.FreshnessSeconds), Is.False);
        });
    }

    [Test]
    public void Legacy_public_client_heartbeat_stays_visible_through_a_delayed_large_lobby_refresh()
    {
        Assert.That(CompatibleClientPresencePolicy.TryBuild(1_000, out string payload), Is.True);

        Assert.That(
            CompatibleClientPresencePolicy.IsFresh(payload, 1_012),
            Is.True,
            "A live 0.2.3/0.2.4 client publishes the same protocol. A 50+ lobby may skip one six-second presentation poll, but its exact current Steam member must retain the read-only [U] indicator.");
    }

    [Test]
    public void Unknown_versions_malformed_payloads_and_excessive_future_skew_fail_closed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CompatibleClientPresencePolicy.IsFresh("2|1000", 1_000), Is.False);
            Assert.That(CompatibleClientPresencePolicy.IsFresh("1|1000|extra", 1_000), Is.False);
            Assert.That(CompatibleClientPresencePolicy.IsFresh("1|not-time", 1_000), Is.False);
            Assert.That(CompatibleClientPresencePolicy.IsFresh("", 1_000), Is.False);
            Assert.That(CompatibleClientPresencePolicy.IsFresh(
                "1|" + (1_000 + CompatibleClientPresencePolicy.MaximumFutureSkewSeconds + 1),
                1_000), Is.False);
        });
    }

    [Test]
    public void Protocol_has_a_dedicated_general_client_key_and_does_not_reuse_rescue_state()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CompatibleClientPresencePolicy.MemberDataKey, Is.EqualTo("bluesage_qol_client_v1"));
            Assert.That(CompatibleClientPresencePolicy.MemberDataKey, Does.Not.Contain("rescue"));
            Assert.That(CompatibleClientPresencePolicy.MemberDataKey, Does.Not.Contain("role_label"));
        });
    }
}
