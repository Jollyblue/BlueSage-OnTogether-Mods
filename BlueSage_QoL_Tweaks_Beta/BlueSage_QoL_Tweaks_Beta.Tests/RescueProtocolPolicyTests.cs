using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RescueProtocolPolicyTests
{
    [Test]
    public void Hidden_receiver_cooldown_is_short_enough_for_rescue_then_hook_workflows()
    {
        Assert.That(RescueProtocolPolicy.CooldownSeconds, Is.EqualTo(1));
    }

    [Test]
    public void Transient_apply_deferrals_keep_the_nonce_retryable()
    {
        Assert.That(RescueProtocolPolicy.ShouldRememberNonce(RescueApplyResult.Deferred), Is.False);
        Assert.That(RescueProtocolPolicy.ShouldRememberNonce(RescueApplyResult.Applied), Is.True);
        Assert.That(RescueProtocolPolicy.ShouldRememberNonce(RescueApplyResult.Rejected), Is.True);
    }

    [Test]
    public void Freshness_rejects_stale_and_future_requests()
    {
        RescueProtocolPolicy.TryParseMemberData("v2|76561198000000001|hook|30|1700000000|abcd1234", out RescueRequest request);
        Assert.That(RescueProtocolPolicy.IsFresh(request, 1700000015), Is.True);
        Assert.That(RescueProtocolPolicy.IsFresh(request, 1700000016), Is.False);
        Assert.That(RescueProtocolPolicy.IsFresh(request, 1699999997), Is.False);
    }

    [Test]
    public void Hidden_member_data_uses_exact_steam_id_and_bounded_action()
    {
        string payload = RescueProtocolPolicy.BuildMemberData("76561198000000001", "hook", 999, 1700000000, "abcd1234");
        Assert.That(payload, Is.EqualTo("v2|76561198000000001|hook|30|1700000000|abcd1234"));
        Assert.That(RescueProtocolPolicy.TryParseMemberData(payload, out RescueRequest request), Is.True);
        Assert.That(request.TargetSteamId, Is.EqualTo("76561198000000001"));
        Assert.That(request.Seconds, Is.EqualTo(30));
    }

    [Test]
    public void Hidden_member_data_can_target_a_unique_native_player_id_while_steam_identity_syncs()
    {
        string payload = RescueProtocolPolicy.BuildPlayerIdMemberData("035", "hook", 999, 1700000000, "void1234");

        Assert.That(payload, Is.EqualTo("v3|pid|035|hook|30|1700000000|void1234"));
        Assert.That(RescueProtocolPolicy.TryParseMemberData(payload, out RescueRequest request), Is.True);
        Assert.That(request.TargetPlayerId, Is.EqualTo("035"));
        Assert.That(request.TargetSteamId, Is.Empty);
        Assert.That(RescueProtocolPolicy.IsLocalTarget(request, "76561198000000001", "035"), Is.True);
        Assert.That(RescueProtocolPolicy.IsLocalTarget(request, "76561198000000001", "036"), Is.False);
    }

    [TestCase("v2|not-an-id|hook|30|1700000000|abcd1234")]
    [TestCase("v2|76561198000000001|ban|0|1700000000|abcd1234")]
    [TestCase("v2|76561198000000001|hook|30|1700000000|bad!")]
    public void Hidden_member_data_rejects_invalid_target_action_or_nonce(string payload)
    {
        Assert.That(RescueProtocolPolicy.TryParseMemberData(payload, out _), Is.False);
    }

    [Test]
    public void Source_contains_no_legacy_public_chat_control_prefix()
    {
        string root = FindRoot();
        foreach (string file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            Assert.That(source, Does.Not.Contain("!bsrescue:"), file);
            Assert.That(source, Does.Not.Contain("!bsvisible:"), file);
        }
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
