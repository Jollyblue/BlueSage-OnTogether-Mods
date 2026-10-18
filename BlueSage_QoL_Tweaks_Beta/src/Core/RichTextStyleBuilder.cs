using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal sealed class RichTextStyleResult
{
    private RichTextStyleResult(bool success, string generatedText, string message, int rawLength)
    {
        Success = success;
        GeneratedText = generatedText ?? string.Empty;
        Message = message ?? string.Empty;
        RawLength = rawLength;
    }

    public bool Success { get; }
    public string GeneratedText { get; }
    public string Message { get; }
    public int RawLength { get; }

    public static RichTextStyleResult Ok(string generatedText)
    {
        return new RichTextStyleResult(true, generatedText, "Style copied.", generatedText?.Length ?? 0);
    }

    public static RichTextStyleResult Fail(string message, int rawLength = 0)
    {
        return new RichTextStyleResult(false, string.Empty, message, rawLength);
    }
}

internal static class RichTextStyleBuilder
{
    public const int DefaultMaxGeneratedCharacters = InputFieldLimitPolicy.DefaultMaxProfileCharacters;
    private static readonly Regex RichTextTagPattern = new Regex("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex OrphanRichTextFragmentPattern = new Regex("(?:#?[0-9A-Fa-f]{3}(?:[0-9A-Fa-f]{3})?>|/?(?:color|b|i|u)>)", RegexOptions.Compiled);

    public static RichTextStyleResult TryBuild(string arguments, int maxGeneratedCharacters = DefaultMaxGeneratedCharacters)
    {
        string trimmed = (arguments ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return RichTextStyleResult.Fail(Usage());
        }

        string[] tokens = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string mode = tokens[0].ToLowerInvariant();

        switch (mode)
        {
            case "color":
                return BuildColorMode(trimmed, maxGeneratedCharacters, bold: false, italic: false);
            case "bold":
                return BuildColorMode(trimmed, maxGeneratedCharacters, bold: true, italic: false);
            case "italic":
                return BuildColorMode(trimmed, maxGeneratedCharacters, bold: false, italic: true);
            case "bolditalic":
            case "bi":
                return BuildColorMode(trimmed, maxGeneratedCharacters, bold: true, italic: true);
            case "gradient":
            case "grad":
                return BuildGradientMode(trimmed, maxGeneratedCharacters, colorCount: 2);
            case "gradient3":
            case "grad3":
                return BuildGradientMode(trimmed, maxGeneratedCharacters, colorCount: 3);
            case "status":
                return BuildStatusMode(trimmed, maxGeneratedCharacters);
            default:
                return RichTextStyleResult.Fail(Usage());
        }
    }

    private static RichTextStyleResult BuildColorMode(string input, int maxGeneratedCharacters, bool bold, bool italic)
    {
        string[] parts = input.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return RichTextStyleResult.Fail("Style Helper: use /style color #83E Your Text.");
        }

        if (!TryNormalizeHex(parts[1], out string hex))
        {
            return RichTextStyleResult.Fail("Style Helper: expected a hex color like #83E or #8833EE.");
        }

        if (!RichTextDraftPolicy.TryValidate(parts[2], out string validationMessage))
        {
            return RichTextStyleResult.Fail("Style Helper: " + validationMessage);
        }

        string generated = WrapStyle(BuildColorTag(hex, parts[2]), bold, italic);
        return EnforceLength(generated, maxGeneratedCharacters);
    }

    private static RichTextStyleResult BuildGradientMode(string input, int maxGeneratedCharacters, int colorCount)
    {
        string[] parts = input.Split(new[] { ' ' }, colorCount + 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < colorCount + 2)
        {
            return RichTextStyleResult.Fail(colorCount == 2
                ? "Style Helper: use /style gradient #83E #A59 Your Text."
                : "Style Helper: use /style gradient3 #83E #A59 #D85 Your Text.");
        }

        if (!TryNormalizeHex(parts[1], out string start) ||
            !TryNormalizeHex(parts[2], out string middleOrEnd))
        {
            return RichTextStyleResult.Fail("Style Helper: expected hex colors like #83E or #8833EE.");
        }

        string end = middleOrEnd;
        string text;
        if (colorCount == 3)
        {
            if (!TryNormalizeHex(parts[3], out end))
            {
                return RichTextStyleResult.Fail("Style Helper: expected hex colors like #83E or #8833EE.");
            }

            text = parts[4];
        }
        else
        {
            text = parts[3];
        }

        if (!RichTextDraftPolicy.TryValidate(text, out string validationMessage))
        {
            return RichTextStyleResult.Fail("Style Helper: " + validationMessage);
        }

        string generated = colorCount == 2
            ? BuildGradient(text, ColorRgb.Parse(start), ColorRgb.Parse(middleOrEnd))
            : BuildGradient(text, ColorRgb.Parse(start), ColorRgb.Parse(middleOrEnd), ColorRgb.Parse(end));

        return EnforceLength(generated, maxGeneratedCharacters);
    }

    private static RichTextStyleResult BuildStatusMode(string input, int maxGeneratedCharacters)
    {
        string[] parts = input.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return RichTextStyleResult.Fail("Style Helper: use /style status #FFD45D BRB.");
        }

        if (!TryNormalizeHex(parts[1], out string hex))
        {
            return RichTextStyleResult.Fail("Style Helper: expected a hex color like #FFD45D.");
        }

        if (!RichTextDraftPolicy.TryValidate(parts[2], out string validationMessage))
        {
            return RichTextStyleResult.Fail("Style Helper: " + validationMessage);
        }

        string generated = BuildColorTag(hex, $"({parts[2]})");
        return EnforceLength(generated, maxGeneratedCharacters);
    }

