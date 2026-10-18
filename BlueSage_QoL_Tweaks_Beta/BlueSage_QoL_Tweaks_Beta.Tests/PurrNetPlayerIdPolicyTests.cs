using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PurrNetPlayerIdPolicyTests
{
    [TestCase(0UL, "0")]
    [TestCase(1UL, "1")]
    [TestCase(609UL, "609")]
    [TestCase(ulong.MaxValue, "18446744073709551615")]
    public void Format_uses_one_invariant_numeric_identity(ulong value, string expected)
    {
        Assert.That(PurrNetPlayerIdPolicy.Format(value), Is.EqualTo(expected));
    }

    [Test]
    public void Identity_critical_runtime_paths_do_not_compare_PlayerID_ToString()
    {
        string root = FindProjectRoot();
        string nativeId = File.ReadAllText(Path.Combine(root, "src", "NativeIdCardController.cs"));
        string evidence = File.ReadAllText(Path.Combine(root, "src", "PlayerIdentityEvidenceController.cs"));
        string panel = File.ReadAllText(Path.Combine(root, "src", "Patches", "PlayerPanelSurfaceController.cs"));
        string chat = File.ReadAllText(Path.Combine(root, "src", "Patches", "ChatSenderContextPatch.cs"));
        string miniMap = File.ReadAllText(Path.Combine(root, "src", "Patches", "MiniMapPlayerLabelPatch.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(nativeId, Does.Not.Contain("PlayerIDs[currentRowIndex].ToString()"));
            Assert.That(nativeId, Does.Not.Contain("PlayerIDs[index].ToString()"));
            Assert.That(evidence, Does.Not.Contain("PlayerIDs.Select(id => id.ToString())"));
            Assert.That(panel, Does.Not.Contain("PlayerIDs[observedIndex].ToString()"));
            Assert.That(chat, Does.Not.Contain("PlayerIDs[senderIndex].ToString()"));
            Assert.That(miniMap, Does.Not.Contain("PlayerIDs[index].ToString()"));
            Assert.That(nativeId, Does.Contain("PurrNetPlayerIdPolicy.Format"));
            Assert.That(evidence, Does.Contain("PurrNetPlayerIdPolicy.Format"));
            Assert.That(panel, Does.Contain("PurrNetPlayerIdPolicy.Format"));
            Assert.That(chat, Does.Contain("PurrNetPlayerIdPolicy.Format"));
            Assert.That(miniMap, Does.Contain("PurrNetPlayerIdPolicy.Format"));
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
