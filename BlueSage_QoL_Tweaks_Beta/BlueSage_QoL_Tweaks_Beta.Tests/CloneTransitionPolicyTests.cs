using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CloneTransitionPolicyTests
{
    [Test]
    public void NewEntrantMatchingStableVictim_AttributesEntrantAsPotentialCulprit()
    {
        CloneMatchKind result = CloneTransitionPolicy.Evaluate(
            false, string.Empty, "avatar-a", string.Empty, "<color=#ABCDEF>Robin</color>",
            "avatar-a", "avatar-a", "<color=#ABCDEF>Robin</color>", "<color=#ABCDEF>Robin</color>",
            true, true);

        Assert.That(result, Is.EqualTo(CloneMatchKind.AvatarOutfit | CloneMatchKind.ExactStyledName));
    }

    [Test]
    public void UnchangedDuplicate_DoesNotRepeatAlert()
    {
        CloneMatchKind result = CloneTransitionPolicy.Evaluate(
            true, "avatar-a", "avatar-a", "styled", "styled",
            "avatar-a", "avatar-a", "styled", "styled", true, true);

        Assert.That(result, Is.EqualTo(CloneMatchKind.None));
    }

    [Test]
    public void VictimChangingAtSameTime_DoesNotReverseAccusation()
    {
        CloneMatchKind result = CloneTransitionPolicy.Evaluate(
            true, "avatar-b", "avatar-a", "other", "styled",
            "avatar-old", "avatar-a", "victim-old", "styled", true, true);

        Assert.That(result, Is.EqualTo(CloneMatchKind.None));
    }

    [Test]
    public void ExactStyledNameRequiresOrdinalMarkupMatch()
    {
        CloneMatchKind result = CloneTransitionPolicy.Evaluate(
            true, "x", "x", "<color=#ffffff>Robin</color>", "<color=#FFFFFF>Robin</color>",
            "y", "y", "<color=#ffffff>Robin</color>", "<color=#ffffff>Robin</color>", false, true);

        Assert.That(result, Is.EqualTo(CloneMatchKind.None));
    }
}
