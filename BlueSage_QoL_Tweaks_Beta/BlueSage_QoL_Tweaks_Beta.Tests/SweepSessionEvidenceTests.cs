using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SweepSessionEvidenceTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Formatter_reports_process_roster_and_voice_context_without_identity_data()
    {
        SweepSessionSnapshot before = new(
            captured: true,
            status: "captured",
            processWorkingSetBytes: 8L * 1024 * 1024 * 1024,
            processPrivateBytes: 12L * 1024 * 1024 * 1024,
            processHandleCount: 2500,
            processThreadCount: 150,
            nativeRows: 17,
            steamMembers: 12,
            voiceManagerPresent: true,
            voiceLobbyOff: false,
            voiceJoined: true,
            queuedVoiceParticipants: 3);
        SweepSessionSnapshot after = new(
            captured: true,
            status: "captured",
            processWorkingSetBytes: 4L * 1024 * 1024 * 1024,
            processPrivateBytes: 6L * 1024 * 1024 * 1024,
            processHandleCount: 2505,
            processThreadCount: 151,
            nativeRows: 17,
            steamMembers: 12,
            voiceManagerPresent: true,
            voiceLobbyOff: false,
            voiceJoined: true,
            queuedVoiceParticipants: 3);

        string result = SweepSessionEvidenceFormatter.Format(before, after);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("processPrivateBeforeMiB=12288.0"));
            Assert.That(result, Does.Contain("processPrivateAfterMiB=6144.0"));
            Assert.That(result, Does.Contain("processPrivateDeltaMiB=-6144.0"));
            Assert.That(result, Does.Contain("workingSetDeltaMiB=-4096.0"));
            Assert.That(result, Does.Contain("handles=2500->2505"));
            Assert.That(result, Does.Contain("threads=150->151"));
            Assert.That(result, Does.Contain("nativeRows=17"));
            Assert.That(result, Does.Contain("steamMembers=12"));
            Assert.That(result, Does.Contain("nativeSteamDelta=5"));
            Assert.That(result, Does.Contain("voiceManager=true"));
            Assert.That(result, Does.Contain("voiceLobbyOff=false"));
            Assert.That(result, Does.Contain("voiceJoined=true"));
            Assert.That(result, Does.Contain("voiceQueued=3"));
            Assert.That(result, Does.Not.Contain("7656119"));
        });
    }

    [Test]
    public void Formatter_degrades_to_one_bounded_status_when_capture_is_unavailable()
    {
        SweepSessionSnapshot unavailable = SweepSessionSnapshot.Unavailable("InvalidOperationException");

        string result = SweepSessionEvidenceFormatter.Format(unavailable, unavailable);

        Assert.That(result, Is.EqualTo("sessionEvidence=unavailable:InvalidOperationException"));
        Assert.That(result.Length, Is.LessThan(128));
    }

    [Test]
    public void Sweep_wires_one_before_after_session_capture_without_identity_payloads()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string capture = File.ReadAllText(Path.Combine(Root, "src", "SweepSessionEvidenceCapture.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("SweepSessionSnapshot sessionBefore = SweepSessionEvidenceCapture.Capture();"));
            Assert.That(plugin, Does.Contain("SweepSessionSnapshot sessionAfter = SweepSessionEvidenceCapture.Capture();"));
            Assert.That(plugin, Does.Contain("SweepSessionEvidenceFormatter.Format(sessionBefore, sessionAfter)"));
            Assert.That(capture, Does.Contain("Process.GetCurrentProcess()"));
            Assert.That(capture, Does.Contain("nativeRows: nativeRows"));
            Assert.That(capture, Does.Contain("steamMembers: steamMembers"));
            Assert.That(capture, Does.Not.Contain("GetFriendPersonaName"));
            Assert.That(capture, Does.Not.Contain("GetLobbyData"));
            Assert.That(capture, Does.Not.Contain("PlayerNames"));
        });
    }
}
