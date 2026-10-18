using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class AuthoritativeChatSenderPolicyTests
{
    [TestCase("@lobby", true)]
    [TestCase("please read @LOBBY before focus", true)]
    [TestCase("<b>@lobby</b>", true)]
    [TestCase("plain chat without a mass ping", false)]
    [TestCase("email@lobby", false)]
    [TestCase("@lobbyist", false)]
    [TestCase("@lobby!", false)]
    public void Lobby_token_gate_matches_the_rendering_regex_and_requires_a_valid_left_boundary(string message, bool expected)
    {
        Assert.That(AuthoritativeChatSenderPolicy.ContainsLobbyPingToken(message), Is.EqualTo(expected));
    }

    [Test]
    public void Unique_row_zero_sender_is_valid()
    {
        Assert.That(AuthoritativeChatSenderPolicy.TryResolveUniqueIndex("host-player-id", new[]
        {
            "host-player-id",
            "helper-player-id",
        }, out int index), Is.True);
        Assert.That(index, Is.Zero);
    }

    [Test]
    public void Unique_nonzero_sender_is_valid()
    {
        Assert.That(AuthoritativeChatSenderPolicy.TryResolveUniqueIndex("helper-player-id", new[]
        {
            "host-player-id",
            "helper-player-id",
        }, out int index), Is.True);
        Assert.That(index, Is.EqualTo(1));
    }

    [Test]
    public void Missing_sender_fails_closed_instead_of_falling_back_to_row_zero()
    {
        Assert.That(AuthoritativeChatSenderPolicy.TryResolveUniqueIndex("missing-player-id", new[]
        {
            "host-player-id",
            "helper-player-id",
        }, out int index), Is.False);
        Assert.That(index, Is.EqualTo(-1));
    }

    [Test]
    public void Duplicate_sender_fails_closed()
    {
        Assert.That(AuthoritativeChatSenderPolicy.TryResolveUniqueIndex("duplicate-player-id", new[]
        {
            "duplicate-player-id",
            "helper-player-id",
            "duplicate-player-id",
        }, out int index), Is.False);
        Assert.That(index, Is.EqualTo(-1));
    }
}
