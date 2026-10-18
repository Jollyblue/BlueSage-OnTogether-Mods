using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class VoiceWelcomePolicyTests
{
    [TestCase(false, true, false, "Voice chat is Off")]
    [TestCase(true, false, true, "Lobby voice is Off")]
    [TestCase(true, true, true, "Push to Talk is enabled")]
    [TestCase(true, true, false, "Voice Activation is enabled")]
    public void Guidance_reports_the_selected_game_setting(bool voiceChat, bool lobbyVoice, bool pushToTalk, string expected)
    {
        string result = VoiceWelcomePolicy.Build(voiceChat, lobbyVoice, pushToTalk);
        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain(expected));
            Assert.That(result, Does.Not.Contain("microphone device"));
        });
    }

    [Test]
    public void Missing_runtime_state_fails_helpfully_without_guessing()
    {
        string result = VoiceWelcomePolicy.Build(null, null, null);
        Assert.That(result, Does.Contain("still loading"));
        Assert.That(result, Does.Contain("Push to Talk vs Voice Activation"));
    }
}
