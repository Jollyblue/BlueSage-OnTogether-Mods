using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerRowGeometryReadinessPolicyTests
{
    [Test]
    public void Second_consecutive_matching_valid_observation_unlocks_row_layout()
    {
        var policy = new PlayerRowGeometryReadinessPolicy();

        Assert.Multiple(() =>
        {
            Assert.That(policy.Observe(320f, 1001), Is.False);
            Assert.That(policy.Observe(320f, 1001), Is.True);
            Assert.That(policy.Observe(320f, 1001), Is.True);
        });
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void Invalid_width_defers_and_does_not_prime_the_next_valid_observation(
        float invalidWidth)
    {
        var policy = new PlayerRowGeometryReadinessPolicy();

        Assert.Multiple(() =>
        {
            Assert.That(policy.Observe(320f, 1001), Is.False);
            Assert.That(policy.Observe(320f, 1001), Is.True);
            Assert.That(policy.Observe(invalidWidth, 1001), Is.False);
            Assert.That(policy.Observe(320f, 1001), Is.False);
            Assert.That(policy.Observe(320f, 1001), Is.True);
        });
    }

    [Test]
    public void Stable_row_remains_renderable_across_later_valid_geometry_changes()
    {
        var policy = new PlayerRowGeometryReadinessPolicy();

        Assert.Multiple(() =>
        {
            Assert.That(policy.Observe(320f, 1001), Is.False);
            Assert.That(policy.Observe(320f, 1001), Is.True);
            Assert.That(policy.Observe(321f, 1001), Is.True);
            Assert.That(policy.Observe(321f, 1001), Is.True);
            Assert.That(policy.Observe(321f, 1002), Is.True);
            Assert.That(policy.Observe(321f, 1002), Is.True);
        });
    }

    [Test]
    public void Reset_requires_a_new_two_observation_handshake_for_the_next_row_lifecycle()
    {
        var policy = new PlayerRowGeometryReadinessPolicy();

        Assert.That(policy.Observe(320f, 1001), Is.False);
        Assert.That(policy.Observe(320f, 1001), Is.True);

        policy.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(policy.Observe(320f, 1001), Is.False);
            Assert.That(policy.Observe(320f, 1001), Is.True);
        });
    }
}
