using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class AuditSnapshotPolicyTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "BlueSageAuditSnapshotTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public void TryCreate_copies_the_exact_source_boundary_to_a_date_named_file()
    {
        string source = Path.Combine(_root, "LogOutput.log");
        byte[] expected = Enumerable.Range(0, 10000).Select(value => (byte)(value % 251)).ToArray();
        File.WriteAllBytes(source, expected);
        string session = Path.Combine(_root, "2026-07-14_180000-session");

        bool created = AuditSnapshotPolicy.TryCreate(source, session, new DateTime(2026, 7, 14, 18, 4, 31, DateTimeKind.Local), out string snapshot, out long bytes, out string error);

        Assert.That(created, Is.True, error);
        Assert.That(Path.GetFileName(snapshot), Is.EqualTo("2026-07-14_180431-snapshot-game-log.log"));
        Assert.That(bytes, Is.EqualTo(expected.Length));
        Assert.That(File.ReadAllBytes(snapshot), Is.EqualTo(expected));
        Assert.That(Directory.GetFiles(session, "*.tmp"), Is.Empty);
    }

    [Test]
    public void TryCreate_keeps_the_newest_part_of_a_huge_log_from_a_whole_line()
    {
        string source = Path.Combine(_root, "LogOutput.log");
        string line = new string('x', 1023) + "\n";
        using (StreamWriter writer = new StreamWriter(source))
        {
            for (int index = 0; index < 9 * 1024; index++)
            {
                writer.Write(index == 9 * 1024 - 1 ? "last line\n" : line);
            }
        }

        string session = Path.Combine(_root, "2026-07-14_180000-session");
        Assert.That(AuditSnapshotPolicy.TryCreate(source, session, new DateTime(2026, 7, 14, 18, 5, 0, DateTimeKind.Local), out string snapshot, out long bytes, out string error), Is.True, error);
        string text = File.ReadAllText(snapshot);
        Assert.Multiple(() =>
        {
            Assert.That(bytes, Is.LessThanOrEqualTo(LocalFileBudgetPolicy.AuditSnapshotMaxBytes));
            Assert.That(text, Does.StartWith("xxxx"), "Starts on a whole line.");
            Assert.That(text, Does.EndWith("last line\n"), "The newest lines are the ones kept.");
        });
    }

    [Test]
    public void TryCreate_retains_only_the_latest_two_snapshots()
    {
        string source = Path.Combine(_root, "LogOutput.log");
        File.WriteAllText(source, "runtime evidence");
        string session = Path.Combine(_root, "2026-07-14_180000-session");

        for (int index = 0; index < 5; index++)
        {
            bool created = AuditSnapshotPolicy.TryCreate(source, session, new DateTime(2026, 7, 14, 18, 0, index, DateTimeKind.Local), out _, out _, out string error);
            Assert.That(created, Is.True, error);
        }

        string[] kept = Directory.GetFiles(session, "*-snapshot-game-log.log").Select(Path.GetFileName).OrderBy(name => name).ToArray()!;
        Assert.That(kept, Is.EqualTo(new[] { "2026-07-14_180003-snapshot-game-log.log", "2026-07-14_180004-snapshot-game-log.log" }));
    }

    [Test]
    public void TryCreate_fails_cleanly_when_the_live_log_is_missing()
    {
        string session = Path.Combine(_root, "2026-07-14_180000-session");
        bool created = AuditSnapshotPolicy.TryCreate(Path.Combine(_root, "missing.log"), session, DateTime.Now, out string snapshot, out long bytes, out string error);

        Assert.That(created, Is.False);
        Assert.That(snapshot, Is.Empty);
        Assert.That(bytes, Is.Zero);
        Assert.That(error, Does.Contain("not found"));
    }
}
