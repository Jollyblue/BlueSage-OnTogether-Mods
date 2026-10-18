using System;
using System.Text.RegularExpressions;

#nullable disable

namespace BlueSage.QoLTweaks.Core;

internal static class ChatUrlDetector
{
    private static readonly Regex WebUrlPattern = new Regex(
        @"\b((?:https?://|www\.)[^\s<>""']+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly char[] TrailingSentencePunctuation =
    {
        '.', ',', '!', '?', ';', ':', ')', ']', '}'
    };

    internal static bool TryGetFirstUrl(string value, out string url)
    {
        url = string.Empty;
        string text = ChatCopySanitizer.ToClipboardText(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = WebUrlPattern.Match(text);
        if (!match.Success)
        {
            return false;
        }

        string candidate = match.Groups[1].Value.Trim().TrimEnd(TrailingSentencePunctuation);
        if (candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        url = parsed.AbsoluteUri;
        return true;
    }
}
