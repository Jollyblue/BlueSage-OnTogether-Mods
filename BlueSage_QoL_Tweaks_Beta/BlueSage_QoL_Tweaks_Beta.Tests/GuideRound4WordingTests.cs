using System.IO;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4b (Blue, 2026-10-20): the in-game guide and the config text are human surfaces too. They must say what
// 0.2.6 does (40-minute cleanup that avoids the autosave; kick, mute and the host journal for hosts) without
// growing into a manual: one short sentence per fact, same warm voice as before.
public sealed class GuideRound4WordingTests
{
    [Test]
    public void Guide_tells_the_truth_about_cleanup_and_the_autosave()
    {
        string body = Body("Keeping things smooth");
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("every 40 minutes"));
            Assert.That(body, Does.Contain("autosave"));
            Assert.That(body, Does.Not.Contain("15 minutes"));
            Assert.That(body.Length, Is.LessThan(360), "one short paragraph, not a manual");
        });
    }

    [Test]
    public void Guide_hosts_section_names_the_new_tools_in_one_breath()
    {
        string body = Body("Hosts & Helpers");
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("kick"));
            Assert.That(body, Does.Contain("mute"));
            Assert.That(body, Does.Contain("Host journal"));
            Assert.That(body, Does.Contain("reconnect").IgnoreCase);
            Assert.That(body.Length, Is.LessThan(520), "one short paragraph, not a manual");
        });
    }

    [Test]
    public void Config_description_no_longer_claims_a_15_minute_timer()
    {
        string plugin = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Not.Contain("coordinated 15-minute default timer"));
            Assert.That(plugin, Does.Contain("\"EnableAutoSweep\", publicDefaults.AutomaticCleanup, \"Run BlueSage Automatic Cleanup on its own timer (40 minutes by default"));
        });
    }

    private static string Body(string heading)
    {
        GuideSection section = GuideOverlayPolicy.BuildSections("0.2.6", "Home", "Insert", "End", "F8").Single(item => item.Heading == heading);
        return section.Body;
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
