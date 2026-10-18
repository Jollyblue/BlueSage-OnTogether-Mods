using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class LobbyPingAuthorizationPolicyTests
{
    [Test] public void Host_is_authorized() => Assert.That(LobbyPingAuthorizationPolicy.IsAuthorized("10", "10", "", "", true), Is.True);
    [Test] public void Delegate_is_authorized() => Assert.That(LobbyPingAuthorizationPolicy.IsAuthorized("20", "10", "20", "10", true), Is.True);
    [Test] public void Delegate_from_another_host_is_denied() => Assert.That(LobbyPingAuthorizationPolicy.IsAuthorized("20", "10", "20", "99", true), Is.False);
    [Test] public void Unauthorized_member_is_denied() => Assert.That(LobbyPingAuthorizationPolicy.IsAuthorized("30", "10", "20", "10", true), Is.False);
    [Test] public void No_lobby_is_denied() => Assert.That(LobbyPingAuthorizationPolicy.IsAuthorized("10", "10", "", "", false), Is.False);
    [TestCase("Blue")][TestCase("<color=red>10</color>")][TestCase("0")]
    public void Spoofed_name_or_invalid_id_is_denied(string sender) => Assert.That(LobbyPingAuthorizationPolicy.IsAuthorized(sender, "10", "", "10", true), Is.False);

    [Test]
    public void Persisted_ids_are_sanitized_and_deduplicated()
    {
        Assert.That(LobbyPingAuthorizationPolicy.SerializeDelegates(new[] { "20", "Blue", "20", " 30 " }), Is.EqualTo("20,30"));
        Assert.That(LobbyPingAuthorizationPolicy.SerializeDelegates(Array.Empty<string>()), Is.Empty);
    }
}
