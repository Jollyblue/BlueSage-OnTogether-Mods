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
    public void TryCreate_copies_the_exact_source_boundary_to_a_timestamped_LogOutput()
    {
        string source = Path.Combine(_root, "LogOutput.log");
        byte[] expected = Enumerable.Range(0, 10000).Select(value => (byte)(value % 251)).ToArray();
        File.WriteAllBytes(source, expected);
        string session = Path.Combine(_root, "session-1");

        bool created = AuditSnapshotPolicy.TryCreate(source, session, new DateTime(2026, 7, 14, 18, 4, 31, 123, DateTimeKind.Utc), "a0f6b6eb", out string snapshot, out long bytes, out string error);

        Assert.That(created, Is.True, error);
        Assert.That(snapshot, Does.EndWith("snapshot-20260714T180431123Z-a0f6b6eb"));
        Assert.That(bytes, Is.EqualTo(expected.Length));
        Assert.That(File.ReadAllBytes(Path.Combine(snapshot, "LogOutput.log")), Is.EqualTo(expected));
        Assert.That(Directory.GetFiles(snapshot, "*.tmp"), Is.Empty);
    }

    [Test]
    public void TryCreate_retains_only_the_latest_eight_snapshots()
    {
        string source = Path.Combine(_root, "LogOutput.log");
        File.WriteAllText(source, "runtime evidence");
        string session = Path.Combine(_root, "session-1");

        for (int index = 0; index < 10; index++)
        {
            bool created = AuditSnapshotPolicy.TryCreate(source, session, new DateTime(2026, 7, 14, 18, 0, index, DateTimeKind.Utc), "abcd" + index.ToString("D4"), out _, out _, out string error);
            Assert.That(created, Is.True, error);
        }

        Assert.That(Directory.GetDirectories(Path.Combine(session, "snapshots"), "snapshot-*"), Has.Length.EqualTo(AuditSnapshotPolicy.MaxRetainedSnapshots));
    }

    [Test]
    public void TryCreate_fails_cleanly_when_the_live_log_is_missing()
    {
        string session = Path.Combine(_root, "session-1");
        bool created = AuditSnapshotPolicy.TryCreate(Path.Combine(_root, "missing.log"), session, DateTime.UtcNow, "a0f6b6eb", out string snapshot, out long bytes, out string error);

        Assert.That(created, Is.False);
        Assert.That(snapshot, Is.Empty);
        Assert.That(bytes, Is.Zero);
        Assert.That(error, Does.Contain("not found"));
    }
}
