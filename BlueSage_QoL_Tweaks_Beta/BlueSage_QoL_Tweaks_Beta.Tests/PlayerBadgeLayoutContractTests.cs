using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerBadgeLayoutContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Badge_strip_is_marker_owned_measured_and_separate_from_the_action_lane()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("BlueSage_BadgeStrip"));
            Assert.That(controller, Does.Not.Contain("BlueSage_ActionLane"));
            Assert.That(controller, Does.Contain("layoutElement.ignoreLayout = true"));
            Assert.That(controller, Does.Contain("PlayerBadgeLayoutPolicy.Resolve"));
            Assert.That(controller, Does.Not.Contain("BadgeStripWidth"));
            Assert.That(controller, Does.Contain("TextMeshProUGUI"));
            Assert.That(controller, Does.Not.Contain("AddComponent<Button>"));
            Assert.That(marker, Does.Contain("IPointerEnterHandler"));
            Assert.That(marker, Does.Contain("IPointerExitHandler"));
            Assert.That(marker, Does.Contain("TooltipText"));
        });
    }

    [Test]
    public void Controller_never_mutates_the_games_native_name_geometry()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        Assert.Multiple(() =>
        {
            Assert.That(marker, Does.Not.Contain("OriginalNameOffsetMin"));
            Assert.That(marker, Does.Not.Contain("OriginalNameOffsetMax"));
            Assert.That(marker, Does.Not.Contain("NameGeometrySaved"));
            Assert.That(controller, Does.Not.Contain("SaveNameGeometry"));
            Assert.That(controller, Does.Not.Contain("RestoreNameGeometry"));
            Assert.That(controller, Does.Not.Contain("nameRect.offsetMin ="));
            Assert.That(controller, Does.Not.Contain("nameRect.offsetMax ="));
            Assert.That(controller, Does.Not.Contain("nameRect.anchoredPosition ="));
            Assert.That(controller, Does.Not.Contain("nameRect.sizeDelta ="));
            Assert.That(controller, Does.Not.Contain("nameRect.anchorMin ="));
            Assert.That(controller, Does.Not.Contain("nameRect.anchorMax ="));
            Assert.That(controller, Does.Not.Contain("nameRect.pivot ="));
            Assert.That(controller, Does.Not.Contain("NameText.text ="));
            Assert.That(controller, Does.Not.Contain("nameText.text ="));
        });
    }

    [Test]
    public void Role_badges_reserve_native_text_space_without_joining_the_action_lane()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        int cacheLookup = controller.IndexOf(
            "marker.TryGetCachedLayout(",
            StringComparison.Ordinal);
        int measurementReset = controller.IndexOf(
            "badgeLabel.margin = Vector4.zero;",
            StringComparison.Ordinal);
        int preferredMeasurement = controller.IndexOf(
            "badgeLabel.GetPreferredValues(",
            StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("layoutElement.ignoreLayout = true"));
            Assert.That(controller, Does.Contain("marker.ApplyNativePresentationReservation("));
            Assert.That(controller, Does.Contain("layout.NativeNameReservation"));
            Assert.That(controller, Does.Contain("nativeName.GetPreferredValues("));
            Assert.That(controller, Does.Contain("PlayerBadgePolicy.Collapse("));
            Assert.That(controller, Does.Contain("PlayerBadgeLayoutPolicy.ResolveResponsiveAllocation("));
            Assert.That(controller, Does.Contain("private const int BadgeCapacity = 7;"));
            Assert.That(controller, Does.Contain("GetPreferredValues"));
            Assert.That(controller, Does.Not.Contain("BadgeStripWidth"));
            Assert.That(controller, Does.Not.Contain("BlueSage_ActionLane"));
            Assert.That(cacheLookup, Is.GreaterThanOrEqualTo(0));
            Assert.That(measurementReset, Is.GreaterThan(cacheLookup),
                "An unchanged tick must return from cache before resetting marker margins.");
            Assert.That(measurementReset, Is.GreaterThanOrEqualTo(0));
            Assert.That(preferredMeasurement, Is.GreaterThan(measurementReset),
                "Preferred width must not inherit the previous marker-owned label margin.");
            Assert.That(controller, Does.Contain("marker.StoreCachedLayout("));
            Assert.That(controller, Does.Contain("fontSharedMaterial")
                .And.Contain("fontSize")
                .And.Contain("fontStyle")
                .And.Contain("characterSpacing")
                .And.Contain("wordSpacing")
                .And.Contain("nameRect.rect.width")
                .And.Contain("canvas.scaleFactor"),
                "The cache key must cover materially relevant TMP, native-width, and scale inputs.");
        });
    }

    [Test]
    public void Native_host_suffix_substitution_uses_live_localization_without_reflowing_the_native_row()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain(
                "MonoSingleton<SettingsController>.I?.HostString?.String ?? string.Empty"));
            Assert.That(controller, Does.Contain("marker.GetNativeNameSourceText()"));
            Assert.That(controller, Does.Contain("layoutElement.ignoreLayout = true"));
            Assert.That(controller, Does.Contain("marker.ApplyNativePresentationReservation("));
            Assert.That(controller, Does.Not.Contain("nameRect.offsetMin ="));
            Assert.That(controller, Does.Not.Contain("nameRect.offsetMax ="));
        });
    }

    [Test]
    public void Badge_layout_never_resizes_the_native_panel_row_or_name()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Not.Contain("EnsurePanelExpansion("));
            Assert.That(controller, Does.Not.Contain("ApplyNativeNameWidthExpansion("));
            Assert.That(marker, Does.Not.Contain("ApplyNativeNameWidthExpansion("));
            Assert.That(marker, Does.Not.Contain("_rowBaselineOffsetMax"));
            Assert.That(marker, Does.Not.Contain("_nativeNameBaselineOffsetMax"));
        });
    }

    [Test]
    public void Badge_strip_uses_an_explicit_collapsed_right_edge_lane()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("float rightAnchor = nameRect.anchorMax.x;"));
            Assert.That(controller, Does.Contain("badgeRect.anchorMin = new Vector2(")
                .And.Contain("rightAnchor, nameRect.anchorMin.y"));
            Assert.That(controller, Does.Contain("badgeRect.anchorMax = new Vector2(")
                .And.Contain("rightAnchor, nameRect.anchorMax.y"));
            Assert.That(controller, Does.Contain("badgeRect.pivot = new Vector2(1f, nameRect.pivot.y);"));
            Assert.That(controller, Does.Not.Contain("badgeRect.anchorMin = nameRect.anchorMin;"));
            Assert.That(controller, Does.Not.Contain("badgeRect.anchorMax = nameRect.anchorMax;"));
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
}
