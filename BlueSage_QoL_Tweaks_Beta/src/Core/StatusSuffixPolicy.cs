using System;

namespace BlueSage.QoLTweaks.Core;

internal static class StatusSuffixPolicy
{
    internal static string Build(string statusMessage, string statusColor, string statusBrackets)
    {
        string status = (statusMessage ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(status)) return string.Empty;

        string brackets = statusBrackets ?? "()";
        if (brackets.Length < 2) brackets = "()";

        string color = RichTextStyleBuilder.TryNormalizeHex(statusColor, out string normalized)
            ? normalized
            : "FFD45D";
        return RichTextStyleBuilder.BuildColorTag(
            color,
            brackets.Substring(0, 1) + status + brackets.Substring(1, 1));
    }
}
