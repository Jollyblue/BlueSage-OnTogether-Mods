using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class BlueSageReportWindowThemeContractTests
{
    [Test]
    public void Report_owns_an_opaque_themed_resizable_surface()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlueSageReportWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("DefaultWindowWidth = 1180f"));
            Assert.That(source, Does.Contain("DefaultWindowHeight = 900f"));
            Assert.That(source, Does.Contain("MinWindowWidth = 920f"));
            Assert.That(source, Does.Contain("MinWindowHeight = 640f"));
            Assert.That(source, Does.Contain("EnsureStyles();"));
            Assert.That(source, Does.Contain("BlueSageUiTheme.Current"));
            Assert.That(source, Does.Contain("GUI.DrawTexture"));
            Assert.That(source, Does.Contain("MenuDisplayScaleRuntime.ToPhysicalRect(_window)"));
            Assert.That(source, Does.Contain("MenuDisplayScaleRuntime.Apply(out Matrix4x4 previousMatrix)"));
            Assert.That(source, Does.Contain("MenuDisplayScaleRuntime.Restore(previousMatrix)"));
            Assert.That(source, Does.Contain("WindowFitPolicy.FitToScreen"));
            Assert.That(source, Does.Contain("DrawResizeGrip();"));
            Assert.That(source, Does.Contain("new GUIContent(\"↘\""));
            Assert.That(source, Does.Contain("GUI.Window(WindowId, _window, DrawWindow, string.Empty, _windowStyle)"));
            Assert.That(source, Does.Contain("ReportWindowResizePolicy.MergeGuiWindowResult"));
            Assert.That(source, Does.Contain("ReportWindowLayoutPolicy.UseStackedAllocationLayout"));
            Assert.That(source, Does.Not.Contain("_window = GUI.Window("));
            Assert.That(source, Does.Not.Contain("GUI.Window(WindowId, _window, DrawWindow, \"BlueSage Report\")"));
            Assert.That(source, Does.Not.Contain("GUI.skin.box"));
        });
    }

    [Test]
    public void Report_uses_the_semantic_metric_catalog_not_raw_member_names()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "BlueSageReportWindow.cs"));

        Assert.That(source, Does.Contain("BlueSageMetricPresentation.FriendlyLabel(key)"));
        Assert.That(source, Does.Not.Contain("private static string Friendly("));
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
