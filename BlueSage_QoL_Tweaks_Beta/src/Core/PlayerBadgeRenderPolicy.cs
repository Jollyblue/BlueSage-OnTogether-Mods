using System;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal static class PlayerBadgeRenderPolicy
{
    internal static string RenderCompactRichText(string displayText)
    {
        if (string.IsNullOrWhiteSpace(displayText))
        {
            return string.Empty;
        }

        return string.Concat(
            displayText.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(RenderToken));
    }

    private static string RenderToken(string token)
    {
        switch (token)
        {
            case "[O]":
                return "<color=#70D6FF>[O]</color>";
            case "[M]":
                return "<color=#A8D27A>[M]</color>";
            case "[U]":
                return "<color=#F4A261>[U]</color>";
            case "[V]":
                return "<color=#54D9E8>[V]</color>";
            case "[S]":
                return "<color=#FF5C5C>[S]</color>";
            case "[♥]":
                return "<color=#FF9EC4>[♥]</color>";
            case "[♪]":
                return "<color=#C29BFF>[♪]</color>";
            default:
                return "<color=#B9C7D6>" + token + "</color>";
        }
    }
}
