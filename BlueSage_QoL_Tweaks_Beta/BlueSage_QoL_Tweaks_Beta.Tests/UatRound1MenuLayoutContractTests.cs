using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// UAT findings on candidate F3FAB760BEB5 (Blue, 2026-10-17).
public sealed class UatRound1MenuLayoutContractTests
{
    [Test]
    public void Save_buttons_size_to_their_label_so_text_is_never_cut_off()
    {
        // "Save Auto/75-200" rendered as "ave Auto/75-20" because every Save button was a fixed
        // 112px wide. Width must come from the label's measured size (with a 112px floor).
        string draw = Slice(ReadSource("QolMenuWindow.cs"), "private void DrawTextSetting(", "string undoTooltip =");

        Assert.Multiple(() =>
        {
            Assert.That(draw, Does.Contain("_buttonStyle.CalcSize(saveContent)"));
            Assert.That(draw, Does.Not.Contain("GUILayout.Button(new GUIContent(buttonLabel, tooltip), _buttonStyle, GUILayout.Width(112f)"));
        });
    }

    [Test]
    public void Toast_toggle_lives_with_the_other_on_off_toggles_not_under_customize()
    {
        // Nothing about toasts is customizable; the On/Off switch belongs with the Toggles.
        string menu = ReadSource("QolMenuWindow.cs");
        string toggles = Slice(menu, "private void DrawComfortMovementToggles()", "\n    }\n");
        string hotkeys = Slice(menu, "private void DrawHotkeySettings()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(toggles, Does.Contain("DrawToggle(\"Toast Pop-ups\", Plugin.EnableToastNotifications"));
            Assert.That(hotkeys, Does.Not.Contain("\"Toast Pop-ups\""));
        });
    }

    [Test]
    public void Stale_pooled_rows_are_refreshed_with_vanilla_UpdateServerPanel_and_resolved_exactly_once_more()
    {
        // Minimap click-to-ID failed for late joiners: vanilla only re-stamps pooled rows while the
        // player list is open. One guarded vanilla refresh, then a single full re-check; never a loop.
        string controller = ReadSource("NativeIdCardController.cs");
        string resolve = Slice(controller, "internal static bool TryResolveCurrentRow(", "private static bool TryResolveCurrentRowOnce(");

        Assert.Multiple(() =>
        {
            Assert.That(resolve, Does.Contain("NativeIdCardResolutionState.NativeRowStaleRefreshable"));
            Assert.That(CountOf(resolve, "panel.UpdateServerPanel()"), Is.EqualTo(1));
            Assert.That(resolve, Does.Contain("try"));
            Assert.That(resolve, Does.Contain("catch"));
            Assert.That(CountOf(resolve, "TryResolveCurrentRowOnce("), Is.EqualTo(2), "First attempt plus exactly one retry.");
            Assert.That(resolve, Does.Not.Contain("while ("));
            Assert.That(resolve, Does.Not.Contain("for ("));
            Assert.That(controller, Does.Contain("CurrentRowListSteamId ="));
            // A recovered late-joiner click must not leave a warning behind (review 2026-10-17).
            Assert.That(controller, Does.Contain("decision.State != NativeIdCardResolutionState.NativeRowStaleRefreshable)\n            {\n                LogOutcome("));
        });
    }

    private static int CountOf(string haystack, string needle)
    {
        int count = 0;
        for (int i = haystack.IndexOf(needle, System.StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, System.StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
