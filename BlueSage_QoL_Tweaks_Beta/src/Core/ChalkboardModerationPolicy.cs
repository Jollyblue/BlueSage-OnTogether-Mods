using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal enum ChalkboardClearResult
{
    Rejected,
    Complete,
    Partial
}

internal readonly struct ChalkboardDeliveryResult
{
    internal ChalkboardDeliveryResult(int attempted, int sent, int skipped)
    {
        if (attempted < 0) throw new ArgumentOutOfRangeException(nameof(attempted));
        if (sent < 0 || sent > attempted) throw new ArgumentOutOfRangeException(nameof(sent));
        if (skipped < 0 || skipped > attempted) throw new ArgumentOutOfRangeException(nameof(skipped));
        if (sent + skipped != attempted)
        {
            throw new ArgumentException("Sent and skipped counts must account for every attempted recipient.");
        }

        Attempted = attempted;
        Sent = sent;
        Skipped = skipped;
    }

    internal int Attempted { get; }
    internal int Sent { get; }
    internal int Skipped { get; }
    internal bool IsComplete => Sent == Attempted && Skipped == 0;
}

internal readonly struct ChalkboardClearRequest
{
    internal ChalkboardClearRequest(
        string action,
        int boardIndex,
        string boardFingerprint,
        long issuedUnixSeconds,
        string nonce,
        string snapshotName)
    {
        Action = action;
        BoardIndex = boardIndex;
        BoardFingerprint = boardFingerprint;
        IssuedUnixSeconds = issuedUnixSeconds;
        Nonce = nonce;
        SnapshotName = snapshotName;
    }

    internal string Action { get; }
    internal int BoardIndex { get; }
    internal string BoardFingerprint { get; }
    internal long IssuedUnixSeconds { get; }
    internal string Nonce { get; }
    internal string SnapshotName { get; }
}

internal static class ChalkboardModerationPolicy
{
    internal const string MemberDataKey = "bluesage_chalk_request_v2";
    internal const int RequestExpirySeconds = 15;
    internal const int AllowedFutureClockSkewSeconds = 2;
    internal const int MaxProtocolBoardIndex = ChalkboardCatalogPolicy.MaxCatalogIndex;
    internal const int MaxPayloadBytes = 256;
    private const double RiverCoordinateTolerance = 0.25d;
    private const double ClassroomFaceCoordinateTolerance = 0.02d;

    private const string Prefix = "v2|";
    private static readonly Regex NoncePattern =
        new Regex("^[A-Za-z0-9_-]{8,64}$", RegexOptions.Compiled);
    private static readonly Regex FingerprintPattern =
        new Regex("^[0-9A-F]{16}$", RegexOptions.Compiled);
    private static readonly Regex SnapshotNamePattern =
        new Regex("^[A-Za-z0-9][A-Za-z0-9_-]{0,39}$", RegexOptions.Compiled);