    internal static string BuildColorTag(string hex, string text)
    {
        string normalized = TryNormalizeHex(hex, out string cleanHex) ? cleanHex : "FFFFFF";
        return "<#" + normalized + ">" + (text ?? string.Empty) + "</color>";
    }

    internal static string ToPlainStyleEditorText(string value)
    {
        string plainText = value ?? string.Empty;
        for (int i = 0; i < 8; i++)
        {
            string next = RichTextTagPattern.Replace(plainText, string.Empty);
            next = OrphanRichTextFragmentPattern.Replace(next, string.Empty);
            if (string.Equals(next, plainText, StringComparison.Ordinal))
            {
                break;
            }

            plainText = next;
        }

        return plainText.Trim().Trim('\0');
    }

    private static string WrapStyle(string value, bool bold, bool italic)
    {
        string output = value;
        if (italic)
        {
            output = "<i>" + output + "</i>";
        }

        if (bold)
        {
            output = "<b>" + output + "</b>";
        }

        return output;
    }

    private static string BuildGradient(string text, ColorRgb start, ColorRgb end)
    {
        return BuildGradient(text, start, end, end, useMiddle: false);
    }

    private static string BuildGradient(string text, ColorRgb start, ColorRgb middle, ColorRgb end)
    {
        return BuildGradient(text, start, middle, end, useMiddle: true);
    }

