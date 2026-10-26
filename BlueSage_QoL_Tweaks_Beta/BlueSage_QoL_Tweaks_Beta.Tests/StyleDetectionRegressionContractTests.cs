using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StyleDetectionRegressionContractTests
{
    [Test]
    public void Detect_current_hydrates_live_colors_and_style_controls()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.That(source, Does.Contain("HydrateDetectedStyleControls(_detectedNamePreview);"));
        Assert.That(source, Does.Contain("Regex.Matches(styledName ?? string.Empty"));
        Assert.That(source, Does.Contain("color\\s*=\\s*#?|#"));
        Assert.That(source, Does.Contain("_colorA = colors[0];"));
        Assert.That(source, Does.Contain("_colorC = colors[colors.Count - 1];"));
        Assert.That(source, Does.Contain("_mode = DetectColorMode(colors);"));
        Assert.That(source, Does.Contain("private static int DetectColorMode"));
        Assert.That(source, Does.Contain("_bold = Regex.IsMatch"));
        Assert.That(source, Does.Contain("_italic = Regex.IsMatch"));
    }

    [Test]
    public void Status_and_spoons_use_the_repaired_saved_base_and_only_bootstrap_from_live_when_empty()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));

        Assert.That(source, Does.Contain("CaptureDisplayNameDraft(captureLiveBase: true)"));
        Assert.That(source, Does.Contain("BuildStatusSuffix(draft),"));
        Assert.That(source, Does.Contain("BuildSpoonSuffix(draft),"));
        Assert.That(source, Does.Contain("captureLiveBase && string.IsNullOrWhiteSpace(draft.BaseName)"));
        Assert.That(source, Does.Contain("ApplyStyleStateMigration();"));
        Assert.That(source, Does.Contain("StatusBaseNameLegacyBackup"));
        Assert.That(source, Does.Contain("draft.BaseName = liveBaseName;"));
        Assert.That(source.IndexOf("PlayerData?.Name?.Trim()"), Is.LessThan(source.IndexOf("TextChannelManager>.I?.UserName?.Trim()")));
    }

    [Test]
    public void Detect_current_hydrates_status_and_spoons_from_one_live_state_reconciliation()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.That(source, Does.Contain("GetCurrentDisplayNameStateForStyleUi()"));
        Assert.That(source, Does.Contain("HydrateCurrentStatus(detected)"));
        Assert.That(source, Does.Contain("HydrateSpoons(detected)"));
        Assert.That(source, Does.Contain("detected?.DriftSummary"));
        Assert.That(source, Does.Contain("Your own style codes are kept exactly as typed."));
    }

    [Test]
    public void Explicit_name_changes_do_not_get_replaced_by_the_old_live_name()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));

        Assert.That(source, Does.Not.Contain("preferLiveBaseName"));
        // Voice pass (2026-10-23): "Style Helper", never "Style UI", in player text.
        Assert.That(Count(source, "TryApplyDisplayNameDraft(draft, \"Style Helper: name updated.\""), Is.EqualTo(2));
        Assert.That(source, Does.Not.Contain("\"Style UI"));
        Assert.That(source, Does.Contain("TryApplyDisplayNameDraft(draft, \"Status Helper: name updated.\""));
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

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
}
