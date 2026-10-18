using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct PlayerBadgeLayout
{
    internal PlayerBadgeLayout(
        float laneWidth,
        float nativeNameReservation,
        float hostStart,
        float hostWidth,
        float badgeStart,
        float badgeWidth)
    {
        LaneWidth = laneWidth;
        NativeNameReservation = nativeNameReservation;
        HostStart = hostStart;
        HostWidth = hostWidth;
        BadgeStart = badgeStart;
        BadgeWidth = badgeWidth;
    }

    internal float LaneWidth { get; }
    internal float NativeNameReservation { get; }
    internal float HostStart { get; }
    internal float HostWidth { get; }
    internal float BadgeStart { get; }
    internal float BadgeWidth { get; }
}

internal readonly struct PlayerBadgeAllocation
{
    internal PlayerBadgeAllocation(
        float nativeNameWidth,
        float ownedLaneWidth,
        int badgeCapacity,
        bool useOverflowLane = false)
    {
        NativeNameWidth = nativeNameWidth;
        OwnedLaneWidth = ownedLaneWidth;
        BadgeCapacity = badgeCapacity;
        UseOverflowLane = useOverflowLane;
    }

    internal float NativeNameWidth { get; }
    internal float OwnedLaneWidth { get; }
    internal int BadgeCapacity { get; }
    internal bool UseOverflowLane { get; }
}

internal readonly struct PlayerBadgeLayoutCacheKey :
    IEquatable<PlayerBadgeLayoutCacheKey>
{
    internal PlayerBadgeLayoutCacheKey(
        string badgeText,
        string hostText,
        string nativeText,
        int measurementFingerprint,
        int geometryFingerprint,
        float nativeRectWidth,
        float scale)
    {
        BadgeText = badgeText ?? string.Empty;
        HostText = hostText ?? string.Empty;
        NativeText = nativeText ?? string.Empty;
        MeasurementFingerprint = measurementFingerprint;
        GeometryFingerprint = geometryFingerprint;
        NativeRectWidth = nativeRectWidth;
        Scale = scale;
    }

    internal string BadgeText { get; }
    internal string HostText { get; }
    internal string NativeText { get; }
    internal int MeasurementFingerprint { get; }
    internal int GeometryFingerprint { get; }
    internal float NativeRectWidth { get; }
    internal float Scale { get; }

    public bool Equals(PlayerBadgeLayoutCacheKey other)
    {
        return string.Equals(
                   BadgeText,
                   other.BadgeText,
                   StringComparison.Ordinal) &&
               string.Equals(
                   HostText,
                   other.HostText,
                   StringComparison.Ordinal) &&
               string.Equals(
                   NativeText,
                   other.NativeText,
                   StringComparison.Ordinal) &&
               MeasurementFingerprint ==
                   other.MeasurementFingerprint &&
               GeometryFingerprint ==
                   other.GeometryFingerprint &&
               NativeRectWidth.Equals(
                   other.NativeRectWidth) &&
               Scale.Equals(other.Scale);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) +
                   StringComparer.Ordinal.GetHashCode(BadgeText);
            hash = (hash * 31) +
                   StringComparer.Ordinal.GetHashCode(HostText);
            hash = (hash * 31) +
                   StringComparer.Ordinal.GetHashCode(NativeText);
            hash = (hash * 31) + MeasurementFingerprint;
            hash = (hash * 31) + GeometryFingerprint;
            hash = (hash * 31) + NativeRectWidth.GetHashCode();
            hash = (hash * 31) + Scale.GetHashCode();
            return hash;
        }
    }
}

internal sealed class PlayerBadgeLayoutMeasurementCache
{
    private bool _hasValue;
    private PlayerBadgeLayoutCacheKey _key;
    private PlayerBadgeLayout _layout;

    internal bool TryGet(
        PlayerBadgeLayoutCacheKey key,
        out PlayerBadgeLayout layout)
    {
        if (_hasValue && _key.Equals(key))
        {
            layout = _layout;
            return true;
        }

        layout = default;
        return false;
    }

    internal void Store(
        PlayerBadgeLayoutCacheKey key,
        PlayerBadgeLayout layout)
    {
        _key = key;
        _layout = layout;
        _hasValue = true;
    }
}

