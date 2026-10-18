using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatUrlDetectorTests
{
    [Test]
    public void TryGetFirstUrl_UsesSanitizedChatText_AndTrimsSentencePunctuation()
    {
        bool found = ChatUrlDetector.TryGetFirstUrl("<color=#FFD700>https://example.com/path</color>, please", out string url);

        Assert.That(found, Is.True);
        Assert.That(url, Is.EqualTo("https://example.com/path"));
    }

    [Test]
    public void TryGetFirstUrl_NormalizesWwwLinksToHttps()
    {
        bool found = ChatUrlDetector.TryGetFirstUrl("check www.example.org/cute", out string url);

        Assert.That(found, Is.True);
        Assert.That(url, Is.EqualTo("https://www.example.org/cute"));
    }

    [Test]
    public void TryGetFirstUrl_RejectsNonWebSchemes()
    {
        bool found = ChatUrlDetector.TryGetFirstUrl("file:///C:/Users/Blue/secret.txt", out string url);

        Assert.That(found, Is.False);
        Assert.That(url, Is.Empty);
    }
}