    private static string BuildGradient(string text, ColorRgb start, ColorRgb middle, ColorRgb end, bool useMiddle)
    {
        string cleanText = text ?? string.Empty;
        List<GradientTextElement> elements = TokenizeGradientText(cleanText);
        int visibleCharacters = elements.Count(element => !element.IsTag && !element.IsWhitespace);
        if (visibleCharacters == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        int visibleIndex = 0;
        foreach (GradientTextElement element in elements)
        {
            if (element.IsTag || element.IsWhitespace)
            {
                builder.Append(element.Value);
                continue;
            }

            double ratio = visibleCharacters == 1 ? 0 : visibleIndex / (double)(visibleCharacters - 1);
            ColorRgb color = !useMiddle
                ? ColorRgb.Lerp(start, end, ratio)
                : LerpThreePoint(start, middle, end, ratio);
            builder.Append(BuildColorTag(color.ToHex(), element.Value));
            visibleIndex++;
        }

        return builder.ToString();
    }

    private static List<GradientTextElement> TokenizeGradientText(string value)
    {
        var elements = new List<GradientTextElement>();
        int offset = 0;
        while (offset < value.Length)
        {
            if (value[offset] == '<')
            {
                int tagEnd = value.IndexOf('>', offset + 1);
                if (tagEnd >= 0)
                {
                    elements.Add(new GradientTextElement(value.Substring(offset, tagEnd - offset + 1), isTag: true));
                    offset = tagEnd + 1;
                    continue;
                }
            }

            int textEnd = value.IndexOf('<', offset);
            if (textEnd < 0)
            {
                textEnd = value.Length;
            }
            else if (textEnd == offset)
            {
                textEnd = offset + 1;
            }

            string segment = value.Substring(offset, textEnd - offset);
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(segment);
            while (enumerator.MoveNext())
            {
                elements.Add(new GradientTextElement(enumerator.GetTextElement(), isTag: false));
            }

            offset = textEnd;
        }

        return elements;
    }

    private sealed class GradientTextElement
    {
        public GradientTextElement(string value, bool isTag)
        {
            Value = value ?? string.Empty;
            IsTag = isTag;
        }

        public string Value { get; }
        public bool IsTag { get; }
        public bool IsWhitespace => !IsTag && string.IsNullOrWhiteSpace(Value);
    }

    private static ColorRgb LerpThreePoint(ColorRgb start, ColorRgb middle, ColorRgb end, double ratio)
    {
        if (ratio <= 0.5d)
        {
            return ColorRgb.Lerp(start, middle, ratio * 2d);
        }

        return ColorRgb.Lerp(middle, end, (ratio - 0.5d) * 2d);
    }

    private static RichTextStyleResult EnforceLength(string generated, int maxGeneratedCharacters)
    {
        int length = generated.Length;
        if (length == 0)
        {
            return RichTextStyleResult.Fail("Style Helper: add some text to style.");
        }

        if (length > maxGeneratedCharacters)
        {
            return RichTextStyleResult.Fail($"Style Helper: generated text is too long ({length}/{maxGeneratedCharacters}). Shorten the text or use fewer per-letter colors.", length);
        }

        return RichTextStyleResult.Ok(generated);
    }

    internal static bool TryNormalizeHex(string value, out string hex)
    {
        hex = string.Empty;
        string cleaned = (value ?? string.Empty).Trim().TrimStart('#');
        if (cleaned.Length == 3)
        {
            cleaned = new string(new[]
            {
                cleaned[0], cleaned[0],
                cleaned[1], cleaned[1],
                cleaned[2], cleaned[2]
            });
        }

        if (cleaned.Length != 6 || cleaned.Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        hex = cleaned.ToUpperInvariant();
        return true;
    }

    private static string Usage()
    {
        return "Style Helper: /style color #83E Blue, /style bold #83E Blue, /style gradient #83E #A59 Blue, /style gradient3 #83E #A59 #D85 Blue, /style status #FFD45D BRB.";
    }

    private sealed class ColorRgb
    {
        private ColorRgb(int red, int green, int blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        private int Red { get; }
        private int Green { get; }
        private int Blue { get; }

        public static ColorRgb Parse(string hex)
        {
            return new ColorRgb(
                Convert.ToInt32(hex.Substring(0, 2), 16),
                Convert.ToInt32(hex.Substring(2, 2), 16),
                Convert.ToInt32(hex.Substring(4, 2), 16));
        }

        public static ColorRgb Lerp(ColorRgb start, ColorRgb end, double ratio)
        {
            return new ColorRgb(
                LerpChannel(start.Red, end.Red, ratio),
                LerpChannel(start.Green, end.Green, ratio),
                LerpChannel(start.Blue, end.Blue, ratio));
        }

        public string ToHex()
        {
            return Red.ToString("X2") + Green.ToString("X2") + Blue.ToString("X2");
        }

        private static int LerpChannel(int start, int end, double ratio)
        {
            double value = start + ((end - start) * ratio);
            int rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded < 0)
            {
                return 0;
            }

            return rounded > 255 ? 255 : rounded;
        }
    }
}
