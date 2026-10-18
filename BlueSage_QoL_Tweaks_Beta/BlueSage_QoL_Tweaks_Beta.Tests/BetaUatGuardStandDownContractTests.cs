using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class BetaUatGuardStandDownContractTests
{
    [Test]
    public void Roster_and_network_guards_have_one_fail_open_uat_switch()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string network = File.ReadAllText(Path.Combine(root, "src", "Patches", "NetworkReflectionNullGuardPatch.cs"));
        string roster = File.ReadAllText(Path.Combine(root, "src", "Patches", "RosterRegistrationGuardPatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("EnableRosterAndNetworkGuards"));
            Assert.That(plugin, Does.Contain("Config.Bind(\"Compatibility Safety\", \"EnableRosterAndNetworkGuards\", true"));
            Assert.That(network, Does.Contain("Plugin.EnableRosterAndNetworkGuards?.Value != true"));
            Assert.That(network, Does.Contain("return true;"));
            Assert.That(roster, Does.Contain("Plugin.EnableRosterAndNetworkGuards?.Value != true"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage QoL project root.");
    }
}
