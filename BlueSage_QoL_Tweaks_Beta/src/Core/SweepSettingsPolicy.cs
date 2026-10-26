namespace BlueSage.QoLTweaks.Core;

internal static class SweepSettingsPolicy
{
    public const int MinimumIntervalMinutes = 5;
    // Round 4 Stage G (Blue, 2026-10-20): the default avoids the game's own 30-minute cleanup and still adds at
    // least one BlueSage sweep inside every 75 minutes. 40 only meets vanilla's boundary at 120 minutes, where the
    // coordination window already defers. The autosave guard (AutosaveBoundaryPolicy) handles the 180 s autosave.
    public const int DefaultIntervalMinutes = 40;
    public const int OverlapAvoidanceIntervalMinutes = 40;
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

    // Stage G: the two historical migrations above left every 0.2.4 and 0.2.5 updater on 10 or 15 minutes, values
    // the player never chose. Those two move to the overlap-avoiding default once; any other value is the player's.
    public static int MigrateOverlapAvoidanceInterval(int configuredMinutes, bool alreadyMigrated)
    {
        if (!alreadyMigrated && (configuredMinutes == TenMinuteSafetyMigrationInterval || configuredMinutes == LegacyFifteenMinuteDefault))
        {
            return OverlapAvoidanceIntervalMinutes;
        }

        return ClampIntervalMinutes(configuredMinutes);
    }

    // Fresh installs keep today's default; only real older configs go through the historical steps.
    public static int ResolveStartupIntervalMinutes(int configuredMinutes, bool freshInstall, bool legacyDefaultsMigrated, bool tenMinuteSafetyMigrated, bool overlapAvoidanceMigrated)
    {
        if (freshInstall)
        {
            return ClampIntervalMinutes(configuredMinutes);
        }

        int migrated = MigrateLegacyDefaultIntervalMinutes(configuredMinutes, legacyDefaultsMigrated);
        migrated = MigrateTenMinuteSafetyInterval(migrated, tenMinuteSafetyMigrated);
        return MigrateOverlapAvoidanceInterval(migrated, overlapAvoidanceMigrated);
    }

    public static bool MigrateAutomaticCleanupOptIn(bool configuredEnabled, bool alreadyMigrated)
    {
        return configuredEnabled;
    }

    // Round 5 F (2026-10-22): the "swept unused assets" toast every 40 minutes is noise in a cozy session, and
    // every 0.2.6 install has NotifyOnSweep=true saved, so a default change alone reaches nobody. One-time
    // migration to off (the round-4 v4 precedent); anyone who turns it back on keeps it, because the ledger
    // records the migration and it never runs twice. The manual cleanup still confirms (SweepNotificationPolicy).
    public static bool MigrateSweepNotification(bool configuredEnabled, bool alreadyMigrated)
    {
        return alreadyMigrated && configuredEnabled;
    }

    // Automatic sweeps are silent unless the user asked for toasts; a cleanup the user triggered always confirms.
    public static bool ShouldNotifyOnSweep(bool notifyOnSweep, string trigger)
    {
        bool automatic = string.Equals(trigger, "automatic timer", System.StringComparison.Ordinal);
        return notifyOnSweep || !automatic;
    }
}
