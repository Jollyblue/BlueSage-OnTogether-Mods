namespace BlueSage.QoLTweaks.Core;

internal static class ConfigMigrationPolicy
{
    internal const string FinalDefaultsV2Migration = "final-defaults-v2";
    internal const string SweepFifteenMinuteV1Migration = "sweep-15-default-v1";
    internal const string SweepTenMinuteSafetyV2Migration = "sweep-10-safety-v2";
    internal const string SweepAutomaticOptInV3Migration = "sweep-auto-opt-in-v3";
    internal const string CommunityBanAnyClientOptInV1Migration = "community-ban-any-client-opt-in-v1";
    // Round 4 Stage G (2026-10-20): the 10/15-minute sweep values written by the two migrations above move to 40 once.
    internal const string SweepFortyMinuteOverlapV4Migration = "sweep-40-overlap-v4";
    // Round 5 F (2026-10-22): the automatic-sweep toast goes quiet once for every existing install.
    internal const string SweepNotifyQuietV5Migration = "sweep-notify-quiet-v5";
    // Round 5d (2026-10-23): the optional local audit goes off once for every existing install (LocalFileBudgetPolicy).
    internal const string LocalAuditOffV6Migration = "local-audit-off-v6";
    internal const string CurrentMigrationLedger =
        FinalDefaultsV2Migration + ";" +
        SweepFifteenMinuteV1Migration + ";" +
        SweepTenMinuteSafetyV2Migration + ";" +
        SweepAutomaticOptInV3Migration + ";" +
        CommunityBanAnyClientOptInV1Migration + ";" +
        SweepFortyMinuteOverlapV4Migration + ";" +
        SweepNotifyQuietV5Migration + ";" +
        LocalAuditOffV6Migration;

    private const string LegacyVersionDerivedV2Marker = "0.2.0-final-defaults-v2";

    public static bool HasCompletedMigration(string ledger, string migration)
    {
        if (string.IsNullOrWhiteSpace(migration))
        {
            return false;
        }

        foreach (string token in (ledger ?? string.Empty).Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string completedMigration = token.Trim();
            if (string.Equals(completedMigration, migration, System.StringComparison.Ordinal) ||
                (string.Equals(migration, FinalDefaultsV2Migration, System.StringComparison.Ordinal) &&
                 string.Equals(completedMigration, LegacyVersionDerivedV2Marker, System.StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    public static string RecordMigration(string ledger, string migration)
    {
        var completed = new System.Collections.Generic.List<string>();
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

        foreach (string token in (ledger ?? string.Empty).Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string completedMigration = token.Trim();
            if (completedMigration.Length > 0 && seen.Add(completedMigration))
            {
                completed.Add(completedMigration);
            }
        }

        string normalizedMigration = migration?.Trim() ?? string.Empty;
        string normalizedLedger = string.Join(";", completed.ToArray());
        if (normalizedMigration.Length > 0 &&
            normalizedMigration.IndexOf(';') < 0 &&
            !HasCompletedMigration(normalizedLedger, normalizedMigration) &&
            seen.Add(normalizedMigration))
        {
            completed.Add(normalizedMigration);
        }

        return string.Join(";", completed.ToArray());
    }

    public static int MigrateOldDefaultInt(int currentValue, int oldDefault, int newDefault, bool alreadyMigrated)
    {
        return !alreadyMigrated && currentValue == oldDefault ? newDefault : currentValue;
    }

    public static string MigrateOldDefaultString(string currentValue, string oldDefault, string newDefault, bool alreadyMigrated)
    {
        return !alreadyMigrated && string.Equals(currentValue, oldDefault, System.StringComparison.OrdinalIgnoreCase)
            ? newDefault
            : currentValue;
    }

    public static bool MigrateOldDefaultBool(bool currentValue, bool oldDefault, bool newDefault, bool alreadyMigrated)
    {
        return !alreadyMigrated && currentValue == oldDefault ? newDefault : currentValue;
    }

    public static bool ShouldDisableDefaultSpoons(bool enabled, string spoonsUpdatedUtc, bool alreadyMigrated)
    {
        if (alreadyMigrated || !enabled)
        {
            return false;
        }

        // A valid update timestamp is evidence that this player explicitly opted in.
        // Only the old timestamp-less/default-on state should be migrated to the new
        // opt-in default, so an update never silently erases a real Spoons choice.
        return !System.DateTimeOffset.TryParse(
            spoonsUpdatedUtc,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces | System.Globalization.DateTimeStyles.AssumeUniversal,
            out _);
    }
}
