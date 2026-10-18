using System;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerCustomizationController), "UpdateOnNewPeopleJoin_Original_0")]
internal static class RosterRegistrationGuardPatch
{
    private static int _invalidRegistrationCount;
    private static int _exactDuplicateCount;
    private static int _replacementCount;

    [HarmonyPrefix]
    private static bool Prefix(
        PlayerCustomizationController __instance,
        string playerSteamID,
        NetworkTransform networkTransform,
        string vivoxId)
    {
#if MODDERBRANCH_UAT
        if (Plugin.EnableRosterAndNetworkGuards?.Value != true)
        {
            return true;
        }
#endif

        if (__instance == null || !__instance.isServer)
        {
            return true;
        }

        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel == null)
        {
            return true;
        }

        RosterRegistrationDecision decision = RosterRegistrationPolicy.Evaluate(
            playerSteamID,
            vivoxId,
            networkTransform,
            panel.PlayerSteamIDs,
            panel.VivoxIDs,
            panel.PlayerTransforms);

        switch (decision.Action)
        {
            case RosterRegistrationAction.RejectInvalid:
                _invalidRegistrationCount++;
                Plugin.Log?.LogWarning($"[BlueSageRosterGuard] utc={DateTime.UtcNow:O}; action=reject-invalid; count={_invalidRegistrationCount}; identityDetail=private-audit-only");
                return false;

            case RosterRegistrationAction.IgnoreExactDuplicate:
                _exactDuplicateCount++;
                if (_exactDuplicateCount == 1 || _exactDuplicateCount % 25 == 0)
                {
                    Plugin.Log?.LogInfo($"[BlueSageRosterGuard] utc={DateTime.UtcNow:O}; action=ignore-exact-duplicate; count={_exactDuplicateCount}; identityDetail=private-audit-only");
                }
                return false;

            case RosterRegistrationAction.ReplaceStableIdentity:
                if (IsServerBannedIdentity(playerSteamID))
                {
                    return true;
                }

                if (PanelRosterRowGuard.RemoveParallelRow(panel, decision.ExistingIndex))
                {
                    _replacementCount++;
                    Plugin.Log?.LogInfo($"[BlueSageRosterGuard] utc={DateTime.UtcNow:O}; action=replace-stale-identity; count={_replacementCount}; identityDetail=private-audit-only");
                }
                return true;

            default:
                return true;
        }
    }

    private static bool IsServerBannedIdentity(string playerSteamId)
    {
        try
        {
            return MonoSingleton<DataManager>.I != null &&
                   MonoSingleton<DataManager>.I.BanData != null &&
                   MonoSingleton<DataManager>.I.BanData.BanServerPlayers.Contains(playerSteamId);
        }
        catch
        {
            return false;
        }
    }
}

[HarmonyPatch(typeof(PlayerPanelController), "DespawnHandle", typeof(NetworkTransform))]
internal static class RosterDespawnDuplicateGuardPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerPanelController __instance, NetworkTransform netTransform)
    {
#if MODDERBRANCH_UAT
        if (Plugin.EnableRosterAndNetworkGuards?.Value != true)
        {
            return;
        }
#endif

        if (__instance == null || ReferenceEquals(netTransform, null))
        {
            return;
        }

        int removed = 0;
        for (int index = __instance.PlayerTransforms.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(__instance.PlayerTransforms[index], netTransform))
            {
                continue;
            }

            if (PanelRosterRowGuard.RemoveParallelRow(__instance, index))
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            __instance.UpdateServerPanelOnChange();
            Plugin.Log?.LogInfo($"[BlueSageRosterGuard] utc={DateTime.UtcNow:O}; action=remove-despawn-duplicates; removed={removed}; identityDetail=private-audit-only");
        }
    }
}

internal static class PanelRosterRowGuard
{
    internal static bool RemoveParallelRow(PlayerPanelController panel, int index)
    {
        if (panel == null || index < 0 ||
            panel.PlayerSteamIDs == null || panel.PlayerSteamIDs.Count <= index ||
            panel.VivoxIDs == null || panel.VivoxIDs.Count <= index ||
            panel.PomoShows == null || panel.PomoShows.Count <= index ||
            panel.IsVoiceChatActive == null || panel.IsVoiceChatActive.Count <= index ||
            panel.PlayerTransforms == null || panel.PlayerTransforms.Count <= index ||
            panel.PlayerIDs == null || panel.PlayerIDs.Count <= index ||
            panel.IDInfos == null || panel.IDInfos.Count <= index ||
            panel.PlayerControllers == null || panel.PlayerControllers.Count <= index)
        {
            return false;
        }

        panel.PlayerSteamIDs.RemoveAt(index);
        panel.VivoxIDs.RemoveAt(index);
        panel.PomoShows.RemoveAt(index);
        panel.IsVoiceChatActive.RemoveAt(index);
        panel.PlayerTransforms.RemoveAt(index);
        panel.PlayerIDs.RemoveAt(index);
        panel.IDInfos.RemoveAt(index);
        panel.PlayerControllers.RemoveAt(index);
        return true;
    }
}
