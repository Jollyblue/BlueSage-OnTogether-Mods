#nullable disable

namespace BlueSage.QoLTweaks.Core;

internal sealed class SweepOperationGate
{
    private int _lastSequence;

    internal bool IsRunning { get; private set; }
    internal int ActiveSequence { get; private set; }
    internal string ActiveReason { get; private set; } = string.Empty;

    internal bool TryStart(string reason, out int sequence)
    {
        if (IsRunning)
        {
            sequence = ActiveSequence;
            return false;
        }

        _lastSequence++;
        ActiveSequence = _lastSequence;
        ActiveReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim();
        IsRunning = true;
        sequence = ActiveSequence;
        return true;
    }

    internal void Complete(int sequence)
    {
        if (!IsRunning || sequence != ActiveSequence)
        {
            return;
        }

        IsRunning = false;
        ActiveReason = string.Empty;
    }
}
