using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class OfflineBanPolicyTests
{
    private const string Owner = "76561198000000001";
    private const string Target = "76561198000000002";
    private const string Helper = "76561198000000003";

    [Test]
    public void ActiveHostMaySaveExactAbsentSteamId()
    {
        Assert.That(OfflineBanPolicy.CanSave(Owner, Owner, Target, Helper, false, true, out string reason), Is.True);
        Assert.That(reason, Is.Empty);
    }

    [Test]
    public void HelperMayNotOriginateOffRosterBan()
    {
        Assert.That(OfflineBanPolicy.CanSave(Owner, Helper, Target, Helper, false, true, out string reason), Is.False);
        Assert.That(reason, Does.Contain("active lobby host"));
    }

    [TestCase("not-a-steam-id", false, true)]
    [TestCase(Target, true, true)]
    [TestCase(Target, false, false)]
    public void InvalidProtectedOrPresentTargetIsRejected(string target, bool protectedVictim, bool absent)
    {
        Assert.That(OfflineBanPolicy.CanSave(Owner, Owner, target, Helper, protectedVictim, absent, out _), Is.False);
    }

    [Test]
    public void OwnerAndHelpersCannotBeOfflineTargets()
    {
        Assert.That(OfflineBanPolicy.CanSave(Owner, Owner, Owner, Helper, false, true, out _), Is.False);
        Assert.That(OfflineBanPolicy.CanSave(Owner, Owner, Helper, Helper, false, true, out string reason), Is.False);
        Assert.That(reason, Does.Contain("Remove this SteamID64 from Helpers"));
    }

    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    public void CurrentSteamLobbyMembershipIsAuthoritativeForAbsence(int currentLobbyMatches, bool expected)
    {
        Assert.That(OfflineBanPolicy.IsConfirmedAbsentFromCurrentLobby(currentLobbyMatches), Is.EqualTo(expected));
    }
}
