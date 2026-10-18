using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReconnectSingleFlightContractTests
{
    private static string PluginSource => File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..", "src", "Plugin.cs"));

    [Test]
    public void ManagedReconnect_UsesCanonicalLobbyRejoinInsteadOfDirectStartClient()
    {
        Assert.That(PluginSource, Does.Contain("using canonical saved-lobby cleanup/rejoin instead of calling StartClient directly"));
        Assert.That(PluginSource, Does.Contain("before first canonical saved-lobby cleanup/rejoin attempt"));
        Assert.That(PluginSource, Does.Not.Contain("before first StartClient call"));
        Assert.That(PluginSource, Does.Contain("HandleReconnectFailureFallback(\"transport disconnected; canonical lobby rejoin required\")"));
    }

    [Test]
    public void SavedLobbyFallback_SuppressesReentrantDisconnectHandling()
    {
        Assert.That(PluginSource, Does.Contain("_lastLobbyRejoinInProgress"));
        Assert.That(PluginSource, Does.Contain("saved-lobby recovery is already in progress; suppressing re-entrant disconnect handling"));
    }
}
