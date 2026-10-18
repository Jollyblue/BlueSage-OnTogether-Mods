using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct RescueFollowLease
{
    internal RescueFollowLease(string lobbyId, string ownerSteamId, string issuerSteamId, bool longHaul)
    {
        LobbyId = lobbyId ?? string.Empty;
        OwnerSteamId = ownerSteamId ?? string.Empty;
        IssuerSteamId = issuerSteamId ?? string.Empty;
        LongHaul = longHaul;
    }

    internal string LobbyId { get; }
    internal string OwnerSteamId { get; }
    internal string IssuerSteamId { get; }
    internal bool LongHaul { get; }
}

internal static class RescueFollowLeasePolicy
{
    internal static bool IsCurrent(
        RescueFollowLease lease,
        string currentLobbyId,
        string currentOwnerSteamId,
        bool issuerIsCurrentMember,
        bool issuerIsAuthorized,
        bool longHaulIsEnabled)
    {
        if (string.IsNullOrWhiteSpace(lease.LobbyId) ||
            string.IsNullOrWhiteSpace(lease.OwnerSteamId) ||
            string.IsNullOrWhiteSpace(lease.IssuerSteamId))
        {
            return false;
        }

        return string.Equals(lease.LobbyId, currentLobbyId, StringComparison.Ordinal) &&
               string.Equals(lease.OwnerSteamId, currentOwnerSteamId, StringComparison.Ordinal) &&
               issuerIsCurrentMember &&
               issuerIsAuthorized &&
               (!lease.LongHaul || longHaulIsEnabled);
    }
}
