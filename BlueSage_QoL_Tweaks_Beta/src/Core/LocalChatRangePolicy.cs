using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

/// <summary>
/// How far away a local chat line can be and still show up in this player's own chat window.
/// The game decides this on the receiving side: it drops a local line when the squared distance to the sender
/// is greater than 25 (5 m). Only this player's screen changes; nothing is sent to anyone else.
/// </summary>
internal static class LocalChatRangePolicy
{
    public const float GameMeters = 5f;
    public const float GameSquaredMeters = 25f;
    public const float DefaultMeters = 15f;
    public const float MinMeters = 5f;
    public const float MaxMeters = 25f;

    public static float ClampMeters(float configuredMeters)
    {
        if (float.IsNaN(configuredMeters))
        {
            return DefaultMeters;
        }

        return Math.Max(MinMeters, Math.Min(MaxMeters, configuredMeters));
    }

    /// <summary>
    /// The squared range the game's distance check should use. A setting at the game's own 5 m keeps the game's
    /// value exactly (25), so choosing 5 is the same as having no change at all.
    /// </summary>
    public static float ResolveSquaredMeters(float configuredMeters)
    {
        float meters = ClampMeters(configuredMeters);
        return meters <= GameMeters ? GameSquaredMeters : meters * meters;
    }

    /// <summary>
    /// Finds the single distance constant to replace: a <c>ldc.r4 25</c> that directly follows a call to
    /// <c>SqrMagnitude</c>. Both lists describe the same method body, one entry per instruction (opcode name such as
    /// "call" or "ldc.r4", and the operand as text). Anything other than exactly one match means the game changed, and
    /// the caller must leave the method alone.
    /// </summary>
    public static int[] FindGateConstantIndexes(IList<string> opcodes, IList<string> operands)
    {
        List<int> hits = new List<int>();
        if (opcodes == null || operands == null || opcodes.Count != operands.Count)
        {
            return hits.ToArray();
        }

        for (int i = 1; i < opcodes.Count; i++)
        {
            if (!string.Equals(opcodes[i], "ldc.r4", StringComparison.Ordinal) ||
                !IsGameSquaredRange(operands[i]))
            {
                continue;
            }

            if (string.Equals(opcodes[i - 1], "call", StringComparison.Ordinal) &&
                operands[i - 1] != null &&
                operands[i - 1].IndexOf("SqrMagnitude", StringComparison.Ordinal) >= 0)
            {
                hits.Add(i);
            }
        }

        return hits.ToArray();
    }

    private static bool IsGameSquaredRange(string operand)
    {
        float parsed;
        return float.TryParse(operand, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed) &&
               parsed == GameSquaredMeters;
    }
}
