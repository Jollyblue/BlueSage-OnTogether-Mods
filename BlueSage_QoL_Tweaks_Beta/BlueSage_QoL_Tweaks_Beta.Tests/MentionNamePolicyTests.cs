using System.Text.RegularExpressions;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class MentionNamePolicyTests
{
    private const string DecoratedRosterName = "</color><color=#EA34A7>.</color><color=#D643AF> </color><color=#C151B6>·</color><color=#AD60BE> </color><color=#986EC6>.</color><color=#847DCD> </color><color=#6F8BD5>˚</color><color=#5B9ADD> </color><color=#47A8E4>w</color><color=#32B7EC>r</color><color=#26C1EB>e</color><color=#22C8E1>n</color><color=#1DCFD7> </color><color=#19D6CD>˚</color><color=#15DDC3> </color><color=#11E4B9>.</color><color=#0DEAAF> </color><color=#08F1A5>·</color><color=#04F89B> </color><color=#00FF91>.</color><color=#FBF5A7><color=#FFFFFF>";

    [TestCase("Robin [Tired] [4/5sp]", "Robin")]
    [TestCase("<#5AC8FA>Robin</color> [Tired] [4/5sp]", "Robin")]
    [TestCase("Wisteria", "Wisteria")]
    [TestCase("Blue (Homelab)", "Blue")]
    [TestCase("Blue (Homelab) [3/5sp]", "Blue")]
    public void ToMentionDisplayName_StripsTrailingStatusAndSpoons(string value, string expected)
    {
        Assert.That(MentionNamePolicy.ToMentionDisplayName(value), Is.EqualTo(expected));
    }

    [TestCase("Robin [Knitting?] [4/5sp]", "Robin [Knitting?] [4/5sp]")]
    [TestCase("<#5AC8FA>Blue</color> (Homelab) [3/5sp]", "Blue (Homelab) [3/5sp]")]
    public void ToMentionDisplayLabel_PreservesStatusAndSpoonsForPingRendering(string value, string expected)
    {
        Assert.That(MentionNamePolicy.ToMentionDisplayLabel(value), Is.EqualTo(expected));
    }

    [TestCase("@Blue (Homelab) [3/5sp] IDK", "@Blue (Homelab) [3/5sp]", "Blue")]
    [TestCase("@Robin [Knitting?] [4/5sp] DOES IT WORK", "@Robin [Knitting?] [4/5sp]", "Robin")]
    public void MentionReferenceRegex_ConsumesTypedStatusAndSpoonsSuffix(string text, string expectedValue, string expectedToken)
    {
        Match match = MentionNamePolicy.MentionReferenceRegex.Match(text);

        Assert.That(match.Success, Is.True);
        Assert.That(match.Value, Is.EqualTo(expectedValue));
        Assert.That(match.Groups[1].Value, Is.EqualTo(expectedToken));
    }

    [Test]
    public void IsLocalMentionMatch_UsesBaseNameInsteadOfDuplicatingStatus()
    {
        Assert.That(MentionNamePolicy.IsLocalMentionMatch("Robin [Tired] [4/5sp]", "Robin", "Row"), Is.True);
        Assert.That(MentionNamePolicy.IsLocalMentionMatch("Blue (Homelab)", "Blue", "Blu"), Is.True);
    }

    [Test]
    public void ToCanonicalMentionName_StripsDecoratedTmpNameAndSpoons()
    {
        Assert.That(MentionNamePolicy.ToCanonicalMentionName(DecoratedRosterName), Is.EqualTo("wren"));
        Assert.That(MentionNamePolicy.ToCanonicalMentionName(DecoratedRosterName + " <#70FFBD>[5/5sp]</color>"), Is.EqualTo("wren"));
    }

    [Test]
    public void MentionInsertionToken_ConvertsDecoratedNameIntoAResolvablePingReference()
    {
        string rosterName = DecoratedRosterName + " <#70FFBD>[5/5sp]</color>";
        string insertionToken = MentionNamePolicy.ToMentionInsertionToken(rosterName);
        Match parsed = MentionNamePolicy.MentionReferenceRegex.Match("@" + insertionToken + " hello");

        Assert.That(insertionToken, Is.EqualTo("wren"));
        Assert.That(parsed.Success, Is.True);
        Assert.That(parsed.Groups[1].Value, Is.EqualTo("wren"));
        Assert.That(MentionNamePolicy.FindUniqueMentionMatch(parsed.Groups[1].Value, new[] { rosterName, "Wrenna" }), Is.EqualTo(rosterName));
        Assert.That(MentionNamePolicy.IsLocalMentionMatch(rosterName, "Wren", parsed.Groups[1].Value), Is.True);
    }

    [TestCase("Tony boy", "Tony")]
    [TestCase("A.B", "A.B")]
    [TestCase("x_x", "x_x")]
    [TestCase("***", "***")]
    public void MentionInsertionToken_StaysWhitespaceFreeAndPreservesOrdinaryOrSymbolNames(string value, string expected)
    {
        Assert.That(MentionNamePolicy.ToMentionInsertionToken(value), Is.EqualTo(expected));
    }

    [TestCase("Wisteria", "Wisteria")]
    [TestCase("Tony boy", "Tony boy")]
    [TestCase("x_x", "x_x")]
    [TestCase("A.B", "A.B")]
    public void ToCanonicalMentionName_PreservesOrdinaryIdentityCharacters(string value, string expected)
    {
        Assert.That(MentionNamePolicy.ToCanonicalMentionName(value), Is.EqualTo(expected));
    }

    [Test]
    public void FindUniqueMentionMatch_ResolvesExactDecoratedStyleName()
    {
        string[] roster = { "Robin [Tired] [4/5sp]", DecoratedRosterName + " <#70FFBD>[5/5sp]</color>", "Wrenna" };

        Assert.That(MentionNamePolicy.FindUniqueMentionMatch("@wren", roster), Is.EqualTo(roster[1]));
    }

    [Test]
    public void FindUniqueMentionMatch_FailsClosedForAmbiguousPartialOrCanonicalCollision()
    {
        Assert.That(MentionNamePolicy.FindUniqueMentionMatch("wre", new[] { DecoratedRosterName, "Wrenna" }), Is.Empty);
        Assert.That(MentionNamePolicy.FindUniqueMentionMatch("wren", new[] { DecoratedRosterName, "<#FFFFFF>Wren</color>" }), Is.Empty);
    }

    [Test]
    public void IsLocalMentionMatch_DoesNotLetTokenOverrideResolvedIdentity()
    {
        Assert.That(MentionNamePolicy.IsLocalMentionMatch(DecoratedRosterName, "Wren", "wren"), Is.True);
        Assert.That(MentionNamePolicy.IsLocalMentionMatch("Wrenna", "Wren", "wren"), Is.False);
    }
}
