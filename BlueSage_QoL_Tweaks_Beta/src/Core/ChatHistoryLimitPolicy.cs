using System;

namespace BlueSage.QoLTweaks.Core;

internal static class ChatHistoryLimitPolicy
{
    public const int DefaultRows = 150;
    public const int MinRows = 25;
    public const int MaxRows = 250;

    public static int ClampRows(int configuredRows)
    {
        return Math.Max(MinRows, Math.Min(MaxRows, configuredRows));
    }

    public static int ApplyConfiguredRows(int originalLimit, int configuredRows)
    {
        // Never lower a newer vanilla limit or another mod's higher getter result.
        return Math.Max(originalLimit, ClampRows(configuredRows));
    }
}
