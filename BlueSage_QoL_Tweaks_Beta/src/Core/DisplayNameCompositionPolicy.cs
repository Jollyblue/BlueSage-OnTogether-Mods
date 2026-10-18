namespace BlueSage.QoLTweaks.Core;

internal static class DisplayNameCompositionPolicy
{
    public static string Compose(string styledBaseName, string statusSuffix, string spoonSuffix)
    {
        string result = (styledBaseName ?? string.Empty).Trim();
        string status = (statusSuffix ?? string.Empty).Trim();
        string spoons = (spoonSuffix ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(status))
        {
            result += " " + status;
        }
        if (!string.IsNullOrWhiteSpace(spoons))
        {
            result += " " + spoons;
        }
        return result;
    }
}
