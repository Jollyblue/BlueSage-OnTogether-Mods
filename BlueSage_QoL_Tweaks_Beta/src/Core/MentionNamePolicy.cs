using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal static class MentionNamePolicy
{
    private const string StatusGroupPattern = @"(?:\[[^\[\]]+\]|\([^\(\)]+\))";
    private static readonly Regex RichTextTagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex TrailingStatusGroups = new Regex(@"(?:\s*" + StatusGroupPattern + @")+\s*$", RegexOptions.Compiled);
    private static readonly Regex RepeatedWhitespace = new Regex(@"\s+", RegexOptions.Compiled);
    private static readonly Regex MentionSafeWord = new Regex(@"[\p{L}\p{Nd}_][\p{L}\p{Nd}\p{M}_.'-]*", RegexOptions.Compiled);
    private static readonly Regex MentionSafeFallback = new Regex(@"\S+", RegexOptions.Compiled);
    public static readonly Regex MentionReferenceRegex = new Regex(@"@([^\s<]+)((?:\s*" + StatusGroupPattern + @")*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ToMentionDisplayName(string value)
    {
        string clean = ToMentionDisplayLabel(value);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return string.Empty;
        }

        string withoutStatus = TrailingStatusGroups.Replace(clean, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(withoutStatus) ? clean.Trim() : withoutStatus;
    }

    public static string ToMentionDisplayLabel(string value)
    {
        return RichTextTagRegex.Replace(value ?? string.Empty, string.Empty).Trim().Trim('\0');
    }

    public static string ToCanonicalMentionName(string value)
    {
        string clean = ToMentionDisplayName(value);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return string.Empty;
        }

        string normalized = RepeatedWhitespace.Replace(clean.Normalize(NormalizationForm.FormKC), " ").Trim();
        int start = 0;
        int end = normalized.Length - 1;

        while (start <= end && IsHarmlessBoundaryDecoration(normalized[start]))
        {
            start++;
        }

        while (end >= start && IsHarmlessBoundaryDecoration(normalized[end]))
        {
            end--;
        }

        // Symbol-only names are valid identities. Decoration trimming is only useful when
        // there is a stable letter/digit core to match, so retain the original otherwise.
        return start <= end ? normalized.Substring(start, end - start + 1).Trim() : normalized;
    }

    public static string ToMentionInsertionToken(string value)
    {
        string canonical = ToCanonicalMentionName(value);
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return string.Empty;
        }

        // The chat protocol intentionally keeps @references whitespace-free. Rich display names
        // such as a decorative gradient name must therefore insert a stable identity token, not
        // the full rendered label whose leading punctuation would be parsed as the target.
        Match word = MentionSafeWord.Match(canonical);
        if (word.Success)
        {
            return word.Value;
        }

        // Symbol-only identities remain valid. Retain their first non-whitespace token and let
        // the unique roster resolver decide whether it is safe to complete/highlight.
        Match fallback = MentionSafeFallback.Match(canonical);
        return fallback.Success ? fallback.Value : string.Empty;
    }

    public static string FindUniqueMentionMatch(string query, IEnumerable<string> candidates)
    {
        string cleanQuery = ToCanonicalMentionName(query);
        if (string.IsNullOrWhiteSpace(cleanQuery) || candidates == null)
        {
            return string.Empty;
        }

        var roster = candidates
            .Select(value => new MentionCandidate(value, ToCanonicalMentionName(value)))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.CanonicalName))
            .ToArray();

        string match = FindUniqueTier(roster, candidate =>
            string.Equals(candidate.CanonicalName, cleanQuery, StringComparison.OrdinalIgnoreCase), out bool hadMatches);
        if (hadMatches)
        {
            return match;
        }

        match = FindUniqueTier(roster, candidate =>
            candidate.CanonicalName.StartsWith(cleanQuery, StringComparison.OrdinalIgnoreCase), out hadMatches);
        if (hadMatches)
        {
            return match;
        }

        return FindUniqueTier(roster, candidate =>
            candidate.CanonicalName.IndexOf(cleanQuery, StringComparison.OrdinalIgnoreCase) >= 0, out _);
    }

    public static bool IsLocalMentionMatch(string matchedName, string localName, string token)
    {
        string cleanMatched = ToCanonicalMentionName(matchedName);
        string cleanLocal = ToCanonicalMentionName(localName);

        if (string.IsNullOrWhiteSpace(cleanLocal) || string.IsNullOrWhiteSpace(cleanMatched))
        {
            return false;
        }

        // The token is user input, not identity proof. Only the unique roster match chosen by
        // FindUniqueMentionMatch may authorize a local ping.
        return string.Equals(cleanMatched, cleanLocal, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindUniqueTier(IEnumerable<MentionCandidate> candidates, Func<MentionCandidate, bool> predicate, out bool hadMatches)
    {
        MentionCandidate[] matches = candidates.Where(predicate).Take(2).ToArray();
        hadMatches = matches.Length > 0;
        return matches.Length == 1 ? matches[0].OriginalName : string.Empty;
    }

    private static bool IsHarmlessBoundaryDecoration(char value)
    {
        if (char.IsWhiteSpace(value) || char.IsSurrogate(value))
        {
            return true;
        }

        switch (CharUnicodeInfo.GetUnicodeCategory(value))
        {
            case UnicodeCategory.DashPunctuation:
            case UnicodeCategory.OpenPunctuation:
            case UnicodeCategory.ClosePunctuation:
            case UnicodeCategory.InitialQuotePunctuation:
            case UnicodeCategory.FinalQuotePunctuation:
            case UnicodeCategory.OtherPunctuation:
            case UnicodeCategory.MathSymbol:
            case UnicodeCategory.CurrencySymbol:
            case UnicodeCategory.ModifierSymbol:
            case UnicodeCategory.OtherSymbol:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.SpacingCombiningMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.Control:
            case UnicodeCategory.Format:
                return true;
            default:
                return false;
        }
    }

    private sealed class MentionCandidate
    {
        public MentionCandidate(string originalName, string canonicalName)
        {
            OriginalName = originalName ?? string.Empty;
            CanonicalName = canonicalName ?? string.Empty;
        }

        public string OriginalName { get; }
        public string CanonicalName { get; }
    }
}
