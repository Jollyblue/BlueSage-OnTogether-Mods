using System.Collections.Generic;
using HarmonyLib;

namespace BlueSagePatched.PlayerLimitLift.Patches;

[HarmonyPatch(typeof(MultiplayerManager))]
internal static class ServerTagPatch
{
    [HarmonyPatch("CreateLobby")]
    [HarmonyPrefix]
    private static void MarkCreatedLobbyAsModded(ref List<bool> ___FilterSocialTags)
    {
        if (___FilterSocialTags == null)
        {
            return;
        }

        while (___FilterSocialTags.Count <= 4)
        {
            ___FilterSocialTags.Add(false);
        }

        ___FilterSocialTags[4] = true;
    }
}
