using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class AuditMirrorTransferBufferTests
{
    [Test]
    public void Idle_ticks_do_not_allocate_a_transfer_buffer()
    {
        AuditMirrorTransferBuffer buffer = new();

        for (int index = 0; index < 10_000; index++)
        {
            Assert.That(buffer.Acquire(sourceLength: 42, sourceOffset: 42, maximumBytes: 512 * 1024), Is.Null);
        }

        Assert.That(buffer.AllocationCount, Is.Zero);
    }

    [Test]
    public void Active_ticks_reuse_one_sub_loh_buffer()
    {
        AuditMirrorTransferBuffer buffer = new();

        byte[] first = buffer.Acquire(sourceLength: 1024 * 1024, sourceOffset: 0, maximumBytes: 512 * 1024)!;
        byte[] second = buffer.Acquire(sourceLength: 1024 * 1024, sourceOffset: 64 * 1024, maximumBytes: 512 * 1024)!;

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.SameAs(second));
            Assert.That(first.Length, Is.EqualTo(AuditMirrorTransferBuffer.Capacity));
            Assert.That(first.Length, Is.LessThan(85_000));
            Assert.That(buffer.AllocationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Invalid_or_truncated_offsets_fail_idle_until_the_caller_resets_them()
    {
        AuditMirrorTransferBuffer buffer = new();

        Assert.Multiple(() =>
        {
            Assert.That(buffer.Acquire(sourceLength: 100, sourceOffset: 101, maximumBytes: 512 * 1024), Is.Null);
            Assert.That(buffer.Acquire(sourceLength: 100, sourceOffset: 0, maximumBytes: 0), Is.Null);
            Assert.That(buffer.AllocationCount, Is.Zero);
        });
    }
}
