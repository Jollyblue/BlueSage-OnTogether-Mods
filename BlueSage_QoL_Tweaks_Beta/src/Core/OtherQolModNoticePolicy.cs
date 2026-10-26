using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

// Round 4 WS6 (Blue, 2026-10-19): a calm compatibility heads-up when another Helper-like QoL mod is loaded.
// Voice pass (2026-10-23): Blue's meaning in shorter, gentler words, plus the names actually detected so the
// note stays truthful.
// Review R11: detection (GUIDs and names) lives in PluginCompatibility, the one place that knows other mods.
internal static class OtherQolModNoticePolicy
{
    internal const string BlueSentence =
        "BlueSage QoL may act oddly next to other big QoL mods like TogetherPlus. If something breaks, try without them first and tell us on Discord.";

    internal static bool TryBuildNotice(IEnumerable<string> loadedPluginGuids, out string message)
    {
        IReadOnlyList<string> detected = PluginCompatibility.DetectHelperLikeMods(loadedPluginGuids);
        if (detected.Count == 0)
        {
            message = string.Empty;
            return false;
        }

        string[] names = new string[detected.Count];
        for (int index = 0; index < detected.Count; index++)
        {
            names[index] = detected[index];
        }

        message = BlueSentence + " Detected: " + string.Join(", ", names) + ".";
        return true;
    }

    // Review R3: "overlay visible" covers the welcome pop-up and the full guide pop-up alike.
    internal static bool ShouldFire(bool inLobby, bool anyOverlayVisible, bool alreadyShownThisSession)
    {
        return inLobby && !anyOverlayVisible && !alreadyShownThisSession;
    }
}
