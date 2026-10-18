using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicDiagnosticsPrivacyContractTests
{
    [Test]
    public void Shareable_bepinex_diagnostics_are_timestamped_and_exclude_exact_identity_surfaces()
    {
        string source = ReadPluginSource();
        int start = source.IndexOf("private void LogHiddenDiagnosticSnapshot(string reason, HostHealthInput input", StringComparison.Ordinal);
        int end = source.IndexOf("private static string BuildProcessMemoryDiagnosticSummary()", start, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        string diagnosticMethod = source.Substring(start, end - start);

        Assert.Multiple(() =>
        {
            Assert.That(diagnosticMethod, Does.Contain("utc={DateTime.UtcNow:O}"));
            Assert.That(diagnosticMethod, Does.Contain("identityDetail=private-audit-only"));
            Assert.That(diagnosticMethod, Does.Not.Contain("localSteamId="));
            Assert.That(diagnosticMethod, Does.Not.Contain("lobbyOwnerSteamId="));
            Assert.That(diagnosticMethod, Does.Not.Contain("lobbyTitle='"));
            Assert.That(diagnosticMethod, Does.Not.Contain("hostPersona='"));
            Assert.That(diagnosticMethod, Does.Not.Contain("string.Join(\",\", playerSteamIds)"));
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
