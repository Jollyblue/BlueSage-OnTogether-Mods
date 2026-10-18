using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WelcomeMessageContractTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Test]
    public void WelcomeMessage_RetriesUntilLocalNotificationActuallyPosts()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string policy = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Core", "VoiceWelcomePolicy.cs"));

        Assert.That(plugin, Does.Contain("internal static bool AddLocalNotification"));
        Assert.That(plugin, Does.Contain("_welcomeMessageShownForLobby = ShowWelcomeMessageForLobby();"));
        Assert.That(plugin, Does.Contain("private bool ShowWelcomeMessageForLobby()"));
        Assert.That(plugin, Does.Contain("_welcomePrimaryShownForLobby"));
        Assert.That(plugin, Does.Contain("_welcomeContextShownForLobby"));
        Assert.That(plugin, Does.Contain("return firstShown && secondShown;"));
        Assert.That(plugin, Does.Contain("BuildVoiceWelcomeGuidance"));
        Assert.That(plugin, Does.Contain("SettingsData.PushToTalk").Or.Contain("settings.PushToTalk"));
        Assert.That(policy, Does.Contain("Voice Activation is enabled"));
        Assert.That(plugin, Does.Contain("Pomodoro/focus timer"));
        Assert.That(plugin, Does.Contain("can earn tickets and XP for focus time"));
        Assert.That(plugin, Does.Not.Contain("completed sessions can earn tickets"));
        Assert.That(plugin, Does.Contain("No microphone device name was logged"));
        Assert.That(plugin, Does.Contain("_welcomeVoiceStateLogKey"));
        Assert.That(plugin, Does.Contain("if (!string.Equals(_welcomeVoiceStateLogKey, stateLogKey"));
        Assert.That(plugin, Does.Contain("_welcomeVoiceStateLogKey = string.Empty;"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Project root not found.");
    }
}
