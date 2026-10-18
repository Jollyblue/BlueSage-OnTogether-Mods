using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal sealed class RichTextRepairResult
{
    public RichTextRepairResult(string value, bool changed, bool usedPlainTextFallback, string message)
    {
        Value = value ?? string.Empty;
        Changed = changed;
        UsedPlainTextFallback = usedPlainTextFallback;
        Message = message ?? string.Empty;
    }

    public string Value { get; }
    public bool Changed { get; }
    public bool UsedPlainTextFallback { get; }
    public string Message { get; }
}

internal static class RichTextDraftPolicy
{
    private static readonly Regex TokenPattern = new Regex(@"<[^<>]*>", RegexOptions.Compiled);
    private static readonly Regex NamedTagPattern = new Regex(
        @"^<\s*(/)?\s*([a-zA-Z][a-zA-Z0-9-]*)(?:\s*=\s*[^>]*)?\s*(/)?>$",
        RegexOptions.Compiled);
    private static readonly Regex ShorthandColorPattern = new Regex(
        @"^<\s*#(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})\s*>$",
        RegexOptions.Compiled);
    private static readonly Regex IncompleteTagPattern = new Regex(
        @"<\s*(?:/|#|[a-zA-Z])[^<>]*",
        RegexOptions.Compiled);
    private static readonly HashSet<string> StandaloneTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "br", "page", "pos", "space", "sprite"
    };

    public static bool TryValidate(string value, out string message)
    {
        string input = value ?? string.Empty;
        string textWithoutCompleteTokens = TokenPattern.Replace(input, string.Empty);
        if (IncompleteTagPattern.IsMatch(textWithoutCompleteTokens) || textWithoutCompleteTokens.IndexOf('>') >= 0)
        {
            message = "Rich text contains a cut-off tag. Finish or remove the incomplete <...> markup before applying.";
            return false;
        }

        Stack<string> open = new Stack<string>();
        foreach (Match token in TokenPattern.Matches(input))
        {
            string raw = token.Value;
            if (ShorthandColorPattern.IsMatch(raw))
            {
                open.Push("color");
                continue;
            }

            Match named = NamedTagPattern.Match(raw);
            if (!named.Success)
            {
                message = "Rich text contains an unsupported or malformed tag: " + raw;
                return false;
            }

            string name = named.Groups[2].Value;
            bool closing = named.Groups[1].Success;
            bool standalone = named.Groups[3].Success || StandaloneTags.Contains(name);
            if (standalone)
            {
                if (closing)
                {
                    message = "Rich text tag " + raw + " cannot close a standalone TMP tag.";
                    return false;
                }

                continue;
            }

            if (!closing)
            {
                open.Push(name);
                continue;
            }

            if (open.Count == 0 || !string.Equals(open.Pop(), name, StringComparison.OrdinalIgnoreCase))
            {
                message = "Rich text tags must close in reverse order (for example <b><size=120%>text</size></b>).";
                return false;
            }
        }

        if (open.Count > 0)
        {
            message = "Rich text tag <" + open.Peek() + "> is missing its closing tag.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static RichTextRepairResult RepairLegacy(string value)
    {
        string input = (value ?? string.Empty).Trim();
        if (TryValidate(input, out _))
        {
            return new RichTextRepairResult(input, changed: false, usedPlainTextFallback: false, message: string.Empty);
        }

        string textWithoutCompleteTokens = TokenPattern.Replace(input, string.Empty);
        if (IncompleteTagPattern.IsMatch(textWithoutCompleteTokens) || textWithoutCompleteTokens.IndexOf('>') >= 0)
        {
            return BuildPlainTextFallback(input, "A cut-off legacy tag could not be balanced safely.");
        }

        var repaired = new StringBuilder(input.Length + 32);
        var openTags = new List<string>();
        int offset = 0;
        foreach (Match token in TokenPattern.Matches(input))
        {
            repaired.Append(input, offset, token.Index - offset);
            offset = token.Index + token.Length;

            string raw = token.Value;
            if (ShorthandColorPattern.IsMatch(raw))
            {
                repaired.Append(raw);
                openTags.Add("color");
                continue;
            }

            Match named = NamedTagPattern.Match(raw);
            if (!named.Success)
            {
                return BuildPlainTextFallback(input, "An unsupported legacy tag could not be balanced safely.");
            }

            string name = named.Groups[2].Value;
            bool closing = named.Groups[1].Success;
            bool standalone = named.Groups[3].Success || StandaloneTags.Contains(name);
            if (standalone)
            {
                if (!closing)
                {
                    repaired.Append(raw);
                }
                continue;
            }

            if (!closing)
            {
                repaired.Append(raw);
                openTags.Add(name);
                continue;
            }

            int matchingIndex = FindOpenTag(openTags, name);
            if (matchingIndex < 0)
            {
                // A 0.1.8/0.1.9 style can begin or end with an orphan close. Dropping only that
                // markup preserves the visible name without manufacturing a new style span.
                continue;
            }

            for (int index = openTags.Count - 1; index > matchingIndex; index--)
            {
                repaired.Append("</").Append(openTags[index]).Append('>');
                openTags.RemoveAt(index);
            }

            repaired.Append("</").Append(name).Append('>');
            openTags.RemoveAt(matchingIndex);
        }

        repaired.Append(input, offset, input.Length - offset);
        for (int index = openTags.Count - 1; index >= 0; index--)
        {
            repaired.Append("</").Append(openTags[index]).Append('>');
        }

        string candidate = repaired.ToString().Trim();
        if (TryValidate(candidate, out _) &&
            !string.IsNullOrWhiteSpace(RichTextStyleBuilder.ToPlainStyleEditorText(candidate)))
        {
            return new RichTextRepairResult(
                candidate,
                changed: !string.Equals(candidate, input, StringComparison.Ordinal),
                usedPlainTextFallback: false,
                message: "Legacy rich text was balanced while preserving its visible text and supported tags.");
        }

        return BuildPlainTextFallback(input, "Legacy markup could not be balanced deterministically.");
    }

    private static int FindOpenTag(List<string> openTags, string name)
    {
        for (int index = openTags.Count - 1; index >= 0; index--)
        {
            if (string.Equals(openTags[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static RichTextRepairResult BuildPlainTextFallback(string input, string reason)
    {
        string plain = TokenPattern.Replace(input ?? string.Empty, string.Empty);
        plain = IncompleteTagPattern.Replace(plain, string.Empty);
        plain = plain.Replace(">", string.Empty).Trim().Trim('\0');
        return new RichTextRepairResult(
            plain,
            changed: !string.Equals(plain, (input ?? string.Empty).Trim(), StringComparison.Ordinal),
            usedPlainTextFallback: true,
            message: reason + " BlueSage kept the visible plain text instead.");
    }
}
