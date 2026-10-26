using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

// Round 5 R0 (Blue, 2026-10-22). The Report's five Journal faces come from the game's own
// StatisticManager._moodIcons list. When they cannot be shown, the plain labels stay, and this
// policy names the one reason so the log tells the next reader what happened instead of nothing.
// Pure: no Unity types, so the net8 tests exercise every branch.
internal static class JournalMoodFaceResolutionPolicy
{
    internal const int RequiredFaceCount = 5;

    // Returns an empty string when the faces can be drawn; otherwise one short reason.
    internal static string Describe(bool managerAvailable, bool fieldFound, int spriteCount, int destroyedCount)
    {
        if (!managerAvailable)
        {
            return "the game's StatisticManager is not available yet (no Statistics tab opened and the scene singleton is absent)";
        }

        if (!fieldFound)
        {
            return "the game's StatisticManager has no _moodIcons list on this build";
        }

        if (spriteCount < RequiredFaceCount)
        {
            return "the game's _moodIcons list has " +
                   spriteCount.ToString(CultureInfo.InvariantCulture) +
                   " sprites; " +
                   RequiredFaceCount.ToString(CultureInfo.InvariantCulture) +
                   " are needed";
        }

        if (destroyedCount > 0)
        {
            return destroyedCount.ToString(CultureInfo.InvariantCulture) +
                   " of the first " +
                   RequiredFaceCount.ToString(CultureInfo.InvariantCulture) +
                   " mood sprites are destroyed or missing";
        }

        return string.Empty;
    }
}
