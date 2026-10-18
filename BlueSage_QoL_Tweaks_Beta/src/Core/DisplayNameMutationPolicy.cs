namespace BlueSage.QoLTweaks.Core;

internal static class DisplayNameMutationPolicy
{
    public static bool TryCompose(
        string styledBaseName,
        string statusSuffix,
        string spoonSuffix,
        int maxCharacters,
        out string displayName,
        out string error)
    {
        string baseName = styledBaseName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseName.Trim()))
        {
            displayName = string.Empty;
            error = "Status Helper: player name is not ready yet.";
            return false;
        }

        if (!TryValidateComponent(baseName, "name", out error) ||
            !TryValidateComponent(statusSuffix, "status", out error) ||
            !TryValidateComponent(spoonSuffix, "Spoons", out error))
        {
            displayName = string.Empty;
            return false;
        }

        displayName = DisplayNameCompositionPolicy.Compose(baseName, statusSuffix, spoonSuffix);
        if (displayName.Length > maxCharacters)
        {
            error = $"Status Helper: display name is too long ({displayName.Length}/{maxCharacters}). Shorten your name or status.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateComponent(string value, string label, out string error)
    {
        if (string.IsNullOrEmpty(value) || RichTextDraftPolicy.TryValidate(value, out string detail))
        {
            error = string.Empty;
            return true;
        }

        error = "Status Helper: " + label + " rich text is not balanced. " + detail;
        return false;
    }
}
