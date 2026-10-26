using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class VoicePositionRateGuardContractTests
{
    [Test]
    public void Vivox_guard_is_optional_fail_open_bounded_and_defaults_on()
    {
        string root = FindProjectRoot();
        string patch = File.ReadAllText(Path.Combine(root, "src", "Patches", "VoicePositionRateGuardPatch.cs"));
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("AccessTools.TypeByName(\"Unity.Services.Vivox.VivoxServiceInternal\")"));
            Assert.That(patch, Does.Not.Contain("AccessTools.TypeByName(\"Unity.Services.Vivox.VivoxService\")"));
            Assert.That(patch, Does.Contain("[HarmonyPrepare]"));
            Assert.That(patch, Does.Contain("private static bool _targetResolutionAttempted;"));
            Assert.That(patch, Does.Contain("if (_targetResolutionAttempted) return _targetMethod;"));
            Assert.That(patch, Does.Contain("_targetResolutionAttempted = true;"));
            // Round 4 R9: the diagnostic summary (which once risked re-running target resolution) is removed.
            Assert.That(patch, Does.Not.Contain("GetDiagnosticSummary"));
            Assert.That(patch, Does.Contain("VoicePositionRatePolicy.ShouldForward("));
            Assert.That(patch, Does.Contain("MaximumTrackedObjects = 64"));
            Assert.That(patch, Does.Contain("return true;"));
            Assert.That(plugin, Does.Contain("EnableVivoxPositionRateGuard = Config.Bind(\"Performance Safety\", \"EnableVivoxPositionRateGuard\", true"));
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
}
