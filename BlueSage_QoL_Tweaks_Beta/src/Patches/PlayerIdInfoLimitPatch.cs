using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.GetPlayerIdInfo))]
internal static class PlayerIdInfoLimitPatch
{
    internal const int VanillaSafeMaxCharacters = 1000;
    internal static bool HasExpectedCoverage { get; private set; }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> GetPlayerIdInfoTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        int replacedVanillaIdCardCaps = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int value && value == VanillaSafeMaxCharacters)
            {
                replacedVanillaIdCardCaps++;
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(PlayerIdInfoLimitPatch), nameof(GetMaxIdCardCharacters)));
                continue;
            }

            yield return instruction;
        }

        HasExpectedCoverage = replacedVanillaIdCardCaps == 6;
        if (!HasExpectedCoverage)
        {
            Plugin.Log?.LogWarning($"ID-card text limit patch expected 6 vanilla 1000-character caps but replaced {replacedVanillaIdCardCaps}.");
        }
    }

    private static int GetMaxIdCardCharacters()
    {
        return Plugin.LockedMaxIdCardCharacters;
    }
}
