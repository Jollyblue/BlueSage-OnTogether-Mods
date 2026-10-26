using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 review R5 (2026-10-19): a read-only session (store present but unreadable, or a failed first migration)
// was only visible in the log. The player sees one in-game notice the first time the avatar dropdown opens, and
// log lines claim only what the code did.
[TestFixture]
public sealed class AvatarSlotStoreNoticePolicyTests
{
    private static string Root
    {
        get
        {
            string root = TestContext.CurrentContext.TestDirectory;
            while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
            }

            return root;
        }
    }

    [TestCase(false, false, false, false, TestName = "Nothing_shows_before_the_store_was_loaded")]
    [TestCase(true, true, false, false, TestName = "A_writable_session_shows_nothing")]
    [TestCase(true, false, false, true, TestName = "A_read_only_session_shows_the_notice_once")]
    [TestCase(true, false, true, false, TestName = "The_notice_is_not_repeated")]
    public void Read_only_notice_fires_once_per_read_only_session(bool loaded, bool writable, bool alreadyShown, bool expected)
    {
        Assert.That(AvatarSlotStoreNoticePolicy.ShouldShowReadOnlyNotice(loaded, writable, alreadyShown), Is.EqualTo(expected));
    }

    [Test]
    public void Bridge_shows_the_read_only_notice_in_game_and_marks_it_shown_only_when_the_chat_line_landed()
    {
        string bridge = File.ReadAllText(Path.Combine(Root, "src", "Patches", "AvatarStyleSlotsPatch.cs")).Replace("\r\n", "\n");
        int start = bridge.IndexOf("internal static void ExtendDropdown(", System.StringComparison.Ordinal);
        string extend = bridge.Substring(start, bridge.IndexOf("\n    }\n", start, System.StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(extend, Does.Contain("AvatarSlotStoreNoticePolicy.ShouldShowReadOnlyNotice(_storeLoaded, _storeWritable, _readOnlyNoticeShown)"));
            Assert.That(extend, Does.Contain("Plugin.AddLocalNotificationWithToast(Plugin.FormatBlueSageWarning("));
            Assert.That(extend, Does.Contain("_readOnlyNoticeShown = true;"));
            Assert.That(extend.IndexOf("Plugin.AddLocalNotificationWithToast(", System.StringComparison.Ordinal),
                Is.LessThan(extend.IndexOf("_readOnlyNoticeShown = true;", System.StringComparison.Ordinal)),
                "Shown is recorded after the chat line landed, so a notice that could not be posted is retried next time.");
            Assert.That(extend, Does.Contain("read-only this session"));
        });
    }

    [Test]
    public void Log_lines_claim_only_what_the_store_did()
    {
        string bridge = File.ReadAllText(Path.Combine(Root, "src", "Patches", "AvatarStyleSlotsPatch.cs")).Replace("\r\n", "\n");
        int persistStart = bridge.IndexOf("private static bool PersistStore()", System.StringComparison.Ordinal);
        int migrateStart = bridge.IndexOf("private static void MigrateFromLegacyConfig()", System.StringComparison.Ordinal);
        Assert.That(persistStart, Is.GreaterThan(0), "PersistStore reports whether the save happened.");
        string persist = bridge.Substring(persistStart, bridge.IndexOf("\n    }\n", persistStart, System.StringComparison.Ordinal) - persistStart);
        string migrate = bridge.Substring(migrateStart, bridge.IndexOf("\n    }\n", migrateStart, System.StringComparison.Ordinal) - migrateStart);
        Assert.Multiple(() =>
        {
            Assert.That(bridge, Does.Not.Contain("The previous saved copy is still intact"), "Not something the catch block can know.");
            Assert.That(persist, Does.Contain("was not replaced"), "File.Replace is the last step, so a failure before it leaves the file as it was.");
            Assert.That(migrate, Does.Contain("if (PersistStore())"), "'moved to crash-safe storage' is logged only after the save succeeded.");
            Assert.That(migrate.IndexOf("if (PersistStore())", System.StringComparison.Ordinal),
                Is.LessThan(migrate.IndexOf("moved to crash-safe storage", System.StringComparison.Ordinal)));
            Assert.That(bridge, Does.Contain("because the main file was missing or incomplete"));
        });
    }
}
