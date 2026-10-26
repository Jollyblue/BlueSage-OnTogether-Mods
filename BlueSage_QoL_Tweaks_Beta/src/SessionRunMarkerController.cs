using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class SessionRunMarkerController : MonoBehaviour
{
    private static readonly object Sync = new object();
    private static SessionRunMarkerController _instance;

    private string _path = string.Empty;
    private SessionRunMarker _current;
    private float _nextHeartbeatAt;

    private void Awake()
    {
        _instance = this;
        // Round 5d: BlueSageQoLModSuite/trackers/session-run-marker.v1 (the old run-marker.v1 moves there once).
        _path = ModSuitePaths.Current.RunMarkerPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_path));

        SessionRunClassification classification = ReadAndClassifyPrior();
        SessionAuditController.RecordRunMarker(
            classification.ToString().ToLowerInvariant(),
            AgeBucket(ReadPriorHeartbeatUtcTicks()),
            _path);

        Process process = Process.GetCurrentProcess();
        long startedUtcTicks = SafeProcessStartUtcTicks(process);
        long nowUtcTicks = DateTime.UtcNow.Ticks;
        _current = SessionRunMarkerPolicy.CreateRunning(
            Plugin.PluginBuildIdentity,
            process.Id,
            startedUtcTicks > 0L ? startedUtcTicks : nowUtcTicks,
            nowUtcTicks,
            "runtime");
        AtomicWrite(_path, SessionRunMarkerPolicy.Serialize(_current));
        _nextHeartbeatAt = Time.realtimeSinceStartup + SessionRunMarkerPolicy.HeartbeatIntervalSeconds;
    }

    private void Update()
    {
        if (PluginShutdownController.IsShuttingDown ||
            _current == null ||
            Time.realtimeSinceStartup < _nextHeartbeatAt)
        {
            return;
        }

        _nextHeartbeatAt = Time.realtimeSinceStartup + SessionRunMarkerPolicy.HeartbeatIntervalSeconds;
        _current = SessionRunMarkerPolicy.WithHeartbeat(_current, DateTime.UtcNow.Ticks, "runtime");
        AtomicWrite(_path, SessionRunMarkerPolicy.Serialize(_current));
    }

    internal static void MarkCleanClose()
    {
        lock (Sync)
        {
            if (_instance == null || _instance._current == null || string.IsNullOrEmpty(_instance._path))
            {
                return;
            }

            _instance._current = SessionRunMarkerPolicy.CreateCleanClose(
                _instance._current,
                DateTime.UtcNow.Ticks);
            AtomicWrite(_instance._path, SessionRunMarkerPolicy.Serialize(_instance._current));
        }
    }

    private SessionRunClassification ReadAndClassifyPrior()
    {
        if (!TryReadMarker(out SessionRunMarker prior))
        {
            return File.Exists(_path)
                ? SessionRunClassification.Uncertain
                : SessionRunClassification.None;
        }

        bool processExists = false;
        bool processStartMatches = false;
        try
        {
            using (Process process = Process.GetProcessById(prior.ProcessId))
            {
                processExists = !process.HasExited;
                processStartMatches =
                    SafeProcessStartUtcTicks(process) == prior.StartedUtcTicks;
            }
        }
        catch
        {
            processExists = false;
            processStartMatches = false;
        }

        return SessionRunMarkerPolicy.ClassifyPrior(
            prior,
            DateTime.UtcNow.Ticks,
            processExists,
            processStartMatches);
    }

    private long ReadPriorHeartbeatUtcTicks() =>
        TryReadMarker(out SessionRunMarker prior) ? prior.HeartbeatUtcTicks : 0L;

    private bool TryReadMarker(out SessionRunMarker marker)
    {
        marker = null;
        try
        {
            return File.Exists(_path) &&
                   SessionRunMarkerPolicy.TryParse(File.ReadAllText(_path), out marker);
        }
        catch
        {
            return false;
        }
    }

    private static string AgeBucket(long heartbeatUtcTicks)
    {
        if (heartbeatUtcTicks <= 0L) return "unknown";
        long seconds = Math.Max(0L, (DateTime.UtcNow.Ticks - heartbeatUtcTicks) / TimeSpan.TicksPerSecond);
        if (seconds <= 15L) return "0-15s";
        if (seconds <= 60L) return "16-60s";
        if (seconds <= 300L) return "1-5m";
        return "over-5m";
    }

    private static long SafeProcessStartUtcTicks(Process process)
    {
        try { return process.StartTime.ToUniversalTime().Ticks; }
        catch { return 0L; }
    }

    private static void AtomicWrite(string path, string content)
    {
        string temporary = path + ".tmp";
        string backup = path + ".bak";
        File.WriteAllText(temporary, content);
        if (File.Exists(path))
        {
            try
            {
                File.Replace(temporary, path, backup, true);
                TryDelete(backup);
                return;
            }
            catch
            {
                TryDelete(temporary);
                return;
            }
        }
        File.Move(temporary, path);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
