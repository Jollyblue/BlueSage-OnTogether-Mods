using System;
using System.Globalization;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using Steamworks;

namespace BlueSage.QoLTweaks;

internal static class ChalkboardModerationController
{
    internal static string DescribeAuthority()
    {
        if (!SteamIdModerationController.CanLocalIssue(out bool isHost, out _))
        {
            return "local-only";
        }
        return isHost ? "Host-direct" : "authenticated Helper-to-Host";
    }

    internal static int CountCompatibleRecipients()
    {
        return (PlayerIdentityEvidenceController.GetVerifiedRoster() ??
                Array.Empty<PlayerIdentityEvidence>())
            .Count(IsCompatibleRecipient);
    }

    internal static bool IsCompatibleRecipient(PlayerIdentityEvidence evidence)
    {
        if (evidence == null ||
            evidence.IsLocal ||
            !ulong.TryParse(
                evidence.SteamId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong steamId) ||
            !Plugin.TryGetRescueLobby(out CSteamID lobby) ||
            lobby == CSteamID.Nil)
        {
            return false;
        }

        try
        {
            string payload = SteamMatchmaking.GetLobbyMemberData(
                lobby,
                new CSteamID(steamId),
                CompatibleClientPresencePolicy.MemberDataKey);
            return CompatibleClientPresencePolicy.IsFresh(
                payload,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        catch
        {
            return false;
        }
    }

    internal static string BuildTruth(
        string action,
        int boardIndex,
        string scene,
        string snapshot,
        string rollback,
        string vanillaObserver = "not-observed")
    {
        return ChalkboardCommandPolicy.BuildActionTruth(
            action,
            boardIndex,
            scene,
            DescribeAuthority(),
            CountCompatibleRecipients(),
            snapshot,
            rollback,
            vanillaObserver);
    }
}
