using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SessionRowCorrelatorPolicyTests
{
    private const string SteamId = "76561198012345678";
    private const string PlayerId = "player-id-0";

    [Test]
    public void Same_session_and_exact_tuple_are_stable()
    {
        Assert.That(
            SessionRowCorrelatorPolicy.TryCreate(
                "session-alpha",
                SteamId,
                PlayerId,
                0,
                out string first),
            Is.True);
        Assert.That(
            SessionRowCorrelatorPolicy.TryCreate(
                "session-alpha",
                SteamId,
                PlayerId,
                0,
                out string second),
            Is.True);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void New_lobby_session_breaks_cross_session_tracking()
    {
        SessionRowCorrelatorPolicy.TryCreate(
            "session-alpha",
            SteamId,
            PlayerId,
            0,
            out string first);
        SessionRowCorrelatorPolicy.TryCreate(
            "session-bravo",
            SteamId,
            PlayerId,
            0,
            out string second);

        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void Exact_tuple_components_distinguish_rows_without_using_visible_names()
    {
        SessionRowCorrelatorPolicy.TryCreate(
            "session-alpha",
            SteamId,
            PlayerId,
            0,
            out string first);
        SessionRowCorrelatorPolicy.TryCreate(
            "session-alpha",
            "76561198012345679",
            "player-id-1",
            1,
            out string second);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(first, Does.Match("^row-[0-9a-f]{12}$"));
            Assert.That(first, Does.Not.Contain(SteamId));
            Assert.That(first, Does.Not.Contain(PlayerId));
        });
    }

    [TestCase("", "76561198012345678", "player-id-0", 0)]
    [TestCase("session-alpha", "bad", "player-id-0", 0)]
    [TestCase("session-alpha", "76561198012345678", "", 0)]
    [TestCase("session-alpha", "76561198012345678", "player-id-0", -1)]
    public void Incomplete_tuple_or_session_scope_fails_closed(
        string sessionScope,
        string steamId,
        string playerId,
        int rowIndex)
    {
        Assert.That(
            SessionRowCorrelatorPolicy.TryCreate(
                sessionScope,
                steamId,
                playerId,
                rowIndex,
                out string correlator),
            Is.False);
        Assert.That(correlator, Is.Empty);
    }
}
