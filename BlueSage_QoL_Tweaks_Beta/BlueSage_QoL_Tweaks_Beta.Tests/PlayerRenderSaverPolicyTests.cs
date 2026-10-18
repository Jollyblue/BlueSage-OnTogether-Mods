using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerRenderSaverPolicyTests
{
    [Test]
    public void ClampRadiusMeters_keeps_public_menu_range_safe()
    {
        Assert.That(PlayerRenderSaverPolicy.ClampRadiusMeters(1), Is.EqualTo(PlayerRenderSaverPolicy.MinRadiusMeters));
        Assert.That(PlayerRenderSaverPolicy.ClampRadiusMeters(45), Is.EqualTo(45));
        Assert.That(PlayerRenderSaverPolicy.ClampRadiusMeters(999), Is.EqualTo(PlayerRenderSaverPolicy.MaxRadiusMeters));
    }

    [Test]
    public void ClampMaxVisiblePlayers_keeps_roster_cap_safe()
    {
        Assert.That(PlayerRenderSaverPolicy.ClampMaxVisiblePlayers(0), Is.EqualTo(PlayerRenderSaverPolicy.MinMaxVisiblePlayers));
        Assert.That(PlayerRenderSaverPolicy.ClampMaxVisiblePlayers(32), Is.EqualTo(32));
        Assert.That(PlayerRenderSaverPolicy.ClampMaxVisiblePlayers(999), Is.EqualTo(PlayerRenderSaverPolicy.MaxMaxVisiblePlayers));
    }

    [TestCase(10f, 0, 45, 1, false, TestName = "Nearest nearby player stays visible")]
    [TestCase(80f, 0, 45, 32, true, TestName = "Distance budget culls even a high-ranked player")]
    [TestCase(20f, 32, 45, 32, true, TestName = "Visible-count budget culls crowded nearby avatars")]
    [TestCase(45f, 31, 45, 32, false, TestName = "Players inside both budgets remain visible")]
    public void ShouldHideRemotePlayer_uses_radius_then_closest_visible_cap(
        float distanceMeters,
        int distanceRank,
        int radiusMeters,
        int maxVisiblePlayers,
        bool expected)
    {
        bool result = PlayerRenderSaverPolicy.ShouldHideRemotePlayer(distanceMeters, distanceRank, radiusMeters, maxVisiblePlayers);

        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(50f, 10, 45, 32, true, TestName = "Hidden player remains hidden inside distance hysteresis band")]
    [TestCase(38f, 10, 45, 32, false, TestName = "Hidden player restores after crossing distance hysteresis")]
    [TestCase(20f, 30, 45, 32, true, TestName = "Hidden player remains hidden inside rank hysteresis band")]
    [TestCase(20f, 29, 45, 32, false, TestName = "Hidden player restores after crossing rank hysteresis")]
    public void Hidden_state_uses_hysteresis(float distance, int rank, int radius, int maxVisible, bool expected)
    {
        Assert.That(PlayerRenderSaverPolicy.ShouldHideRemotePlayer(distance, rank, radius, maxVisible, true), Is.EqualTo(expected));
    }
}