    internal static string BuildMemberData(
        int boardIndex,
        string boardFingerprint,
        long issuedUnixSeconds,
        string nonce)
    {
        string safeFingerprint = boardFingerprint ?? string.Empty;
        string safeNonce = nonce ?? string.Empty;
        if (boardIndex < 0 || boardIndex > MaxProtocolBoardIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(boardIndex));
        }
        if (!FingerprintPattern.IsMatch(safeFingerprint))
        {
            throw new ArgumentException("Board fingerprint must be sixteen uppercase hexadecimal characters.", nameof(boardFingerprint));
        }
        if (issuedUnixSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(issuedUnixSeconds));
        }
        if (!NoncePattern.IsMatch(safeNonce))
        {
            throw new ArgumentException("Nonce must be 8-64 URL-safe characters.", nameof(nonce));
        }

        return BuildMemberData(
            "clear",
            boardIndex,
            safeFingerprint,
            issuedUnixSeconds,
            safeNonce,
            string.Empty);
    }

    internal static string BuildRestoreMemberData(
        int boardIndex,
        string boardFingerprint,
        long issuedUnixSeconds,
        string nonce,
        string snapshotName)
    {
        string safeSnapshotName = snapshotName ?? string.Empty;
        if (!SnapshotNamePattern.IsMatch(safeSnapshotName))
        {
            throw new ArgumentException("Snapshot name must be 1-40 safe characters.", nameof(snapshotName));
        }
        return BuildMemberData(
            "load",
            boardIndex,
            boardFingerprint,
            issuedUnixSeconds,
            nonce,
            safeSnapshotName);
    }

    private static string BuildMemberData(
        string action,
        int boardIndex,
        string boardFingerprint,
        long issuedUnixSeconds,
        string nonce,
        string snapshotName)
    {
        if (boardIndex < 0 || boardIndex > MaxProtocolBoardIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(boardIndex));
        }
        if (!FingerprintPattern.IsMatch(boardFingerprint ?? string.Empty))
        {
            throw new ArgumentException("Board fingerprint must be sixteen uppercase hexadecimal characters.", nameof(boardFingerprint));
        }
        if (issuedUnixSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(issuedUnixSeconds));
        }
        if (!NoncePattern.IsMatch(nonce ?? string.Empty))
        {
            throw new ArgumentException("Nonce must be 8-64 URL-safe characters.", nameof(nonce));
        }

        string payload = Prefix +
               action + "|" +
               boardIndex.ToString(CultureInfo.InvariantCulture) + "|" +
               boardFingerprint + "|" +
               issuedUnixSeconds.ToString(CultureInfo.InvariantCulture) + "|" +
               nonce +
               (snapshotName.Length > 0 ? "|" + snapshotName : string.Empty);
        if (Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
        {
            throw new ArgumentException("Chalkboard request exceeds the transport limit.");
        }
        return payload;
    }

    internal static bool TryParseMemberData(string payload, out ChalkboardClearRequest request)
    {
        request = default;
        if (Encoding.UTF8.GetByteCount(payload ?? string.Empty) > MaxPayloadBytes)
        {
            return false;
        }
        string[] parts = (payload ?? string.Empty).Split('|');
        bool clearShape = parts.Length == 6 &&
                          parts.Length > 1 &&
                          string.Equals(parts[1], "clear", StringComparison.Ordinal);
        bool loadShape = parts.Length == 7 &&
                         parts.Length > 1 &&
                         string.Equals(parts[1], "load", StringComparison.Ordinal);
        if ((!clearShape && !loadShape) ||
            !string.Equals(parts[0], "v2", StringComparison.Ordinal) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int boardIndex) ||
            boardIndex < 0 ||
            boardIndex > MaxProtocolBoardIndex ||
            !FingerprintPattern.IsMatch(parts[3]) ||
            !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out long issuedUnixSeconds) ||
            issuedUnixSeconds < 1 ||
            !NoncePattern.IsMatch(parts[5]))
        {
            return false;
        }
        string snapshotName = loadShape ? parts[6] : string.Empty;
        if (loadShape && !SnapshotNamePattern.IsMatch(snapshotName))
        {
            return false;
        }

        request = new ChalkboardClearRequest(
            parts[1],
            boardIndex,
            parts[3],
            issuedUnixSeconds,
            parts[5],
            snapshotName);
        return true;
    }

    internal static bool IsFresh(long issuedUnixSeconds, long nowUnixSeconds)
    {
        long age = nowUnixSeconds - issuedUnixSeconds;
        return age >= -AllowedFutureClockSkewSeconds && age <= RequestExpirySeconds;
    }

    internal static bool TryParseExplicitBoardIndex(
        string token,
        int boardCount,
        out int boardIndex,
        out string error)
    {
        boardIndex = -1;
        error = string.Empty;
        bool parsed = ChalkboardCatalogPolicy.TryParseExactIndex(
            token,
            boardCount,
            out ChalkboardCatalogEntry entry,
            out error);
        boardIndex = parsed ? entry.Index : -1;
        return parsed;
    }

    internal static string ClassifyLocation(string sceneName, string hierarchyPath)
    {
        string evidence = ((sceneName ?? string.Empty) + "/" + (hierarchyPath ?? string.Empty))
            .ToLowerInvariant();
        if (ContainsAny(evidence, "classroom", "school", "lesson", "teacher", "academy"))
        {
            return "Classroom";
        }
        if (ContainsAny(evidence, "river", "stream", "water", "bridge", "dock"))
        {
            return "River";
        }
        return "Unmapped";
    }

    internal static string ClassifyBuild24259292Location(
        int boardIndex,
        double x,
        double y,
        double z)
    {
        if (boardIndex == 0 &&
            Near(x, 38.909, RiverCoordinateTolerance) &&
            Near(y, 1.601, RiverCoordinateTolerance) &&
            Near(z, -12.322, RiverCoordinateTolerance))
        {
            return "River/Stage";
        }
        if (boardIndex == 1 &&
            Near(x, -19.605, ClassroomFaceCoordinateTolerance) &&
            Near(y, 4.999, ClassroomFaceCoordinateTolerance) &&
            Near(z, 7.912, ClassroomFaceCoordinateTolerance))
        {
            return "Classroom board A (face A)";
        }
        if (boardIndex == 2 &&
            Near(x, -19.620, ClassroomFaceCoordinateTolerance) &&
            Near(y, 4.999, ClassroomFaceCoordinateTolerance) &&
            Near(z, 7.954, ClassroomFaceCoordinateTolerance))
        {
            return "Classroom board B (face B)";
        }
        return "Unmapped";
    }

    internal static string BuildBoardFingerprint(string sceneName, string hierarchyPath)
    {
        return BuildBoardFingerprint(sceneName, hierarchyPath, 0d, 0d, 0d);
    }

    internal static string BuildBoardFingerprint(
        string sceneName,
        string hierarchyPath,
        double worldX,
        double worldY,
        double worldZ)
    {
        string canonical = (
                (sceneName ?? string.Empty).Trim() + "|" +
                (hierarchyPath ?? string.Empty).Trim() + "|" +
                QuantizeCoordinate(worldX) + "," +
                QuantizeCoordinate(worldY) + "," +
                QuantizeCoordinate(worldZ))
            .ToLowerInvariant();
        byte[] bytes = Encoding.UTF8.GetBytes(canonical);
        ulong hash = 14695981039346656037UL;
        for (int index = 0; index < bytes.Length; index++)
        {
            hash ^= bytes[index];
            hash *= 1099511628211UL;
        }
        return hash.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static string QuantizeCoordinate(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Board coordinates must be finite.");
        }

        long centimeters = checked((long)Math.Round(value * 100d, MidpointRounding.AwayFromZero));
        return centimeters.ToString(CultureInfo.InvariantCulture);
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (value.IndexOf(candidate, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static bool Near(double actual, double expected, double tolerance)
    {
        return Math.Abs(actual - expected) <= tolerance;
    }
}
