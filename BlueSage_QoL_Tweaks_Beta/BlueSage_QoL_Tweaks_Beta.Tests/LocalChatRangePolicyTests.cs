using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class LocalChatRangePolicyTests
{
    [Test]
    public void Defaults_match_what_players_are_told()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LocalChatRangePolicy.DefaultMeters, Is.EqualTo(15f));
            Assert.That(LocalChatRangePolicy.GameMeters, Is.EqualTo(5f));
            Assert.That(LocalChatRangePolicy.GameSquaredMeters, Is.EqualTo(25f), "The game compares squared distance against 25.");
            Assert.That(LocalChatRangePolicy.MinMeters, Is.EqualTo(5f));
            Assert.That(LocalChatRangePolicy.MaxMeters, Is.EqualTo(25f));
        });
    }

    [TestCase(0f, 5f)]
    [TestCase(-3f, 5f)]
    [TestCase(5f, 5f)]
    [TestCase(15f, 15f)]
    [TestCase(24.5f, 24.5f)]
    [TestCase(250f, 25f)]
    [TestCase(float.PositiveInfinity, 25f)]
    [TestCase(float.NegativeInfinity, 5f)]
    [TestCase(float.NaN, 15f)]
    public void ClampMeters_keeps_the_setting_between_the_game_range_and_the_cap(float configured, float expected)
    {
        Assert.That(LocalChatRangePolicy.ClampMeters(configured), Is.EqualTo(expected));
    }

    [TestCase(15f, 225f)]
    [TestCase(25f, 625f)]
    [TestCase(100f, 625f)]
    [TestCase(5f, 25f)]
    [TestCase(1f, 25f)]
    [TestCase(float.NaN, 225f)]
    public void ResolveSquaredMeters_squares_the_clamped_setting_and_five_meters_is_exactly_the_game_value(float configured, float expected)
    {
        Assert.That(LocalChatRangePolicy.ResolveSquaredMeters(configured), Is.EqualTo(expected));
    }

    [Test]
    public void FindGateConstantIndexes_finds_the_one_squared_distance_check()
    {
        string[] ops = { "call", "ldc.r4", "ble.un.s", "ldc.i4.0", "call" };
        string[] operands = { "UnityEngine.Vector3::SqrMagnitude", "25", "", "", "TextChannelManager::AddMessageUI" };

        Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(ops, operands), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void FindGateConstantIndexes_reads_the_float_in_any_invariant_spelling()
    {
        string[] ops = { "call", "ldc.r4" };
        string[] operands = { "UnityEngine.Vector3::SqrMagnitude", "25.0" };

        Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(ops, operands), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void FindGateConstantIndexes_ignores_other_25s_and_other_distance_calls()
    {
        string[] ops = { "ldc.r4", "call", "ldc.r4", "call", "ldc.r4" };
        string[] operands = { "25", "UnityEngine.Vector3::Distance", "25", "UnityEngine.Vector3::SqrMagnitude", "26" };

        Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(ops, operands), Is.Empty,
            "A bare 25, a 25 after Distance, or a different constant after SqrMagnitude must not be touched.");
    }

    [Test]
    public void FindGateConstantIndexes_reports_two_matches_so_the_patch_can_refuse()
    {
        string[] ops = { "call", "ldc.r4", "call", "ldc.r4" };
        string[] operands = { "Vector3::SqrMagnitude", "25", "Vector3::SqrMagnitude", "25" };

        Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(ops, operands), Has.Length.EqualTo(2));
    }

    [Test]
    public void FindGateConstantIndexes_is_safe_on_missing_or_mismatched_input()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(null, null), Is.Empty);
            Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(new[] { "call" }, new string[0]), Is.Empty);
            Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(new[] { "ldc.r4" }, new[] { "25" }), Is.Empty, "A constant with nothing before it has no call to follow.");
        });
    }
}
