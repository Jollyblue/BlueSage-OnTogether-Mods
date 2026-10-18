using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicPerformanceOverlayContractTests
{
    [Test]
    public void Public_qol_owns_the_overlay_without_a_private_provider()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        string controller = File.ReadAllText(Path.Combine(root, "src", "PerformanceOverlayController.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("EnablePerformanceOverlay = Config.Bind"));
            Assert.That(plugin, Does.Contain("StartPerformanceOverlayController"));
            Assert.That(menu, Does.Contain("DrawToggle(\"Performance Overlay\", Plugin.EnablePerformanceOverlay"));
            Assert.That(menu, Does.Not.Contain("TrySetDiagnosticsOverlayVisible"));
            Assert.That(controller, Does.Contain("FPS --  PING --  RAM --"));
            Assert.That(controller, Does.Contain("ProcessRuntimeMetricsCapture.ReadWorkingSetBytes"));
            Assert.That(controller, Does.Contain("TickManagerPingPolicy.FromRttSeconds"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
               throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
