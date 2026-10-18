using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class InputFieldLimitPolicyTests
{
    [TestCase("InputField_Name")]
    [TestCase("InputField_DateOfBirth")]
    [TestCase("InputField_HoroscopeSign")]
    [TestCase("InputField_Location")]
    [TestCase("InputField_AreaOfStudy")]
    [TestCase("InputField_FavoriteQuote")]
    [TestCase("FavoriteQuoteInputField")]
    public void GetLimit_ReturnsIdCardLimit_ForKnownProfileFields(string fieldName)
    {
        int? limit = InputFieldLimitPolicy.GetLimit(
            fieldName,
            maxIdCardCharacters: 1500,
            maxSessionNameCharacters: 1500,
            maxChatCharacters: 1500);

        Assert.That(limit, Is.EqualTo(1500));
    }

    [TestCase("InputField_SesionName")]
    [TestCase("InputField_SessionName")]
    public void GetLimit_ReturnsSessionLimit_ForSessionNameFields_Always(string fieldName)
    {
        int? limit = InputFieldLimitPolicy.GetLimit(
            fieldName,
            maxIdCardCharacters: 1500,
            maxSessionNameCharacters: 1500,
            maxChatCharacters: 1500);

        Assert.That(limit, Is.EqualTo(1500));
    }

    [TestCase("InputField_Chat")]
    [TestCase("InputField_Message")]
    [TestCase("InputField_ChatMessage")]
    [TestCase("ChatInputField")]
    [TestCase("MessageInputField")]
    public void GetLimit_ReturnsChatLimit_ForLikelyChatInputFields_Always(string fieldName)
    {
        int? limit = InputFieldLimitPolicy.GetLimit(
            fieldName,
            maxIdCardCharacters: 1500,
            maxSessionNameCharacters: 1500,
            maxChatCharacters: 1500);

        Assert.That(limit, Is.EqualTo(1500));
    }

    [Test]
    public void GetLimit_ClampsConfiguredLimits_ToReasonableRange()
    {
        int? profileLimit = InputFieldLimitPolicy.GetLimit("InputField_Name", maxIdCardCharacters: 5000, maxSessionNameCharacters: 5000, maxChatCharacters: 5000);
        int? sessionLimit = InputFieldLimitPolicy.GetLimit("InputField_SessionName", maxIdCardCharacters: 5000, maxSessionNameCharacters: 5000, maxChatCharacters: 5000);
        int? chatLimit = InputFieldLimitPolicy.GetLimit("InputField_Chat", maxIdCardCharacters: 5000, maxSessionNameCharacters: 5000, maxChatCharacters: 5000);

        Assert.That(profileLimit, Is.EqualTo(4000));
        Assert.That(sessionLimit, Is.EqualTo(4000));
        Assert.That(chatLimit, Is.EqualTo(4000));
    }

    [TestCase(-10, 1)]
    [TestCase(0, 1)]
    [TestCase(250, 250)]
    [TestCase(500, 500)]
    [TestCase(750, 750)]
    [TestCase(1000, 1000)]
    [TestCase(1500, 1500)]
    [TestCase(2500, 2500)]
    [TestCase(5000, 4000)]
    public void ClampChatCharacters_UsesSameBounds_ForInputAndSendCaps(int configured, int expected)
    {
        Assert.That(InputFieldLimitPolicy.ClampChatCharacters(configured), Is.EqualTo(expected));
    }

    [Test]
    public void DefaultMaxChatCharacters_LeavesRoomForRichTextTags()
    {
        // 2026-10-17 (Blue): chat defaults to the maximum; the rich-text room is now a warning threshold.
        Assert.That(InputFieldLimitPolicy.DefaultMaxChatCharacters, Is.EqualTo(4000));
        Assert.That(InputFieldLimitPolicy.ChatWrapWarningCharacters, Is.EqualTo(3000));
        Assert.That(InputFieldLimitPolicy.DefaultMaxProfileCharacters, Is.EqualTo(4000));
        Assert.That(InputFieldLimitPolicy.DefaultMaxSessionNameCharacters, Is.EqualTo(4000));
        Assert.That(InputFieldLimitPolicy.AbsoluteMaxChatCharacters, Is.EqualTo(4000));
    }

    [Test]
    public void ResolveCreateSessionNameLimit_ReplacesNativeSixtyCharacterCap()
    {
        int resolved = InputFieldLimitPolicy.ResolveCreateSessionNameLimit(
            currentCharacterLimit: 60,
            configuredSessionNameCharacters: 3000);

        Assert.That(resolved, Is.EqualTo(3000));
    }
}
