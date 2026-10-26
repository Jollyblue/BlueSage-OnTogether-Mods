using System;
using System.Threading;
using BepInEx;

namespace BlueSage.QoLTweaks;

// Round 5d: the one place every QoL writer asks for its path. Set once at Awake by the folder move; until then (and
// whenever the move could not finish) it points at the old layout so nothing ever looks lost.
internal static class ModSuitePaths
{
    private static ModSuiteLayout _current;

    internal static ModSuiteLayout Current => _current ?? (_current = ModSuiteLayout.For(Paths.BepInExRootPath, Paths.ConfigPath, legacyMode: true));

    internal static ModSuiteMigrationResult InitializeAtStartup(Action<string> warn)
    {
        ModSuiteMigrationResult result = ModSuiteMigration.Run(Paths.BepInExRootPath, Paths.ConfigPath, DateTime.Now, warn);
        _current = ModSuiteLayout.For(Paths.BepInExRootPath, Paths.ConfigPath, legacyMode: !result.LayoutReady);
        return result;
    }

    // Packing older sessions and removing old zips happens off the main thread so a long history never stalls a frame.
    internal static void RunRetentionInBackground(string currentSessionDirectory)
    {
        ModSuiteLayout layout = Current;
        if (layout.LegacyMode)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                string summary = ModSuiteRetention.Run(layout, currentSessionDirectory, DateTime.UtcNow.Ticks);
                Plugin.Log?.LogInfo("[BlueSageFiles] tidy-up: " + summary + ".");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug("[BlueSageFiles] tidy-up skipped safely: " + ex.GetType().Name + ": " + ex.Message);
            }
        });
    }
}
