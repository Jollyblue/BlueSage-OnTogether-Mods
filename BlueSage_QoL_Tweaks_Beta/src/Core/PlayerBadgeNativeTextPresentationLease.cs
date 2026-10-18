using System;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct PlayerBadgeNativeTextPresentation
{
    internal PlayerBadgeNativeTextPresentation(
        float marginLeft,
        float marginTop,
        float marginRight,
        float marginBottom,
        int wrappingMode,
        int overflowMode)
    {
        MarginLeft = marginLeft;
        MarginTop = marginTop;
        MarginRight = marginRight;
        MarginBottom = marginBottom;
        WrappingMode = wrappingMode;
        OverflowMode = overflowMode;
    }

    internal float MarginLeft { get; }
    internal float MarginTop { get; }
    internal float MarginRight { get; }
    internal float MarginBottom { get; }
    internal int WrappingMode { get; }
    internal int OverflowMode { get; }
}

internal sealed class PlayerBadgeNativeTextPresentationLease
{
    private bool _active;
    private PlayerBadgeNativeTextPresentation _baseline;
    private PlayerBadgeNativeTextPresentation _applied;

    internal bool HasActiveLease => _active;

    internal PlayerBadgeNativeTextPresentation Apply(
        PlayerBadgeNativeTextPresentation current,
        float rightReservation,
        int noWrapMode,
        int ellipsisMode)
    {
        if (_active)
        {
            _baseline = AdoptExternalChanges(
                current,
                _baseline,
                _applied);
        }
        else
        {
            _baseline = current;
        }

        float reservation = float.IsNaN(rightReservation) ||
                            float.IsInfinity(rightReservation) ||
                            rightReservation <= 0f
            ? 0f
            : rightReservation;
        _applied = new PlayerBadgeNativeTextPresentation(
            _baseline.MarginLeft,
            _baseline.MarginTop,
            _baseline.MarginRight + reservation,
            _baseline.MarginBottom,
            noWrapMode,
            ellipsisMode);
        _active = true;
        return _applied;
    }

    internal PlayerBadgeNativeTextPresentation Restore(
        PlayerBadgeNativeTextPresentation current)
    {
        if (!_active)
        {
            return current;
        }

        var restored = new PlayerBadgeNativeTextPresentation(
            Owned(current.MarginLeft, _applied.MarginLeft)
                ? _baseline.MarginLeft
                : current.MarginLeft,
            Owned(current.MarginTop, _applied.MarginTop)
                ? _baseline.MarginTop
                : current.MarginTop,
            Owned(current.MarginRight, _applied.MarginRight)
                ? _baseline.MarginRight
                : current.MarginRight,
            Owned(current.MarginBottom, _applied.MarginBottom)
                ? _baseline.MarginBottom
                : current.MarginBottom,
            current.WrappingMode == _applied.WrappingMode
                ? _baseline.WrappingMode
                : current.WrappingMode,
            current.OverflowMode == _applied.OverflowMode
                ? _baseline.OverflowMode
                : current.OverflowMode);
        _active = false;
        _baseline = default;
        _applied = default;
        return restored;
    }

    internal bool MatchesApplied(
        PlayerBadgeNativeTextPresentation current)
    {
        return _active &&
               Owned(current.MarginLeft, _applied.MarginLeft) &&
               Owned(current.MarginTop, _applied.MarginTop) &&
               Owned(current.MarginRight, _applied.MarginRight) &&
               Owned(current.MarginBottom, _applied.MarginBottom) &&
               current.WrappingMode == _applied.WrappingMode &&
               current.OverflowMode == _applied.OverflowMode;
    }

    private static PlayerBadgeNativeTextPresentation AdoptExternalChanges(
        PlayerBadgeNativeTextPresentation current,
        PlayerBadgeNativeTextPresentation baseline,
        PlayerBadgeNativeTextPresentation applied)
    {
        return new PlayerBadgeNativeTextPresentation(
            Owned(current.MarginLeft, applied.MarginLeft)
                ? baseline.MarginLeft
                : current.MarginLeft,
            Owned(current.MarginTop, applied.MarginTop)
                ? baseline.MarginTop
                : current.MarginTop,
            Owned(current.MarginRight, applied.MarginRight)
                ? baseline.MarginRight
                : current.MarginRight,
            Owned(current.MarginBottom, applied.MarginBottom)
                ? baseline.MarginBottom
                : current.MarginBottom,
            current.WrappingMode == applied.WrappingMode
                ? baseline.WrappingMode
                : current.WrappingMode,
            current.OverflowMode == applied.OverflowMode
                ? baseline.OverflowMode
                : current.OverflowMode);
    }

    private static bool Owned(float current, float applied)
    {
        return current.Equals(applied);
    }
}
