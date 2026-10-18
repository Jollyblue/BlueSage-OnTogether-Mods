using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class MacOsLoaderProcessFilterContractTests
{
    [Test]
    public void Plugin_declares_windows_and_hyphenated_macos_process_filters()
    {
        string pluginSource = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(pluginSource, Does.Contain("[BepInProcess(\"OnTogether.exe\")]"),
                "Windows must retain its existing executable-name filter.");
            Assert.That(pluginSource, Does.Contain("[BepInProcess(\"On-Together\")]"),
                "The macOS build reports the hyphenated On-Together process name.");
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

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
