using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TextChannelManager))]
internal static class ChatSendLimitPatch
{
    [HarmonyPatch("OnEnterPressed")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OnEnterPressedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        bool replacedVanillaSendLimit = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (!replacedVanillaSendLimit && IsVanillaSendLimit(instruction))
            {
                replacedVanillaSendLimit = true;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(ChatSendLimitPatch), nameof(GetMaxChatSendCharacters)));
                continue;
            }

            yield return instruction;
        }

        if (!replacedVanillaSendLimit)
        {
            Plugin.Log?.LogWarning("ChatLimitRemover could not find the vanilla 250-character send cap in TextChannelManager.OnEnterPressed.");
        }
    }

    private static int GetMaxChatSendCharacters()
    {
        return Plugin.LockedMaxChatCharacters;
    }

    private static bool IsVanillaSendLimit(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int value && value == 250;
    }
}
