using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5h (Blue, 2026-10-25: "we're at 70% of working toasts"; "missing triggers tied to all our related routes that
// can open various menus and GUIs"). Three read-only audits found the same pattern: a toast only fires where somebody
// wired one by hand. Two shared rules now cover most routes: a command-result rule and a menu "saved / done" rule.
public sealed class ToastCoverageRound5hTests
{
    [Test]
    public void Summarize_turns_a_result_into_one_short_plain_line()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToastPolicy.Summarize("<color=#70D6FF><b>BlueSage</b></color> Kick complete: the player left. More detail follows."), Is.EqualTo("BlueSage Kick complete: the player left."));
            Assert.That(ToastPolicy.Summarize("Chat font: 120%.\nSecond line."), Is.EqualTo("Chat font: 120%."));
            Assert.That(ToastPolicy.Summarize("  Timestamps:   On.  "), Is.EqualTo("Timestamps: On."));
            Assert.That(ToastPolicy.Summarize(new string('a', 300)).Length, Is.LessThanOrEqualTo(ToastPolicy.MaxSummaryLength));
            Assert.That(ToastPolicy.Summarize(new string('a', 300)), Does.EndWith("..."));
        });
    }

    [Test]
    public void Summarize_says_nothing_for_blank_or_generic_text()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToastPolicy.Summarize(null), Is.Null);
            Assert.That(ToastPolicy.Summarize("   "), Is.Null);
            Assert.That(ToastPolicy.Summarize("Ready."), Is.Null);
            Assert.That(ToastPolicy.Summarize("Done."), Is.Null);
            Assert.That(ToastPolicy.Summarize("<b></b>"), Is.Null);
        });
    }

    [Test]
    public void State_changing_commands_toast_their_first_line_and_information_commands_stay_in_chat()
    {
        Assert.Multiple(() =>
        {
            foreach (string name in new[] { "timestamps", "chatfont", "reconnectguard", "setname", "status", "brb", "afk", "back", "clearstatus", "spoons", "rebind", "bettermove", "pingsound", "cloneshield" })
            {
                Assert.That(CommandToastPolicy.ShouldToastFirstMessage(name, new[] { "on" }), Is.True, name);
            }

            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("timestamps", new[] { "status" }), Is.False, "asking for the state is information");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("helper", new[] { "list" }), Is.False);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("helper", new[] { "add", "Rowan" }), Is.True);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("sidban", new[] { "123" }), Is.False, "a preview stays in chat");
            // Real grammar (code review of PR #27): "/sidban <id> confirm" - the id first, "confirm" last.
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("sidban", new[] { "123", "confirm" }), Is.True, "the confirmed result toasts");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("sidkick", new[] { "confirm" }), Is.False, "a bare confirm is refused, so it stays in chat");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("sidmute", new[] { "confirm", "123" }), Is.False, "confirm must come last");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("sidlist", new string[0]), Is.False);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("sidwho", new[] { "x" }), Is.False);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("chalklist", new string[0]), Is.False);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("bluedevdebug", new string[0]), Is.False);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("motd", new[] { "hi" }), Is.False, "already toasts by itself");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("announce", new[] { "hi" }), Is.False, "already toasts by itself");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("assetsweep", new string[0]), Is.False, "already toasts by itself");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage(null, null), Is.False);
        });
    }

    // Code review of PR #27: a bare setter prints the current value (a read), and free text such as "/setname Check" is a value,
    // not an information word.
    [Test]
    public void Bare_setters_are_reads_and_free_text_is_a_value()
    {
        Assert.Multiple(() =>
        {
            foreach (string name in new[] { "chatscale", "chatfont", "chatheight", "chatrows", "chatoutline", "setname", "status", "statuscolor", "spoons", "selfmove", "helper", "pingsound", "setpingcolor", "setmood", "auditlog" })
            {
                Assert.That(CommandToastPolicy.ShouldToastFirstMessage(name, new string[0]), Is.False, name + " with no argument only shows the value");
            }

            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("chatfont", new[] { "120" }), Is.True);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("setname", new[] { "Check" }), Is.True, "free text that looks like an information word is still a value");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("status", new[] { "show", "off" }), Is.True);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("chatfont", new[] { "status" }), Is.False, "asking for the state is a read");
            // Advisor merge-step check: "show" and "snapshot" are actions (display the tag, save a file), not reads.
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("spoons", new[] { "show" }), Is.True);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("spoons", new[] { "hide" }), Is.True);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("auditlog", new[] { "snapshot" }), Is.True);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("auditlog", new[] { "status" }), Is.False);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("auditlog", new[] { "folder" }), Is.False);
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("brb", new string[0]), Is.True, "an action with no argument still changes something");
            Assert.That(CommandToastPolicy.ShouldToastFirstMessage("timestamps", new string[0]), Is.True, "a bare toggle toggles");
        });
    }

    [Test]
    public void Review_fixes_open_path_undo_navigation_and_work_relax_result_are_wired()
    {
        string root = FindProjectRoot();
        string style = File.ReadAllText(Path.Combine(root, "src", "StyleHelperWindow.cs")).Replace("\r\n", "\n");
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs")).Replace("\r\n", "\n");
        string overlay = File.ReadAllText(Path.Combine(root, "src", "PerformanceOverlayController.cs")).Replace("\r\n", "\n");
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        Assert.Multiple(() =>
        {
            Assert.That(style, Does.Contain("MarkSavedQuiet(detected?.DriftSummary"), "opening Style Helper already says 'opened'; the drift summary stays in the footer");
            Assert.That(style, Does.Contain("private void MarkSavedQuiet(string detail)"));
            Assert.That(Between(menu, "private void MarkSaved(string detail)", "\n    }\n"), Does.Contain("_suppressSaveToast"), "an Undo's inner save must not toast a contradictory 'saved'");
            Assert.That(menu, Does.Contain("_suppressSaveToast = true;"));
            Assert.That(Between(menu, "private void OpenCommunitySupporters", "\n    }\n"), Does.Contain("MarkActionQuiet("), "a tab jump is navigation");
            Assert.That(overlay, Does.Contain("if (WorkRelaxRuntimeController.SetMode(WorkRelaxMode.Work) && currentMode != WorkRelaxMode.Work)"));
            Assert.That(overlay, Does.Contain("if (WorkRelaxRuntimeController.SetMode(WorkRelaxMode.Relax) && currentMode != WorkRelaxMode.Relax)"));
            Assert.That(plugin, Does.Not.Contain("Helper tools are off for you in this lobby."), "leaving a lobby must not toast at the main menu");
        });
    }

    [Test]
    public void Every_registered_command_is_either_toasting_or_deliberately_quiet()
    {
        string plugin = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"));
        int registered = 0;
        int index = 0;
        while ((index = plugin.IndexOf("new BlueSageCommand(", index, System.StringComparison.Ordinal)) >= 0)
        {
            registered++;
            index += 20;
        }

        // The five Work / Relax commands (workon, relaxon, workoff, relaxoff, mode) register through WorkRelaxCommandPolicy.Register(registry, ...).
        string workRelax = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Core", "WorkRelaxCommandPolicy.cs"));
        int workRelaxRegistered = 0;
        index = 0;
        while ((index = workRelax.IndexOf("        Register(registry, \"", index, System.StringComparison.Ordinal)) >= 0)
        {
            workRelaxRegistered++;
            index += 20;
        }

        Assert.That(registered, Is.GreaterThanOrEqualTo(60));
        Assert.That(workRelaxRegistered, Is.EqualTo(5));
        Assert.That(CommandToastPolicy.ToastingCommandCount + CommandToastPolicy.QuietCommandCount, Is.GreaterThanOrEqualTo(registered + workRelaxRegistered - 2),
            "a new command must be filed as toasting or quiet in CommandToastPolicy (the tolerance of 2 covers the one internal BlueSage command whose name is built at run time)");
    }

    [Test]
    public void The_chat_command_loop_and_the_menu_windows_use_the_shared_rules()
    {
        string root = FindProjectRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs")).Replace("\r\n", "\n");
        string style = File.ReadAllText(Path.Combine(root, "src", "StyleHelperWindow.cs")).Replace("\r\n", "\n");
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("CommandToastPolicy.ShouldToastFirstMessage("));
            Assert.That(plugin, Does.Contain("ToastPolicy.Summarize(result.Messages[0])"));
            foreach (string source in new[] { menu, style })
            {
                string saved = Between(source, "private void MarkSaved(string detail)", "\n    }\n");
                string action = Between(source, "private void MarkAction(string detail)", "\n    }\n");
                Assert.That(saved, Does.Contain("Plugin.Instance?.ShowToast(ToastPolicy.Summarize(detail))"));
                Assert.That(action, Does.Contain("Plugin.Instance?.ShowToast(ToastPolicy.Summarize(detail))"));
            }

            Assert.That(menu, Does.Contain("private void MarkSavedQuiet(string detail)"));
            Assert.That(Between(menu, "private void DrawToggle(string label, ConfigEntry<bool> entry, string tooltip, Action afterChange = null)", "\n    }\n"), Does.Contain("MarkSavedQuiet("), "a switch already toasts its own On/Off");
            Assert.That(Between(style, "private void MarkApplyOutcome(", "\n    }\n"), Does.Contain("ShowToast(ToastPolicy.Summarize(failedDetail))"), "a failed apply is never silent");
        });
    }

    // Advisor review of PR #27: every MarkAction in the menu would toast, including row picks (40 rows = 40 toasts),
    // safe previews, status read-outs and actions whose own code already toasts. Those use MarkActionQuiet.
    [Test]
    public void Row_picks_previews_and_status_reads_in_the_menu_stay_footer_only()
    {
        string menu = File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "QolMenuWindow.cs")).Replace("\r\n", "\n");
        string[] lines = menu.Split('\n');
        Assert.Multiple(() =>
        {
            foreach (string trigger in new[]
            {
                "Run Cleanup Now requested.",
                "_selectedSafetySteamId = selected ? string.Empty : row.SteamId;",
                "Cleared the selected player.",
                "_selectedBannedSteamId = isSelected ? string.Empty : record.SteamId;",
                "Cleared the selected native ban.",
                "Plugin.Instance.PreviewSidBanFromQolMenu(selectedRow.SteamId)",
                "Preview Suspect Ban",
                "Preview SID Unban",
                "Preview Offline SID Ban",
                "Optional lobby joke requested.",
                "Optional lobby joke checked again."
            })
            {
                Assert.That(NextMarkCall(lines, trigger), Does.Contain("MarkActionQuiet("), "should stay footer-only: " + trigger);
            }

            foreach (string trigger in new[]
            {
                "Plugin.Instance.ConfirmSidBanFromQolMenu(selectedRow.SteamId)",
                "Plugin.Instance.ConfirmSidUnbanFromQolMenu(selected.SteamId)",
                "Plugin.Instance.RunHostHealthFromQolMenu()"
            })
            {
                string call = NextMarkCall(lines, trigger);
                Assert.That(call, Does.Contain("MarkAction(_message)").And.Not.Contain("Quiet"), "should toast its result: " + trigger);
            }
        });
    }

    private static string NextMarkCall(string[] lines, string trigger)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IndexOf(trigger, System.StringComparison.Ordinal) < 0)
            {
                continue;
            }

            for (int j = i; j < Math.Min(lines.Length, i + 12); j++)
            {
                if (lines[j].Contains("MarkAction") && !lines[j].Contains("private void"))
                {
                    return lines[j];
                }
            }
        }

        Assert.Fail("trigger not found near a MarkAction call: " + trigger);
        return string.Empty;
    }

    [Test]
    public void Opening_routes_and_the_work_relax_buttons_answer_with_a_toast()
    {
        string root = FindProjectRoot();
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs")).Replace("\r\n", "\n");
        string overlay = File.ReadAllText(Path.Combine(root, "src", "PerformanceOverlayController.cs")).Replace("\r\n", "\n");
        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Not.Contain("MarkAction(\"Style Helper toggled.\")"), "the footer and toast say opened or closed");
            Assert.That(overlay, Does.Contain("ShowToast("));
        });
    }

    private static string Between(string source, string start, string end)
    {
        int from = source.IndexOf(start, System.StringComparison.Ordinal);
        Assert.That(from, Is.GreaterThanOrEqualTo(0), "missing: " + start);
        int to = source.IndexOf(end, from, System.StringComparison.Ordinal);
        return source.Substring(from, (to < 0 ? source.Length : to) - from);
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
