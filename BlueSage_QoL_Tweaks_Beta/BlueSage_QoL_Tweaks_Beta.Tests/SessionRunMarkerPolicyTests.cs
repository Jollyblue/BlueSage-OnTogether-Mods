using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SessionRunMarkerPolicyTests
{
    private static readonly long Now = new DateTime(2026, 7, 20, 3, 50, 0, DateTimeKind.Utc).Ticks;

    [Test]
    public void Marker_round_trip_contains_only_bounded_process_and_runtime_state()
    {
        SessionRunMarker marker = SessionRunMarkerPolicy.CreateRunning(
            "0.2.3+20260722.4-public-performance-overlay",
            4120,
            Now - TimeSpan.FromSeconds(5).Ticks,
            Now,
            "startup");

        string serialized = SessionRunMarkerPolicy.Serialize(marker);
        Assert.That(SessionRunMarkerPolicy.TryParse(serialized, out SessionRunMarker parsed), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(parsed.Version, Is.EqualTo(marker.Version));
            Assert.That(parsed.ProcessId, Is.EqualTo(4120));
            Assert.That(parsed.State, Is.EqualTo(SessionRunState.Running));
            Assert.That(serialized, Does.Not.Contain("steam"));
            Assert.That(serialized, Does.Not.Contain("lobby"));
            Assert.That(serialized, Does.Not.Contain("player"));
            Assert.That(serialized, Does.Not.Contain("chat"));
        });
    }

    [Test]
    public void Clean_close_is_not_classified_as_abnormal()
    {
        SessionRunMarker clean = SessionRunMarkerPolicy.CreateCleanClose(
            SessionRunMarkerPolicy.CreateRunning("candidate", 11, Now - 100, Now - 10, "running"),
            Now);

        SessionRunClassification result = SessionRunMarkerPolicy.ClassifyPrior(
            clean,
            Now + TimeSpan.FromSeconds(2).Ticks,
            processExists: false,
            processStartMatches: false);

        Assert.That(result, Is.EqualTo(SessionRunClassification.Clean));
    }

    [Test]
    public void Live_matching_process_is_concurrent_even_when_heartbeat_is_stale()
    {
        SessionRunMarker running = SessionRunMarkerPolicy.CreateRunning(
            "candidate",
            71,
            Now - TimeSpan.FromMinutes(5).Ticks,
            Now - TimeSpan.FromMinutes(2).Ticks,
            "running");

        Assert.That(
            SessionRunMarkerPolicy.ClassifyPrior(running, Now, processExists: true, processStartMatches: true),
            Is.EqualTo(SessionRunClassification.Concurrent));
    }

    [Test]
    public void Dead_recent_process_waits_for_staleness_before_abnormal_classification()
    {
        SessionRunMarker running = SessionRunMarkerPolicy.CreateRunning(
            "candidate",
            72,
            Now - TimeSpan.FromSeconds(8).Ticks,
            Now - TimeSpan.FromSeconds(2).Ticks,
            "running");

        Assert.That(
            SessionRunMarkerPolicy.ClassifyPrior(running, Now, processExists: false, processStartMatches: false),
            Is.EqualTo(SessionRunClassification.Uncertain));
    }

    [Test]
    public void Dead_stale_or_reused_pid_is_abnormal()
    {
        SessionRunMarker stale = SessionRunMarkerPolicy.CreateRunning(
            "candidate",
            73,
            Now - TimeSpan.FromMinutes(2).Ticks,
            Now - TimeSpan.FromSeconds(20).Ticks,
            "running");

        Assert.Multiple(() =>
        {
            Assert.That(
                SessionRunMarkerPolicy.ClassifyPrior(stale, Now, processExists: false, processStartMatches: false),
                Is.EqualTo(SessionRunClassification.Abnormal));
            Assert.That(
                SessionRunMarkerPolicy.ClassifyPrior(stale, Now, processExists: true, processStartMatches: false),
                Is.EqualTo(SessionRunClassification.Abnormal));
        });
    }

    [Test]
    public void Malformed_or_identity_bearing_markers_fail_closed()
    {
        string extra = "schema=1\nstate=running\nversion=x\npid=1\nstartedUtcTicks=1\nheartbeatUtcTicks=1\nclosedUtcTicks=0\ncontext=ok\nsteamId=7656119\n";

        Assert.That(SessionRunMarkerPolicy.TryParse(extra, out _), Is.False);
    }
}
