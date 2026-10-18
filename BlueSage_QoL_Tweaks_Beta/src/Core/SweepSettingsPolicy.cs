namespace BlueSage.QoLTweaks.Core;

internal static class SweepSettingsPolicy
{
    public const int MinimumIntervalMinutes = 5;
    // Fresh installs (Blue, 2026-10-17). The one-time safety migration below keeps its own target.
    public const int DefaultIntervalMinutes = 15;
    public const int TenMinuteSafetyMigrationInterval = 10;
    public const int MaximumIntervalMinutes = 180;
    private const int LegacyFifteenMinuteDefault = 15;

    public static int ClampIntervalMinutes(int configuredMinutes)
    {
        if (configuredMinutes < MinimumIntervalMinutes)
        {
            return MinimumIntervalMinutes;
        }

        if (configuredMinutes > MaximumIntervalMinutes)
        {
            return MaximumIntervalMinutes;
        }

        return configuredMinutes;
    }

    public static int MigrateLegacyDefaultIntervalMinutes(int configuredMinutes, bool alreadyMigrated)
    {
        if (!alreadyMigrated && (configuredMinutes == 30 || configuredMinutes == 60))
        {
            return LegacyFifteenMinuteDefault;
        }

        return ClampIntervalMinutes(configuredMinutes);
    }

    public static int MigrateTenMinuteSafetyInterval(int configuredMinutes, bool alreadyMigrated)
    {
        return alreadyMigrated
            ? ClampIntervalMinutes(configuredMinutes)
            : TenMinuteSafetyMigrationInterval;
    }

    // Fresh installs keep today's default; only real older configs go through the historical steps.
    public static int ResolveStartupIntervalMinutes(int configuredMinutes, bool freshInstall, bool legacyDefaultsMigrated, bool tenMinuteSafetyMigrated)
    {
        if (freshInstall)
        {
            return ClampIntervalMinutes(configuredMinutes);
        }

        int migrated = MigrateLegacyDefaultIntervalMinutes(configuredMinutes, legacyDefaultsMigrated);
        return MigrateTenMinuteSafetyInterval(migrated, tenMinuteSafetyMigrated);
    }

    public static bool MigrateAutomaticCleanupOptIn(bool configuredEnabled, bool alreadyMigrated)
    {
        return configuredEnabled;
    }
}
