using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatCopySanitizerTests
{
    [Test]
    public void ToClipboardText_StripsRichTextTags_AndKeepsMessageOnly()
    {
        string copied = ChatCopySanitizer.ToClipboardText("<color=#FFD700>https://example.com</color> <b>please</b>");

        Assert.That(copied, Is.EqualTo("https://example.com please"));
    }

    [Test]
    public void ToClipboardText_DoesNotInventSenderOrTimestamp()
    {
        string copied = ChatCopySanitizer.ToClipboardText("  hello lobby  ");

        Assert.That(copied, Is.EqualTo("hello lobby"));
        Assert.That(copied, Does.Not.Contain(":"));
    }
}
