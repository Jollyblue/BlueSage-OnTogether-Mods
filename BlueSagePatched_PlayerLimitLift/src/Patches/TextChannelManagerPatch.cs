using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using PurrNet;
using PurrNet.Modules;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;

namespace BlueSagePatched.PlayerLimitLift.Patches;

[HarmonyPatch(typeof(TextChannelManager))]
internal static class TextChannelManagerPatch
{
    private static readonly MethodInfo OnChannelMessageReceivedMethod =
        AccessTools.Method(
            typeof(TextChannelManager),
            "OnChannelMessageReceived",
            new[] { typeof(string), typeof(string), typeof(Vector3), typeof(bool), typeof(int), typeof(string) });

    private static readonly MethodInfo AddMessageUiMethod =
        AccessTools.Method(
            typeof(TextChannelManager),
            "AddMessageUI",
            new[] { typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool) });

    private static bool _compatibilityWarningLogged;

    [HarmonyPatch(typeof(TextChannelManager), nameof(TextChannelManager.SendMessageAsync_Original_0))]
    [HarmonyPrefix]
    private static bool SendMessagePatch(
        TextChannelManager __instance,
        ref byte[] textBytes,
        ref byte[] userName,
        ref bool isLocal,
        ref Vector3 pos,
        ref string playerID,
        ref RPCInfo info,
        ref string ____playerId)
    {
        if (!Plugin.EnableChatRelayPatch.Value || !EnsureChatRelayCompatible())
        {
            return true;
        }

        string message = Encoding.Unicode.GetString(textBytes);
        string senderName = Encoding.Unicode.GetString(userName);
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        int senderIndex = FindSenderIndex(panel, info.sender, playerID);
        int colorIndex = Plugin.ClampSenderIndexForColor(senderIndex);

        if (!IsFromLocalPlayer(__instance, info.sender))
        {
            InvokeOnChannelMessageReceived(__instance, senderName, message, pos, isLocal, colorIndex, playerID);
            return false;
        }

        if (playerID != ____playerId)
        {
            InvokeAddMessageUi(__instance, senderName, message, isLocal, colorIndex);
            ShowRemoteTextBubble(panel, textBytes, isLocal, pos, playerID);
            return false;
        }

        InvokeAddMessageUi(__instance, senderName, message, isLocal, colorIndex);
        __instance.MainTextBubble?.ShowTextBubble(textBytes, isLocal, pos, playerID);
        return false;
    }

    // Preserve the field-proven relay path while failing open to vanilla chat
    // whenever a future game build changes either reflected chat target.
    // The generated-handler-only alternative remains benched until above-15 multiplayer UAT
    // proves it offers a real compatibility benefit without changing working chat behavior.
    [HarmonyPatch(typeof(TextChannelManager), nameof(TextChannelManager.OnReceivedRpc))]
    [HarmonyPrefix]
    private static bool RPCInterceptPatcher(ref int id, ref RPCInfo info, ref RPCPacket packet, ref bool asServer, TextChannelManager __instance)
    {
        if (!Plugin.EnableChatRelayPatch.Value || !EnsureChatRelayCompatible())
        {
            return true;
        }

        if (id != 0)
        {
            return true;
        }

        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        int senderIndex = FindSenderIndex(panel, info.sender, null);
        if (asServer && senderIndex > 15)
        {
            if (TryReadSendMessagePacket(packet, info, out byte[] textBytes, out byte[] userName, out bool isLocal, out Vector3 pos, out string playerID))
            {
                // Preserve the authoritative remote sender while the server
                // re-broadcasts the decoded payload. QoL/BlueExclusive patch
                // this wrapper to handle local slash commands; replacing the
                // sender with default(RPCInfo) would make a slot 17+ remote
                // message look host-local and could execute privileged tools.
                __instance.SendMessageAsync(textBytes, userName, isLocal, pos, playerID, info);
            }

            return false;
        }

        __instance.HandleRPCGenerated_0(packet, info, asServer);
        return false;
    }

    private static bool TryReadSendMessagePacket(
        RPCPacket packet,
        RPCInfo info,
        out byte[] textBytes,
        out byte[] userName,
        out bool isLocal,
        out Vector3 pos,
        out string playerID)
    {
        textBytes = null;
        userName = null;
        isLocal = false;
        pos = Vector3.zero;
        playerID = null;

        try
        {
            info.compileTimeSignature = RPCSignature.Make(
                RPCType.ObserversRPC,
                Channel.ReliableOrdered,
                runLocally: false,
                requireOwnership: false,
                bufferLast: false,
                requireServer: true,
                excludeOwner: false,
                "SendMessageAsync",
                isStatic: false,
                5f,
                PurrNet.CompressionLevel.None,
                excludeSender: false,
                deltaPacked: false);

            BitData data = packet.data;
            RPCModule.PostProcessRpc(info, ref data);
            using (data.AutoScope())
            {
                BitPacker packer = data.packer;
                Packer<byte[]>.Read(packer, ref textBytes);
                Packer<byte[]>.Read(packer, ref userName);
                Packer<bool>.Read(packer, ref isLocal);
                Packer<Vector3>.Read(packer, ref pos);
                Packer<string>.Read(packer, ref playerID);
            }

            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Failed to decode oversized-lobby chat RPC; dropping packet to avoid corrupt chat state. {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static int FindSenderIndex(PlayerPanelController panel, PlayerID sender, string playerID)
    {
        if (panel == null)
        {
            return 0;
        }

        List<PlayerID> playerIDs = panel.PlayerIDs;
        for (int i = 0; i < playerIDs.Count; i++)
        {
            if (sender == playerIDs[i])
            {
                return i;
            }
        }

        if (!string.IsNullOrEmpty(playerID))
        {
            int steamIndex = panel.PlayerSteamIDs.IndexOf(playerID);
            if (steamIndex >= 0)
            {
                return steamIndex;
            }
        }

        return 0;
    }

    private static bool IsFromLocalPlayer(TextChannelManager instance, PlayerID sender)
    {
        PlayerID? localPlayer = instance.localPlayer;
        return localPlayer.HasValue && sender == localPlayer.GetValueOrDefault();
    }

    private static void InvokeOnChannelMessageReceived(TextChannelManager instance, string senderName, string message, Vector3 pos, bool isLocal, int colorIndex, string playerID)
    {
        OnChannelMessageReceivedMethod.Invoke(instance, new object[] { senderName, message, pos, isLocal, colorIndex, playerID });
    }

    private static void InvokeAddMessageUi(TextChannelManager instance, string senderName, string message, bool isLocal, int colorIndex)
    {
        AddMessageUiMethod.Invoke(instance, new object[] { senderName, message, isLocal, colorIndex, true });
    }

    private static bool EnsureChatRelayCompatible()
    {
        if (OnChannelMessageReceivedMethod != null && AddMessageUiMethod != null)
        {
            return true;
        }

        if (!_compatibilityWarningLogged)
        {
            _compatibilityWarningLogged = true;
            Plugin.Log?.LogWarning(
                "Chat Relay compatibility target changed. PlayerLimitLift is failing open to the game's vanilla chat handler; " +
                "lobby-size patches remain active.");
        }

        return false;
    }

    private static void ShowRemoteTextBubble(PlayerPanelController panel, byte[] textBytes, bool isLocal, Vector3 pos, string playerID)
    {
        if (panel == null)
        {
            return;
        }

        int playerIndex = panel.PlayerSteamIDs.IndexOf(playerID);
        if (playerIndex < 0 || playerIndex >= panel.PlayerTransforms.Count)
        {
            return;
        }

        NetworkTransform playerTransform = panel.PlayerTransforms[playerIndex];
        if (playerTransform == null)
        {
            return;
        }

        TextBubbleController textBubble = playerTransform.GetComponentInChildren<TextBubbleController>();
        textBubble?.ShowTextBubble(textBytes, isLocal, pos, playerID);
    }
}
