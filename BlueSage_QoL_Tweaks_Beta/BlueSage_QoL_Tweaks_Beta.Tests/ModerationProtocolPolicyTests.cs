using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ModerationProtocolPolicyTests
{
    [Test]
    public void Round_trip_preserves_action_target_incident_time_and_nonce()
    {
        string text = ModerationProtocolPolicy.Build("ban", "76561198012345678", "C003", 1700000000, "ab12CD34");

        Assert.That(ModerationProtocolPolicy.TryParse(text, out ModerationRequest request), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(request.Action, Is.EqualTo("ban"));
            Assert.That(request.TargetSteamId, Is.EqualTo("76561198012345678"));
            Assert.That(request.IncidentId, Is.EqualTo("C003"));
            Assert.That(request.IssuedUnixSeconds, Is.EqualTo(1700000000));
            Assert.That(request.Nonce, Is.EqualTo("ab12CD34"));
        });
    }

    // Round 4 Stage E #3: "kick" moved from the rejected list to the accepted one (deliberate pin change); "warn" now
    // stands in as the unknown action.
    [TestCase("warn")]
    [TestCase("BAN")]
    [TestCase("")]
    public void Rejects_unknown_or_noncanonical_actions(string action)
    {
        string text = ModerationProtocolPolicy.Prefix + action + ":76561198012345678:C003:1700000000:ab12CD34";
        Assert.That(ModerationProtocolPolicy.TryParse(text, out _), Is.False);
    }

    [TestCase("ban")]
    [TestCase("unban")]
    [TestCase("kick")]
    [TestCase("mute")]
    [TestCase("unmute")]
    public void Accepts_the_five_host_actions_in_member_data_and_chat_shaped_requests(string action)
    {
        string memberData = ModerationProtocolPolicy.BuildMemberData(action, "76561198012345678", "none", 1700000000, "ab12CD34");
        Assert.Multiple(() =>
        {
            Assert.That(ModerationProtocolPolicy.IsModerationAction(action), Is.True);
            Assert.That(memberData, Is.Not.Empty);
            Assert.That(ModerationProtocolPolicy.TryParseMemberData(memberData, out ModerationRequest request), Is.True);
            Assert.That(request.Action, Is.EqualTo(action));
        });
    }

    [TestCase("123")]
    [TestCase("not-a-steamid")]
    [TestCase("765611980123456789")]
    public void Rejects_non_SteamID64_targets(string target)
    {
        string text = ModerationProtocolPolicy.Prefix + "ban:" + target + ":C003:1700000000:ab12CD34";
        Assert.That(ModerationProtocolPolicy.TryParse(text, out _), Is.False);
    }

    [Test]
    public void Freshness_is_bounded()
    {
        ModerationProtocolPolicy.TryParse(ModerationProtocolPolicy.Build("unban", "76561198012345678", "none", 1700000000, "ab12CD34"), out ModerationRequest request);
        Assert.That(ModerationProtocolPolicy.IsFresh(request, 1700000015), Is.True);
        Assert.That(ModerationProtocolPolicy.IsFresh(request, 1700000016), Is.False);
        Assert.That(ModerationProtocolPolicy.IsFresh(request, 1699999997), Is.False);
    }

    [Test]
    public void Matching_pending_preview_exposes_an_honest_confirmation_countdown()
    {
        ModerationConfirmationReadiness state = ModerationProtocolPolicy.EvaluateConfirmation(
            "ban",
            "76561198012345678",
            "123456",
            1700000000,
            "ban",
            "76561198012345678",
            "C003",
            "123456",
            1700000030);

        Assert.Multiple(() =>
        {
            Assert.That(state.IsReady, Is.True);
            Assert.That(state.SecondsRemaining, Is.EqualTo(30));
        });
    }

    [Test]
    public void Stale_or_mismatched_preview_never_looks_confirmable()
    {
        ModerationConfirmationReadiness wrongTarget = ModerationProtocolPolicy.EvaluateConfirmation(
            "ban", "76561198099999999", "123456", 1700000000,
            "ban", "76561198012345678", "C003", "123456", 1700000030);
        ModerationConfirmationReadiness wrongLobby = ModerationProtocolPolicy.EvaluateConfirmation(
            "ban", "76561198012345678", "654321", 1700000000,
            "ban", "76561198012345678", "C003", "123456", 1700000030);
        ModerationConfirmationReadiness expired = ModerationProtocolPolicy.EvaluateConfirmation(
            "ban", "76561198012345678", "123456", 1700000031,
            "ban", "76561198012345678", "C003", "123456", 1700000030);

        Assert.Multiple(() =>
        {
            Assert.That(wrongTarget.IsReady, Is.False);
            Assert.That(wrongLobby.IsReady, Is.False);
            Assert.That(expired.IsReady, Is.False);
        });
    }
}
