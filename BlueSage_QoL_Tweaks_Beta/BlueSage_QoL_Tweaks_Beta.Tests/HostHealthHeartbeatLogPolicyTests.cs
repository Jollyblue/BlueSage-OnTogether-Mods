using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 F: the 5-minute heartbeat line is written on change only, and the lobby count is not a change.
[TestFixture]
internal sealed class HostHealthHeartbeatLogPolicyTests
{
    private const string Loaded24 = "PlayerLimit=loaded lobby=24/111 pllMax=128 default=16 shift=1 chatRelay=on";
    private const string Loaded57 = "PlayerLimit=loaded lobby=57/111 pllMax=128 default=16 shift=1 chatRelay=on";

    [Test]
    public void Lobby_count_changes_do_not_change_the_key()
    {
        string a = HostHealthHeartbeatLogPolicy.BuildKey(true, 6, Loaded24);
        string b = HostHealthHeartbeatLogPolicy.BuildKey(true, 6, Loaded57);
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a, Does.Not.Contain("lobby="));
    }

    [Test]
    public void Fifty_count_changes_log_once()
    {
        string last = string.Empty;
        int logged = 0;
        for (int i = 0; i < 50; i++)
        {
            string key = HostHealthHeartbeatLogPolicy.BuildKey(true, 6, "PlayerLimit=loaded lobby=" + (10 + i) + "/111 pllMax=128 default=16 shift=1 chatRelay=on");
            if (HostHealthHeartbeatLogPolicy.ShouldLog(last, key))
            {
                logged++;
                last = key;
            }
        }

        Assert.That(logged, Is.EqualTo(1));
    }

    [Test]
    public void Real_changes_do_log()
    {
        string a = HostHealthHeartbeatLogPolicy.BuildKey(true, 6, Loaded24);
        Assert.Multiple(() =>
        {
            Assert.That(HostHealthHeartbeatLogPolicy.ShouldLog(a, HostHealthHeartbeatLogPolicy.BuildKey(false, 6, Loaded24)), Is.True, "heartbeat compat");
            Assert.That(HostHealthHeartbeatLogPolicy.ShouldLog(a, HostHealthHeartbeatLogPolicy.BuildKey(true, 5, Loaded24)), Is.True, "preserved keys");
            Assert.That(HostHealthHeartbeatLogPolicy.ShouldLog(a, HostHealthHeartbeatLogPolicy.BuildKey(true, 6, "PlayerLimit=loaded lobby=24/111 pllMax=64 default=16 shift=1 chatRelay=on")), Is.True, "pll max");
            Assert.That(HostHealthHeartbeatLogPolicy.ShouldLog(a, HostHealthHeartbeatLogPolicy.BuildKey(true, 6, "PlayerLimit=not loaded")), Is.True, "not loaded");
            Assert.That(HostHealthHeartbeatLogPolicy.ShouldLog(a, HostHealthHeartbeatLogPolicy.BuildKey(true, 6, Loaded24)), Is.False, "identical");
        });
    }
}
