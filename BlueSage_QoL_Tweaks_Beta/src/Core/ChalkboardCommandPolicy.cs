using System;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal static class ChalkboardCommandPolicy
{
    internal const int FreshListSeconds = 30;

    internal static bool TryAuthorizeDestructiveAction(
        int boardIndex,
        int liveBoardCount,
        string lastListedBoardSetSignature,
        long lastListedUnixSeconds,
        string currentBoardSetSignature,
        long nowUnixSeconds,
        bool rollbackUnresolved,
        out string error)
    {
        error = string.Empty;
        if (rollbackUnresolved)
        {
            error = "A prior board replacement has unresolved rollback state. Destructive chalk actions remain locked until the board set changes or the game is restarted.";
            return false;
        }
        if (!ChalkboardCatalogPolicy.TryGet(boardIndex, out _) ||
            liveBoardCount <= boardIndex)
        {
            error = "That exact board is not in the verified chalkboard catalog (boards 0-2).";
            return false;
        }
        if (string.IsNullOrWhiteSpace(lastListedBoardSetSignature) ||
            string.IsNullOrWhiteSpace(currentBoardSetSignature) ||
            !string.Equals(lastListedBoardSetSignature, currentBoardSetSignature, StringComparison.Ordinal))
        {
            error = "The live board map changed or has not been listed. Run /chalk boards, then repeat the exact action within 30 seconds.";
            return false;
        }
        long age = nowUnixSeconds - lastListedUnixSeconds;
        if (age < 0 || age > FreshListSeconds)
        {
            error = "The board map is stale. Run /chalk boards, then repeat the exact action within 30 seconds.";
            return false;
        }
        return true;
    }

    internal static string BuildActionTruth(
        string action,
        int boardIndex,
        string scene,
        string authority,
        int compatibleRecipients,
        string snapshot,
        string rollback,
        string vanillaObserver)
    {
        return "Chalk " + Bound(action, "status") +
               ": board=" + boardIndex.ToString(CultureInfo.InvariantCulture) +
               ", scene=" + Bound(scene, "unavailable") +
               ", authority=" + Bound(authority, "local-only") +
               ", compatibleRecipients=" + Math.Max(0, compatibleRecipients).ToString(CultureInfo.InvariantCulture) +
               ", snapshot=" + Bound(snapshot, "none") +
               ", rollback=" + Bound(rollback, "not-needed") +
               ", vanillaObserver=" + Bound(vanillaObserver, "not-observed") + ".";
    }

    private static string Bound(string value, string fallback)
    {
        string source = (value ?? string.Empty).Trim();
        StringBuilder clean = new StringBuilder(Math.Min(source.Length, 96));
        bool whitespace = false;
        for (int index = 0; index < source.Length && clean.Length < 96; index++)
        {
            char character = source[index];
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (clean.Length > 0 && !whitespace)
                {
                    clean.Append(' ');
                    whitespace = true;
                }
                continue;
            }
            if (character == '<' || character == '>')
            {
                continue;
            }
            clean.Append(character);
            whitespace = false;
        }
        string result = clean.ToString().Trim();
        return result.Length == 0 ? fallback : result;
    }
}
