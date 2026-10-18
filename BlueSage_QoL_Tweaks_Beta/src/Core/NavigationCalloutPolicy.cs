namespace BlueSage.QoLTweaks.Core;

internal static class NavigationCalloutPolicy
{
    internal const string NotificationKeywordHex = "0B6FA4";

    internal static string BuildPrimaryWelcomeMessage(string version)
    {
        return BuildPrimaryWelcomeMessage(version, "HOME", "INSERT");
    }

    internal static string BuildPrimaryWelcomeMessage(string version, string menuKeyName, string styleKeyName)
    {
        return "BlueSage QoL v" + SanitizeVersion(version) + ": " +
               Highlight(KeyOrCommandKeyword(menuKeyName, "/qolmenu"), NotificationKeywordHex) + " opens QoL Menu. " +
               Highlight(KeyOrCommandKeyword(styleKeyName, "/styleui"), NotificationKeywordHex) + " opens Style Helper for names, status, Spoons, colors, and templates. " +
               "Hotkeys rebindable in the menu's Hotkeys section or /rebind. /bsqol opens the friendly guide; full README: " +
               HeaderActionLayoutPolicy.ThunderstoreUrl + " . /qol status shows what is on.";
    }

    internal static string BuildCommandBrowserHint()
    {
        return "open " + Highlight("SLASH COMMANDS", NotificationKeywordHex) + " by typing /, then press Tab or click a suggestion.";
    }

    internal static string BuildMenuNavigationHint(string keywordHex)
    {
        return BuildMenuNavigationHint(keywordHex, "HOME", "INSERT");
    }

    internal static string BuildMenuNavigationHint(string keywordHex, string menuKeyName, string styleKeyName)
    {
        return Highlight(KeyOrCommandKeyword(menuKeyName, "/qolmenu"), keywordHex) + " reopens this menu. " +
               Highlight(KeyOrCommandKeyword(styleKeyName, "/styleui"), keywordHex) + " opens Style Helper. Type / to browse " +
               Highlight("SLASH COMMANDS", keywordHex) + ".";
    }

    // Plain text for /qol help, which posts as an ordinary chat line.
    internal static string BuildHelpBasicsLine(string menuKeyName, string styleKeyName)
    {
        return "BlueSage basics: " +
               HotkeyLabelPolicy.KeyOrCommand(menuKeyName, "/qolmenu") + ", " +
               HotkeyLabelPolicy.KeyOrCommand(styleKeyName, "/styleui") + ", " +
               "/qol status, /rebind to change hotkeys, /welcome for the quick-start overlay, and type / then Tab or click a suggestion.";
    }

    // An unbound hotkey (None) shows its slash command instead of a key name.
    private static string KeyOrCommandKeyword(string keyName, string command)
    {
        return HotkeyLabelPolicy.IsUnbound(keyName) ? command : SanitizeKeyName(keyName);
    }

    private static string Highlight(string keyword, string hex)
    {
        return "<b><color=#" + SanitizeHex(hex) + ">" + keyword + "</color></b>";
    }

    private static string SanitizeKeyName(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return "HOME";

        var safe = new System.Text.StringBuilder(keyName.Length);
        foreach (char value in keyName)
        {
            if (char.IsLetterOrDigit(value)) safe.Append(value);
        }

        return safe.Length > 0 ? safe.ToString().ToUpperInvariant() : "HOME";
    }

    private static string SanitizeVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return "unknown";

        var safe = new System.Text.StringBuilder(version.Length);
        foreach (char value in version)
        {
            if (char.IsLetterOrDigit(value) || value == '.' || value == '+' || value == '-') safe.Append(value);
        }

        return safe.Length > 0 ? safe.ToString() : "unknown";
    }

    private static string SanitizeHex(string hex)
    {
        if (hex != null && hex.Length == 6)
        {
            foreach (char value in hex)
            {
                if (!System.Uri.IsHexDigit(value)) return NotificationKeywordHex;
            }

            return hex.ToUpperInvariant();
        }

        return NotificationKeywordHex;
    }
}
