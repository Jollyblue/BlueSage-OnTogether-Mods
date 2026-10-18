using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

public static class ChatCopySanitizer
{
    private static readonly Regex RichTextTagPattern = new Regex("<[^>]+>", RegexOptions.Compiled);

    public static string ToClipboardText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string plainText = RichTextTagPattern.Replace(value, string.Empty);
        return plainText
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();
    }
}
