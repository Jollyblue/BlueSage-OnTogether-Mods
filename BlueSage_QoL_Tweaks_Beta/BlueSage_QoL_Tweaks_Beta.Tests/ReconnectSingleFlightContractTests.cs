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
        // Round 5d: the unreachable managed retry loop and last-lobby fallback wait (5b review) are deleted; every
        // reconnect generation goes through StartCleanMenuReconnect, the canonical saved-lobby cleanup/rejoin.
        Assert.That(PluginSource, Does.Not.Contain("StartManagedReconnectLoop"));
        Assert.That(PluginSource, Does.Not.Contain("LastLobbyRejoinFallbackWait"));
        Assert.That(PluginSource, Does.Contain("StartCleanMenuReconnect("));
        Assert.That(PluginSource, Does.Not.Contain("before first StartClient call"));
    }

    [Test]
    public void SavedLobbyFallback_SuppressesReentrantDisconnectHandling()
    {
        Assert.That(PluginSource, Does.Contain("_lastLobbyRejoinInProgress"));
        Assert.That(PluginSource, Does.Contain("saved-lobby recovery is already in progress; suppressing re-entrant disconnect handling"));
    }
}
