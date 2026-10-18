using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SweepResourceEvidenceTests
{
    [Test]
    public void Format_reports_before_after_signed_delta_and_dominant_reclaimed_category()
    {
        SweepResourceSnapshot before = Snapshot(
            29684,
            new SweepResourceCategoryTotal("Texture2D", 29263, MiB(3598.98)),
            new SweepResourceCategoryTotal("Material", 4341, MiB(34.53)));
        SweepResourceSnapshot after = Snapshot(
            29684,
            new SweepResourceCategoryTotal("Texture2D", 2888, MiB(192.64)),
            new SweepResourceCategoryTotal("Material", 4292, MiB(34.43)));

        string result = SweepResourceEvidenceFormatter.Format(before, after);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("resourceStatus=ok"));
            Assert.That(result, Does.Contain("sceneObjects=29684->29684(+0)"));
            Assert.That(result, Does.Contain("Texture2D=29263/3599.0MiB->2888/192.6MiB(delta=-26375/-3406.3MiB)"));
            Assert.That(result, Does.Contain("Material=4341/34.5MiB->4292/34.4MiB(delta=-49/-0.1MiB)"));
            Assert.That(result, Does.Contain("dominantReclaimed=Texture2D/3406.3MiB"));
        });
    }

    [Test]
    public void Format_reports_only_five_largest_reclaimed_asset_types_and_sizes_without_names()
    {
        SweepResourceAssetFact survivor = new(99, "Texture2D", MiB(100));
        SweepResourceSnapshot before = Snapshot(10, Array.Empty<SweepResourceCategoryTotal>(),
            survivor,
            new SweepResourceAssetFact(1, "Texture2D", MiB(1)),
            new SweepResourceAssetFact(2, "Texture2D", MiB(2)),
            new SweepResourceAssetFact(3, "Texture2D", MiB(3)),
            new SweepResourceAssetFact(4, "Texture2D", MiB(4)),
            new SweepResourceAssetFact(5, "Texture2D", MiB(5)),
            new SweepResourceAssetFact(6, "Texture2D", MiB(6)));
        SweepResourceSnapshot after = Snapshot(10, Array.Empty<SweepResourceCategoryTotal>(), survivor);

        string result = SweepResourceEvidenceFormatter.Format(before, after);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("largestReclaimed=Texture2D:6.0MiB|Texture2D:5.0MiB|Texture2D:4.0MiB|Texture2D:3.0MiB|Texture2D:2.0MiB"));
            Assert.That(result, Does.Not.Contain("assetName"));
        });
    }

    [Test]
    public void Format_marks_truncated_detail_without_claiming_full_individual_coverage()
    {
        SweepResourceSnapshot before = Snapshot(1, Array.Empty<SweepResourceCategoryTotal>());
        before.DetailTruncated = true;
        SweepResourceSnapshot after = Snapshot(1, Array.Empty<SweepResourceCategoryTotal>());

        Assert.That(SweepResourceEvidenceFormatter.Format(before, after), Does.Contain("detailTruncated=true"));
    }

    [Test]
    public void Format_fails_open_when_either_census_is_unavailable()
    {
        SweepResourceSnapshot before = SweepResourceSnapshot.Unavailable("InvalidOperationException");
        SweepResourceSnapshot after = Snapshot(1, Array.Empty<SweepResourceCategoryTotal>());

        Assert.That(
            SweepResourceEvidenceFormatter.Format(before, after),
            Is.EqualTo("resourceStatus=unavailable:InvalidOperationException"));
    }

    private static SweepResourceSnapshot Snapshot(
        int sceneObjects,
        SweepResourceCategoryTotal category,
        SweepResourceCategoryTotal secondCategory)
    {
        return Snapshot(sceneObjects, new[] { category, secondCategory });
    }

    private static SweepResourceSnapshot Snapshot(
        int sceneObjects,
        SweepResourceCategoryTotal[] categories,
        params SweepResourceAssetFact[] assets)
    {
        return new SweepResourceSnapshot
        {
            Captured = true,
            Status = "ok",
            SceneObjectCount = sceneObjects,
            Categories = categories,
            Assets = assets
        };
    }

    private static long MiB(double value) => (long)Math.Round(value * 1024d * 1024d);
}
