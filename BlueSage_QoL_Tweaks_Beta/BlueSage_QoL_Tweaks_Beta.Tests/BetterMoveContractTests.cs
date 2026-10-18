using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class BetterMoveContractTests
{
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

    [Test]
    public void BetterMovePatch_keeps_legacy_behavior_local_owner_only()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "BetterMovePatch.cs"));

        Assert.That(source, Does.Contain("PlayerMovementController"));
        Assert.That(source, Does.Contain("\"Update\""));
        Assert.That(source, Does.Contain("\"MovePlayer\""));
        Assert.That(source, Does.Contain("PlayerAnimationController"));
        Assert.That(source, Does.Contain("\"SetWalk\""));
        Assert.That(source, Does.Contain("PlayerFXController"));
        Assert.That(source, Does.Contain("\"SetPopsicle\""));
        Assert.That(source, Does.Contain("NetworkIdentity"));
        Assert.That(source, Does.Contain("isOwner"));
        Assert.That(source, Does.Contain("IsTurbo = shift"));
        Assert.That(source, Does.Contain("Input.GetKey(KeyCode.LeftControl)"));
        Assert.That(source, Does.Contain("IsPopsicleDouble"));
        Assert.That(source, Does.Contain("IsFreeze = false"));
    }

    [Test]
    public void BetterMove_is_default_on_and_available_from_menu_and_commands()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string menu = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string typeahead = File.ReadAllText(Path.Combine(ProjectRoot, "src", "CommandTypeaheadController.cs"));

        Assert.That(plugin, Does.Contain("EnableBetterMove"));
        Assert.That(plugin, Does.Contain("\"BetterMove\""));
        Assert.That(plugin, Does.Contain("Hold Shift to run or Ctrl to slow-walk"));
        Assert.That(plugin, Does.Contain("\"bettermove\""));
        Assert.That(plugin, Does.Contain("\"bm\""));
        Assert.That(plugin, Does.Contain("BetterMove={FormatToggleState(ShouldApplyBetterMove)}"));
        Assert.That(menu, Does.Contain("DrawToggle(\"BetterMove\""));
        Assert.That(typeahead, Does.Contain("[\"bettermove\"]"));
        Assert.That(typeahead, Does.Contain("[\"bm\"]"));
    }
}
