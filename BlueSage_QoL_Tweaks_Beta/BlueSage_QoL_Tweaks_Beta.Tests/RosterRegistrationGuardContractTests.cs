using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RosterRegistrationGuardContractTests
{
    [Test]
    public void Host_registration_is_deduplicated_without_destroying_network_objects()
    {
        string source = ReadPatchSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("[HarmonyPatch(typeof(PlayerCustomizationController), \"UpdateOnNewPeopleJoin_Original_0\")]"));
            Assert.That(source, Does.Contain("RosterRegistrationPolicy.Evaluate("));
            Assert.That(source, Does.Contain("RosterRegistrationAction.RejectInvalid"));
            Assert.That(source, Does.Contain("RosterRegistrationAction.IgnoreExactDuplicate"));
            Assert.That(source, Does.Contain("RosterRegistrationAction.ReplaceStableIdentity"));
            Assert.That(source, Does.Contain("RemoveParallelRow(panel, decision.ExistingIndex)"));
            Assert.That(source, Does.Contain("[HarmonyPatch(typeof(PlayerPanelController), \"DespawnHandle\", typeof(NetworkTransform))]"));
            Assert.That(source, Does.Not.Contain("UnityEngine.Object.Destroy"));
            Assert.That(source, Does.Not.Contain("DestroyImmediate"));
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

    private static string ReadPatchSource()
    {
        return File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Patches", "RosterRegistrationGuardPatch.cs"));
    }
}
