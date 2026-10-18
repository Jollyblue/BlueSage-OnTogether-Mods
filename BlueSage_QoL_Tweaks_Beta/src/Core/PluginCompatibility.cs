using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal sealed class PluginCompatibilityResult
{
    public PluginCompatibilityResult(
        bool autoSweepAllowed,
        bool manualSweepAllowed,
        bool chatTweaksAllowed,
        bool hostHealthRepairsAllowed,
        bool hushLoaded,
        bool fomoLoaded,
        bool echoLoaded,
        bool alphaLoaded,
        bool chalkyLoaded,
        bool desyncLoaded,
        IReadOnlyList<string> reasons)
    {
        AutoSweepAllowed = autoSweepAllowed;
        ManualSweepAllowed = manualSweepAllowed;
        ChatTweaksAllowed = chatTweaksAllowed;
        HostHealthRepairsAllowed = hostHealthRepairsAllowed;
        HushLoaded = hushLoaded;
        FomoLoaded = fomoLoaded;
        EchoLoaded = echoLoaded;
        AlphaLoaded = alphaLoaded;
        ChalkyLoaded = chalkyLoaded;
        DesyncLoaded = desyncLoaded;
        Reasons = reasons;
    }

    public bool AutoSweepAllowed { get; }
    public bool ManualSweepAllowed { get; }
    public bool ChatTweaksAllowed { get; }
    public bool HostHealthRepairsAllowed { get; }
    public bool HushLoaded { get; }
    public bool FomoLoaded { get; }
    public bool EchoLoaded { get; }
    public bool AlphaLoaded { get; }
    public bool ChalkyLoaded { get; }
    public bool DesyncLoaded { get; }
    public IReadOnlyList<string> Reasons { get; }
}

internal static class PluginCompatibility
{
    public const string AndrewSweepGuid = "com.andrewlin.ontogether.sweep";
    public const string AndrewReconnectGuid = "com.andrewlin.ontogether.reconnect";
    public const string SimpleQolGuid = "tinyplume.SimpleQOL";
    public const string BlueSagePlayerLimitLiftGuid = "com.bluesage.ontogether.playerlimitlift";
    public const string LegacyCopyChatGuid = "com.jai.ontogether.copychat";
    public const string AndrewHushGuid = "com.andrewlin.ontogether.hush";
    public const string AndrewFomoGuid = "com.andrewlin.ontogether.fomo";
    public const string AndrewEchoGuid = "com.andrewlin.ontogether.echo";
    public const string AndrewAlphaGuid = "com.andrewlin.ontogether.alpha";
    public const string AndrewChalkyGuid = "com.andrewlin.ontogether.chalky";
    public const string AndrewDesyncGuid = "com.andrewlin.ontogether.desync";

    public static PluginCompatibilityResult Evaluate(
        IEnumerable<string> loadedPluginGuids,
        bool autoDisableSweepWhenAndrewSweepInstalled,
        bool allowManualSweepWhenAndrewSweepInstalled,
        bool autoDisableChatTweaksWhenSimpleQoLInstalled)
    {
        HashSet<string> loaded = new HashSet<string>(loadedPluginGuids ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        List<string> reasons = new List<string>();

        bool autoSweepAllowed = true;
        bool manualSweepAllowed = true;
        bool chatTweaksAllowed = true;
        bool hushLoaded = loaded.Contains(AndrewHushGuid);
        bool fomoLoaded = loaded.Contains(AndrewFomoGuid);
        bool echoLoaded = loaded.Contains(AndrewEchoGuid);
        bool alphaLoaded = loaded.Contains(AndrewAlphaGuid);
        bool chalkyLoaded = loaded.Contains(AndrewChalkyGuid);
        bool desyncLoaded = loaded.Contains(AndrewDesyncGuid);
        bool hostHealthRepairsAllowed = !desyncLoaded;

        bool andrewSweepLoaded = HasGuidOrFuzzyMatch(loaded, AndrewSweepGuid, "sweep");
        if (autoDisableSweepWhenAndrewSweepInstalled && andrewSweepLoaded)
        {
            autoSweepAllowed = false;
            reasons.Add("AndrewLin Sweep is loaded; BlueSage auto sweep is disabled to avoid double sweeping.");

            if (!allowManualSweepWhenAndrewSweepInstalled)
            {
                manualSweepAllowed = false;
                reasons.Add("AndrewLin Sweep is loaded; BlueSage manual sweep hotkey is disabled unless explicitly allowed in config.");
            }
        }

        if (autoDisableChatTweaksWhenSimpleQoLInstalled &&
            (HasGuidOrFuzzyMatch(loaded, SimpleQolGuid, "simpleqol", "simple_qol") ||
             HasGuidOrFuzzyMatch(loaded, "officerballs.ChatTweaks", "chattweaks", "chat_tweaks")))
        {
            chatTweaksAllowed = false;
            reasons.Add("Tiny_Plume Simple_QOL is loaded; BlueSage timestamps and leave notices are disabled to avoid duplicate chat formatting.");
        }

        if (desyncLoaded)
        {
            reasons.Add("AndrewLin Desync is loaded; BlueSage automatic Host Health repairs are paused to avoid duplicate lobby metadata and persona refresh work. Manual Host Health remains read-only.");
        }

        return new PluginCompatibilityResult(
            autoSweepAllowed,
            manualSweepAllowed,
            chatTweaksAllowed,
            hostHealthRepairsAllowed,
            hushLoaded,
            fomoLoaded,
            echoLoaded,
            alphaLoaded,
            chalkyLoaded,
            desyncLoaded,
            reasons);
    }

    private static bool HasGuidOrFuzzyMatch(HashSet<string> loaded, string exactGuid, params string[] fuzzyNeedles)
    {
        if (loaded.Contains(exactGuid))
        {
            return true;
        }

        foreach (string pluginGuid in loaded)
        {
            if (!LooksLikeLoadedPluginGuid(pluginGuid))
            {
                continue;
            }

            string normalized = pluginGuid.Replace("-", string.Empty).Replace("_", string.Empty).Replace(".", string.Empty);
            foreach (string needle in fuzzyNeedles)
            {
                string normalizedNeedle = needle.Replace("-", string.Empty).Replace("_", string.Empty).Replace(".", string.Empty);
                if (normalized.IndexOf(normalizedNeedle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool LooksLikeLoadedPluginGuid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.IndexOfAny(new[] { '\\', '/', ':' }) >= 0)
        {
            return false;
        }

        string lower = value.ToLowerInvariant();
        return !lower.EndsWith(".dll") &&
               !lower.Contains(".dll.") &&
               !lower.EndsWith(".old") &&
               !lower.EndsWith(".bak") &&
               !lower.EndsWith(".disabled");
    }
}
