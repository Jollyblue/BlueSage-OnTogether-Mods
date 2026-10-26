using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5d (Blue, 2026-10-23): "we shouldn't be providing a community of hundreds of players risks of long term
// players with super long sessions resulting with MBs worth of mod external files". Every file BlueSage writes has
// a hard ceiling; these tests pin the ceilings. Session and archive retention are pinned in ModSuiteStorageTests.
public sealed class LocalFileBudgetPolicyTests
{
    private const long MiB = 1024L * 1024L;

    [Test]
    public void Audit_and_journal_ceilings_add_up_to_a_small_stated_budget()
    {
        long perSession = LocalFileBudgetPolicy.AuditChatPartBytes * LocalFileBudgetPolicy.AuditChatParts +
                          LocalFileBudgetPolicy.AuditMirrorPartBytes * LocalFileBudgetPolicy.AuditMirrorParts +
                          LocalFileBudgetPolicy.AuditSnapshotMaxBytes * LocalFileBudgetPolicy.AuditSnapshotsPerSession;
        Assert.Multiple(() =>
        {
            Assert.That(perSession, Is.LessThanOrEqualTo(36 * MiB), "One session's chat + log mirror + snapshots stays under 36 MiB.");
            Assert.That(ModSuiteLayoutPolicy.RawSessionsKept, Is.EqualTo(3));
            Assert.That(ModSuiteLayoutPolicy.RawSessionDays, Is.EqualTo(3));
            Assert.That(ModSuiteLayoutPolicy.RawLogsBudgetBytes, Is.EqualTo(24 * MiB));
            Assert.That(ModSuiteLayoutPolicy.ArchiveDays, Is.EqualTo(14), "Blue 2026-10-23: keep the last 14 days (2 weeks) of playtime.");
            Assert.That(ModSuiteLayoutPolicy.FolderCeilingBytes, Is.EqualTo(1024L * MiB), "Blue 2026-10-23: the whole folder never grows past 1 GB.");
            Assert.That(ModSuiteLayoutPolicy.ArchiveBudgetBytes, Is.EqualTo(48 * MiB));
            Assert.That(LocalFileBudgetPolicy.HostJournalMaxBytes, Is.EqualTo(512L * 1024L), "Host journal: one 512 KiB page; full pages are zipped into the archive.");
        });
    }

    [Test]
    public void Existing_installs_turn_the_local_audit_off_once_and_a_later_choice_sticks()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LocalFileBudgetPolicy.MigrateLocalAudit(configuredEnabled: true, alreadyMigrated: false), Is.False, "0.2.6 saved true by default: goes off once.");
            Assert.That(LocalFileBudgetPolicy.MigrateLocalAudit(configuredEnabled: true, alreadyMigrated: true), Is.True, "Turned back on after the migration: kept.");
            Assert.That(LocalFileBudgetPolicy.MigrateLocalAudit(configuredEnabled: false, alreadyMigrated: true), Is.False);
            Assert.That(ConfigMigrationPolicy.LocalAuditOffV6Migration, Is.EqualTo("local-audit-off-v6"));
            Assert.That(ConfigMigrationPolicy.CurrentMigrationLedger, Does.EndWith(";local-audit-off-v6"));
        });
    }
}
