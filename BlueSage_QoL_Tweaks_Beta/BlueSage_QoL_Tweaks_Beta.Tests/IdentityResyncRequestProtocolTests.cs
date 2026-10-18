using System.Text;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityResyncRequestProtocolTests
{
    [Test]
    public void Request_is_versioned_bounded_generation_and_capability_bound()
    {
        bool built = IdentityResyncRequestProtocol.TryBuild(
            "ABCDEF0123456789",
            generation: 7,
            "1234567890ABCDEF",
            issuedUnixSeconds: 1_000,
            nonce: "0123456789abcdef",
            out string payload,
            out string error);
        bool parsed = IdentityResyncRequestProtocol.TryParse(
            payload,
            out IdentityResyncRequest request,
            out string parseError);

        Assert.Multiple(() =>
        {
            Assert.That(built, Is.True, error);
            Assert.That(parsed, Is.True, parseError);
            Assert.That(IdentityResyncRequestProtocol.MemberDataKey, Is.EqualTo("bluesage_idsync_request_v1"));
            Assert.That(Encoding.UTF8.GetByteCount(payload), Is.LessThanOrEqualTo(256));
            Assert.That(request.Version, Is.EqualTo(1));
            Assert.That(request.LobbyFingerprint, Is.EqualTo("ABCDEF0123456789"));
            Assert.That(request.Generation, Is.EqualTo(7));
            Assert.That(request.CapabilityFingerprint, Is.EqualTo("1234567890ABCDEF"));
            Assert.That(IdentityResyncRequestProtocol.IsFresh(request, 1_015), Is.True);
            Assert.That(IdentityResyncRequestProtocol.IsFresh(request, 1_016), Is.False);
        });
    }

    [TestCase("v2|request|ABCDEF0123456789|7|1234567890ABCDEF|1000|0123456789abcdef")]
    [TestCase("v1|request|ABCDEF0123456789|0|1234567890ABCDEF|1000|0123456789abcdef")]
    [TestCase("v1|request|not-a-scope|7|1234567890ABCDEF|1000|0123456789abcdef")]
    [TestCase("v1|request|ABCDEF0123456789|7|not-a-capability|1000|0123456789abcdef")]
    [TestCase("v1|request|ABCDEF0123456789|7|1234567890ABCDEF|1000|bad nonce")]
    public void Forged_or_malformed_request_is_refused(string payload)
    {
        Assert.That(
            IdentityResyncRequestProtocol.TryParse(payload, out _, out _),
            Is.False);
    }

    [Test]
    public void Oversized_request_is_refused_and_scope_fingerprints_are_stable()
    {
        string oversized = "v1|request|" + new string('A', 300);
        Assert.Multiple(() =>
        {
            Assert.That(IdentityResyncRequestProtocol.TryParse(oversized, out _, out _), Is.False);
            Assert.That(
                IdentityResyncRequestProtocol.FingerprintScope("lobby", "owner"),
                Is.EqualTo(IdentityResyncRequestProtocol.FingerprintScope("lobby", "owner")));
            Assert.That(
                IdentityResyncRequestProtocol.FingerprintScope("lobby", "owner"),
                Is.Not.EqualTo(IdentityResyncRequestProtocol.FingerprintScope("lobby", "other-owner")));
        });
    }
}
