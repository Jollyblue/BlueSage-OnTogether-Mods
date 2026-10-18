#nullable disable

namespace BlueSage.QoLTweaks.Core;

/// <summary>
/// Lazily owns the reusable transfer buffer used by the local BepInEx log mirror.
/// Keeping the chunk below the large-object-heap threshold avoids a 512 KiB LOH
/// allocation on every maintenance tick while preserving bounded copy behavior.
/// </summary>
internal sealed class AuditMirrorTransferBuffer
{
    internal const int Capacity = 64 * 1024;

    private byte[] _buffer;

    internal int AllocationCount { get; private set; }

    internal byte[] Acquire(long sourceLength, long sourceOffset, int maximumBytes)
    {
        if (maximumBytes <= 0 || sourceOffset < 0L || sourceOffset >= sourceLength)
        {
            return null;
        }

        if (_buffer == null)
        {
            _buffer = new byte[Capacity];
            AllocationCount++;
        }

        return _buffer;
    }
}
