using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RescueFollowLeasePolicyTests
{
    private static readonly RescueFollowLease LongHaul = new(
        lobbyId: "109775241042000001",
        ownerSteamId: "76561198000000001",
        issuerSteamId: "76561198000000002",
        longHaul: true);

    [Test]
    public void ActiveLongHaulRequiresSameLobbyOwnerAuthorizedCurrentIssuerAndLiveOptIn()
    {
        Assert.That(
            RescueFollowLeasePolicy.IsCurrent(
                LongHaul,
                currentLobbyId: "109775241042000001",
                currentOwnerSteamId: "76561198000000001",
                issuerIsCurrentMember: true,
                issuerIsAuthorized: true,
                longHaulIsEnabled: true),
            Is.True);
    }

    [TestCase("109775241042000099", "76561198000000001", true, true, true)]
    [TestCase("109775241042000001", "76561198000000009", true, true, true)]
    [TestCase("109775241042000001", "76561198000000001", false, true, true)]
    [TestCase("109775241042000001", "76561198000000001", true, false, true)]
    [TestCase("109775241042000001", "76561198000000001", true, true, false)]
    public void ActiveLongHaulFailsClosedWhenAnyAuthorityScopeChanges(
        string lobbyId,
        string ownerSteamId,
        bool issuerIsCurrentMember,
        bool issuerIsAuthorized,
        bool longHaulIsEnabled)
    {
        Assert.That(
            RescueFollowLeasePolicy.IsCurrent(
                LongHaul,
                lobbyId,
                ownerSteamId,
                issuerIsCurrentMember,
                issuerIsAuthorized,
                longHaulIsEnabled),
            Is.False);
    }

    [Test]
    public void BoundedHookStillRequiresCurrentLobbyOwnerAndIssuerButNotLongHaulFlag()
    {
        var bounded = new RescueFollowLease(
            lobbyId: "109775241042000001",
            ownerSteamId: "76561198000000001",
            issuerSteamId: "76561198000000001",
            longHaul: false);

        Assert.That(
            RescueFollowLeasePolicy.IsCurrent(
                bounded,
                currentLobbyId: "109775241042000001",
                currentOwnerSteamId: "76561198000000001",
                issuerIsCurrentMember: true,
                issuerIsAuthorized: true,
                longHaulIsEnabled: false),
            Is.True);
    }

    [Test]
    public void EmptyLeaseNeverAuthorizesFollow()
    {
        Assert.That(
            RescueFollowLeasePolicy.IsCurrent(
                default,
                currentLobbyId: "109775241042000001",
                currentOwnerSteamId: "76561198000000001",
                issuerIsCurrentMember: true,
                issuerIsAuthorized: true,
                longHaulIsEnabled: true),
            Is.False);
    }
}
