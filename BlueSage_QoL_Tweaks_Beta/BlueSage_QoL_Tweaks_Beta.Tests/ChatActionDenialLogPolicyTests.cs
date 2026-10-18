using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatActionDenialLogPolicyTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Blank_reasons_use_a_privacy_safe_category(string? reason)
    {
        Assert.That(
            ChatActionDenialLogPolicy.CategorizeReason(reason!),
            Is.EqualTo("unspecified safety rejection"));
    }

    [TestCase("the exact sender tuple changed; reopen the current player panel", "sender evidence changed or disagreed")]
    [TestCase("this row has no exact player-sender binding", "sender binding unavailable")]
    [TestCase("the row has no bounded copy payload", "bounded copy payload unavailable")]
    [TestCase("this row has no enabled safe HTTP(S) link", "safe link unavailable")]
    [TestCase("use exactly one modifier", "invalid modifier combination")]
    public void Known_internal_reasons_map_to_bounded_categories(string reason, string expected)
    {
        Assert.That(ChatActionDenialLogPolicy.CategorizeReason(reason), Is.EqualTo(expected));
    }

    [TestCase("SteamID64 76561198000000000")]
    [TestCase("PurrNet PlayerID account-7 token=abc123")]
    [TestCase("Authorization: Bearer secret-token-value")]
    [TestCase("session row-abcdef123456")]
    [TestCase("unknown\r\naccount_token=top-secret")]
    public void Identifier_and_token_shaped_content_never_reaches_the_log_category(string reason)
    {
        string category = ChatActionDenialLogPolicy.CategorizeReason(reason);

        Assert.Multiple(() =>
        {
            Assert.That(category, Is.EqualTo("unclassified safety rejection"));
            Assert.That(category.Length, Is.LessThanOrEqualTo(180));
            Assert.That(category, Does.Not.Match(@"\d{17}"));
            Assert.That(category, Does.Not.Contain("SteamID"));
            Assert.That(category, Does.Not.Contain("PlayerID"));
            Assert.That(category, Does.Not.Contain("token"));
            Assert.That(category, Does.Not.Contain("secret"));
        });
    }

    [Test]
    public void Sensitive_suffixes_are_not_echoed_even_when_the_safe_category_is_recognized()
    {
        string category = ChatActionDenialLogPolicy.CategorizeReason(
            "the exact sender tuple changed; SteamID64 76561198000000000 token=top-secret");

        Assert.Multiple(() =>
        {
            Assert.That(category, Is.EqualTo("sender evidence changed or disagreed"));
            Assert.That(category, Does.Not.Match(@"\d{17}"));
            Assert.That(category, Does.Not.Contain("token"));
            Assert.That(category, Does.Not.Contain("secret"));
        });
    }
}
