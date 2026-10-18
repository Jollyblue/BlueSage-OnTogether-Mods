using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ResponsiveWindowLayoutContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Qol_sections_use_one_two_or_three_columns_without_forcing_overflow()
    {
        string source = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));
        Assert.That(source, Does.Contain("int columnCount = _windowRect.width >= 1320f ? 3 : (_windowRect.width >= 880f ? 2 : 1);"));
        Assert.That(source, Does.Contain("if (columnCount == 1)"));
        Assert.That(source, Does.Contain("if (columnCount == 3)"));
        Assert.That(source, Does.Contain("bool useTwoColumns = _windowRect.width >= 1280f;"));
        Assert.That(source, Does.Contain("private static int GetThanksGridColumnCount(float availableWidth)"));
        Assert.That(source, Does.Contain("if (availableWidth >= 1400f) return 4;"));
        Assert.That(source, Does.Contain("if (availableWidth >= 1020f) return 3;"));
        Assert.That(source, Does.Contain("if (availableWidth >= 680f) return 2;"));
        Assert.That(source, Does.Contain("return 1;"));
        Assert.That(source, Does.Contain("DrawSupporterDirectory"));
        Assert.That(source, Does.Contain("DrawSelectedSupporterMessages"));
        Assert.That(source, Does.Contain("GUILayout.Height(34f)"));
        Assert.That(source, Does.Contain("BlueSageHelpFrame.Resolve"));
        Assert.That(source, Does.Not.Contain("GUILayout.MinHeight(190f)"));
        Assert.That(source, Does.Not.Contain("GUILayout.Height(132f)"));
    }

    [Test]
    public void Style_footer_wraps_actions_and_reserves_matching_height()
    {
        string source = File.ReadAllText(Path.Combine(Root, "src", "StyleHelperWindow.cs"));
        Assert.That(source, Does.Contain("private float GetFooterHeight()"));
        Assert.That(source, Does.Contain("CompactFooterHeight = 198f"));
        Assert.That(source, Does.Contain("BlueSageHelpFrame.Resolve"));
        Assert.That(source, Does.Contain("BlueSageHelpFrame.DrawStateAndHelp"));
        Assert.That(source, Does.Contain("bool wrapFooterActions = _windowRect.width < 1100f;"));
        Assert.That(source, Does.Contain("if (wrapFooterActions)"));
        Assert.That(source, Does.Not.Contain("stateLine += \"  •  \" + _message"));
    }

    [Test]
    public void Public_windows_preserve_independent_saved_geometry_on_every_open_path()
    {
        string source = File.ReadAllText(Path.Combine(Root, "src", "BlueSageWindowCoordinator.cs"));
        int pairCalls = source.Split("SharedWindowLayoutPolicy.CreatePair").Length - 1;

        Assert.Multiple(() =>
        {
            Assert.That(pairCalls, Is.EqualTo(0),
                "Opening either window must not replace its saved geometry just because the other window is visible.");
            Assert.That(source, Does.Contain("return WindowFitPolicy.FitForOpen"));
            Assert.That(source, Does.Contain("return WindowFitPolicy.FitToScreen"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
