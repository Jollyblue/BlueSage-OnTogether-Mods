#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum SessionRunState
{
    Unknown = 0,
    Running = 1,
    Clean = 2,
}

internal enum SessionRunClassification
{
    None = 0,
    Clean = 1,
    Concurrent = 2,
    Uncertain = 3,
    Abnormal = 4,
}

internal sealed class SessionRunMarker
{
    internal static readonly SessionRunMarker Empty =
        new SessionRunMarker(string.Empty, 0, 0L, 0L, 0L, SessionRunState.Unknown, string.Empty);

    internal SessionRunMarker(
        string version,
        int processId,
        long startedUtcTicks,
        long heartbeatUtcTicks,
        long closedUtcTicks,
        SessionRunState state,
        string context)
    {
        Version = version ?? string.Empty;
        ProcessId = processId;
        StartedUtcTicks = startedUtcTicks;
        HeartbeatUtcTicks = heartbeatUtcTicks;
        ClosedUtcTicks = closedUtcTicks;
        State = state;
        Context = context ?? string.Empty;
    }

    internal string Version { get; }
    internal int ProcessId { get; }
    internal long StartedUtcTicks { get; }
    internal long HeartbeatUtcTicks { get; }
    internal long ClosedUtcTicks { get; }
    internal SessionRunState State { get; }
    internal string Context { get; }
}

internal static class SessionRunMarkerPolicy
{
    internal const int SchemaVersion = 1;
    internal const int AbnormalStaleSeconds = 15;
    internal const int HeartbeatIntervalSeconds = 5;

    internal static SessionRunMarker CreateRunning(
        string version,
        int processId,
        long startedUtcTicks,
        long heartbeatUtcTicks,
        string context) =>
        new SessionRunMarker(
            Bound(version, 128),
            processId,
            startedUtcTicks,
            heartbeatUtcTicks,
            0L,
            SessionRunState.Running,
            Bound(context, 64));

    internal static SessionRunMarker CreateCleanClose(SessionRunMarker current, long closedUtcTicks)
    {
        if (current == null) throw new ArgumentNullException(nameof(current));
        return new SessionRunMarker(
            current.Version,
            current.ProcessId,
            current.StartedUtcTicks,
            Math.Max(current.HeartbeatUtcTicks, closedUtcTicks),
            closedUtcTicks,
            SessionRunState.Clean,
            "clean-close");
    }

    internal static SessionRunMarker WithHeartbeat(SessionRunMarker current, long heartbeatUtcTicks, string context)
    {
        if (current == null) throw new ArgumentNullException(nameof(current));
        return new SessionRunMarker(
            current.Version,
            current.ProcessId,
            current.StartedUtcTicks,
            heartbeatUtcTicks,
            0L,
            SessionRunState.Running,
            Bound(context, 64));
    }

    internal static SessionRunClassification ClassifyPrior(
        SessionRunMarker prior,
        long nowUtcTicks,
        bool processExists,
        bool processStartMatches)
    {
        if (prior == null) return SessionRunClassification.None;
        if (prior.State == SessionRunState.Clean) return SessionRunClassification.Clean;
        if (prior.State != SessionRunState.Running || !IsValidTicks(nowUtcTicks))
            return SessionRunClassification.Uncertain;
        if (processExists && processStartMatches) return SessionRunClassification.Concurrent;
        if (processExists && !processStartMatches) return SessionRunClassification.Abnormal;

        long age = nowUtcTicks - prior.HeartbeatUtcTicks;
        if (age < 0L || age < TimeSpan.FromSeconds(AbnormalStaleSeconds).Ticks)
            return SessionRunClassification.Uncertain;
        return SessionRunClassification.Abnormal;
    }

    internal static string Serialize(SessionRunMarker marker)
    {
        if (marker == null) return string.Empty;
        return new StringBuilder(256)
            .Append("schema=").Append(SchemaVersion).Append('\n')
            .Append("state=").Append(marker.State == SessionRunState.Clean ? "clean" : "running").Append('\n')
            .Append("version=").Append(Escape(marker.Version)).Append('\n')
            .Append("pid=").Append(marker.ProcessId.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("startedUtcTicks=").Append(marker.StartedUtcTicks.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("heartbeatUtcTicks=").Append(marker.HeartbeatUtcTicks.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("closedUtcTicks=").Append(marker.ClosedUtcTicks.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append("context=").Append(Escape(marker.Context)).Append('\n')
            .ToString();
    }

    internal static bool TryParse(string serialized, out SessionRunMarker marker)
    {
        marker = SessionRunMarker.Empty;
        string[] lines = (serialized ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        if (lines.Length != 9 || lines[8].Length != 0) return false;
        if (!TryValue(lines[0], "schema", out string schema) ||
            schema != SchemaVersion.ToString(CultureInfo.InvariantCulture) ||
            !TryValue(lines[1], "state", out string stateText) ||
            !TryValue(lines[2], "version", out string version) ||
            !TryValue(lines[3], "pid", out string pidText) ||
            !TryValue(lines[4], "startedUtcTicks", out string startedText) ||
            !TryValue(lines[5], "heartbeatUtcTicks", out string heartbeatText) ||
            !TryValue(lines[6], "closedUtcTicks", out string closedText) ||
            !TryValue(lines[7], "context", out string context) ||
            !int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid) ||
            !long.TryParse(startedText, NumberStyles.None, CultureInfo.InvariantCulture, out long started) ||
            !long.TryParse(heartbeatText, NumberStyles.None, CultureInfo.InvariantCulture, out long heartbeat) ||
            !long.TryParse(closedText, NumberStyles.None, CultureInfo.InvariantCulture, out long closed) ||
            !TryUnescape(version, out version) ||
            !TryUnescape(context, out context))
        {
            return false;
        }

        SessionRunState state = stateText == "running"
            ? SessionRunState.Running
            : stateText == "clean"
                ? SessionRunState.Clean
                : SessionRunState.Unknown;
        if (state == SessionRunState.Unknown ||
            pid <= 0 ||
            version.Length == 0 ||
            version.Length > 128 ||
            context.Length > 64 ||
            !IsValidTicks(started) ||
            !IsValidTicks(heartbeat) ||
            heartbeat < started ||
            (state == SessionRunState.Running && closed != 0L) ||
            (state == SessionRunState.Clean && (!IsValidTicks(closed) || closed < started)))
        {
            return false;
        }

        marker = new SessionRunMarker(version, pid, started, heartbeat, closed, state, context);
        return true;
    }

    private static bool TryValue(string line, string name, out string value)
    {
        string prefix = name + "=";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }
        value = line.Substring(prefix.Length);
        return true;
    }

    private static bool IsValidTicks(long ticks) =>
        ticks >= 621355968000000000L && ticks <= DateTime.MaxValue.Ticks;

    private static string Bound(string value, int maximum)
    {
        string safe = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("=", "-").Trim();
        return safe.Length <= maximum ? safe : safe.Substring(0, maximum);
    }

    private static string Escape(string value) =>
        (value ?? string.Empty).Replace("%", "%25").Replace("\n", "%0A").Replace("\r", "%0D").Replace("=", "%3D");

    private static bool TryUnescape(string value, out string result)
    {
        result = (value ?? string.Empty)
            .Replace("%3D", "=")
            .Replace("%0D", "\r")
            .Replace("%0A", "\n")
            .Replace("%25", "%");
        return result.IndexOf('\r') < 0 && result.IndexOf('\n') < 0;
    }
}
