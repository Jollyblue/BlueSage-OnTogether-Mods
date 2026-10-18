using HarmonyLib;
using PurrNet;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TextChannelManager))]
internal static class BlueSageCommandPatch
{
    [HarmonyPatch("SendMessageAsync", typeof(byte[]), typeof(byte[]), typeof(bool), typeof(Vector3), typeof(string), typeof(RPCInfo))]
    [HarmonyPrefix]
    private static bool SendMessageAsyncPrefix(byte[] __0, RPCInfo __5)
    {
        if (!IsLocalCommandInvocation(__5))
        {
            // Human-readable chat is presentation only. Never turn a sentence a
            // normal player can type into moderation-grade Clone Shield state.
            // Every observer must derive suspect/victim direction from its own
            // aligned Steam/PurrNet/player-row transition evidence.
            return true;
        }

        return Plugin.HandleOutgoingChatCommand(__0);
    }

    private static bool IsLocalCommandInvocation(RPCInfo info)
    {
        return info.manager == null && info.sender.Equals(default(PlayerID));
    }
}
