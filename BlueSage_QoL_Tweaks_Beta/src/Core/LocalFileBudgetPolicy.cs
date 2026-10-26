#nullable enable
namespace BlueSage.QoLTweaks.Core;

// Round 5d (Blue, 2026-10-23): hundreds of players host and play for days at a time, so every file BlueSage writes
// has a hard ceiling stated in one place. 0.2.6 measured on Blue's PC: the local audit folder 27 MB (one 63 h
// session alone 22.7 MB, worst case allowed ~2.3 GB), the host journal a status row on every join and leave.
// Session and archive retention live in ModSuiteLayoutPolicy; DiskBudgetContractTests names every writer in src.
internal static class LocalFileBudgetPolicy
{
    // Optional local audit (off by default since 0.2.7): one session = at most 2 x 2 MiB chat + 2 x 8 MiB log mirror
    // + 2 snapshots of at most 8 MiB each.
    internal const long AuditChatPartBytes = 2L * 1024L * 1024L;
    internal const int AuditChatParts = 2;
    internal const long AuditMirrorPartBytes = 8L * 1024L * 1024L;
    internal const int AuditMirrorParts = 2;
    internal const int AuditSnapshotsPerSession = 2;
    internal const long AuditSnapshotMaxBytes = 8L * 1024L * 1024L;

    // The one-time move keeps at most the 3 newest old (0.2.6) audit sessions, 48 MiB, in its archive copy.
    internal const int AuditRetainedSessions = 3;
    internal const long AuditFolderBudgetBytes = 48L * 1024L * 1024L;

    // Host journal: one 512 KiB page; full pages go into the archive as zips.
    internal const long HostJournalMaxBytes = 512L * 1024L;

    // 0.2.6 and older saved EnableLocalAuditExports=true by default, so a default change alone reaches nobody.
    // Off once for every existing install (the sweep-notify-quiet-v5 pattern); a later "on" is kept for good.
    internal static bool MigrateLocalAudit(bool configuredEnabled, bool alreadyMigrated)
    {
        return alreadyMigrated && configuredEnabled;
    }
}
