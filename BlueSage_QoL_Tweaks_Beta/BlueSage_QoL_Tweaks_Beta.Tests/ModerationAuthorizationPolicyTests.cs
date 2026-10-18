using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ModerationAuthorizationPolicyTests
{
    [Test]
    public void Owner_or_owner_scoped_helper_can_request_but_unlisted_sender_cannot()
    {
        const string owner = "76561198000000001";
        const string helper = "76561198000000002";
        Assert.That(ModerationAuthorizationPolicy.IsAuthorizedIssuer(owner, owner, helper, owner, true), Is.True);
        Assert.That(ModerationAuthorizationPolicy.IsAuthorizedIssuer(helper, owner, helper, owner, true), Is.True);
        Assert.That(ModerationAuthorizationPolicy.IsAuthorizedIssuer("76561198000000003", owner, helper, owner, true), Is.False);
        Assert.That(ModerationAuthorizationPolicy.IsAuthorizedIssuer(helper, owner, helper, "76561198099999999", true), Is.False);
    }

    [Test]
    public void Protected_targets_fail_closed()
    {
        const string owner = "76561198000000001";
        const string helper = "76561198000000002";
        Assert.That(ModerationAuthorizationPolicy.CanBanTarget(owner, owner, helper, "76561198000000004", false, true), Is.False, "host");
        Assert.That(ModerationAuthorizationPolicy.CanBanTarget(helper, owner, helper, "76561198000000004", false, true), Is.False, "helper");
        Assert.That(ModerationAuthorizationPolicy.CanBanTarget("76561198000000003", owner, helper, "76561198000000003", false, true), Is.False, "issuer");
        Assert.That(ModerationAuthorizationPolicy.CanBanTarget("76561198000000004", owner, helper, "76561198000000003", true, true), Is.False, "clone victim");
        Assert.That(ModerationAuthorizationPolicy.CanBanTarget("76561198000000004", owner, helper, "76561198000000003", false, true), Is.True);
    }
}
