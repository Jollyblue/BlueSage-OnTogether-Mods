using System.Collections.Generic;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

/// <summary>
/// Speech bubble: the bubble above a player's head uses the same 5 m local-chat check as the chat window, in its own
/// method. Widening only the chat window would show a line with no bubble over the speaker, so it follows the same
/// setting. The ignore and mute checks in front of the distance test stay as the game wrote them.
/// </summary>
[HarmonyPatch(typeof(TextBubbleController), "ShowTextBubble_Original_1")]
internal static class LocalChatBubbleRangePatch
{
    internal static bool HasExpectedCoverage { get; private set; }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ShowTextBubbleTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> body = LocalChatRangeTranspiler.Apply(instructions, "speech bubble", out bool applied);
        HasExpectedCoverage = applied;
        return body;
    }
}
