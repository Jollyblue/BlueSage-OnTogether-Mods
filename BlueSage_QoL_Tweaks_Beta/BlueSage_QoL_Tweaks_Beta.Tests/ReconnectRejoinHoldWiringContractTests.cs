using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Wiring proofs for the reconnect rejoin hold (community feedback, 2026-10-17). Decisions are
// behavior-tested in ReconnectRejoinHoldPolicyTests; the real timing is a UAT measurement.
public sealed class ReconnectRejoinHoldWiringContractTests
{
    [Test]
    public void Clean_menu_rejoin_holds_before_JoinByCode()
    {
        string loop = Slice(ReadSource("Plugin.cs"), "private IEnumerator CleanMenuReconnectLoop(", "_cleanMenuReconnectPhase = \"validating-playable-scene\";");
        int hold = loop.IndexOf("ReconnectRejoinHoldPolicy.ShouldJoinNow(", System.StringComparison.Ordinal);
        int join = loop.IndexOf("JoinByCode(targetLobbyCode, false)", System.StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(hold, Is.GreaterThan(0), "The rejoin must wait on the hold policy.");
            Assert.That(join, Is.GreaterThan(hold), "The hold must come before the join.");
            Assert.That(loop, Does.Contain("ReconnectRejoinHoldPolicy.BuildHoldNotice("), "The player must be told why they are waiting.");
            Assert.That(loop, Does.Contain("_cleanMenuReconnectPhase = \"holding-before-rejoin\";"));
        });
    }

    [Test]
    public void Hold_cancels_instead_of_double_joining_when_the_player_leaves_the_clean_menu()
    {
        string loop = Slice(ReadSource("Plugin.cs"), "_cleanMenuReconnectPhase = \"holding-before-rejoin\";", "JoinByCode(targetLobbyCode, false)");

        Assert.Multiple(() =>
        {
            Assert.That(loop, Does.Contain("generation != _cleanMenuReconnectGeneration"));
            Assert.That(loop, Does.Contain("IsCleanMenuReadyForReconnect("));
            Assert.That(loop, Does.Contain("ReconnectRejoinHoldPolicy.UpdateNotReadyStreak("), "The menu check is noisy: track a sustained streak, not one frame.");
            Assert.That(loop, Does.Contain("ReconnectRejoinHoldPolicy.ShouldCancelHold("));
            Assert.That(loop, Does.Contain("FinishCleanMenuReconnect(generation,"));
        });
    }

    [Test]
    public void Hold_aborts_at_once_on_guard_off_or_a_player_join_and_rechecks_right_before_joining()
    {
        string plugin = ReadSource("Plugin.cs");
        string hold = Slice(plugin, "_cleanMenuReconnectPhase = \"holding-before-rejoin\";", "JoinByCode(targetLobbyCode, false)");
        int finalCheck = hold.LastIndexOf("ReconnectRejoinHoldPolicy.IsSafeToJoinNow(", System.StringComparison.Ordinal);
        int lastLoopLine = hold.LastIndexOf("ShouldCancelHold(", System.StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(hold, Does.Contain("ReconnectRejoinHoldPolicy.ShouldAbortImmediately(EnableReconnectGuard.Value, IsPlayerJoinInProgress(), joinInProgressAtHoldStart)"));
            Assert.That(finalCheck, Is.GreaterThan(lastLoopLine), "A final readiness check must sit after the hold loop, right before JoinByCode.");
            Assert.That(plugin, Does.Contain("multiplayerManager.IsConnecting"), "The game's own IsConnecting must count as a player join, not just be logged.");
            Assert.That(hold, Does.Contain("bool joinInProgressAtHoldStart = IsPlayerJoinInProgress();"), "The join baseline must be captured when the hold starts (edge-triggered).");
        });
    }

    [Test]
    public void Turning_the_guard_off_in_the_menu_stops_a_pending_reconnect()
    {
        string plugin = ReadSource("Plugin.cs");
        Assert.That(plugin, Does.Contain("EnableReconnectGuard.SettingChanged += (_, __) =>"));
        string handler = Slice(plugin, "EnableReconnectGuard.SettingChanged += (_, __) =>", "};");
        Assert.That(handler, Does.Contain("StopManagedReconnect("));
    }

    [Test]
    public void Hold_is_a_saved_setting_adjustable_in_the_menu_without_editing_config()
    {
        string plugin = ReadSource("Plugin.cs");
        string menu = ReadSource("QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ReconnectRejoinHoldSeconds = Config.Bind(\"Reconnect Guard\", \"ReconnectRejoinHoldSeconds\", ReconnectRejoinHoldPolicy.DefaultHoldSeconds"));
            Assert.That(menu, Does.Contain("SaveReconnectRejoinHold"));
            Assert.That(menu, Does.Contain("ReconnectRejoinHoldPolicy.ClampHoldSeconds("));
        });
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
