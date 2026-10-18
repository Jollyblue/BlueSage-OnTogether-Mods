using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CloneIncidentMessagePolicyTests
{
    [Test]
    public void Parses_existing_human_alert_without_needing_cloneable_names()
    {
        string text = "[Clone Shield] TestVictim (ID ...000001) was potentially cloned by TestSuspect (ID ...000002) (avatar/outfit and exact styled name). Evidence IDs: victim ...000001; suspect ...000002; match avatar/outfit and exact styled name. Be aware of the player list, Steam ID, and profile; verify identity in case this alert is incorrect.";

        Assert.That(CloneIncidentMessagePolicy.TryParseAlert(text, out CloneIncidentNotice notice), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(notice.VictimSuffix, Is.EqualTo("000001"));
            Assert.That(notice.SuspectSuffix, Is.EqualTo("000002"));
            Assert.That(notice.MatchType, Is.EqualTo("avatar/outfit and exact styled name"));
        });
    }

    [Test]
    public void Private_resolution_request_and_owner_metadata_round_trip_without_chat_text()
    {
        string victim = "76561198000000001";
        string suspect = "76561198000000002";
        string stable = CloneIncidentLedger.BuildStableId(victim, suspect);
        string text = CloneIncidentResolutionProtocolPolicy.BuildRequest(victim, suspect, stable, 100, "abc12345");
        Assert.That(CloneIncidentResolutionProtocolPolicy.TryParseRequest(text, out CloneIncidentResolutionRequest request), Is.True);
        string metadata = CloneIncidentResolutionProtocolPolicy.BuildLobbyValue("76561198000000003", request);
        Assert.That(CloneIncidentResolutionProtocolPolicy.TryParseLobbyValue(metadata, "76561198000000003", 120, out CloneIncidentResolutionRequest received), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(received.StableId, Is.EqualTo(stable));
            Assert.That(received.VictimSteamId, Is.EqualTo(victim));
            Assert.That(received.SuspectSteamId, Is.EqualTo(suspect));
            Assert.That(text, Does.StartWith("!bsincident:v1:resolve:"));
            Assert.That(text, Does.Not.Contain("[Clone Shield] Incident"));
        });
    }

    [TestCase("[Clone Shield] fake")]
    [TestCase("[Clone Shield] A (ID ...000001) was potentially cloned by B (ID ...000002) (name).")]
    [TestCase("[Clone Shield] Incident I1234 was marked resolved by a verified host/helper (victim ID ...000001, suspect ID ...000002).")]
    public void Rejects_partial_or_ambiguous_messages(string text)
    {
        Assert.That(CloneIncidentMessagePolicy.TryParseAlert(text, out _), Is.False);
        Assert.That(CloneIncidentResolutionProtocolPolicy.TryParseRequest(text, out _), Is.False);
    }
}
