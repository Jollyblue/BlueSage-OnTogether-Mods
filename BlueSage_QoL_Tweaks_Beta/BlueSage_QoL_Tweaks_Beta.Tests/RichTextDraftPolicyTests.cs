using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RichTextDraftPolicyTests
{
    [Test]
    public void PairedNoParseTagsAreAccepted()
    {
        bool valid = RichTextDraftPolicy.TryValidate("<noparse><b>literal</b></noparse>", out string message);
        Assert.That(valid, Is.True, message);
    }
    [TestCase("<b><rotate=5><size=120%>Blue</size></rotate></b>")]
    [TestCase("<color=#0E87CC>Blue</color>")]
    [TestCase("<#0E87CC>Blue</color>")]
    [TestCase("<#0E87CC><b><rotate=-5>Blue</rotate></b></color>")]
    [TestCase("plain text")]
    [TestCase("Blue <3")]
    public void Accepts_balanced_nested_tmp_markup(string value)
    {
        Assert.That(RichTextDraftPolicy.TryValidate(value, out _), Is.True);
    }

    [TestCase("<b><size=120%>Blue</b></size>")]
    [TestCase("<rotate=5>Blue")]
    [TestCase("<#0E87CC>Blue")]
    [TestCase("<#0E87CC>Blue</co")]
    [TestCase("<color=#0E87CC")]
    [TestCase("Blue</color>")]
    [TestCase("Blue <b")]
    public void Rejects_unbalanced_or_crossed_markup(string value)
    {
        Assert.That(RichTextDraftPolicy.TryValidate(value, out string message), Is.False);
        Assert.That(message, Is.Not.Empty);
    }

    [Test]
    public void Accepts_balanced_nested_tmp_markup_beyond_the_old_220_character_editor_cap()
    {
        string repeated = string.Concat(Enumerable.Repeat("<color=#5AC8FA><rotate=5>B</rotate></color>", 8));
        string value = "<size=90%><b>" + repeated + "</b></size>";

        Assert.That(value.Length, Is.GreaterThan(220));
        Assert.That(RichTextDraftPolicy.TryValidate(value, out string message), Is.True, message);
    }

    [Test]
    public void Style_helper_preserves_manual_markup_instead_of_gradient_coloring_tag_characters()
    {
        string root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
        string source = File.ReadAllText(Path.Combine(root, "src", "StyleHelperWindow.cs"));
        Assert.That(source, Does.Contain("Custom Rich Text mode preserves exact balanced markup"));
        Assert.That(source, Does.Contain("the game decides whether each tag/value renders"));
        Assert.That(source, Does.Contain("text.IndexOf('<') >= 0"));
    }

    [Test]
    public void RepairLegacy_closes_crossed_tags_without_losing_visible_text()
    {
        RichTextRepairResult repaired = RichTextDraftPolicy.RepairLegacy("<b><size=120%>Blue</b></size>");

        Assert.That(repaired.Value, Is.EqualTo("<b><size=120%>Blue</size></b>"));
        Assert.That(repaired.Changed, Is.True);
        Assert.That(repaired.UsedPlainTextFallback, Is.False);
        Assert.That(RichTextDraftPolicy.TryValidate(repaired.Value, out string message), Is.True, message);
    }

    [Test]
    public void RepairLegacy_drops_orphan_close_and_finishes_unclosed_shorthand_color()
    {
        RichTextRepairResult repaired = RichTextDraftPolicy.RepairLegacy("</color><#0E87CC><b>Blue</b>");

        Assert.That(repaired.Value, Is.EqualTo("<#0E87CC><b>Blue</b></color>"));
        Assert.That(repaired.UsedPlainTextFallback, Is.False);
        Assert.That(RichTextStyleBuilder.ToPlainStyleEditorText(repaired.Value), Is.EqualTo("Blue"));
    }

    [Test]
    public void RepairLegacy_uses_visible_plain_text_when_a_cut_off_tag_is_not_deterministic()
    {
        RichTextRepairResult repaired = RichTextDraftPolicy.RepairLegacy("Blue <color=#");

        Assert.That(repaired.Value, Is.EqualTo("Blue"));
        Assert.That(repaired.Changed, Is.True);
        Assert.That(repaired.UsedPlainTextFallback, Is.True);
    }

    [Test]
    public void RepairLegacy_is_idempotent_after_deterministic_repair()
    {
        RichTextRepairResult first = RichTextDraftPolicy.RepairLegacy("</color><#0E87CC><b>Blue</b>");
        RichTextRepairResult second = RichTextDraftPolicy.RepairLegacy(first.Value);

        Assert.That(second.Value, Is.EqualTo(first.Value));
        Assert.That(second.Changed, Is.False);
        Assert.That(second.UsedPlainTextFallback, Is.False);
    }
}
