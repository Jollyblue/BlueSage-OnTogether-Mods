using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class DisplayNameMutationPolicyTests
{
    [Test]
    public void TryCompose_ValidatesTheFullyComposedName()
    {
        bool accepted = DisplayNameMutationPolicy.TryCompose(
            "<#0E6EED>Robin</color>",
            "<#F0C7D8>(Reading)</color>",
            "<#70FFBD>[4/5sp]</color>",
            100,
            out string displayName,
            out string error);

        Assert.That(accepted, Is.True);
        Assert.That(displayName, Is.EqualTo("<#0E6EED>Robin</color> <#F0C7D8>(Reading)</color> <#70FFBD>[4/5sp]</color>"));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void TryCompose_RejectsEmptyBaseWithoutInventingAName()
    {
        bool accepted = DisplayNameMutationPolicy.TryCompose(
            string.Empty,
            "<#F0C7D8>(AFK)</color>",
            string.Empty,
            100,
            out string displayName,
            out string error);

        Assert.That(accepted, Is.False);
        Assert.That(displayName, Is.Empty);
        Assert.That(error, Does.Contain("player name is not ready"));
    }

    [Test]
    public void TryCompose_RejectsSuffixOverflowBeforeMutation()
    {
        bool accepted = DisplayNameMutationPolicy.TryCompose(
            "Blue",
            "(This status makes the full rendered value too long)",
            "[5/5energy]",
            24,
            out string displayName,
            out string error);

        Assert.That(accepted, Is.False);
        Assert.That(displayName.Length, Is.GreaterThan(24));
        Assert.That(error, Does.Contain($"{displayName.Length}/24"));
    }

    [TestCase("<#0E6EED>Robin", "", "")]
    [TestCase("Robin", "<#F0C7D8>(AFK)", "")]
    [TestCase("Robin", "", "<#70FFBD>[4/5sp]")]
    public void TryCompose_RejectsMalformedRichTextBeforeMutation(string baseName, string status, string spoons)
    {
        bool accepted = DisplayNameMutationPolicy.TryCompose(
            baseName,
            status,
            spoons,
            3000,
            out _,
            out string error);

        Assert.That(accepted, Is.False);
        Assert.That(error, Does.Contain("rich text"));
    }
}
