using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatusTransitionBehaviorTests
{
    private const string StyledRobin = "<#0E6EED>R</color><#5C62C3>o</color><#AA5599>b</color><#C46F77>i</color><#DD8855>n</color>";
    private const string Reading = "<#F0C7D8>(Reading)</color>";
    private const string Brb = "<#F0C7D8>(BRB)</color>";
    private const string Afk = "<#F0C7D8>(AFK)</color>";
    private const string Spoons = "<#70FFBD>[4/5sp]</color>";
    private const string NestedStyledBlue = "<size=90%><b><rotate=-5><#5AC8FA>B</color></rotate><rotate=8><#1E90FF>l</color></rotate><rotate=-5><#5AC8FA>u</color></rotate><rotate=8><#1E90FF>e</color></rotate></b></size>";

    [Test]
    public void ReadingToBrbToAfkToBackPreservesExactBaseAndSpoons()
    {
        string live = DisplayNameCompositionPolicy.Compose(StyledRobin, Reading, Spoons);
        string baseBeforePreset = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(live, "stale", Reading, Spoons);
        Assert.That(baseBeforePreset, Is.EqualTo(StyledRobin));

        live = DisplayNameCompositionPolicy.Compose(baseBeforePreset, Brb, Spoons);
        string baseBeforeAfk = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(live, "stale", Brb, Spoons);
        Assert.That(baseBeforeAfk, Is.EqualTo(StyledRobin));

        live = DisplayNameCompositionPolicy.Compose(baseBeforeAfk, Afk, Spoons);
        string baseBeforeBack = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(live, "stale", Afk, Spoons);
        live = DisplayNameCompositionPolicy.Compose(baseBeforeBack, Reading, Spoons);

        Assert.That(live, Is.EqualTo(DisplayNameCompositionPolicy.Compose(StyledRobin, Reading, Spoons)));
    }

    [TestCase("", "")]
    [TestCase("<#F0C7D8>(BRB)</color>", "")]
    [TestCase("", "<#70FFBD>[4/5energy]</color>")]
    [TestCase("<#F0C7D8>(AFK)</color>", "<#70FFBD>[4/5sp]</color>")]
    public void CompositionNeverRewritesStyledBase(string status, string spoons)
    {
        string display = DisplayNameCompositionPolicy.Compose(StyledRobin, status, spoons);
        Assert.That(display, Does.StartWith(StyledRobin));
    }

    [Test]
    public void NestedRotateSizeBoldAndPerLetterColorsSurviveEveryStatusAndSpoonsTransition()
    {
        string live = DisplayNameCompositionPolicy.Compose(NestedStyledBlue, Reading, Spoons);

        foreach (string nextStatus in new[] { Brb, Afk, Reading, string.Empty })
        {
            string currentStatus = live.Contains("(BRB)") ? Brb
                : live.Contains("(AFK)") ? Afk
                : live.Contains("(Reading)") ? Reading
                : string.Empty;
            string captured = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(live, "stale", currentStatus, Spoons);
            Assert.That(captured, Is.EqualTo(NestedStyledBlue));
            live = DisplayNameCompositionPolicy.Compose(captured, nextStatus, Spoons);
            Assert.That(live, Does.StartWith(NestedStyledBlue));
        }

        string withoutSpoons = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(live, "stale", string.Empty, Spoons);
        Assert.That(withoutSpoons, Is.EqualTo(NestedStyledBlue));
        Assert.That(DisplayNameMutationPolicy.TryCompose(withoutSpoons, Brb, string.Empty, 4000, out string final, out string error), Is.True, error);
        Assert.That(final, Is.EqualTo(NestedStyledBlue + " " + Brb));
    }
}
