using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatLinkFormatterTests
{
    [Test]
    public void TryColorizeUrls_HighlightsHttpHttpsAndWwwLinks()
    {
        Assert.That(ChatLinkFormatter.TryColorizeUrls("see https://example.com/path and www.example.org", out string formatted), Is.True);

        Assert.That(formatted, Does.Contain("<color=#70D6FF><u>https://example.com/path</u></color>"));
        Assert.That(formatted, Does.Contain("<color=#70D6FF><u>www.example.org</u></color>"));
    }

    [Test]
    public void TryColorizeUrls_LeavesTrailingSentencePunctuationOutsideLink()
    {
        Assert.That(ChatLinkFormatter.TryColorizeUrls("look https://example.com/test).", out string formatted), Is.True);

        Assert.That(formatted, Does.Contain("<u>https://example.com/test</u></color>)."));
    }

    [Test]
    public void TryColorizeUrls_DoesNotDoubleWrapAlreadyFormattedLinks()
    {
        string alreadyFormatted = "<color=#70D6FF><u>https://example.com</u></color>";

        Assert.That(ChatLinkFormatter.TryColorizeUrls(alreadyFormatted, out string formatted), Is.False);
        Assert.That(formatted, Is.EqualTo(alreadyFormatted));
    }
}
