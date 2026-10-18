using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class MiniMapPerformanceContractTests
{
    private static string ProjectRoot
    {
        get
        {
            string current = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(current, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
            }

            return current;
        }
    }

    [Test]
    public void MiniMap_patch_removes_floating_labels_and_keeps_id_click_throttled()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "MiniMapPlayerLabelPatch.cs"));
        string compact = string.Concat(source.Where(character => !char.IsWhiteSpace(character)));

        Assert.That(source, Does.Contain("RefreshIntervalSeconds"));
        Assert.That(source, Does.Contain("private const float RefreshIntervalSeconds = 2f"));
        Assert.That(source, Does.Contain("Time.unscaledTime < _nextRefreshAt"));
        Assert.That(compact, Does.Contain("_nextRefreshAt=Time.unscaledTime+RefreshIntervalSeconds;"));
        Assert.That(source, Does.Contain("MiniMapIdCardClickHandler"));
        Assert.That(source, Does.Contain("OpenIdCardFromMiniMap"));
        Assert.That(source, Does.Not.Contain("TextMeshProUGUI"));
        Assert.That(source, Does.Not.Contain("BlueSageMiniMapNameLabel"));
        Assert.That(source, Does.Not.Contain("GetOrCreateLabel"));
        Assert.That(source, Does.Not.Contain("MaxLabelsWithoutZoom"));
        Assert.That(source, Does.Not.Contain("ZoomScaleThreshold"));
        Assert.That(source, Does.Contain("private static readonly FieldInfo PlayerIconsField"));
        Assert.That(source, Does.Contain("FailureLogRepeatSeconds"));
        Assert.That(source, Does.Contain("AppliedFriendColorStateByIconInstanceId"));
        Assert.That(source, Does.Contain("ApplyFriendColorIfChanged"));
        Assert.That(source, Does.Contain("MiniMapFriendVisualPolicy.ShouldMoveToLastSibling"));
        Assert.That(source, Does.Contain("internal static void ClearAll()"));
        Assert.That(source, Does.Contain("DisableAndDestroy"));
        Assert.That(
            source.IndexOf("Time.unscaledTime < _nextRefreshAt", StringComparison.Ordinal),
            Is.LessThan(
                source.IndexOf("PlayerIconsField?.GetValue", StringComparison.Ordinal)),
            "The two-second guard must run before reflection.");
    }

    [Test]
    public void Plugin_cleanup_removes_minimap_handlers_before_unpatch_and_on_config_disable()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("MiniMapPlayerLabelPatch.ClearAll();"));
            Assert.That(plugin, Does.Contain("EnableMiniMapPlayerLabels.SettingChanged"));
            Assert.That(
                plugin.IndexOf("MiniMapPlayerLabelPatch.ClearAll();", StringComparison.Ordinal),
                Is.LessThan(plugin.IndexOf("_harmony.UnpatchSelf();", StringComparison.Ordinal)));
        });
    }
}
