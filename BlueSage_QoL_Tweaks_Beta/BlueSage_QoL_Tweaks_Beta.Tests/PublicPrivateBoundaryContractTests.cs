using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicPrivateBoundaryContractTests
{
    [Test]
    public void Public_source_does_not_contain_BlueExclusive_lab_surfaces()
    {
        string root = FindProjectRoot();
        string source = string.Join("\n", Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        string[] forbidden =
        {
            "BlueSage_QoL_Tweaks_BlueExclusive",
            "Private Experimental Lab",
            "Ghost Probe",
            "Host Broadcast",
            "Publish Metadata",
            "Clone Outfit",
            "Invisible Model"
        };

        Assert.Multiple(() =>
        {
            foreach (string token in forbidden)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        });
    }

    [Test]
    public void Public_style_owner_exposes_a_narrow_transactional_companion_seam()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("public string GetCompanionStyledBase()"));
            Assert.That(plugin, Does.Contain("public bool TryApplyCompanionStyledBase(string baseName, out string resultMessage)"));
            Assert.That(plugin, Does.Contain("TryApplyDisplayNameDraft("));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
