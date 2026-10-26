using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5 R0 (Blue, 2026-10-22: "No faces on the bluesage report screen on todays build"). The resolver was
// primed only by the native Statistics tab; the QoL-menu path never primed it. The reason policy makes the
// next log say which of the four things went wrong instead of a silent plain-label fallback.
[TestFixture]
internal sealed class JournalMoodFaceResolutionPolicyTests
{
    [Test]
    public void Five_live_sprites_is_success_with_no_reason()
    {
        string reason = JournalMoodFaceResolutionPolicy.Describe(
            managerAvailable: true, fieldFound: true, spriteCount: 5, destroyedCount: 0);
        Assert.That(reason, Is.Empty);
    }

    [Test]
    public void Missing_manager_is_named_first()
    {
        string reason = JournalMoodFaceResolutionPolicy.Describe(
            managerAvailable: false, fieldFound: false, spriteCount: 0, destroyedCount: 0);
        Assert.That(reason, Does.Contain("StatisticManager"));
        Assert.That(reason, Does.Contain("not available"));
    }

    [Test]
    public void Missing_field_names_the_game_field()
    {
        string reason = JournalMoodFaceResolutionPolicy.Describe(
            managerAvailable: true, fieldFound: false, spriteCount: 0, destroyedCount: 0);
        Assert.That(reason, Does.Contain("_moodIcons"));
    }

    [Test]
    public void Wrong_count_reports_the_count_seen()
    {
        string reason = JournalMoodFaceResolutionPolicy.Describe(
            managerAvailable: true, fieldFound: true, spriteCount: 3, destroyedCount: 0);
        Assert.That(reason, Does.Contain("3"));
        Assert.That(reason, Does.Contain("5"));
    }

    [Test]
    public void Destroyed_sprites_are_counted()
    {
        string reason = JournalMoodFaceResolutionPolicy.Describe(
            managerAvailable: true, fieldFound: true, spriteCount: 5, destroyedCount: 2);
        Assert.That(reason, Does.Contain("2"));
        Assert.That(reason, Does.Contain("destroyed"));
    }

    [Test]
    public void More_than_five_is_fine_because_only_the_first_five_are_used()
    {
        string reason = JournalMoodFaceResolutionPolicy.Describe(
            managerAvailable: true, fieldFound: true, spriteCount: 7, destroyedCount: 0);
        Assert.That(reason, Is.Empty);
    }
}
