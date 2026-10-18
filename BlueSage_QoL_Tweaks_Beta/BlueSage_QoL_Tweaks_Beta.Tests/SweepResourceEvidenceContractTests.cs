using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SweepResourceEvidenceContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Sweep_captures_before_unload_and_after_awaited_completion()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        int before = plugin.IndexOf("SweepResourceSnapshot resourceBefore = SweepResourceEvidenceCapture.Capture()", StringComparison.Ordinal);
        int unload = plugin.IndexOf("operation = Resources.UnloadUnusedAssets()", StringComparison.Ordinal);
        int awaited = plugin.IndexOf("yield return operation", StringComparison.Ordinal);
        int after = plugin.IndexOf("SweepResourceSnapshot resourceAfter = SweepResourceEvidenceCapture.Capture()", StringComparison.Ordinal);
        int formatted = plugin.IndexOf("SweepResourceEvidenceFormatter.Format(resourceBefore, resourceAfter)", StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.GreaterThanOrEqualTo(0));
            Assert.That(unload, Is.GreaterThan(before));
            Assert.That(awaited, Is.GreaterThan(unload));
            Assert.That(after, Is.GreaterThan(awaited));
            Assert.That(formatted, Is.GreaterThan(after));
        });
    }

    [Test]
    public void Capture_is_bounded_fail_open_and_never_retains_Unity_objects()
    {
        string capturePath = Path.Combine(Root, "src", "SweepResourceEvidenceCapture.cs");
        Assert.That(File.Exists(capturePath), Is.True, "Unity sweep capture source must exist.");
        string source = File.ReadAllText(capturePath);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("MaximumAssetFacts = 50000"));
            Assert.That(source, Does.Contain("Profiler.GetRuntimeMemorySizeLong"));
            Assert.That(source, Does.Contain("Resources.FindObjectsOfTypeAll<Texture2D>()"));
            Assert.That(source, Does.Contain("Resources.FindObjectsOfTypeAll<RenderTexture>()"));
            Assert.That(source, Does.Contain("Resources.FindObjectsOfTypeAll<Material>()"));
            Assert.That(source, Does.Contain("Resources.FindObjectsOfTypeAll<Mesh>()"));
            Assert.That(source, Does.Contain("Resources.FindObjectsOfTypeAll<AudioClip>()"));
            Assert.That(source, Does.Contain("Resources.FindObjectsOfTypeAll<GameObject>().Length"));
            Assert.That(source, Does.Contain("SweepResourceSnapshot.Unavailable(ex.GetType().Name)"));
            Assert.That(source, Does.Not.Contain("List<UnityEngine.Object>"));
            Assert.That(source, Does.Not.Contain("Dictionary<int, UnityEngine.Object>"));
        });
    }

    [Test]
    public void Public_sweep_evidence_contains_no_identity_or_chat_fields()
    {
        string evidence = File.ReadAllText(Path.Combine(Root, "src", "Core", "SweepResourceEvidence.cs"));
        string capture = File.ReadAllText(Path.Combine(Root, "src", "SweepResourceEvidenceCapture.cs"));
        string combined = evidence + capture;

        Assert.Multiple(() =>
        {
            Assert.That(combined, Does.Not.Contain("SteamId"));
            Assert.That(combined, Does.Not.Contain("SteamID"));
            Assert.That(combined, Does.Not.Contain("LobbyId"));
            Assert.That(combined, Does.Not.Contain("LobbyTitle"));
            Assert.That(combined, Does.Not.Contain("Persona"));
            Assert.That(combined, Does.Not.Contain("Chat"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
