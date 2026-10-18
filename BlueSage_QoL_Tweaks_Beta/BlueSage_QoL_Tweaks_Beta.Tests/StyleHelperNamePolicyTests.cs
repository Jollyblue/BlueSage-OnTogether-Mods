using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StyleHelperNamePolicyTests
{
    [Test]
    public void DetectCurrentBaseName_PrefersLiveDisplayName_AndRemovesBlueSageStatusAndSpoons()
    {
        string liveDisplayName = "<#0E6EED>Robin</color> <#FFD45D>[Coffee]</color> <#70FFBD>[5/5sp]</color>";

        string plain = StyleHelperNamePolicy.ToPlainBaseNameForDetectCurrent(
            liveDisplayName,
            staleSavedBaseName: "OldTemplate",
            knownStatusSuffix: "<#FFD45D>[Coffee]</color>",
            knownSpoonSuffix: "<#70FFBD>[5/5sp]</color>");

        Assert.That(plain, Is.EqualTo("Robin"));
    }

    [Test]
    public void DetectCurrentBaseName_FallsBackToSavedName_WhenLiveNameIsMissing()
    {
        string plain = StyleHelperNamePolicy.ToPlainBaseNameForDetectCurrent(string.Empty, staleSavedBaseName: "<#000000>Blue</color>");

        Assert.That(plain, Is.EqualTo("Blue"));
    }

    [Test]
    public void DetectCurrentStyledName_PreservesLiveBaseStyle_AndRemovesBlueSageStatusAndSpoons()
    {
        string liveDisplayName = "<#0E6EED>R</color><#5C62C3>o</color><#AA5599>b</color><#C46F77>i</color><#DD8855>n</color> <#FFD45D>[Tired]</color> <#70FFBD>[2/5sp]</color>";

        string styled = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(
            liveDisplayName,
            staleSavedBaseName: "<#000000>OldTemplate</color>",
            knownStatusSuffix: "<#FFD45D>[Tired]</color>",
            knownSpoonSuffix: "<#70FFBD>[2/5sp]</color>");

        Assert.That(styled, Is.EqualTo("<#0E6EED>R</color><#5C62C3>o</color><#AA5599>b</color><#C46F77>i</color><#DD8855>n</color>"));
    }

    [Test]
    public void DetectCurrentStyledName_FallsBackToSavedStyledName_WhenLiveNameIsMissing()
    {
        string styled = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(string.Empty, staleSavedBaseName: "<#000000>Blue</color>");

        Assert.That(styled, Is.EqualTo("<#000000>Blue</color>"));
    }

    [TestCase("Pru (^-^)")]
    [TestCase("Artist [OC]")]
    [TestCase("Helper {QA}")]
    public void LegitimateBracketedBaseNamesAreNeverGuessedToBeStatus(string baseName)
    {
        string styled = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(baseName, "fallback");
        Assert.That(styled, Is.EqualTo(baseName));
    }

    [Test]
    public void OnlyExactKnownOldSuffixesAreRemovedBeforeAStatusTransition()
    {
        const string baseName = "<#0E6EED>Robin</color>";
        const string oldStatus = "<#FFD45D>(Reading)</color>";
        const string spoons = "<#70FFBD>[4/5sp]</color>";
        string live = baseName + " " + oldStatus + " " + spoons;

        string captured = StyleHelperNamePolicy.ToStyledBaseNameForDetectCurrent(live, "stale", oldStatus, spoons);

        Assert.That(captured, Is.EqualTo(baseName));
    }

    [Test]
    public void DetectCurrent_removes_repeated_018_suffix_contamination()
    {
        const string baseName = "<#0E6EED>Robin</color>";
        const string status = "<#FFD45D>(Reading)</color>";
        const string spoons = "<#70FFBD>[4/5sp]</color>";
        string contaminated = baseName + " " + status + " " + spoons + " " + status + " " + spoons;

        DetectedDisplayNameState detected = StyleHelperNamePolicy.DetectCurrentState(
            contaminated,
            contaminated,
            "Reading",
            "FFD45D",
            "()",
            true,
            4,
            "sp",
            status,
            spoons);

        Assert.That(detected.BaseName, Is.EqualTo(baseName));
        Assert.That(detected.StatusMessage, Is.EqualTo("Reading"));
        Assert.That(detected.SpoonsEnabled, Is.True);
        Assert.That(detected.SpoonCount, Is.EqualTo(4));
    }

    [Test]
    public void DetectCurrent_reads_live_status_and_spoons_instead_of_hiding_config_drift()
    {
        const string baseName = "<size=90%><b><#0E6EED>Blue</color></b></size>";
        string live = baseName + " <color=#FFD45D>(Deep Focus)</color> <#70FFBD>[2/5energy]</color>";

        DetectedDisplayNameState detected = StyleHelperNamePolicy.DetectCurrentState(
            live,
            baseName,
            "Reading",
            "FFD45D",
            "()",
            true,
            4,
            "sp",
            "<#FFD45D>(Reading)</color>",
            "<#70FFBD>[4/5sp]</color>");

        Assert.That(detected.BaseName, Is.EqualTo(baseName));
        Assert.That(detected.StatusMessage, Is.EqualTo("Deep Focus"));
        Assert.That(detected.StatusColor, Is.EqualTo("FFD45D"));
        Assert.That(detected.SpoonsEnabled, Is.True);
        Assert.That(detected.SpoonCount, Is.EqualTo(2));
        Assert.That(detected.SpoonLabel, Is.EqualTo("energy"));
        Assert.That(detected.DriftSummary, Does.Contain("live status differed"));
        Assert.That(detected.DriftSummary, Does.Contain("live Spoons differed"));
    }

    [Test]
    public void DetectCurrent_recovers_live_status_when_saved_message_is_blank_but_base_is_anchored()
    {
        const string baseName = "<size=90%><b><#0E6EED>Blue</color></b></size>";
        string live = baseName + " <color=#FFD45D>(Deep Focus)</color>";

        DetectedDisplayNameState detected = StyleHelperNamePolicy.DetectCurrentState(
            live,
            baseName,
            string.Empty,
            "FFD45D",
            "()",
            false,
            5,
            "sp",
            string.Empty,
            string.Empty);

        Assert.That(detected.BaseName, Is.EqualTo(baseName));
        Assert.That(detected.StatusMessage, Is.EqualTo("Deep Focus"));
        Assert.That(detected.DriftSummary, Does.Contain("live status differed"));
    }

    [Test]
    public void DetectCurrent_does_not_strip_a_legitimate_matching_wrapper_from_the_saved_base()
    {
        const string baseName = "Blue <color=#FFD45D>(Artist)</color>";

        DetectedDisplayNameState detected = StyleHelperNamePolicy.DetectCurrentState(
            baseName,
            baseName,
            string.Empty,
            "FFD45D",
            "()",
            false,
            5,
            "sp",
            string.Empty,
            string.Empty);

        Assert.That(detected.BaseName, Is.EqualTo(baseName));
        Assert.That(detected.StatusMessage, Is.Empty);
    }

    [Test]
    public void DetectCurrent_repairs_malformed_live_base_but_keeps_detected_suffix_state()
    {
        string live = "</color><#0E6EED><b>Blue</b> <#FFD45D>(AFK)</color>";

        DetectedDisplayNameState detected = StyleHelperNamePolicy.DetectCurrentState(
            live,
            "<#0E6EED>Blue</color>",
            "Reading",
            "FFD45D",
            "()",
            false,
            5,
            "sp",
            "<#FFD45D>(Reading)</color>",
            string.Empty);

        Assert.That(detected.BaseName, Is.EqualTo("<#0E6EED><b>Blue</b></color>"));
        Assert.That(detected.StatusMessage, Is.EqualTo("AFK"));
        Assert.That(RichTextDraftPolicy.TryValidate(detected.BaseName, out string message), Is.True, message);
    }
}
