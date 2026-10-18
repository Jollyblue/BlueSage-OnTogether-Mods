using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class AuthoritativePlayerRowReconciliationPolicyTests
{
    [Test]
    public void Incomplete_or_misaligned_authority_never_prunes_owned_rows()
    {
        var pruned = new List<int>();
        int[] owned = { 10, 20, 30 };

        Assert.Multiple(() =>
        {
            Assert.That(Reconcile(false, new[] { 10, 20 }, owned, pruned.Add), Is.Zero);
            Assert.That(Reconcile(true, Array.Empty<int>(), owned, pruned.Add), Is.Zero);
            Assert.That(Reconcile(true, new[] { 10, 10 }, owned, pruned.Add), Is.Zero);
            Assert.That(Reconcile(true, new[] { 10, 0 }, owned, pruned.Add), Is.Zero);
            Assert.That(pruned, Is.Empty,
                "Transient, empty, duplicated, or invalid authority must preserve all owned UI.");
        });
    }

    [Test]
    public void Complete_three_to_two_leave_prunes_surface_and_badge_ownership_once()
    {
        var surface = OwnedRows(10, 20, 30);
        var badges = OwnedRows(10, 20, 30);

        int surfacePruned = Reconcile(
            true,
            new[] { 10, 20 },
            surface.Keys,
            rowId => surface[rowId].DestroySurface());
        int badgesPruned = Reconcile(
            true,
            new[] { 10, 20 },
            badges.Keys,
            rowId => badges[rowId].DestroyBadge());

        Assert.Multiple(() =>
        {
            Assert.That(surfacePruned, Is.EqualTo(1));
            Assert.That(badgesPruned, Is.EqualTo(1));
            Assert.That(surface[30].Destroyed, Is.True);
            Assert.That(surface[30].Unregistered, Is.True);
            Assert.That(surface[30].ListenersRemoved, Is.True);
            Assert.That(surface[30].CleanupCalls, Is.EqualTo(1));
            Assert.That(badges[30].Destroyed, Is.True);
            Assert.That(badges[30].Unregistered, Is.True);
            Assert.That(badges[30].CleanupCalls, Is.EqualTo(1));
            Assert.That(surface[10].Destroyed || surface[20].Destroyed, Is.False);
            Assert.That(badges[10].Destroyed || badges[20].Destroyed, Is.False);
        });
    }

    [Test]
    public void Rejoin_can_reuse_the_pooled_row_with_fresh_ownership()
    {
        var firstSurface = new OwnedRow(30, "departed");
        var firstBadge = new OwnedRow(30, "departed");
        Reconcile(true, new[] { 10, 20 }, new[] { 30 }, _ => firstSurface.DestroySurface());
        Reconcile(true, new[] { 10, 20 }, new[] { 30 }, _ => firstBadge.DestroyBadge());

        var reusedSurface = new OwnedRow(30, "rejoined");
        var reusedBadge = new OwnedRow(30, "rejoined");
        int surfacePruned = Reconcile(
            true,
            new[] { 10, 20, 30 },
            new[] { reusedSurface.RowInstanceId },
            _ => reusedSurface.DestroySurface());
        int badgePruned = Reconcile(
            true,
            new[] { 10, 20, 30 },
            new[] { reusedBadge.RowInstanceId },
            _ => reusedBadge.DestroyBadge());

        Assert.Multiple(() =>
        {
            Assert.That(firstSurface.Destroyed && firstSurface.Unregistered && firstSurface.ListenersRemoved, Is.True);
            Assert.That(firstBadge.Destroyed && firstBadge.Unregistered, Is.True);
            Assert.That(surfacePruned, Is.Zero);
            Assert.That(badgePruned, Is.Zero);
            Assert.That(reusedSurface.Identity, Is.EqualTo("rejoined"));
            Assert.That(reusedBadge.Identity, Is.EqualTo("rejoined"));
            Assert.That(reusedSurface.Destroyed || reusedBadge.Destroyed, Is.False,
                "The same pooled row instance is current again and must retain its fresh ownership.");
        });
    }

    private static int Reconcile(
        bool complete,
        IReadOnlyCollection<int> currentRows,
        ICollection<int> ownedRows,
        Action<int>? prune = null) =>
        AuthoritativePlayerRowReconciliationPolicy.Reconcile(
            complete,
            currentRows,
            ownedRows,
            prune ?? (_ => { }));

    private static Dictionary<int, OwnedRow> OwnedRows(params int[] rowIds) =>
        rowIds.ToDictionary(rowId => rowId, rowId => new OwnedRow(rowId, "initial"));

    private sealed class OwnedRow
    {
        internal OwnedRow(int rowInstanceId, string identity)
        {
            RowInstanceId = rowInstanceId;
            Identity = identity;
        }

        internal int RowInstanceId { get; }
        internal string Identity { get; }
        internal bool Destroyed { get; private set; }
        internal bool Unregistered { get; private set; }
        internal bool ListenersRemoved { get; private set; }
        internal int CleanupCalls { get; private set; }

        internal void DestroySurface()
        {
            CleanupCalls++;
            Unregistered = true;
            ListenersRemoved = true;
            Destroyed = true;
        }

        internal void DestroyBadge()
        {
            CleanupCalls++;
            Unregistered = true;
            Destroyed = true;
        }
    }
}
