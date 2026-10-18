using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatRowReconciliationPolicyTests
{
    [Test]
    public void Exactly_one_created_row_is_selected_by_instance_identity()
    {
        ChatRowReconciliationDecision decision =
            ChatRowReconciliationPolicy.Resolve(
                new[] { 11, 22 },
                new[] { 11, 22, 33 });

        Assert.Multiple(() =>
        {
            Assert.That(decision.HasExactCreatedRow, Is.True);
            Assert.That(decision.CreatedRowInstanceId, Is.EqualTo(33));
        });
    }

    [TestCase(new int[] { 11, 22 }, new int[] { 11, 22 })]
    [TestCase(new int[] { 11, 22 }, new int[] { 11, 22, 33, 44 })]
    [TestCase(new int[] { 11, 22 }, new int[] { 22 })]
    public void Ambiguous_or_missing_creation_fails_closed(
        int[] before,
        int[] after)
    {
        ChatRowReconciliationDecision decision =
            ChatRowReconciliationPolicy.Resolve(before, after);

        Assert.That(decision.HasExactCreatedRow, Is.False);
    }

    [Test]
    public void Reordered_existing_rows_do_not_look_new()
    {
        ChatRowReconciliationDecision decision =
            ChatRowReconciliationPolicy.Resolve(
                new[] { 11, 22, 33 },
                new[] { 33, 11, 22 });

        Assert.That(decision.HasExactCreatedRow, Is.False);
    }
}
