using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RosterRegistrationPolicyTests
{
    private sealed class TransformToken { }

    [Test]
    public void Invalid_identity_or_transform_is_rejected_before_game_dictionary_access()
    {
        TransformToken token = new();
        Assert.Multiple(() =>
        {
            Assert.That(Evaluate("", "voice", token).Action, Is.EqualTo(RosterRegistrationAction.RejectInvalid));
            Assert.That(Evaluate("steam", "voice", null!).Action, Is.EqualTo(RosterRegistrationAction.RejectInvalid));
        });
    }

    [Test]
    public void Exact_transform_registration_is_idempotent()
    {
        TransformToken token = new();
        RosterRegistrationDecision decision = RosterRegistrationPolicy.Evaluate(
            "steam", "voice", token,
            new[] { "steam" }, new[] { "voice" }, new[] { token });

        Assert.Multiple(() =>
        {
            Assert.That(decision.Action, Is.EqualTo(RosterRegistrationAction.IgnoreExactDuplicate));
            Assert.That(decision.ExistingIndex, Is.Zero);
        });
    }

    [TestCase("steam", "other", "ReplaceStableIdentity")]
    [TestCase("other", "voice", "ReplaceStableIdentity")]
    [TestCase("other", "other", "Append")]
    public void Stable_identity_replaces_stale_row_while_unique_identity_appends(
        string steamId,
        string voiceId,
        string expected)
    {
        RosterRegistrationDecision decision = RosterRegistrationPolicy.Evaluate(
            steamId, voiceId, new TransformToken(),
            new[] { "steam" }, new[] { "voice" }, new[] { new TransformToken() });

        Assert.That(decision.Action.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Steam_identity_match_wins_when_an_older_vivox_row_conflicts()
    {
        RosterRegistrationDecision decision = RosterRegistrationPolicy.Evaluate(
            "steam-current",
            "voice-reused",
            new TransformToken(),
            new[] { "steam-other", "steam-current" },
            new[] { "voice-reused", "voice-current" },
            new[] { new TransformToken(), new TransformToken() });

        Assert.Multiple(() =>
        {
            Assert.That(decision.Action, Is.EqualTo(RosterRegistrationAction.ReplaceStableIdentity));
            Assert.That(decision.ExistingIndex, Is.EqualTo(1),
                "Steam identity is authoritative; replacing the earlier Vivox-only row would leave a duplicate Steam row.");
        });
    }

    private static RosterRegistrationDecision Evaluate(string steamId, string voiceId, TransformToken transform)
    {
        return RosterRegistrationPolicy.Evaluate(
            steamId,
            voiceId,
            transform,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<TransformToken>());
    }
}
