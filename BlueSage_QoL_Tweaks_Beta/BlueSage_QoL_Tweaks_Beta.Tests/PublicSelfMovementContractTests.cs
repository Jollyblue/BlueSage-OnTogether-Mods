using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublicSelfMovementContractTests
{
    [Test]
    public void Public_self_movement_is_self_only_reversible_and_not_on_F6()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        string patch = File.ReadAllText(Path.Combine(root, "src", "Patches", "SelfMovementPatch.cs"));
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        string customize = ExtractMethod(menu, "private void DrawCustomizeTab()", "private void DrawCreditsOtherTab()");
        string credits = ExtractMethod(menu, "private void DrawCreditsOtherTab()", "private void DrawLobbySafetyTab()");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("EnableNoclipFly\", false"));
            Assert.That(plugin, Does.Contain("SelfMovementPolicy.DefaultMultiplier"));
            Assert.That(plugin, Does.Contain("Patches.SelfMovementPatch.CleanupRuntimeState();"));
            Assert.That(plugin, Does.Contain("EnableNoclipFly.Value = false;"));
            Assert.That(patch, Does.Contain("instance == NetworkSingleton<TextChannelManager>.I?.MainMovementController"));
            Assert.That(patch, Does.Contain("OptInRescuePatch.Stop();"));
            Assert.That(patch, Does.Contain("movement.SpeedY = 0f;"));
            Assert.That(menu, Does.Contain("\"Self movement\","));
            Assert.That(customize, Does.Contain("DrawSelfMovementSection("));
            Assert.That(credits, Does.Not.Contain("DrawSelfMovementSection("));
            Assert.That(credits, Does.Not.Contain("DrawSettingsSection(\"Self movement\""));
            Assert.That(Count(menu, "\"Self movement\","), Is.EqualTo(1));
            Assert.That(menu, Does.Contain("Optional self-only movement and rescue-from-a-glitch controls."));
            Assert.That(menu, Does.Contain("Save Pos"));
            Assert.That(menu, Does.Contain("Teleport Saved"));
            Assert.That(menu, Does.Contain("Noclip / Fly"));
            Assert.That(plugin, Does.Not.Contain("KeyCode.F6"));
            Assert.That(plugin, Does.Not.Contain("KeyCode.F9"));
            Assert.That(plugin, Does.Not.Contain("KeyCode.F10"));
            Assert.That(menu, Does.Not.Contain("KeyCode.F9"));
            Assert.That(menu, Does.Not.Contain("KeyCode.F10"));
            Assert.That(patch, Does.Not.Contain("follow"));
            Assert.That(patch, Does.Not.Contain("target player"));
            Assert.That(patch, Does.Not.Contain("invisible"));
            Assert.That(patch, Does.Not.Contain("metadata"));
        });
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method marker: {startMarker}");
        Assert.That(end, Is.GreaterThan(start), $"Missing method marker: {endMarker}");
        return source.Substring(start, end - start);
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
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
