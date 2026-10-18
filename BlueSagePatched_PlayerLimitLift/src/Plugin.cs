using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BlueSagePatched.PlayerLimitLift;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("OnTogether.exe")]
[BepInProcess("On-Together")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.bluesage.ontogether.playerlimitlift";
    public const string PluginName = "BlueSagePatched PlayerLimitLift";
    public const string PluginVersion = "1.1.8";

    private readonly Harmony _harmony = new Harmony(PluginGuid);

    internal static ManualLogSource Log { get; private set; }
    internal static ConfigEntry<int> DefaultLobbySize { get; private set; }
    internal static ConfigEntry<int> MaxLobbySize { get; private set; }
    internal static ConfigEntry<int> ShiftSkipRate { get; private set; }
    internal static ConfigEntry<bool> EnableChatRelayPatch { get; private set; }

    internal static int EffectiveMaxLobbySize => Clamp(MaxLobbySize.Value, 16, 128);
    internal static int EffectiveDefaultLobbySize => Clamp(DefaultLobbySize.Value, 1, EffectiveMaxLobbySize);
    internal static int EffectiveShiftSkipRate => Clamp(ShiftSkipRate.Value, 1, EffectiveMaxLobbySize);

    private void Awake()
    {
        Log = Logger;
        BindConfig();
        _harmony.PatchAll();
        Log.LogInfo($"{PluginName} {PluginVersion} loaded. Lobby cap={EffectiveMaxLobbySize}, default={EffectiveDefaultLobbySize}.");
        LogStartupDiagnostics();
    }

    private void OnDestroy()
    {
        _harmony.UnpatchSelf();
        Log?.LogInfo($"{PluginName} unloaded and Harmony patches removed.");
    }

    private void BindConfig()
    {
        DefaultLobbySize = Config.Bind<int>(
            "General",
            "DefaultLobbySize",
            16,
            "Lobby size shown on launch. Clamped between 1 and MaxLobbySize. Takes effect after restart.");

        MaxLobbySize = Config.Bind<int>(
            "General",
            "MaxLobbySize",
            128,
            "Maximum lobby size exposed by this replacement mod. On-Together currently works best at 128 or lower.");

        ShiftSkipRate = Config.Bind<int>(
            "General",
            "ShiftSkipRate",
            16,
            "Amount that shift+click changes lobby size by. Clamped between 1 and MaxLobbySize.");

        EnableChatRelayPatch = Config.Bind<bool>(
            "General",
            "EnableChatRelayPatch",
            true,
            "Keeps chat functional when sender indexes exceed the vanilla 16-player color/name assumptions.");
    }

    private static void LogStartupDiagnostics()
    {
        WarnIfClamped("DefaultLobbySize", DefaultLobbySize.Value, EffectiveDefaultLobbySize);
        WarnIfClamped("MaxLobbySize", MaxLobbySize.Value, EffectiveMaxLobbySize);
        WarnIfClamped("ShiftSkipRate", ShiftSkipRate.Value, EffectiveShiftSkipRate);

        foreach (var pluginInfo in Chainloader.PluginInfos.Values)
        {
            if (pluginInfo?.Metadata == null || string.Equals(pluginInfo.Metadata.GUID, PluginGuid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string descriptor = $"{pluginInfo.Metadata.GUID} {pluginInfo.Metadata.Name}".Trim();
            string lower = descriptor.ToLowerInvariant();
            bool looksLikePlayerLimit = lower.Contains("playerlimit") || lower.Contains("player limit");
            bool looksLikeOfficerBalls = lower.Contains("officerballs") || lower.Contains("officer_balls");

            if (looksLikePlayerLimit && looksLikeOfficerBalls)
            {
                Log.LogWarning($"Old officerballs PlayerLimitLift appears loaded ({descriptor}). Remove or disable it so BlueSage PlayerLimitLift is the only PlayerLimitLift mod on the host; old client copies are the common NickName1/chat-glitch cause.");
                continue;
            }

            if (looksLikePlayerLimit)
            {
                Log.LogWarning($"Another PlayerLimitLift-like plugin appears loaded ({descriptor}). Hosts should run only one lobby-size mod at a time.");
            }
        }
    }

    private static void WarnIfClamped(string name, int configured, int effective)
    {
        if (configured == effective)
        {
            return;
        }

        Log.LogWarning($"{name}={configured} was clamped to {effective}. Check BepInEx/config/{PluginGuid}.cfg if this was not intentional.");
    }

    internal static int WrapLobbySize(int current, int delta)
    {
        int max = EffectiveMaxLobbySize;
        int zeroBased = current - 1;
        int wrapped = Mod(zeroBased + delta, max);
        return wrapped + 1;
    }

    internal static int WrapLobbyListFilter(int current, int delta)
    {
        int maxWithAll = EffectiveMaxLobbySize + 1;
        return Mod(current + delta, maxWithAll);
    }

    internal static int ClampSenderIndexForColor(int senderIndex)
    {
        return Clamp(senderIndex, 0, 15);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
