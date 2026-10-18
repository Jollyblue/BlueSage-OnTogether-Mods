#nullable enable

using System;

namespace BlueSage.QoLTweaks.Core;

internal static class StatisticsTypeResolutionPolicy
{
    private static readonly string[] RequiredGlobalTypes =
    {
        "StatisticsData",
        "DataManager",
        "AchievementManager",
        "Achievement"
    };

    public static bool IsExactGameSurface(string? assemblySimpleName, Func<string, bool>? hasGlobalType)
    {
        if (!string.Equals(assemblySimpleName, "Assembly-CSharp", StringComparison.Ordinal) ||
            hasGlobalType == null)
        {
            return false;
        }

        try
        {
            foreach (string requiredGlobalType in RequiredGlobalTypes)
            {
                if (!hasGlobalType(requiredGlobalType))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
