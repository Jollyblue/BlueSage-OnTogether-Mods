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

    // Stage C, review P2: a Steam lobby member with no live row yet is not "absent", so it does not pass CanSave's
    // absent flag; the same host-only rules apply through their own entry point.
    [Test]
    public void ActiveHostMaySaveATargetWithNoLiveRowUnderTheSameRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OfflineBanPolicy.CanSaveWithoutLiveRow(Owner, Owner, Target, Helper, false, out string reason), Is.True);
            Assert.That(reason, Is.Empty);
            Assert.That(OfflineBanPolicy.CanSaveWithoutLiveRow(Owner, Helper, Target, Helper, false, out string helperReason), Is.False, "Helpers never originate it.");
            Assert.That(helperReason, Does.Contain("active lobby host"));
            Assert.That(OfflineBanPolicy.CanSaveWithoutLiveRow(Owner, Owner, Target, Helper, true, out _), Is.False, "Protected victims stay protected.");
            Assert.That(OfflineBanPolicy.CanSaveWithoutLiveRow(Owner, Owner, Helper, Helper, false, out _), Is.False, "Helpers must be removed first.");
            Assert.That(OfflineBanPolicy.CanSave(Owner, Owner, Target, Helper, false, false, out _), Is.False, "CanSave still demands a confirmed absent target.");
        });
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
