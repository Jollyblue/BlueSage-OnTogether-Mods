using System;
using System.Threading;

namespace BlueSage.QoLTweaks.Core;

internal sealed class PluginShutdownPolicy
{
    private const int Running = 0;
    private const int ShutdownStarted = 1;
    private const int SteamCleanupClaimed = 2;
    private const int SteamCleanupFinished = 3;
    private const int LocalCleanupClaimed = 4;
    private const int Complete = 5;

    private int _phase;
    private string _trigger = string.Empty;

    internal bool IsShuttingDown => Volatile.Read(ref _phase) != Running;
    internal bool IsComplete => Volatile.Read(ref _phase) == Complete;
    internal string Trigger => _trigger;

    internal bool TryBegin(string trigger)
    {
        if (Interlocked.CompareExchange(ref _phase, ShutdownStarted, Running) != Running)
        {
            return false;
        }

        _trigger = Bound(trigger, 48);
        return true;
    }

    internal bool TryClaimSteamCleanup() =>
        Interlocked.CompareExchange(ref _phase, SteamCleanupClaimed, ShutdownStarted) == ShutdownStarted;

    internal void FinishSteamCleanup()
    {
        int phase = Volatile.Read(ref _phase);
        while (phase >= ShutdownStarted && phase < SteamCleanupFinished)
        {
            int observed = Interlocked.CompareExchange(ref _phase, SteamCleanupFinished, phase);
            if (observed == phase) return;
            phase = observed;
        }
    }

    internal bool TryClaimLocalCleanup() =>
        Interlocked.CompareExchange(ref _phase, LocalCleanupClaimed, SteamCleanupFinished) == SteamCleanupFinished;

    internal void FinishLocalCleanup()
    {
        if (Volatile.Read(ref _phase) == LocalCleanupClaimed)
        {
            Interlocked.Exchange(ref _phase, Complete);
        }
    }

    private static string Bound(string value, int maximum)
    {
        string safe = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        return safe.Length <= maximum ? safe : safe.Substring(0, maximum);
    }
}
