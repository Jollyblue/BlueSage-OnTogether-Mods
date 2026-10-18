using System;

namespace BlueSage.QoLTweaks.Core;

internal enum IdentityResyncBeginReason
{
    Ready,
    NoCurrentScope,
    ScopeMigrated,
    StaleGeneration,
    QuietWindow,
    GlobalCooldown,
    SingleFlight,
    GenerationAlreadyCompleted,
}

internal readonly struct IdentityResyncBeginDecision
{
    internal IdentityResyncBeginDecision(
        IdentityResyncBeginReason reason,
        long retryAfterMilliseconds)
    {
        Reason = reason;
        RetryAfterMilliseconds = Math.Max(0L, retryAfterMilliseconds);
    }

    internal IdentityResyncBeginReason Reason { get; }
    internal long RetryAfterMilliseconds { get; }
}

internal readonly struct IdentityResyncLease
{
    internal IdentityResyncLease(
        long sequence,
        string scope,
        long generation)
    {
        Sequence = sequence;
        Scope = scope ?? string.Empty;
        Generation = generation;
    }

    internal long Sequence { get; }
    internal string Scope { get; }
    internal long Generation { get; }
    internal bool IsValid => Sequence > 0L && Scope.Length > 0 && Generation > 0L;
}

internal sealed class IdentityResyncCoordinator
{
    internal const long QuietWindowMilliseconds = 500L;
    internal const long GlobalCooldownMilliseconds = 5_000L;

    private string _currentScope = string.Empty;
    private string _currentOwner = string.Empty;
    private long _generation;
    private long _quietUntilMilliseconds;
    private long _lastBeginMilliseconds = long.MinValue;
    private long _completedGeneration;
    private string _completedScope = string.Empty;
    private long _sequence;
    private IdentityResyncLease _inFlight;

    internal long CurrentGeneration => _generation;
    internal string CurrentScope => _currentScope;
    internal bool IsInFlight => _inFlight.IsValid;

    internal long ObserveScope(
        string scope,
        string owner,
        long nowMilliseconds)
    {
        string cleanScope = (scope ?? string.Empty).Trim();
        string cleanOwner = (owner ?? string.Empty).Trim();
        if (cleanScope.Length == 0 || cleanOwner.Length == 0)
        {
            ClearCurrentScope();
            return 0L;
        }

        if (!string.Equals(_currentScope, cleanScope, StringComparison.Ordinal) ||
            !string.Equals(_currentOwner, cleanOwner, StringComparison.Ordinal))
        {
            _currentScope = cleanScope;
            _currentOwner = cleanOwner;
            AdvanceGeneration(nowMilliseconds);
        }
        return _generation;
    }

    internal long ObserveTrigger(
        string scope,
        string owner,
        IdentityResyncTrigger trigger,
        long nowMilliseconds)
    {
        ObserveScope(scope, owner, nowMilliseconds);
        if (_currentScope.Length == 0)
        {
            return 0L;
        }
        if (IdentityResyncTriggerPolicy.AdvancesGeneration(trigger) &&
            trigger != IdentityResyncTrigger.LobbyOrOwnerIdentityChanged)
        {
            AdvanceGeneration(nowMilliseconds);
        }
        return _generation;
    }

    internal bool TryBegin(
        string scope,
        long generation,
        long nowMilliseconds,
        out IdentityResyncLease lease,
        out IdentityResyncBeginDecision decision)
    {
        lease = default;
        if (_currentScope.Length == 0)
        {
            decision = new IdentityResyncBeginDecision(
                IdentityResyncBeginReason.NoCurrentScope,
                0L);
            return false;
        }
        if (!string.Equals(scope, _currentScope, StringComparison.Ordinal))
        {
            decision = new IdentityResyncBeginDecision(
                IdentityResyncBeginReason.ScopeMigrated,
                0L);
            return false;
        }
        if (generation <= 0L || generation != _generation)
        {
            decision = new IdentityResyncBeginDecision(
                IdentityResyncBeginReason.StaleGeneration,
                0L);
            return false;
        }
        if (_inFlight.IsValid)
        {
            decision = new IdentityResyncBeginDecision(
                IdentityResyncBeginReason.SingleFlight,
                0L);
            return false;
        }
        if (string.Equals(_completedScope, _currentScope, StringComparison.Ordinal) &&
            _completedGeneration == _generation)
        {
            decision = new IdentityResyncBeginDecision(
                IdentityResyncBeginReason.GenerationAlreadyCompleted,
                0L);
            return false;
        }
        if (nowMilliseconds < _quietUntilMilliseconds)
        {
            decision = new IdentityResyncBeginDecision(
                IdentityResyncBeginReason.QuietWindow,
                _quietUntilMilliseconds - nowMilliseconds);
            return false;
        }
        if (_lastBeginMilliseconds != long.MinValue)
        {
            long cooldownUntil = _lastBeginMilliseconds + GlobalCooldownMilliseconds;
            if (nowMilliseconds < cooldownUntil)
            {
                decision = new IdentityResyncBeginDecision(
                    IdentityResyncBeginReason.GlobalCooldown,
                    cooldownUntil - nowMilliseconds);
                return false;
            }
        }

        _sequence++;
        _inFlight = new IdentityResyncLease(
            _sequence,
            _currentScope,
            _generation);
        _lastBeginMilliseconds = nowMilliseconds;
        lease = _inFlight;
        decision = new IdentityResyncBeginDecision(
            IdentityResyncBeginReason.Ready,
            0L);
        return true;
    }

    internal void Complete(
        IdentityResyncLease lease,
        long nowMilliseconds)
    {
        if (!_inFlight.IsValid ||
            !lease.IsValid ||
            lease.Sequence != _inFlight.Sequence)
        {
            return;
        }

        _completedScope = lease.Scope;
        _completedGeneration = lease.Generation;
        _inFlight = default;
    }

    internal void Abandon(IdentityResyncLease lease)
    {
        if (_inFlight.IsValid &&
            lease.IsValid &&
            lease.Sequence == _inFlight.Sequence)
        {
            _inFlight = default;
        }
    }

    internal void Reset()
    {
        _currentScope = string.Empty;
        _currentOwner = string.Empty;
        _generation = 0L;
        _quietUntilMilliseconds = 0L;
        _lastBeginMilliseconds = long.MinValue;
        _completedGeneration = 0L;
        _completedScope = string.Empty;
        _sequence = 0L;
        _inFlight = default;
    }

    private void AdvanceGeneration(long nowMilliseconds)
    {
        _generation++;
        _quietUntilMilliseconds = nowMilliseconds + QuietWindowMilliseconds;
        _inFlight = default;
    }

    private void ClearCurrentScope()
    {
        _currentScope = string.Empty;
        _currentOwner = string.Empty;
        _quietUntilMilliseconds = 0L;
        _inFlight = default;
    }
}
