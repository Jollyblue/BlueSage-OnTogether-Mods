using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ReconnectReplacementLobbyContractTests
{
    private static string PluginSource => File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..", "src", "Plugin.cs"));

    [Test]
    public void ReplacementDiscovery_IsBoundedAndRequiresUniqueCandidate()
    {
        Assert.That(PluginSource, Does.Contain("int[] pollDelays = { 5, 5, 10, 10, 15 }"));
        Assert.That(PluginSource, Does.Contain("if (candidates.Count == 1)"));
        Assert.That(PluginSource, Does.Contain("multiple same-host replacement candidates; refusing to guess"));
    }

    [Test]
    public void ReplacementCandidate_RequiresOwnerPublicDirectJoinVersionAndCapacity()
    {
        Assert.That(PluginSource, Does.Contain("_lastReconnectLobbyOwnerSteamId"));
        Assert.That(PluginSource, Does.Contain("GetLobbyOwner(lobbyId)"));
        Assert.That(PluginSource, Does.Contain("GetLobbyData(lobbyId, \"public\")"));
        Assert.That(PluginSource, Does.Contain("GetLobbyData(lobbyId, \"request\")"));
        Assert.That(PluginSource, Does.Contain("GetLobbyData(lobbyId, \"version\")"));
        Assert.That(PluginSource, Does.Contain("members >= limit - 1"));
    }

    [Test]
    public void BluesName_IsOnlyNormalizedSupportingFingerprint()
    {
        Assert.That(PluginSource, Does.Contain("Regex.Replace(lobbyName ?? string.Empty, \"<[^>]*>\""));
        Assert.That(PluginSource, Does.Contain("normalized.Contains(\"♪blues♪\") ? \"♪blues♪\" : normalized"));
        Assert.That(PluginSource, Does.Contain("string.Equals(owner, _lastReconnectLobbyOwnerSteamId"));
    }
}
