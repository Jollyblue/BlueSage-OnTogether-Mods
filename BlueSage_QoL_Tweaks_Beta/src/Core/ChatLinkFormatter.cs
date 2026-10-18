using System.Text.RegularExpressions;

#nullable disable

namespace BlueSage.QoLTweaks.Core;

internal static class ChatLinkFormatter
{
    private const string LinkColor = "70D6FF";

    private static readonly Regex WebUrlPattern = new Regex(
        @"(?<![=>""'])\b((?:https?://|www\.)[^\s<>""']+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly char[] TrailingSentencePunctuation =
    {
        '.', ',', '!', '?', ';', ':', ')', ']', '}'
    };

    internal static bool TryColorizeUrls(string value, out string formatted)
    {
        formatted = value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(formatted) || formatted.Contains("<color=#" + LinkColor + ">"))
        {
            return false;
        }

        bool changed = false;
        formatted = WebUrlPattern.Replace(formatted, match =>
        {
            string token = match.Groups[1].Value;
            string trimmed = token.TrimEnd(TrailingSentencePunctuation);
            if (trimmed.Length == 0)
            {
                return token;
            }

            changed = true;
            string trailing = token.Substring(trimmed.Length);
            return "<color=#" + LinkColor + "><u>" + trimmed + "</u></color>" + trailing;
        });

        return changed;
    }
}
