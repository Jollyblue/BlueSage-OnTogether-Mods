using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerBadgeAccessibilityContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    [Test]
    public void Role_strip_and_host_label_are_independent_accessible_controls()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("MinimumAccessibleFontSize = 14f"));
            Assert.That(controller, Does.Contain("MaximumAccessibleFontSize = 18f"));
            Assert.That(controller, Does.Contain("HostLabel"));
            Assert.That(controller, Does.Contain("PlayerHostLabelDisplayPolicy.TryResolve"));
            Assert.That(controller, Does.Contain("layoutElement.ignoreLayout = true"));
            Assert.That(controller, Does.Not.Contain("nameRect.offsetMin ="));
            Assert.That(controller, Does.Not.Contain("nameRect.offsetMax ="));
        });
    }

    [Test]
    public void Role_colors_are_distinct_and_badges_follow_native_row_size_with_a_sane_cap()
    {
        string controller = Read("src", "Patches", "PlayerBadgeController.cs");
        string renderPolicy = Read("src", "Core", "PlayerBadgeRenderPolicy.cs");

        Assert.Multiple(() =>
        {
            Assert.That(renderPolicy, Does.Contain("<color=#70D6FF>[O]</color>"));
            Assert.That(renderPolicy, Does.Contain("<color=#A8D27A>[M]</color>"));
            Assert.That(renderPolicy, Does.Contain("<color=#F4A261>[U]</color>"));
            Assert.That(renderPolicy, Does.Contain("<color=#54D9E8>[V]</color>"));
            Assert.That(renderPolicy, Does.Contain("<color=#FF5C5C>[S]</color>"));
            Assert.That(renderPolicy, Does.Contain("<color=#FF9EC4>[♥]</color>"));
            Assert.That(renderPolicy, Does.Contain("<color=#C29BFF>[♪]</color>"));
            Assert.That(controller, Does.Contain("Mathf.Max(nativeFontSize, MinimumAccessibleFontSize)"));
            Assert.That(controller, Does.Contain("MaximumAccessibleFontSize"));
        });
    }

    [Test]
    public void Tooltip_uses_an_opaque_scalable_high_contrast_style()
    {
        string marker = Read("src", "Patches", "BlueSagePlayerBadgeMarker.cs");

        Assert.Multiple(() =>
        {
            Assert.That(marker, Does.Contain("TooltipMinimumFontSize = 16"));
            Assert.That(marker, Does.Contain("TooltipBackgroundAlpha = 0.97f"));
            Assert.That(marker, Does.Contain("normal.textColor = Color.white"));
            Assert.That(marker, Does.Contain("wordWrap = true"));
            Assert.That(marker, Does.Not.Contain("GUI.skin.box.CalcSize"));
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
}
