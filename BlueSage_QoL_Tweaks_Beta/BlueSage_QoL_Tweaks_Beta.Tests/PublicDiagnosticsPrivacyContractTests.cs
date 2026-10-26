using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicDiagnosticsPrivacyContractTests
{
    [Test]
    // Round 4 WS2 (Blue, 2026-10-19): the hidden [BlueSageDiag] snapshot that this guarded is removed entirely,
    // so no shareable diagnostic line can carry identity surfaces any more.
    public void Hidden_diagnostic_snapshot_is_removed()
    {
        string source = ReadPluginSource();
        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Not.Contain("LogHiddenDiagnosticSnapshot"));
            Assert.That(source, Does.Not.Contain("[BlueSageDiag]"));
            Assert.That(source, Does.Not.Contain("BuildProcessMemoryDiagnosticSummary"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage QoL project root.");
    }

    private static string ReadPluginSource()
    {
        return File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs")).Replace("\r\n", "\n");
    }
}
