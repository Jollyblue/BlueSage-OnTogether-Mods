using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChalkboardCatalogAndCommandPolicyTests
{
    [Test]
    public void Build_24259292_catalog_has_exactly_three_named_surfaces_and_no_index_three()
    {
        IReadOnlyList<ChalkboardCatalogEntry> catalog = ChalkboardCatalogPolicy.Build24259292;

        Assert.Multiple(() =>
        {
            Assert.That(catalog, Has.Count.EqualTo(3));
            Assert.That(catalog.Select(item => item.Index), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(catalog.Select(item => item.Label), Is.EqualTo(new[]
            {
                "River/Stage",
                "Classroom board A (face A)",
                "Classroom board B (face B)"
            }));
            Assert.That(ChalkboardCatalogPolicy.TryGet(3, out _), Is.False);
            Assert.That(
                ChalkboardCatalogPolicy.TryParseExactIndex("3", 4, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("index 3 never"));
        });
    }

    [Test]
    public void Destructive_action_requires_a_fresh_unchanged_map_and_no_unresolved_rollback()
    {
        const long now = 1_784_520_000;

        bool accepted = ChalkboardCommandPolicy.TryAuthorizeDestructiveAction(
            2,
            3,
            "map-A",
            now - 30,
            "map-A",
            now,
            rollbackUnresolved: false,
            out string acceptedError);
        bool stale = ChalkboardCommandPolicy.TryAuthorizeDestructiveAction(
            2,
            3,
            "map-A",
            now - 31,
            "map-A",
            now,
            rollbackUnresolved: false,
            out string staleError);
        bool changed = ChalkboardCommandPolicy.TryAuthorizeDestructiveAction(
            2,
            3,
            "map-A",
            now,
            "map-B",
            now,
            rollbackUnresolved: false,
            out string changedError);
        bool rollbackBlocked = ChalkboardCommandPolicy.TryAuthorizeDestructiveAction(
            2,
            3,
            "map-A",
            now,
            "map-A",
            now,
            rollbackUnresolved: true,
            out string rollbackError);

        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.True, acceptedError);
            Assert.That(stale, Is.False);
            Assert.That(staleError, Does.Contain("stale"));
            Assert.That(changed, Is.False);
            Assert.That(changedError, Does.Contain("changed"));
            Assert.That(rollbackBlocked, Is.False);
            Assert.That(rollbackError, Does.Contain("unresolved rollback"));
        });
    }

    [Test]
    public void Action_truth_reports_every_required_observation_without_rich_text_controls()
    {
        string result = ChalkboardCommandPolicy.BuildActionTruth(
            "clear",
            1,
            "Classroom\r\n<face A>",
            "Host-direct",
            4,
            "recovery-1",
            "ready",
            "not-observed");

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("board=1"));
            Assert.That(result, Does.Contain("scene=Classroom face A"));
            Assert.That(result, Does.Contain("authority=Host-direct"));
            Assert.That(result, Does.Contain("compatibleRecipients=4"));
            Assert.That(result, Does.Contain("snapshot=recovery-1"));
            Assert.That(result, Does.Contain("rollback=ready"));
            Assert.That(result, Does.Contain("vanillaObserver=not-observed"));
            Assert.That(result, Does.Not.Contain("\r").And.Not.Contain("\n").And.Not.Contain("<").And.Not.Contain(">"));
        });
    }
}
