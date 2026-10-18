using System;
using System.Text;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using PurrNet.Packing;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TextChannelManager), nameof(TextChannelManager.HandleRPCGenerated_0),
    new[] { typeof(RPCPacket), typeof(RPCInfo), typeof(bool) })]
internal static class ModerationRelayPatch
{
    private static bool _legacyPacketWarningLogged;

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(RPCPacket packet, RPCInfo info, bool asServer)
    {
        if (!asServer) return true;
        bool recognizedBlueSagePacket = false;
        var reader = BitPackerPool.Get();
        try
        {
            reader.WriteBitDataWithoutConsumingIt(packet.data);
            reader.ResetPositionAndMode(true);
            byte[] textBytes = null;
            Packer<byte[]>.Read(reader, ref textBytes);
            if (textBytes == null || textBytes.Length == 0 || textBytes.Length > 1024) return true;
            string text = Encoding.Unicode.GetString(textBytes).Trim();
            if (text.StartsWith(CloneIncidentResolutionProtocolPolicy.Prefix, StringComparison.Ordinal) ||
                text.StartsWith(ModerationProtocolPolicy.Prefix, StringComparison.Ordinal))
            {
                recognizedBlueSagePacket = true;
                if (!_legacyPacketWarningLogged)
                {
                    _legacyPacketWarningLogged = true;
                    Plugin.Log?.LogWarning("Rejected a legacy chat-shaped BlueSage moderation request. Matching 0.2.0 v3 member-data capability is required; no action was taken and no packet was relayed to public chat.");
                }
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Moderation relay decode failed safely: " + ex.GetType().Name + ": " + ex.Message);
            // Once the exact private prefix is recognized, fail closed. An
            // invalid internal request must never fall through into chat.
            return !recognizedBlueSagePacket;
        }
        finally
        {
            reader.Dispose();
        }
    }
}
