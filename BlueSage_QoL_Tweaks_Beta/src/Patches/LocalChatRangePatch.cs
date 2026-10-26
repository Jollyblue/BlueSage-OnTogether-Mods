using System.Collections.Generic;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

/// <summary>
/// Chat window: widens the distance at which a local chat line is shown in this player's own chat window.
/// The game's receive check is <c>SqrMagnitude(sender - me) &gt; 25</c> (5 m), after its ignore and mute checks.
/// </summary>
[HarmonyPatch(typeof(TextChannelManager), "OnChannelMessageReceived")]
internal static class LocalChatRangePatch
{
    internal static bool HasExpectedCoverage { get; private set; }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnChannelMessageReceivedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> body = LocalChatRangeTranspiler.Apply(instructions, "chat window", out bool applied);
        HasExpectedCoverage = applied;
        return body;
    }
}