internal static class PlayerBadgeLayoutPolicy
{
    internal static float ResolveRequiredContainerExpansion(
        float nativeNameWidth,
        float nativePreferredWidth,
        float horizontalPadding,
        float hostPreferredWidth,
        float segmentGap,
        float nativeNameGap,
        float fullBadgePreferredWidth,
        float maximumExpansion)
    {
        float currentWidth = PositiveFinite(nativeNameWidth);
        float maximum = PositiveFinite(maximumExpansion);
        if (currentWidth <= 0f || maximum <= 0f)
        {
            return 0f;
        }

        float padding = PositiveFinite(horizontalPadding);
        float hostWidth = PositiveFinite(hostPreferredWidth);
        float badgeWidth = PositiveFinite(fullBadgePreferredWidth);
        float ownedWidth =
            (padding * 2f) +
            hostWidth +
            (hostWidth > 0f && badgeWidth > 0f
                ? PositiveFinite(segmentGap)
                : 0f) +
            badgeWidth;
        float requiredWidth =
            PositiveFinite(nativePreferredWidth) +
            (ownedWidth > 0f
                ? PositiveFinite(nativeNameGap)
                : 0f) +
            ownedWidth;
        return Math.Min(
            maximum,
            Math.Max(0f, requiredWidth - currentWidth));
    }

    internal static PlayerBadgeAllocation ResolveResponsiveAllocation(
        float nativeNameWidth,
        float nativePreferredWidth,
        float minimumReadableNameWidth,
        float horizontalPadding,
        float hostPreferredWidth,
        float segmentGap,
        float nativeNameGap,
        IReadOnlyList<float> preferredWidthsByCapacity,
        float overflowLaneAvailableWidth = 0f)
    {
        float totalWidth = PositiveFinite(nativeNameWidth);
        if (totalWidth <= 0f)
        {
            return default;
        }

        float preferredName = Math.Min(
            totalWidth,
            PositiveFinite(nativePreferredWidth));
        float readableFloor = Math.Min(
            preferredName,
            PositiveFinite(minimumReadableNameWidth));
        float padding = Math.Min(
            PositiveFinite(horizontalPadding),
            totalWidth * 0.5f);
        float hostWidth = Math.Min(
            PositiveFinite(hostPreferredWidth),
            Math.Max(0f, totalWidth - readableFloor));
        float gap = PositiveFinite(segmentGap);
        float nameGap = PositiveFinite(nativeNameGap);

        if (preferredWidthsByCapacity != null &&
            preferredWidthsByCapacity.Count > 0)
        {
            float fullBadgeWidth = PositiveFinite(
                preferredWidthsByCapacity[
                    preferredWidthsByCapacity.Count - 1]);
            float fullOwnedWidth =
                (padding * 2f) +
                hostWidth +
                (hostWidth > 0f ? gap : 0f) +
                fullBadgeWidth;
            float overflowAvailable = PositiveFinite(
                overflowLaneAvailableWidth);
            bool sameLineWouldClipName =
                preferredName + fullOwnedWidth + nameGap > totalWidth;
            if (fullBadgeWidth > 0f &&
                sameLineWouldClipName &&
                fullOwnedWidth <= overflowAvailable)
            {
                return new PlayerBadgeAllocation(
                    preferredName,
                    fullOwnedWidth,
                    preferredWidthsByCapacity.Count,
                    useOverflowLane: true);
            }
        }

        if (preferredWidthsByCapacity != null)
        {
            for (int index = preferredWidthsByCapacity.Count - 1;
                 index >= 0;
                 index--)
            {
                float badgeWidth = PositiveFinite(
                    preferredWidthsByCapacity[index]);
                if (badgeWidth <= 0f)
                {
                    continue;
                }

                float ownedWidth =
                    (padding * 2f) +
                    hostWidth +
                    (hostWidth > 0f ? gap : 0f) +
                    badgeWidth;
                float remainingNameWidth = Math.Max(
                    0f,
                    totalWidth - ownedWidth - nameGap);
                if (remainingNameWidth < readableFloor)
                {
                    continue;
                }

                return new PlayerBadgeAllocation(
                    Math.Min(preferredName, remainingNameWidth),
                    ownedWidth,
                    index + 1);
            }
        }

        float hostOnlyWidth = hostWidth > 0f
            ? (padding * 2f) + hostWidth
            : 0f;
        hostOnlyWidth = Math.Min(
            hostOnlyWidth,
            Math.Max(
                0f,
                totalWidth - readableFloor - nameGap));
        float hostOnlyGap = hostOnlyWidth > 0f
            ? nameGap
            : 0f;
        return new PlayerBadgeAllocation(
            Math.Min(
                preferredName,
                Math.Max(
                    0f,
                    totalWidth - hostOnlyWidth - hostOnlyGap)),
            hostOnlyWidth,
            0);
    }

