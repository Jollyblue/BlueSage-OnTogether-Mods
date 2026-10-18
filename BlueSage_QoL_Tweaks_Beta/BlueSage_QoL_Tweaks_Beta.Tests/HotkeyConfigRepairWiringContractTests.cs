using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Wiring proofs for ultrareview #4 and #5 (2026-10-17). The decisions are behavior-tested in
// HotkeyConfigRepairPolicyTests.
public sealed class HotkeyConfigRepairWiringContractTests
{
    [Test]
    public void Saved_hotkeys_are_repaired_right_after_they_are_bound()
    {
        string plugin = ReadPluginSource();
        int lastBind = plugin.IndexOf("NoclipFlyKey = Config.Bind(", System.StringComparison.Ordinal);
        int repairCall = plugin.IndexOf("ApplyStartupHotkeyRepair();", System.StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(lastBind, Is.GreaterThan(0));
            Assert.That(repairCall, Is.GreaterThan(lastBind), "Repair must run after every hotkey entry is bound.");
        });
    }

    [Test]
    public void Repair_saves_only_when_something_changed_and_queues_a_notice_for_the_first_lobby()
    {
        string plugin = ReadPluginSource();
        string method = Slice(plugin, "private void ApplyStartupHotkeyRepair()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(method, Does.Contain("HotkeyConfigRepairPolicy.Repair("));
            Assert.That(method, Does.Contain("if (!result.Changed)"));
            Assert.That(method, Does.Contain("Config.Save();"));
            Assert.That(method, Does.Contain("_pendingHotkeyRepairNotices"));
        });
    }

    [Test]
    public void Repair_notices_reach_chat_even_when_the_welcome_message_is_turned_off()
    {
        string plugin = ReadPluginSource();
        string loop = Slice(plugin, "private IEnumerator WelcomeMessageLoop()", "if (EnableWelcomeMessage == null || !EnableWelcomeMessage.Value)");

        Assert.That(loop, Does.Contain("FlushPendingHotkeyRepairNotices()"));
    }

    [Test]
    public void Reset_to_default_refuses_and_names_the_holder_instead_of_creating_a_collision()
    {
        string plugin = ReadPluginSource();
        string method = Slice(plugin, "private string ResetHotkeyToDefault(HotkeyBindTarget target)", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(method, Does.Contain("HotkeyConfigRepairPolicy.FindHolder("));
            int holderCheck = method.IndexOf("FindHolder(", System.StringComparison.Ordinal);
            int assign = method.IndexOf("entry.Value = defaultKey;", System.StringComparison.Ordinal);
            Assert.That(assign, Is.GreaterThan(holderCheck), "The holder check must happen before the value is changed.");
        });
    }

    private static string ReadPluginSource()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
        }

        return File.ReadAllText(Path.Combine(root, "src", "Plugin.cs")).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int startIndex = source.IndexOf(start, System.StringComparison.Ordinal);
        if (startIndex < 0) return string.Empty;
        int endIndex = source.IndexOf(end, startIndex + start.Length, System.StringComparison.Ordinal);
        return endIndex < 0 ? string.Empty : source.Substring(startIndex, endIndex - startIndex);
    }
}
