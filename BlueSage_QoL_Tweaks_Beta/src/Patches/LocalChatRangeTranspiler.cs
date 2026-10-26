using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

/// <summary>
/// The one edit shared by every place the game applies its 5 m local-chat distance check (the chat window and the
/// speech bubble): replace the single <c>ldc.r4 25</c> that follows <c>SqrMagnitude</c> with the configured range.
/// Nothing else in either method is touched, so the game's ignore and mute checks and everything after the distance
/// test run exactly as before. Fail-closed: any match count other than one returns the original instructions.
/// </summary>
internal static class LocalChatRangeTranspiler
{
    internal static List<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, string place, out bool applied)
    {
        List<CodeInstruction> body = instructions.ToList();
        string[] opcodes = body.Select(instruction => instruction.opcode.Name).ToArray();
        string[] operands = body.Select(DescribeOperand).ToArray();

        int[] hits = LocalChatRangePolicy.FindGateConstantIndexes(opcodes, operands);
        applied = hits.Length == 1;
        if (!applied)
        {
            Plugin.Log?.LogWarning($"Local chat range ({place}) expected one 5 m distance check but found {hits.Length}; it keeps the game's range.");
            return body;
        }

        // Change the instruction in place so any jump label that points at it keeps working.
        CodeInstruction constant = body[hits[0]];
        constant.opcode = OpCodes.Call;
        constant.operand = AccessTools.Method(typeof(LocalChatRangeTranspiler), nameof(CurrentSquaredMeters));
        return body;
    }

    internal static float CurrentSquaredMeters()
    {
        return Plugin.LocalChatRangeSquaredMeters;
    }

    private static string DescribeOperand(CodeInstruction instruction)
    {
        if (instruction.operand is float single)
        {
            return single.ToString("R", CultureInfo.InvariantCulture);
        }

        return instruction.operand?.ToString() ?? string.Empty;
    }
}
