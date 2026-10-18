using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class LargeLobbyRefreshPolicyTests
{
    [Test]
    public void Large_lobby_display_refreshes_are_bounded_without_changing_exact_action_truth()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LargeLobbyRefreshPolicy.BadgePollIntervalSeconds, Is.EqualTo(2f));
            Assert.That(LargeLobbyRefreshPolicy.RosterSnapshotLifetime, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(LargeLobbyRefreshPolicy.BadgePollIntervalSeconds, Is.LessThanOrEqualTo(2f));
            Assert.That(LargeLobbyRefreshPolicy.RosterSnapshotLifetime, Is.LessThanOrEqualTo(TimeSpan.FromSeconds(2)));
        });
    }

    [Test]
    public void Periodic_display_cache_does_not_replace_fresh_exact_action_truth()
    {
        string root = FindProjectRoot();
        string badgeController = File.ReadAllText(Path.Combine(root, "src", "Patches", "PlayerBadgeController.cs"));
        string identityController = File.ReadAllText(Path.Combine(root, "src", "PlayerIdentityEvidenceController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(badgeController, Does.Contain("LargeLobbyRefreshPolicy.BadgePollIntervalSeconds"));
            Assert.That(identityController, Does.Contain("LargeLobbyRefreshPolicy.RosterSnapshotLifetime.Ticks"));
            Assert.That(identityController, Does.Contain("internal static bool TryResolveExact(string steamId"));
            Assert.That(identityController, Does.Contain("RosterSnapshot snapshot = CaptureRosterSnapshot(cacheAllowed: false);"));
        });
    }

    [Test]
    public void Busy_lobbies_back_off_badge_presentation_without_relaxing_exact_action_truth()
    {
        System.Reflection.MethodInfo? intervalForRoster =
            typeof(LargeLobbyRefreshPolicy).GetMethod(
                "BadgePollIntervalSecondsForRoster",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic);

        Assert.That(
            intervalForRoster,
            Is.Not.Null,
            "Badge presentation needs a roster-aware cadence so large lobbies do not redo every visible row every two seconds.");
        if (intervalForRoster == null)
        {
            return;
        }

        float GetInterval(int activeRows) =>
            (float)intervalForRoster.Invoke(null, new object[] { activeRows })!;

        string root = FindProjectRoot();
        string badgeController = File.ReadAllText(Path.Combine(root, "src", "Patches", "PlayerBadgeController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(GetInterval(1), Is.EqualTo(2f));
            Assert.That(GetInterval(23), Is.EqualTo(2f));
            Assert.That(GetInterval(24), Is.EqualTo(4f));
            Assert.That(GetInterval(47), Is.EqualTo(4f));
            Assert.That(GetInterval(48), Is.EqualTo(6f));
            Assert.That(GetInterval(64), Is.EqualTo(6f));
            Assert.That(LargeLobbyRefreshPolicy.RosterSnapshotLifetime, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(badgeController, Does.Contain("LargeLobbyRefreshPolicy.BadgePollIntervalSecondsForRoster"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
