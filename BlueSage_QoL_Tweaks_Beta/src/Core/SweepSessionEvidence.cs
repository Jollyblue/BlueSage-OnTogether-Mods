using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct SweepSessionSnapshot
{
    internal SweepSessionSnapshot(
        bool captured,
        string status,
        long processWorkingSetBytes,
        long processPrivateBytes,
        int processHandleCount,
        int processThreadCount,
        int nativeRows,
        int steamMembers,
        bool voiceManagerPresent,
        bool voiceLobbyOff,
        bool voiceJoined,
        int queuedVoiceParticipants)
    {
        Captured = captured;
        Status = status ?? string.Empty;
        ProcessWorkingSetBytes = processWorkingSetBytes;
        ProcessPrivateBytes = processPrivateBytes;
        ProcessHandleCount = processHandleCount;
        ProcessThreadCount = processThreadCount;
        NativeRows = nativeRows;
        SteamMembers = steamMembers;
        VoiceManagerPresent = voiceManagerPresent;
        VoiceLobbyOff = voiceLobbyOff;
        VoiceJoined = voiceJoined;
        QueuedVoiceParticipants = queuedVoiceParticipants;
    }

    internal bool Captured { get; }
    internal string Status { get; }
    internal long ProcessWorkingSetBytes { get; }
    internal long ProcessPrivateBytes { get; }
    internal int ProcessHandleCount { get; }
    internal int ProcessThreadCount { get; }
    internal int NativeRows { get; }
    internal int SteamMembers { get; }
    internal bool VoiceManagerPresent { get; }
    internal bool VoiceLobbyOff { get; }
    internal bool VoiceJoined { get; }
    internal int QueuedVoiceParticipants { get; }

    internal static SweepSessionSnapshot Unavailable(string status)
    {
        return new SweepSessionSnapshot(
            captured: false,
            status: status,
            processWorkingSetBytes: 0L,
            processPrivateBytes: 0L,
            processHandleCount: -1,
            processThreadCount: -1,
            nativeRows: -1,
            steamMembers: -1,
            voiceManagerPresent: false,
            voiceLobbyOff: false,
            voiceJoined: false,
            queuedVoiceParticipants: -1);
    }
}

internal static class SweepSessionEvidenceFormatter
{
    private const double BytesPerMiB = 1048576d;

    internal static string Format(SweepSessionSnapshot before, SweepSessionSnapshot after)
    {
        if (!before.Captured || !after.Captured)
        {
            string status = !before.Captured ? before.Status : after.Status;
            return "sessionEvidence=unavailable:" + SanitizeUnavailable(status);
        }

        return string.Join("; ", new[]
        {
            "processPrivateBeforeMiB=" + ToMiB(before.ProcessPrivateBytes),
            "processPrivateAfterMiB=" + ToMiB(after.ProcessPrivateBytes),
            "processPrivateDeltaMiB=" + ToSignedMiB(after.ProcessPrivateBytes - before.ProcessPrivateBytes),
            "workingSetBeforeMiB=" + ToMiB(before.ProcessWorkingSetBytes),
            "workingSetAfterMiB=" + ToMiB(after.ProcessWorkingSetBytes),
            "workingSetDeltaMiB=" + ToSignedMiB(after.ProcessWorkingSetBytes - before.ProcessWorkingSetBytes),
            "handles=" + before.ProcessHandleCount.ToString(CultureInfo.InvariantCulture) + "->" + after.ProcessHandleCount.ToString(CultureInfo.InvariantCulture),
            "threads=" + before.ProcessThreadCount.ToString(CultureInfo.InvariantCulture) + "->" + after.ProcessThreadCount.ToString(CultureInfo.InvariantCulture),
            "nativeRows=" + after.NativeRows.ToString(CultureInfo.InvariantCulture),
            "steamMembers=" + after.SteamMembers.ToString(CultureInfo.InvariantCulture),
            "nativeSteamDelta=" + DifferenceOrUnknown(after.NativeRows, after.SteamMembers),
            "voiceManager=" + Bool(after.VoiceManagerPresent),
            "voiceLobbyOff=" + Bool(after.VoiceLobbyOff),
            "voiceJoined=" + Bool(after.VoiceJoined),
            "voiceQueued=" + after.QueuedVoiceParticipants.ToString(CultureInfo.InvariantCulture)
        });
    }

    private static string ToMiB(long bytes)
    {
        return (bytes / BytesPerMiB).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string ToSignedMiB(long bytes)
    {
        return (bytes / BytesPerMiB).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    }

    private static string DifferenceOrUnknown(int left, int right)
    {
        return left < 0 || right < 0
            ? "unknown"
            : (left - right).ToString(CultureInfo.InvariantCulture);
    }

    private static string Bool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string SanitizeUnavailable(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unavailable";
        string sanitized = value.Trim();
        if (sanitized.StartsWith("unavailable:", StringComparison.Ordinal))
        {
            sanitized = sanitized.Substring("unavailable:".Length);
        }
        sanitized = sanitized.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
        return sanitized.Length <= 96 ? sanitized : sanitized.Substring(0, 96);
    }
}
