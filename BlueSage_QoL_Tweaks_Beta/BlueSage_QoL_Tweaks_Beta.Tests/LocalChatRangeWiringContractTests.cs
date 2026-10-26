using System.Text.RegularExpressions;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

/// <summary>
/// Round 5f. The wider local chat range is a one-constant change inside the game's own receive method. These tests pin
/// the shape that keeps it safe: only the distance constant moves, the game's ignore and mute checks stay in front, a
/// game update that changes the method leaves it alone, and the setting is reachable, clamped and documented.
/// </summary>
public sealed class LocalChatRangeWiringContractTests
{
    [Test]
    public void The_patch_targets_the_receive_method_and_replaces_only_the_distance_constant()
    {
        string shared = Read("src", "Patches", "LocalChatRangeTranspiler.cs");
        string chat = Read("src", "Patches", "LocalChatRangePatch.cs");
        string bubble = Read("src", "Patches", "LocalChatBubbleRangePatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(shared, Does.Contain("LocalChatRangePolicy.FindGateConstantIndexes("));
            Assert.That(shared, Does.Contain("applied = hits.Length == 1;"), "Exactly one match, or the method stays as the game wrote it.");
            Assert.That(shared, Does.Contain("return body;"), "A game update that breaks the pattern returns the untouched instructions.");
            Assert.That(shared, Does.Contain("constant.opcode = OpCodes.Call;"), "The instruction is changed in place so jump labels keep working.");
            Assert.That(Regex.Matches(shared, "OpCodes\\.Call").Count, Is.EqualTo(1), "One replaced instruction, nothing inserted.");

            Assert.That(chat, Does.Contain("[HarmonyPatch(typeof(TextChannelManager), \"OnChannelMessageReceived\")]"));
            Assert.That(bubble, Does.Contain("[HarmonyPatch(typeof(TextBubbleController), \"ShowTextBubble_Original_1\")]"));
            foreach (string patch in new[] { chat, bubble })
            {
                Assert.That(patch, Does.Contain("[HarmonyTranspiler]"));
                Assert.That(patch, Does.Contain("LocalChatRangeTranspiler.Apply("), "Both places share the one edit.");
                Assert.That(patch, Does.Not.Contain("HarmonyPrefix"), "A prefix that re-implements the method would skip the ignore and mute checks.");
                Assert.That(patch, Does.Not.Contain("return false"), "The game's own receive code must always run.");
            }
        });
    }

    [Test]
    public void A_hidden_control_line_is_neither_copyable_nor_written_to_the_audit_log()
    {
        string copy = Read("src", "Patches", "ChatCopyMessagePatch.cs");
        int guard = copy.IndexOf("HiddenControlLinePolicy.IsHiddenControlLine(text)", System.StringComparison.Ordinal);
        int record = copy.IndexOf("SessionAuditController.RecordChat(", System.StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(guard, Is.GreaterThan(0));
            Assert.That(record, Is.GreaterThan(guard), "The check must run before the audit record and the copy handler.");
        });
    }

    [Test]
    public void The_setting_is_bound_clamped_and_reachable_from_the_qol_menu()
    {
        string plugin = Read("src", "Plugin.cs");
        string menu = Read("src", "QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("Config.Bind(\"Chat Readability\", \"LocalChatRangeMeters\", LocalChatRangePolicy.DefaultMeters,"));
            Assert.That(plugin, Does.Contain("LockedLocalChatRangeMeters => LocalChatRangePolicy.ClampMeters("));
            Assert.That(plugin, Does.Contain("LocalChatRangeSquaredMeters => LocalChatRangePolicy.ResolveSquaredMeters(LockedLocalChatRangeMeters)"),
                "The range is its own setting; it does not ride on the chat-formatting stand-down.");
            Assert.That(menu, Does.Contain("DrawTextSetting(\"Local chat range (m)\""));
            Assert.That(menu, Does.Contain("LocalChatRangePolicy.ClampMeters(requested)"));
        });
    }

    [Test]
    public void The_readme_and_changelog_state_the_real_default_without_engineering_words()
    {
        string readme = Read("README.md");
        string changelog = Read("CHANGELOG.md");
        string meters = LocalChatRangePolicy.DefaultMeters.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        string newestChangelog = changelog.Substring(changelog.IndexOf("## 0.2.7", System.StringComparison.Ordinal));
        newestChangelog = newestChangelog.Substring(0, newestChangelog.IndexOf("## 0.2.6", System.StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(readme, Does.Contain($"| Local chat range | {meters} m |"));
            Assert.That(readme, Does.Contain($"See local chat and bubbles from {meters} m away,** not just 5 m"));
            Assert.That(newestChangelog, Does.Contain($"up to {meters} m by default"));
            foreach (string word in new[] { "transpiler", "Harmony", "SqrMagnitude", "OnChannelMessageReceived", "GT1" })
            {
                Assert.That(readme, Does.Not.Contain(word), $"README must not carry '{word}'.");
                Assert.That(newestChangelog, Does.Not.Contain(word), $"The newest CHANGELOG section must not carry '{word}'.");
            }
        });
    }

    [Test]
    public void No_public_file_names_the_mod_that_sends_hidden_control_lines()
    {
        string policy = Read("src", "Core", "HiddenControlLinePolicy.cs");
        string patch = Read("src", "Patches", "LocalChatRangePatch.cs") +
                       Read("src", "Patches", "LocalChatBubbleRangePatch.cs") +
                       Read("src", "Patches", "LocalChatRangeTranspiler.cs");

        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Not.Contain("TogetherPlus"));
            Assert.That(patch, Does.Not.Contain("TogetherPlus"));
            Assert.That(policy, Does.Not.Contain("gerghostface"));
        });
    }

    private static string Read(params string[] relative)
    {
        string root = FindProjectRoot();
        string[] parts = new string[relative.Length + 1];
        parts[0] = root;
        System.Array.Copy(relative, 0, parts, 1, relative.Length);
        return File.ReadAllText(Path.Combine(parts));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find BlueSage_QoL_Tweaks_Beta project root.");
    }
}