    internal static float ResolveNativeNameWidth(
        float nativeNameWidth,
        float nativePreferredWidth,
        float minimumOwnedLaneWidth)
    {
        float width = PositiveFinite(nativeNameWidth);
        if (width <= 0f)
        {
            return 0f;
        }

        float preferred = PositiveFinite(nativePreferredWidth);
        float ownedMinimum = Math.Min(
            width,
            PositiveFinite(minimumOwnedLaneWidth));
        return Math.Min(
            preferred,
            Math.Max(0f, width - ownedMinimum));
    }

    internal static int ResolveBadgeCapacity(
        float maximumLaneWidth,
        float horizontalPadding,
        float hostPreferredWidth,
        float segmentGap,
        IReadOnlyList<float> preferredWidthsByCapacity)
    {
        if (preferredWidthsByCapacity == null ||
            preferredWidthsByCapacity.Count == 0)
        {
            return 0;
        }

        float laneWidth = PositiveFinite(maximumLaneWidth);
        if (laneWidth <= 0f)
        {
            return 0;
        }

        float padding = Math.Min(
            PositiveFinite(horizontalPadding),
            laneWidth * 0.5f);
        float contentAvailable = Math.Max(
            0f,
            laneWidth - (padding * 2f));
        float hostWidth = Math.Min(
            PositiveFinite(hostPreferredWidth),
            contentAvailable);
        float available = Math.Max(
            0f,
            contentAvailable - hostWidth);
        if (hostWidth > 0f && available > 0f)
        {
            available = Math.Max(
                0f,
                available - Math.Min(
                    PositiveFinite(segmentGap),
                    available));
        }

        for (int index = preferredWidthsByCapacity.Count - 1;
             index >= 0;
             index--)
        {
            float preferred = PositiveFinite(
                preferredWidthsByCapacity[index]);
            if (preferred > 0f && preferred <= available)
            {
                return index + 1;
            }
        }

        return 0;
    }

    internal static PlayerBadgeLayout Resolve(
        float hostPreferredWidth,
        float badgePreferredWidth,
        float maximumLaneWidth,
        float horizontalPadding,
        float segmentGap,
        float nativeNameGap)
    {
        float hostPreferred = PositiveFinite(hostPreferredWidth);
        float badgePreferred = PositiveFinite(badgePreferredWidth);
        float laneMaximum = PositiveFinite(maximumLaneWidth);
        if (laneMaximum <= 0f ||
            (hostPreferred <= 0f && badgePreferred <= 0f))
        {
            return default;
        }

        float padding = Math.Min(
            PositiveFinite(horizontalPadding),
            laneMaximum * 0.5f);
        float contentAvailable = Math.Max(
            0f,
            laneMaximum - (padding * 2f));
        float hostWidth = Math.Min(
            hostPreferred,
            contentAvailable);
        float badgeWidth = 0f;
        float gap = 0f;

        if (hostPreferred > 0f && badgePreferred > 0f)
        {
            float afterHost = Math.Max(
                0f,
                contentAvailable - hostWidth);
            gap = Math.Min(
                PositiveFinite(segmentGap),
                afterHost);
            badgeWidth = Math.Min(
                badgePreferred,
                Math.Max(0f, afterHost - gap));
            if (badgeWidth <= 0f)
            {
                gap = 0f;
            }
        }
        else if (badgePreferred > 0f)
        {
            badgeWidth = Math.Min(
                badgePreferred,
                contentAvailable);
        }

        float laneWidth = Math.Min(
            laneMaximum,
            (padding * 2f) + hostWidth + gap + badgeWidth);
        float hostStart = padding;
        float badgeStart = Math.Max(
            hostStart + hostWidth + gap,
            laneWidth - padding - badgeWidth);
        float reservation = Math.Min(
            laneMaximum,
            laneWidth + PositiveFinite(nativeNameGap));
        return new PlayerBadgeLayout(
            laneWidth,
            reservation,
            hostStart,
            hostWidth,
            badgeStart,
            badgeWidth);
    }

    private static float PositiveFinite(float value)
    {
        return float.IsNaN(value) ||
               float.IsInfinity(value) ||
               value <= 0f
            ? 0f
            : value;
    }
}
