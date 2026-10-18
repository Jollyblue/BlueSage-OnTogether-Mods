using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RichTextStyleBuilderTests
{
    [Test]
    public void TryBuild_CreatesSingleColorText_AndNormalizesShortHex()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("color #83E Blue", 1500);

        Assert.That(result.Success, Is.True);
        Assert.That(result.GeneratedText, Is.EqualTo("<#8833EE>Blue</color>"));
        Assert.That(result.RawLength, Is.EqualTo(result.GeneratedText.Length));
    }

    [Test]
    public void TryBuild_CreatesBoldItalicColorText()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("bolditalic 5ec1c7 Blue Sage", 1500);

        Assert.That(result.Success, Is.True);
        Assert.That(result.GeneratedText, Is.EqualTo("<b><i><#5EC1C7>Blue Sage</color></i></b>"));
    }

    [Test]
    public void TryBuild_CreatesTwoPointGradientAcrossVisibleCharacters()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("gradient #000000 #FFFFFF AB", 1500);

        Assert.That(result.Success, Is.True);
        Assert.That(result.GeneratedText, Is.EqualTo("<#000000>A</color><#FFFFFF>B</color>"));
    }

    [Test]
    public void TryBuild_CreatesThreePointGradientAcrossVisibleCharacters()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("gradient3 #FF0000 #00FF00 #0000FF ABC", 1500);

        Assert.That(result.Success, Is.True);
        Assert.That(result.GeneratedText, Is.EqualTo("<#FF0000>A</color><#00FF00>B</color><#0000FF>C</color>"));
    }

    [Test]
    public void TryBuild_ClosesEveryGradientColorTag()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("gradient #5AC8FA #70FFBD Robin", 1500);

        Assert.That(result.Success, Is.True);
        Assert.That(Count(result.GeneratedText, "<#"), Is.EqualTo(Count(result.GeneratedText, "</color>")));
    }

    [Test]
    public void TryBuild_GradientPreservesNestedTmpTagsAndColorsOnlyVisibleText()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild(
            "gradient #000000 #FFFFFF <b><rotate=-5>A</rotate>B</b>",
            1500);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(result.GeneratedText, Is.EqualTo("<b><rotate=-5><#000000>A</color></rotate><#FFFFFF>B</color></b>"));
    }

    [Test]
    public void TryBuild_GradientDoesNotSplitEmojiTextElements()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("gradient #000000 #FFFFFF 💙A", 1500);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(Count(result.GeneratedText, "<#"), Is.EqualTo(2));
        Assert.That(result.GeneratedText, Does.Contain("💙"));
    }

    [Test]
    public void TryBuild_GradientPreservesCombiningTextElements()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("gradient #000000 #FFFFFF e\u0301A", 1500);

        Assert.That(result.Success, Is.True, result.Message);
        Assert.That(Count(result.GeneratedText, "<#"), Is.EqualTo(2));
        Assert.That(result.GeneratedText, Does.Contain("e\u0301"));
    }

    [TestCase("color #83E <b>Blue")]
    [TestCase("gradient #000000 #FFFFFF <rotate=5>Blue")]
    [TestCase("status #FFD45D <size=120%>BRB")]
    public void TryBuild_RejectsIncompleteRichTextBeforeGeneratingOutput(string command)
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild(command, 1500);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("missing its closing tag").Or.Contain("cut-off tag"));
    }

    [Test]
    public void TryBuild_CreatesStatusWithoutNameMutation()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("status #B499F2 Reading", 1500);

        Assert.That(result.Success, Is.True);
        Assert.That(result.GeneratedText, Is.EqualTo("<#B499F2>(Reading)</color>"));
    }

    [Test]
    public void TryBuild_FailsWhenGeneratedTextWouldExceedLockedLimit()
    {
        string text = new string('A', 260);

        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("gradient #000000 #FFFFFF " + text, 1500);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("too long"));
        Assert.That(result.RawLength, Is.GreaterThan(1500));
    }

    [Test]
    public void TryBuild_FailsWithUsefulUsage_ForInvalidInput()
    {
        RichTextStyleResult result = RichTextStyleBuilder.TryBuild("gradient #nope #FFFFFF Blue", 1500);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("hex"));
    }

    [Test]
    public void ToPlainStyleEditorText_StripsExistingRichTextBeforeDetectCurrentHydratesDraft()
    {
        string styled = "<#6495ED>R</color><#8775C3>o</color><#AA5599>b</color><#C46F77>i</color><#DD8855>n</color>";

        string plain = RichTextStyleBuilder.ToPlainStyleEditorText(styled);

        Assert.That(plain, Is.EqualTo("Robin"));
        Assert.That(plain, Does.Not.Contain("<#"));
        Assert.That(plain, Does.Not.Contain("</color>"));
    }

    [Test]
    public void ToPlainStyleEditorText_StripsRichTextThatWasAccidentallyStyledAsLiteralText()
    {
        string styledTagAsLiteralText = "<#111111><</color><#222222>#</color><#333333>5</color><#444444>A</color><#555555>C</color><#666666>8</color><#777777>F</color><#888888>A</color><#999999>></color><#AAAAAA>R</color><#BBBBBB>o</color><#CCCCCC>b</color><#DDDDDD>i</color><#EEEEEE>n</color><#FFFFFF><</color><#EEEEEE>/</color><#DDDDDD>c</color><#CCCCCC>o</color><#BBBBBB>l</color><#AAAAAA>o</color><#999999>r</color><#888888>></color>";

        string plain = RichTextStyleBuilder.ToPlainStyleEditorText(styledTagAsLiteralText);

        Assert.That(plain, Is.EqualTo("Robin"));
    }

    private static int Count(string text, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
